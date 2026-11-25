using System;
using System.Collections.Generic;
using ShapeShifty.Audio;
using ShapeShifty.UI;
using UnityEngine;

namespace ShapeShifty.AI
{
    // A guard dog, let loose when the alarm goes off: runs at the player whatever form
    // they're in, and catches them when it reaches them. Faster than anything but a
    // sprinting wolf. It never leaves its area (one or more box colliders, used for their
    // shape only): while the player is outside it, the dog waits at the nearest edge, barking.
    // Straight at them when nothing's in the way, otherwise round it (DogGrid); the dogs
    // keep a little apart rather than piling up.
    [RequireComponent(typeof(CharacterController))]
    public class GuardDog : MonoBehaviour
    {
        [SerializeField] float runSpeed = 10.5f;
        [SerializeField] float turnSpeed = 720f;
        [SerializeField] float catchDistance = 1.1f;
        [SerializeField] BoxCollider[] area;
        [Tooltip("Bobs while running, hops while barking.")]
        [SerializeField] Transform body;
        [Tooltip("Snaps open on each bark (its model's Animator never moves it).")]
        [SerializeField] Transform jaw;
        [SerializeField] float gravity = 20f;
        [SerializeField] Vector2 barkInterval = new Vector2(0.6f, 1.6f);

        // A dog got the player.
        public static event Action<GuardDog> PlayerCaught;

        public bool IsLoose { get; private set; }

        static readonly List<GuardDog> loose = new List<GuardDog>();

        CharacterController controller;
        Transform player;
        Vector3 bodyRest;
        Quaternion jawRest;
        float verticalSpeed, sideStepUntil, sideSign = 1f, bob, nextBark, lastBark = -1f;
        bool caught;
        DogGrid grid;
        readonly List<Vector3> path = new List<Vector3>();
        int waypoint;
        float nextPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlaySession()
        {
            PlayerCaught = null;
            loose.Clear();
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (body != null) bodyRest = body.localPosition;
            if (jaw != null) jawRest = jaw.localRotation;
        }

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }

        void OnDestroy() => loose.Remove(this);

        public void Release()
        {
            if (IsLoose) return;
            IsLoose = true;
            grid = DogGrid.For(area);
            // They keep each other at a distance instead (Apart), so they can't end up on top
            // of one another.
            foreach (var other in loose) Physics.IgnoreCollision(controller, other.controller);
            loose.Add(this);
            nextBark = Time.time + UnityEngine.Random.Range(0f, barkInterval.x);
        }

        void Update()
        {
            if (!IsLoose || player == null || caught) return;
            float dt = Time.deltaTime;
            bool playerInside = Inside(player.position);
            Vector3 target = playerInside ? player.position : Edge(player.position);
            Vector3 toTarget = target - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            // Which way it wants to go, and how keenly: towards the target (less keen as it
            // gets there), and away from the other dogs.
            Vector3 heading = Vector3.zero;
            if (distance > 0.4f)
            {
                Vector3 steer = grid != null && !grid.Clear(transform.position, target) ? Waypoint(target) : target;
                Vector3 to = steer - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f) heading = to.normalized * Mathf.Clamp01(distance / 2f);
            }
            heading += Apart();

            Vector3 velocity = Vector3.zero;
            if (heading.sqrMagnitude > 0.04f)
            {
                Vector3 direction = heading.normalized;
                // Stuck on something: try going round it for a moment.
                if (Time.time < sideStepUntil) direction = Quaternion.Euler(0f, 75f * sideSign, 0f) * direction;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * dt);
                float keen = distance > 0.4f ? 1f : 0.3f;
                velocity = transform.forward * (runSpeed * keen * Mathf.Clamp01(Vector3.Dot(transform.forward, direction)));
            }
            else if (!playerInside)
            {
                Vector3 look = player.position - transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), turnSpeed * dt);
            }

            verticalSpeed = controller.isGrounded ? -2f : verticalSpeed - gravity * dt;
            Vector3 before = transform.position;
            controller.Move((velocity + Vector3.up * verticalSpeed) * dt);
            Vector3 moved = transform.position - before;
            moved.y = 0f;
            if (velocity.sqrMagnitude > 1f && moved.magnitude < velocity.magnitude * dt * 0.3f && Time.time >= sideStepUntil)
            {
                sideStepUntil = Time.time + 0.6f;
                sideSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                // Maybe something's moved: look at the area again and find a new way.
                if (grid != null) grid.Refresh();
                nextPath = 0f;
            }

            bool running = velocity.sqrMagnitude > 1f;
            if (body != null)
            {
                bob += dt * (running ? 16f : 9f);
                float lift = running ? Mathf.Abs(Mathf.Sin(bob)) * 0.08f : playerInside ? 0f : Mathf.Abs(Mathf.Sin(bob)) * 0.12f;
                body.localPosition = bodyRest + Vector3.up * lift;
            }
            if (Time.time >= nextBark)
            {
                nextBark = Time.time + UnityEngine.Random.Range(barkInterval.x, barkInterval.y);
                lastBark = Time.time;
                FloatingWord.Spawn(transform.position + Vector3.up * 1.6f, "WOOF!", new Color(1f, 0.85f, 0.6f), 3f);
                Sfx.Play(Sound.Bark, transform.position);
            }

            Vector3 gap = player.position - transform.position;
            if (playerInside && new Vector2(gap.x, gap.z).magnitude < catchDistance && Mathf.Abs(gap.y) < 1.5f)
            {
                caught = true;
                PlayerCaught?.Invoke(this);
            }
        }

        // After the Animator, which would otherwise shut it again.
        void LateUpdate()
        {
            if (jaw == null) return;
            const float BarkTime = 0.22f;
            float t = Time.time - lastBark;
            float open = t >= 0f && t < BarkTime ? Mathf.Sin(t / BarkTime * Mathf.PI) * 38f : 0f;
            // About the dog's own right: positive tips the jaw's tip down.
            jaw.localRotation = Quaternion.AngleAxis(open, jaw.parent.InverseTransformDirection(transform.right)) * jawRest;
        }

        // The next point on its way round whatever's between it and the target: as far along
        // the path as it can run straight to.
        Vector3 Waypoint(Vector3 target)
        {
            if (Time.time >= nextPath)
            {
                nextPath = Time.time + 0.3f;
                // No way there: perhaps a door was still opening when the grid was made.
                if (!grid.FindPath(transform.position, target, path)) grid.Refresh(justOut: true);
                waypoint = 0;
            }
            if (path.Count == 0) return target;
            for (int k = 0; k < 10 && waypoint < path.Count - 1; k++)
            {
                if (!grid.Clear(transform.position, path[waypoint + 1])) break;
                waypoint++;
            }
            return path[waypoint];
        }

        // A push away from any other loose dog close by.
        Vector3 Apart()
        {
            const float Room = 1.6f;
            Vector3 push = Vector3.zero;
            foreach (var other in loose)
            {
                if (other == this) continue;
                Vector3 away = transform.position - other.transform.position;
                away.y = 0f;
                float d = away.magnitude;
                if (d >= Room) continue;
                push += (d > 0.01f ? away / d : transform.right) * ((Room - d) / Room);
            }
            return push;
        }

        bool Inside(Vector3 point)
        {
            foreach (var box in area)
            {
                var b = box.bounds;
                if (point.x > b.min.x && point.x < b.max.x && point.z > b.min.z && point.z < b.max.z) return true;
            }
            return false;
        }

        // The point just inside the area nearest the player.
        Vector3 Edge(Vector3 point)
        {
            Vector3 best = transform.position;
            float bestDistance = float.PositiveInfinity;
            foreach (var box in area)
            {
                var b = box.bounds;
                var p = new Vector3(Mathf.Clamp(point.x, b.min.x + 0.8f, b.max.x - 0.8f), transform.position.y, Mathf.Clamp(point.z, b.min.z + 0.8f, b.max.z - 0.8f));
                float d = (new Vector2(p.x, p.z) - new Vector2(point.x, point.z)).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = p; }
            }
            return best;
        }
    }
}
