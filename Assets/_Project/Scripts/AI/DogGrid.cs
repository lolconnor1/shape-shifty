using System.Collections.Generic;
using ShapeShifty.Player;
using UnityEngine;

namespace ShapeShifty.AI
{
    // Where a guard dog can go in its area, worked out from what's actually there: a grid of
    // half-metre cells over the area's boxes, each with its floor height and whether a dog
    // fits above it (crates, walls, bars and anything else solid, on any layer, block it).
    // Shared by every dog with the same area. Made when the first is let loose, and again
    // when a dog gets stuck, in case something has moved since.
    public class DogGrid
    {
        const float Cell = 0.5f;
        const float Clearance = 0.42f;  // half the room a dog needs side to side
        const float Step = 0.4f;        // the most the floor can rise from one cell to the next
        const float RebuildEvery = 3f;
        const float SettleTime = 8f;    // doors still opening when the dogs come out

        static readonly List<DogGrid> grids = new List<DogGrid>();
        static readonly Collider[] overlaps = new Collider[16];
        static readonly RaycastHit[] hits = new RaycastHit[8];
        static readonly Dictionary<Collider, bool> ignored = new Dictionary<Collider, bool>();

        readonly BoxCollider[] area;
        Vector3 origin;
        int width, depth;
        float[] floor;
        bool[] open;
        float builtAt, firstBuilt;

        // A* working arrays, reused between searches.
        float[] cost;
        int[] from, seen, heap;
        float[] heapF;
        int search;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlaySession() => grids.Clear();

        public static DogGrid For(BoxCollider[] area)
        {
            grids.RemoveAll(g => g.area.Length == 0 || g.area[0] == null);
            foreach (var g in grids)
                if (g.area.Length == area.Length && System.Linq.Enumerable.SequenceEqual(g.area, area)) return g;
            var grid = new DogGrid(area);
            grids.Add(grid);
            return grid;
        }

        DogGrid(BoxCollider[] area)
        {
            this.area = area;
            firstBuilt = Time.time;
            Build();
        }

        // Something's in the way that wasn't when the grid was made (or has gone): look again,
        // not too often. justOut: only while the dogs have just come out, when doors may
        // still have been opening; later, somewhere they can't get to stays that way.
        public void Refresh(bool justOut = false)
        {
            if (Time.time - builtAt < RebuildEvery) return;
            if (justOut && Time.time - firstBuilt > SettleTime) return;
            Build();
        }

        void Build()
        {
            builtAt = Time.time;
            var bounds = area[0].bounds;
            foreach (var box in area) bounds.Encapsulate(box.bounds);
            origin = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
            width = Mathf.CeilToInt(bounds.size.x / Cell);
            depth = Mathf.CeilToInt(bounds.size.z / Cell);
            int n = width * depth;
            floor = new float[n];
            open = new bool[n];
            cost = new float[n];
            from = new int[n];
            seen = new int[n];
            heap = new int[n * 4];
            heapF = new float[n * 4];

            for (int z = 0; z < depth; z++)
            for (int x = 0; x < width; x++)
            {
                int i = z * width + x;
                Vector3 c = Centre(x, z, 0f);
                if (!InArea(c)) continue;
                // The floor: straight down from a little over a dog's height (from inside a crate,
                // that finds the floor under it, and the crate then fills the space above).
                if (!FloorAt(new Vector3(c.x, origin.y + 1.2f, c.z), out float y)) continue;
                floor[i] = y;
                Vector3 room = new Vector3(c.x, y + Step + 0.3f, c.z);
                open[i] = !Blocked(room, new Vector3(Clearance, 0.28f, Clearance));
            }
            ignored.Clear();
        }

        bool InArea(Vector3 p)
        {
            foreach (var box in area)
            {
                var b = box.bounds;
                if (p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z) return true;
            }
            return false;
        }

        static bool FloorAt(Vector3 start, out float y)
        {
            y = 0f;
            // Usually the nearest thing below is the floor; only if it's a dog or the player
            // look past it.
            if (!Physics.Raycast(start, Vector3.down, out RaycastHit hit, 2.5f, Physics.AllLayers, QueryTriggerInteraction.Ignore)) return false;
            if (!Ignored(hit.collider)) { y = hit.point.y; return true; }
            int count = Physics.RaycastNonAlloc(start, Vector3.down, hits, 2.5f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int k = 0; k < count; k++)
                if (!Ignored(hits[k].collider) && hits[k].distance < best) { best = hits[k].distance; y = hits[k].point.y; }
            return best < float.PositiveInfinity;
        }

        static bool Blocked(Vector3 centre, Vector3 halfSize)
        {
            int count = Physics.OverlapBoxNonAlloc(centre, halfSize, overlaps, Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int k = 0; k < count; k++)
                if (!Ignored(overlaps[k])) return true;
            return false;
        }

        // The dogs and the player move about: they aren't part of the layout.
        static bool Ignored(Collider c)
        {
            if (!ignored.TryGetValue(c, out bool result))
                ignored[c] = result = c.GetComponentInParent<GuardDog>() != null || c.GetComponentInParent<PlayerController>() != null;
            return result;
        }

        Vector3 Centre(int x, int z, float y) => new Vector3(origin.x + (x + 0.5f) * Cell, y, origin.z + (z + 0.5f) * Cell);

        int CellAt(Vector3 p)
        {
            int x = Mathf.FloorToInt((p.x - origin.x) / Cell), z = Mathf.FloorToInt((p.z - origin.z) / Cell);
            return x < 0 || z < 0 || x >= width || z >= depth ? -1 : z * width + x;
        }

        // The open cell nearest a point (the point's own if it's open), or -1.
        int OpenCellNear(Vector3 p, int reach)
        {
            int c = CellAt(p);
            if (c >= 0 && open[c]) return c;
            int px = Mathf.Clamp(Mathf.FloorToInt((p.x - origin.x) / Cell), 0, width - 1);
            int pz = Mathf.Clamp(Mathf.FloorToInt((p.z - origin.z) / Cell), 0, depth - 1);
            int best = -1;
            float bestD = float.PositiveInfinity;
            for (int z = Mathf.Max(0, pz - reach); z <= Mathf.Min(depth - 1, pz + reach); z++)
            for (int x = Mathf.Max(0, px - reach); x <= Mathf.Min(width - 1, px + reach); x++)
            {
                int i = z * width + x;
                if (!open[i]) continue;
                Vector3 d = Centre(x, z, p.y) - p;
                d.y = 0f;
                if (d.sqrMagnitude < bestD) { bestD = d.sqrMagnitude; best = i; }
            }
            return best;
        }

        // Whether a dog can go straight from a to b without bumping into anything.
        public bool Clear(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            d.y = 0f;
            int steps = Mathf.CeilToInt(d.magnitude / (Cell * 0.5f));
            int last = CellAt(a);
            if (last < 0) return false;
            for (int s = 1; s <= steps; s++)
            {
                int c = CellAt(a + d * ((float)s / steps));
                if (c < 0 || !open[c]) return false;
                if (c != last && last >= 0 && open[last] && Mathf.Abs(floor[c] - floor[last]) > Step) return false;
                last = c;
            }
            return true;
        }

        // Fills path with points from near `start` to `goal`, round whatever's in the way. If the
        // goal can't be reached, the path goes as close to it as a dog can get, and it returns
        // false (as it does if there's nowhere to go at all).
        public bool FindPath(Vector3 start, Vector3 goal, List<Vector3> path)
        {
            path.Clear();
            int s = OpenCellNear(start, 3), g = OpenCellNear(goal, 6);
            if (s < 0) return false;
            int goalCell = CellAt(goal);
            if (g < 0) g = goalCell >= 0 ? goalCell : s;
            int gx = g % width, gz = g / width;

            search++;
            int count = 0;
            cost[s] = 0f;
            from[s] = -1;
            seen[s] = search;
            Push(ref count, s, Heuristic(s, gx, gz));
            int best = s;
            float bestH = Heuristic(s, gx, gz);
            while (count > 0)
            {
                int c = Pop(ref count);
                float h = Heuristic(c, gx, gz);
                if (h < bestH) { bestH = h; best = c; }
                if (c == g) break;
                int cx = c % width, cz = c / width;
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = cx + dx, nz = cz + dz;
                    if (nx < 0 || nz < 0 || nx >= width || nz >= depth) continue;
                    int nb = nz * width + nx;
                    if (!open[nb] || Mathf.Abs(floor[nb] - floor[c]) > Step) continue;
                    // No cutting corners.
                    if (dx != 0 && dz != 0 && (!open[cz * width + nx] || !open[nz * width + cx])) continue;
                    float step = (dx != 0 && dz != 0 ? 1.4142f : 1f) * Cell;
                    float nc = cost[c] + step;
                    if (seen[nb] == search && nc >= cost[nb]) continue;
                    seen[nb] = search;
                    cost[nb] = nc;
                    from[nb] = c;
                    Push(ref count, nb, nc + Heuristic(nb, gx, gz));
                }
            }

            for (int c = best; c >= 0; c = from[c])
                path.Add(Centre(c % width, c / width, floor[c]));
            path.Reverse();
            if (best == g && Clear(path[path.Count - 1], goal)) path.Add(goal);
            return best == g;
        }

        float Heuristic(int c, int gx, int gz)
        {
            int dx = Mathf.Abs(c % width - gx), dz = Mathf.Abs(c / width - gz);
            return (Mathf.Max(dx, dz) + 0.4142f * Mathf.Min(dx, dz)) * Cell;
        }

        // A binary min-heap on heapF. Cells can be in it more than once (with a stale cost);
        // a cell popped again just finds its neighbours already done.
        void Push(ref int count, int cell, float f)
        {
            if (count >= heap.Length) return;
            int i = count++;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (heapF[p] <= f) break;
                heap[i] = heap[p];
                heapF[i] = heapF[p];
                i = p;
            }
            heap[i] = cell;
            heapF[i] = f;
        }

        int Pop(ref int count)
        {
            int top = heap[0];
            count--;
            int cell = heap[count];
            float f = heapF[count];
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1;
                if (l >= count) break;
                int r = l + 1;
                int m = r < count && heapF[r] < heapF[l] ? r : l;
                if (heapF[m] >= f) break;
                heap[i] = heap[m];
                heapF[i] = heapF[m];
                i = m;
            }
            heap[i] = cell;
            heapF[i] = f;
            return top;
        }
    }
}
