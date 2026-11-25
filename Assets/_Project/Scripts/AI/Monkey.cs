using System;
using System.Collections.Generic;
using ShapeShifty.Audio;
using ShapeShifty.Combat;
using ShapeShifty.Dialogue;
using ShapeShifty.Shapes;
using UnityEngine;

namespace ShapeShifty.AI
{
    // A monkey in the middle of a civil war. A Grey (hostile) keeps watch from its post, and
    // once it sees the player, in any form, shrieks to its friends and starts flinging poo:
    // facing them, hopping from side to side about its post, aiming where they'll be with a
    // bit of wobble, so a player who keeps moving and changes direction gets missed. Enough
    // hits (its Health) and it keels over seeing stars. A Brown (not hostile) is on the
    // player's side and just stands about.
    public class Monkey : MonoBehaviour
    {
        public enum State { Idle, Fighting, Dropping, Dazed }

        [Tooltip("A Grey, out to get the player. Off for a friendly Brown.")]
        [SerializeField] bool hostile = true;

        [Header("Senses")]
        [SerializeField] float sightRange = 24f;
        [SerializeField] float eyeHeight = 0.9f;
        [Tooltip("What blocks its view. Ignore Raycast (the invisible level bounds) is left out.")]
        [SerializeField] LayerMask sightMask = ~(1 << 2);
        [Tooltip("Seconds without a sight of the player before it settles down again.")]
        [SerializeField] float forgetTime = 6f;
        [Tooltip("Other Greys this close (m) join in when it spots the player.")]
        [SerializeField] float callRange = 10f;

        [Header("Throwing")]
        [SerializeField] PooProjectile pooPrefab;
        [SerializeField] float throwSpeed = 15f;
        [SerializeField] float gravity = 9f;
        [Tooltip("Seconds between throws (min, max).")]
        [SerializeField] Vector2 throwInterval = new Vector2(1.2f, 2.2f);
        [Tooltip("Seconds it winds up before letting go: the player's warning.")]
        [SerializeField] float windUp = 0.35f;
        [Tooltip("How far (m) off a throw can land, per 15 m of distance.")]
        [SerializeField] float inaccuracy = 0.8f;
        [Tooltip("How much it aims ahead of a moving player: 1 hits a player who keeps going the same way.")]
        [SerializeField, Range(0f, 1f)] float lead = 0.7f;
        [Tooltip("Poos per throw, fanned out side by side (a big Grey's handful).")]
        [SerializeField, Min(1)] int volley = 1;
        [Tooltip("Degrees between the poos in a volley.")]
        [SerializeField] float volleySpread = 7f;

        [Header("Moving")]
        [Tooltip("How far (m) it hops about from its post while fighting. 0 stays put.")]
        [SerializeField] float moveRadius = 2.5f;
        [SerializeField] float moveSpeed = 3f;
        [SerializeField] float turnSpeed = 360f;
        [SerializeField] LayerMask obstacleMask = ~(1 << 2);

        [Header("Visual")]
        [SerializeField] Transform body;
        [SerializeField] Transform armL;
        [SerializeField] Transform armR;
        [Tooltip("Where a throw leaves from.")]
        [SerializeField] Transform hand;
        [Tooltip("\"!\" shown for a moment when it spots the player. Turned to face the camera.")]
        [SerializeField] GameObject alertIcon;
        [Tooltip("Spun round its head once it's down.")]
        [SerializeField] Transform dazedStars;

        public static readonly List<Monkey> All = new List<Monkey>();
        // A Grey has gone down.
        public static event Action<Monkey> Downed;

        public State Current { get; private set; }
        public bool Hostile => hostile;
        public bool IsDown => Current == State.Dazed;

        Health health;
        Scannable scannable;
        Talkable talkable;
        Collider[] colliders;
        Transform player;
        CharacterController playerController;
        Transform viewCamera;
        Vector3 home, strafeTo, lastSeenAt;
        Quaternion homeRotation;
        float nextLook, lastSeen = float.NegativeInfinity, nextThrow, windUntil = -1f, nextStrafe, alertUntil, flinchUntil, phase;
        float fallSpeed, scratchAt, lookYaw;
        bool moved, sawPlayer;
        Vector3 bodyRest;
        Quaternion armLRest, armRRest, bodyRestRotation;
        float armLAngle, armRAngle;
        float swingStart = -99f;
        readonly RaycastHit[] groundHits = new RaycastHit[8];

        void Awake()
        {
            health = GetComponent<Health>();
            scannable = GetComponent<Scannable>();
            talkable = GetComponent<Talkable>();
            colliders = GetComponentsInChildren<Collider>();
            if (body != null) { bodyRest = body.localPosition; bodyRestRotation = body.localRotation; }
            if (armL != null) armLRest = armL.localRotation;
            if (armR != null) armRRest = armR.localRotation;
            if (alertIcon != null) alertIcon.SetActive(false);
            if (dazedStars != null) dazedStars.gameObject.SetActive(false);
            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Died += OnDied;
            }
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            viewCamera = Camera.main != null ? Camera.main.transform : null;
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
                playerController = playerObject.GetComponent<CharacterController>();
            }
            // A reinforcement already has a landing spot (DropIn), and starts in the air.
            if (Current != State.Dropping) home = transform.position;
            homeRotation = transform.rotation;
            phase = UnityEngine.Random.value * 10f;
            scratchAt = Time.time + UnityEngine.Random.Range(3f, 8f);
        }

        void Update()
        {
            moved = false;
            if (scannable != null && scannable.IsBeingScanned) return;
            if (talkable != null && talkable.IsTalking) { Pose(); return; }
            if (player == null || !hostile) { Idle(); Pose(); return; }
            switch (Current)
            {
                case State.Idle: Watch(); Idle(); break;
                case State.Fighting: Fight(); break;
                case State.Dropping: Drop(); break;
            }
            Pose();
        }

        // ---------- Senses ----------

        Vector3 PlayerChest() => player.position + Vector3.up * ((playerController != null ? playerController.height : 1f) * 0.6f);

        bool CanSeePlayer()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 to = PlayerChest() - eye;
            if (to.magnitude > sightRange) return false;
            if (!Physics.Raycast(eye, to.normalized, out RaycastHit hit, to.magnitude + 0.5f, sightMask, QueryTriggerInteraction.Ignore)) return true;
            return hit.transform.CompareTag("Player");
        }

        void Watch()
        {
            if (Time.time < nextLook) return;
            nextLook = Time.time + 0.2f;
            if (CanSeePlayer()) Alert(true);
        }

        // Spotted the player (or was hit, or was called): into the fight.
        public void Alert(bool callOthers)
        {
            if (!hostile || Current == State.Dazed || Current == State.Dropping) return;
            if (Current != State.Fighting)
            {
                Current = State.Fighting;
                alertUntil = Time.time + 1.2f;
                Sfx.Play(Sound.Squeak, transform.position, 0.7f, 1.2f); // the shriek
                nextThrow = Time.time + UnityEngine.Random.Range(0.5f, 1.2f);
                nextStrafe = Time.time;
            }
            lastSeen = Time.time;
            if (player != null) lastSeenAt = player.position;
            if (!callOthers) return;
            foreach (var other in All)
                if (other != this && other.hostile && other.Current == State.Idle && Vector3.Distance(other.transform.position, transform.position) < callRange)
                    other.Alert(false);
        }

        void OnDamaged(float amount, Vector3 from)
        {
            if (Current == State.Dazed) return;
            flinchUntil = Time.time + 0.25f;
            Alert(true);
        }

        void OnDied()
        {
            Current = State.Dazed;
            windUntil = -1f;
            foreach (var c in colliders) c.enabled = false;
            if (alertIcon != null) alertIcon.SetActive(false);
            if (dazedStars != null) dazedStars.gameObject.SetActive(true);
            Downed?.Invoke(this);
        }

        // ---------- States ----------

        void Idle()
        {
            // Looks about from its post, now and then, and scratches its head.
            if (!hostile && player != null && Vector3.Distance(player.position, transform.position) < 6f)
            {
                Turn(Flat(player.position - transform.position), 0.5f);
                return;
            }
            float target = Mathf.Sin((Time.time + phase) * 0.35f) * 50f;
            lookYaw = Mathf.MoveTowards(lookYaw, target, 40f * Time.deltaTime);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, homeRotation * Quaternion.Euler(0f, lookYaw, 0f), turnSpeed * 0.3f * Time.deltaTime);
        }

        void Fight()
        {
            bool sees = false;
            if (Time.time >= nextLook)
            {
                nextLook = Time.time + 0.2f;
                sees = CanSeePlayer();
                sawPlayer = sees;
                if (sees) { lastSeen = Time.time; lastSeenAt = player.position; }
            }
            if (Time.time - lastSeen > forgetTime) { Current = State.Idle; return; }

            Vector3 facing = Flat((sawPlayer ? player.position : lastSeenAt) - transform.position);
            Turn(facing, 1f);

            if (windUntil > 0f)
            {
                if (Time.time < windUntil) return;
                windUntil = -1f;
                Throw();
                return;
            }
            if (sawPlayer && Time.time >= nextThrow && Vector3.Angle(Flat(transform.forward), facing) < 30f)
            {
                windUntil = Time.time + windUp;
                swingStart = Time.time;
                return;
            }
            Strafe();
        }

        void Throw()
        {
            nextThrow = Time.time + UnityEngine.Random.Range(throwInterval.x, throwInterval.y);
            if (pooPrefab == null || player == null) return;
            Vector3 from = hand != null ? hand.position : transform.position + Vector3.up * 1.2f;
            Vector3 target = PlayerChest();
            float distance = Vector3.Distance(from, target);
            float time = distance / throwSpeed;
            Vector3 velocity = playerController != null ? playerController.velocity : Vector3.zero;
            velocity.y = 0f;
            target += velocity * (time * lead);
            Vector2 wobble = UnityEngine.Random.insideUnitCircle * (inaccuracy * distance / 15f);
            target += new Vector3(wobble.x, wobble.y * 0.5f, 0f);
            Vector3 aim = PooProjectile.Aim(from, target, throwSpeed, gravity);
            for (int i = 0; i < volley; i++)
            {
                var poo = Instantiate(pooPrefab, from, Quaternion.identity);
                poo.Launch(Quaternion.Euler(0f, (i - (volley - 1) * 0.5f) * volleySpread, 0f) * aim, Team.Greys, transform, gravity);
            }
        }

        // Hops from side to side about its post so it's harder to hit.
        void Strafe()
        {
            if (moveRadius <= 0f) return;
            if (Time.time >= nextStrafe)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * moveRadius;
                strafeTo = home + new Vector3(offset.x, 0f, offset.y);
                nextStrafe = Time.time + UnityEngine.Random.Range(0.8f, 2f);
            }
            Vector3 to = strafeTo - transform.position;
            to.y = 0f;
            if (to.magnitude < 0.15f) return;
            Vector3 step = to.normalized * Mathf.Min(to.magnitude, moveSpeed * Time.deltaTime);
            if (!CanStep(step)) { nextStrafe = Time.time; return; }
            transform.position += step;
            SnapToGround();
            moved = true;
            phase += Time.deltaTime * moveSpeed * 5f;
        }

        // Not into anything, and not off the edge of whatever it's stood on.
        bool CanStep(Vector3 step)
        {
            Vector3 from = transform.position + Vector3.up * 0.5f;
            if (Physics.SphereCast(from, 0.3f, step.normalized, out RaycastHit hit, 0.5f, obstacleMask, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform) && !hit.transform.CompareTag("Player"))
                return false;
            Vector3 ahead = transform.position + step.normalized * 0.45f;
            return Physics.Raycast(ahead + Vector3.up * 0.5f, Vector3.down, 1f, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        // Reinforcements: falls out of the trees onto `landing`, then joins the fight.
        public void DropIn(Vector3 landing)
        {
            gameObject.SetActive(true);
            home = landing;
            transform.position = landing + Vector3.up * 9f;
            fallSpeed = 0f;
            Current = State.Dropping;
        }

        void Drop()
        {
            fallSpeed += 25f * Time.deltaTime;
            Vector3 next = transform.position + Vector3.down * (fallSpeed * Time.deltaTime);
            if (next.y <= home.y)
            {
                transform.position = home;
                Current = State.Idle;
                Alert(false);
                return;
            }
            transform.position = next;
            if (player != null) Turn(Flat(player.position - transform.position), 1f);
        }

        // ---------- Moving ----------

        void Turn(Vector3 direction, float rate)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * rate * Time.deltaTime);
        }

        void SnapToGround()
        {
            const float From = 0.6f, MaxStep = 0.45f;
            int count = Physics.RaycastNonAlloc(transform.position + Vector3.up * From, Vector3.down, groundHits, 3f, obstacleMask, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var t = groundHits[i].transform;
                if (t.IsChildOf(transform) || t.CompareTag("Player")) continue;
                if (groundHits[i].distance < best) best = groundHits[i].distance;
            }
            if (best == float.PositiveInfinity) return;
            float groundY = transform.position.y + From - best;
            if (groundY - transform.position.y > MaxStep) return;
            transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.zero;
        }

        // ---------- Looks ----------

        void Pose()
        {
            if (body == null) return;
            Vector3 offset = Vector3.zero;
            Quaternion tip = Quaternion.identity;
            float left = 0f, right = 0f;
            float t = Time.time + phase;
            switch (Current)
            {
                case State.Dazed:
                    // Flat on its back.
                    tip = Quaternion.Euler(-80f, 0f, 0f);
                    offset = Vector3.up * 0.15f + Vector3.back * 0.1f;
                    left = right = 60f;
                    break;
                case State.Dropping:
                    left = right = 160f;
                    break;
                case State.Fighting:
                    // Bouncing on the spot, arms going.
                    offset = Vector3.up * (moved ? Mathf.Abs(Mathf.Sin(phase)) * 0.12f : Mathf.Abs(Mathf.Sin(t * 7f)) * 0.06f);
                    left = 30f + Mathf.Sin(t * 7f) * 40f;
                    if (Time.time < alertUntil) left = right = 150f + Mathf.Sin(t * 20f) * 15f; // arms up, shrieking
                    break;
                default:
                    offset = Vector3.up * (Mathf.Sin(t * 1.5f) * 0.015f);
                    // Scratches its head now and then.
                    if (Time.time > scratchAt)
                    {
                        float s = Time.time - scratchAt;
                        if (s < 1.6f) left = 150f + Mathf.Sin(s * 18f) * 10f;
                        else scratchAt = Time.time + UnityEngine.Random.Range(4f, 9f);
                    }
                    break;
            }
            if (Time.time < flinchUntil) tip *= Quaternion.Euler(-18f, 0f, 0f);
            body.SetLocalPositionAndRotation(bodyRest + offset, bodyRestRotation * tip);

            // The throwing arm: wound back, then flung over the top.
            float k = (Time.time - swingStart) / (windUp + 0.25f);
            if (Current != State.Dazed && k >= 0f && k <= 1f)
            {
                float split = windUp / (windUp + 0.25f);
                right = k < split ? Mathf.Lerp(0f, 150f, k / split) : Mathf.Lerp(150f, 360f, (k - split) / (1f - split));
            }
            armLAngle = left;
            armRAngle = right;
        }

        void LateUpdate()
        {
            // After the model's Animator, which would otherwise put the arms back.
            if (armL != null) armL.localRotation = armLRest * Quaternion.Euler(armLAngle, 0f, 0f);
            if (armR != null) armR.localRotation = armRRest * Quaternion.Euler(armRAngle, 0f, 0f);
            if (alertIcon != null)
            {
                bool show = Current == State.Fighting && Time.time < alertUntil;
                if (alertIcon.activeSelf != show) alertIcon.SetActive(show);
                if (show) FaceCamera(alertIcon.transform);
            }
            if (dazedStars != null && Current == State.Dazed)
            {
                // Just above where its head lies.
                dazedStars.position = transform.position + Vector3.up * 0.45f - transform.forward * 0.7f;
                dazedStars.Rotate(Vector3.up, 200f * Time.deltaTime, Space.World);
            }
        }

        void FaceCamera(Transform icon)
        {
            if (viewCamera == null) return;
            Vector3 facing = viewCamera.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f) icon.rotation = Quaternion.LookRotation(facing);
        }
    }
}
