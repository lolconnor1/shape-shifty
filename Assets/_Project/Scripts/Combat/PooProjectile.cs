using System.Collections.Generic;
using ShapeShifty.Audio;
using UnityEngine;

namespace ShapeShifty.Combat
{
    // A flung lump of poo. Flies on its own simple arc (no Rigidbody), sweeping a small sphere
    // along the way, and splats on the first thing it touches: a Health on the other team
    // takes the damage, a Bullseye lights up, and whatever it hit gets a splat. A big one (a splash radius) splats
    // everyone of the other team nearby as well.
    public class PooProjectile : MonoBehaviour
    {
        [SerializeField] float radius = 0.12f;
        [SerializeField] float damage = 1f;
        [Tooltip("Above 0, everyone of the other team this close (m) to the impact is hit too.")]
        [SerializeField] float splashRadius;
        [SerializeField] float lifetime = 6f;
        [Tooltip("Left on whatever it hits.")]
        [SerializeField] Splat splatPrefab;
        [SerializeField] float splatSize = 0.5f;
        [Tooltip("Layers it hits. Ignore Raycast (the invisible level bounds) is left out.")]
        [SerializeField] LayerMask hitMask = ~(1 << 2);
        [Tooltip("Tumbles in flight.")]
        [SerializeField] float spin = 540f;

        public float Gravity => gravity;

        Team team;
        Transform owner;
        Vector3 velocity, spinAxis;
        float gravity = 9f, age;
        readonly HashSet<Health> splashed = new HashSet<Health>();
        static readonly Collider[] nearby = new Collider[16];

        // The launch velocity that lands a throw from `from` on `target` with this gravity,
        // flying at about `speed` (slower throws arc higher).
        public static Vector3 Aim(Vector3 from, Vector3 target, float speed, float gravity)
        {
            Vector3 to = target - from;
            float time = Mathf.Max(0.05f, to.magnitude / Mathf.Max(0.1f, speed));
            return AimTimed(from, target, time, gravity);
        }

        // The launch velocity that lands on `target` after `time` seconds.
        public static Vector3 AimTimed(Vector3 from, Vector3 target, float time, float gravity) =>
            (target - from) / time + Vector3.up * (0.5f * gravity * time);

        public void Launch(Vector3 launchVelocity, Team thrownBy, Transform thrower, float launchGravity)
        {
            velocity = launchVelocity;
            team = thrownBy;
            owner = thrower;
            gravity = launchGravity;
            spinAxis = Random.onUnitSphere;
            Sfx.Play(Sound.Throw, transform.position, 0.8f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age > lifetime || transform.position.y < -20f) { Destroy(gameObject); return; }

            Vector3 from = transform.position;
            velocity += Vector3.down * (gravity * dt);
            Vector3 step = velocity * dt;
            float length = step.magnitude;
            transform.Rotate(spinAxis, spin * dt, Space.World);
            if (length < 0.0001f) return;

            var hits = Physics.SphereCastAll(from, radius, step / length, length, hitMask, QueryTriggerInteraction.Ignore);
            RaycastHit best = default;
            float bestDistance = float.PositiveInfinity;
            foreach (var hit in hits)
            {
                if (owner != null && hit.transform.IsChildOf(owner)) continue;
                // Starting inside something reports distance 0 and no point: ignore it.
                if (hit.distance <= 0f && hit.point == Vector3.zero) continue;
                if (hit.distance < bestDistance) { best = hit; bestDistance = hit.distance; }
            }
            if (bestDistance < float.PositiveInfinity) { Impact(best); return; }
            transform.position = from + step;
        }

        void Impact(RaycastHit hit)
        {
            Vector3 point = hit.point;
            Sfx.Play(Sound.Splat, point, splashRadius > 0f ? 1f : 0.7f, splashRadius > 0f ? 0.7f : 1f);
            var health = hit.collider.GetComponentInParent<Health>();
            if (health != null) health.Damage(damage, point - velocity.normalized, team);
            var target = hit.collider.GetComponentInParent<Bullseye>();
            if (target != null) target.TakeHit(team, velocity);
            if (splashRadius > 0f)
            {
                if (health != null) splashed.Add(health);
                int count = Physics.OverlapSphereNonAlloc(point, splashRadius, nearby, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var other = nearby[i].GetComponentInParent<Health>();
                    if (other == null || !splashed.Add(other)) continue;
                    other.Damage(damage, point, team);
                }
            }
            if (splatPrefab != null)
            {
                var splat = Instantiate(splatPrefab, point + hit.normal * 0.02f, Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                // Sticks to whoever it hit, so it goes where they go.
                bool onCharacter = health != null;
                splat.Begin(splatSize * (splashRadius > 0f ? 2.5f : 1f), onCharacter ? hit.collider.transform : null);
            }
            Destroy(gameObject);
        }
    }
}
