using ShapeShifty.Player;
using UnityEngine;

namespace ShapeShifty.Interaction
{
    // A platform that travels between points, waiting a moment at each, and carries the
    // player along while they stand on it (or on anything parented under it, like the floor
    // of a hanging basket). Runs before the player moves, so they're carried first and then
    // walk about on top as normal. Stop and Resume halt it where it is and set it going again.
    [DefaultExecutionOrder(-50)]
    public class MovingPlatform : MonoBehaviour
    {
        [Tooltip("Visited in order; the platform starts at the first. Keep them outside the platform, or they'll move with it.")]
        [SerializeField] Transform[] points;
        [SerializeField] float speed = 2f;
        [Tooltip("How long (s) it waits at each point.")]
        [SerializeField] float waitTime = 2f;
        [Tooltip("On: goes from the last point straight back to the first. Off: turns round and comes back the way it came.")]
        [SerializeField] bool loop;
        [SerializeField] bool running = true;

        public bool IsRunning => running;

        PlayerController player;
        Vector3[] stops;
        int target;
        int step = 1;
        float waitTimer;

        void Start()
        {
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null) player = playerObject.GetComponent<PlayerController>();
            stops = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++) stops[i] = points[i].position;
            if (stops.Length > 0) transform.position = stops[0];
            target = stops.Length > 1 ? 1 : 0;
        }

        public void Stop() => running = false;
        public void Resume() => running = true;

        void Update()
        {
            if (!running || stops.Length < 2) return;
            if (waitTimer > 0f)
            {
                waitTimer -= Time.deltaTime;
                return;
            }

            Vector3 before = transform.position;
            transform.position = Vector3.MoveTowards(before, stops[target], speed * Time.deltaTime);
            if (transform.position == stops[target])
            {
                waitTimer = waitTime;
                NextTarget();
            }
            Carry(transform.position - before);
        }

        void NextTarget()
        {
            if (loop)
            {
                target = (target + 1) % stops.Length;
                return;
            }
            if (target + step < 0 || target + step >= stops.Length) step = -step;
            target += step;
        }

        // A CharacterController isn't pushed by moving colliders, so a rider is moved by as much
        // as the platform moved. Then the physics scene is brought up to date, so the player's
        // own move this frame collides with where the platform is now, not where it was.
        void Carry(Vector3 delta)
        {
            if (player != null && player.Ground != null && player.Ground.transform.IsChildOf(transform))
                player.transform.position += delta;
            Physics.SyncTransforms();
        }
    }
}
