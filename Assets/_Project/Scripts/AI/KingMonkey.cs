using System;
using System.Collections;
using System.Collections.Generic;
using ShapeShifty.Combat;
using ShapeShifty.Interaction;
using UnityEngine;

namespace ShapeShifty.AI
{
    // The Grey King, on his throne in front of the gate. Lounges until the player comes near
    // (or hits him), then roars and fights in three rounds as his Health runs down:
    //   1. lobs big poo bombs, each with a marker where it will land, between fans of
    //      ordinary throws;
    //   2. roars for help, and Greys drop out of the trees;
    //   3. leaps from perch to perch round the arena, throwing faster and wider.
    // Down for good, his crown falls off and the gate behind the throne opens.
    [RequireComponent(typeof(Health))]
    public class KingMonkey : MonoBehaviour
    {
        public enum State { Waiting, Fighting, Defeated }

        [SerializeField] string displayName = "The Guerilla King";
        [Tooltip("Wakes when the player comes this close (m). Until then hits don't count.")]
        [SerializeField] float wakeRange = 20f;
        [Tooltip("Only throws while the player is this close (m): back out of the arena and he waits, jeering.")]
        [SerializeField] float fightRange = 30f;

        [Header("Throwing")]
        [SerializeField] PooProjectile pooPrefab;
        [SerializeField] PooProjectile bombPrefab;
        [Tooltip("Laid where a bomb will land, for as long as it's in the air.")]
        [SerializeField] GameObject bombMarkerPrefab;
        [SerializeField] float throwSpeed = 17f;
        [SerializeField] float gravity = 9f;
        [SerializeField] float bombFlightTime = 1.5f;
        [Tooltip("Degrees between the throws in a fan.")]
        [SerializeField] float fanAngle = 9f;
        [SerializeField, Range(0f, 1f)] float lead = 0.5f;

        [Header("Rounds")]
        [Tooltip("Health fraction at which he calls for help.")]
        [SerializeField, Range(0f, 1f)] float callAt = 0.6f;
        [Tooltip("Health fraction at which he starts leaping about.")]
        [SerializeField, Range(0f, 1f)] float leapAt = 0.3f;
        [SerializeField] Monkey[] reinforcements;
        [SerializeField] Transform[] dropPoints;
        [Tooltip("Spots he leaps between in the last round (their tops).")]
        [SerializeField] Transform[] perches;
        [SerializeField] float leapTime = 0.9f;
        [SerializeField] float leapHeight = 4f;

        [Header("The way out")]
        [SerializeField] Hatch gate;
        public UnityEngine.Events.UnityEvent onDefeated = new UnityEngine.Events.UnityEvent();

        [Header("Visual")]
        [SerializeField] Transform body;
        [SerializeField] Transform armL;
        [SerializeField] Transform armR;
        [SerializeField] Transform hand;
        [SerializeField] Transform crown;
        [Tooltip("\"ROAAAR!\" shown while he roars. Turned to face the camera.")]
        [SerializeField] GameObject roarIcon;

        public static KingMonkey Current { get; private set; }
        public static event Action Woke;
        public static event Action Beaten;

        public string DisplayName => displayName;
        public Health Health => health;
        public State Now { get; private set; }
        public bool IsAwake => Now == State.Fighting;
        public int Round => health.Fraction > callAt ? 1 : health.Fraction > leapAt ? 2 : 3;

        Health health;
        Collider[] colliders;
        Transform player;
        CharacterController playerController;
        Transform viewCamera;
        Vector3 bodyRest;
        Quaternion bodyRestRotation, armLRest, armRRest;
        float armLAngle, armRAngle;
        float leftArm, rightArm, crouch, pitch;
        bool roaring, leaping, called;
        int nextPerch;
        float flinchUntil;

        void Awake()
        {
            Current = this;
            health = GetComponent<Health>();
            colliders = GetComponentsInChildren<Collider>();
            health.Damaged += (amount, from) => flinchUntil = Time.time + 0.2f;
            health.Died += Defeat;
            health.Invulnerable = true; // until he's woken
            if (body != null) { bodyRest = body.localPosition; bodyRestRotation = body.localRotation; }
            if (armL != null) armLRest = armL.localRotation;
            if (armR != null) armRRest = armR.localRotation;
            if (roarIcon != null) roarIcon.SetActive(false);
            foreach (var m in reinforcements) if (m != null) m.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Start()
        {
            viewCamera = Camera.main != null ? Camera.main.transform : null;
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
                playerController = playerObject.GetComponent<CharacterController>();
            }
        }

        void Update()
        {
            if (player == null) return;
            if (Now == State.Waiting && Vector3.Distance(player.position, transform.position) < wakeRange) Wake();
            if (Now == State.Fighting && !leaping)
            {
                Vector3 to = player.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 220f * Time.deltaTime);
            }
            Pose();
        }

        void Wake()
        {
            if (Now != State.Waiting) return;
            Now = State.Fighting;
            health.Invulnerable = false;
            Woke?.Invoke();
            StartCoroutine(Battle());
        }

        // ---------- The fight ----------

        IEnumerator Battle()
        {
            yield return Roar(1.6f);
            while (Now == State.Fighting)
            {
                if (!InReach())
                {
                    // Out of the arena: beats his chest at them until they come back.
                    yield return Roar(1f);
                    yield return Pause(1f);
                    continue;
                }
                int round = Round;
                if (round >= 2 && !called)
                {
                    called = true;
                    yield return Roar(1.4f);
                    CallForHelp();
                }
                yield return Bomb();
                if (round >= 3) { yield return Pause(0.5f); yield return Bomb(); }
                yield return Pause(round == 1 ? 1.2f : 0.8f);
                yield return Volley(round >= 3 ? 5 : 3);
                yield return Pause(round == 1 ? 1.5f : 1f);
                if (round >= 2) { yield return Volley(round >= 3 ? 5 : 3); yield return Pause(0.9f); }
                if (Round >= 3 && perches.Length > 0) yield return Leap(perches[nextPerch++ % perches.Length].position);
            }
        }

        IEnumerator Pause(float seconds)
        {
            for (float t = 0f; t < seconds && Now == State.Fighting; t += Time.deltaTime) yield return null;
        }

        IEnumerator Roar(float seconds)
        {
            roaring = true;
            if (roarIcon != null) roarIcon.SetActive(true);
            yield return Pause(seconds);
            roaring = false;
            if (roarIcon != null) roarIcon.SetActive(false);
        }

        void CallForHelp()
        {
            for (int i = 0; i < reinforcements.Length; i++)
            {
                if (reinforcements[i] == null) continue;
                Vector3 at = dropPoints != null && i < dropPoints.Length ? dropPoints[i].position : transform.position + transform.right * (i % 2 == 0 ? 4f : -4f);
                reinforcements[i].DropIn(at);
            }
        }

        // Both arms up with a big one, then lobbed high to land on a marker where the player
        // is headed.
        IEnumerator Bomb()
        {
            leftArm = rightArm = 160f;
            yield return Pause(0.55f);
            leftArm = rightArm = 0f;
            if (Now != State.Fighting || !InReach()) yield break;
            Vector3 target = player.position + PlayerVelocity() * (bombFlightTime * lead);
            if (Physics.Raycast(target + Vector3.up * 2f, Vector3.down, out RaycastHit ground, 6f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                target = ground.point;
            Vector3 from = HandPosition() + Vector3.up * 0.6f;
            if (bombMarkerPrefab != null)
            {
                var marker = Instantiate(bombMarkerPrefab, target + Vector3.up * 0.03f, Quaternion.identity);
                Destroy(marker, bombFlightTime);
            }
            var bomb = Instantiate(bombPrefab, from, Quaternion.identity);
            bomb.Launch(PooProjectile.AimTimed(from, target, bombFlightTime, gravity), Team.Greys, transform, gravity);
            leftArm = rightArm = 0f;
        }

        // A fan of ordinary throws at the player.
        IEnumerator Volley(int count)
        {
            rightArm = 150f;
            yield return Pause(0.35f);
            if (Now != State.Fighting || !InReach()) { rightArm = 0f; yield break; }
            Vector3 from = HandPosition();
            Vector3 chest = player.position + Vector3.up * ((playerController != null ? playerController.height : 1f) * 0.6f);
            float time = Vector3.Distance(from, chest) / throwSpeed;
            Vector3 target = chest + PlayerVelocity() * (time * lead);
            for (int i = 0; i < count; i++)
            {
                float angle = (i - (count - 1) * 0.5f) * fanAngle;
                Vector3 aimAt = from + Quaternion.Euler(0f, angle, 0f) * (target - from);
                var poo = Instantiate(pooPrefab, from, Quaternion.identity);
                poo.Launch(PooProjectile.Aim(from, aimAt, throwSpeed, gravity), Team.Greys, transform, gravity);
            }
            rightArm = 330f;
            yield return Pause(0.15f);
            rightArm = 0f;
        }

        IEnumerator Leap(Vector3 to)
        {
            leaping = true;
            Vector3 from = transform.position;
            leftArm = rightArm = 170f;
            for (float t = 0f; t < leapTime && Now == State.Fighting; t += Time.deltaTime)
            {
                float k = t / leapTime;
                transform.position = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * leapHeight);
                yield return null;
            }
            if (Now == State.Fighting) transform.position = to;
            leftArm = rightArm = 0f;
            leaping = false;
        }

        bool InReach() => player != null && Vector3.Distance(player.position, transform.position) < fightRange;

        Vector3 PlayerVelocity()
        {
            Vector3 v = playerController != null ? playerController.velocity : Vector3.zero;
            v.y = 0f;
            return v;
        }

        Vector3 HandPosition() => hand != null ? hand.position : transform.position + Vector3.up * 2.5f;

        void Defeat()
        {
            Now = State.Defeated;
            StopAllCoroutines();
            roaring = leaping = false;
            if (roarIcon != null) roarIcon.SetActive(false);
            foreach (var c in colliders) c.enabled = false;
            // Down on the throne or wherever he was, onto the ground.
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit ground, 20f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                transform.position = ground.point;
            if (crown != null)
            {
                crown.SetParent(null, true);
                var rb = crown.gameObject.AddComponent<Rigidbody>();
                crown.gameObject.AddComponent<SphereCollider>().radius = 0.3f;
                rb.linearVelocity = Vector3.up * 5f + transform.forward * 2f;
                rb.angularVelocity = UnityEngine.Random.onUnitSphere * 6f;
            }
            if (gate != null) gate.Open();
            onDefeated.Invoke();
            Beaten?.Invoke();
        }

        // ---------- Looks ----------

        void Pose()
        {
            if (body == null) return;
            float t = Time.time;
            float targetCrouch = 0f, targetPitch = 0f, l = leftArm, r = rightArm;
            Vector3 bounce = Vector3.zero;
            switch (Now)
            {
                case State.Waiting:
                    // Slouched on the throne, scratching.
                    targetCrouch = 0.35f; targetPitch = 12f;
                    l = 20f; r = 140f + Mathf.Sin(t * 10f) * 12f;
                    break;
                case State.Defeated:
                    targetPitch = -82f;
                    l = r = 70f;
                    break;
                default:
                    if (roaring)
                    {
                        // Beats his chest, head back.
                        targetPitch = -14f;
                        l = 110f + Mathf.Sin(t * 16f) * 35f;
                        r = 110f - Mathf.Sin(t * 16f) * 35f;
                    }
                    else if (!leaping) bounce = Vector3.up * (Mathf.Abs(Mathf.Sin(t * 5f)) * 0.12f);
                    break;
            }
            float rate = Time.deltaTime * 8f;
            crouch = Mathf.Lerp(crouch, targetCrouch, rate);
            pitch = Mathf.Lerp(pitch, targetPitch + (Time.time < flinchUntil ? -12f : 0f), rate);
            body.SetLocalPositionAndRotation(bodyRest + bounce + Vector3.down * crouch, bodyRestRotation * Quaternion.Euler(pitch, 0f, 0f));
            armLAngle = Mathf.Lerp(armLAngle, l, rate * 1.5f);
            armRAngle = Mathf.Lerp(armRAngle, r, rate * 1.5f);
        }

        void LateUpdate()
        {
            // After the model's Animator, which would otherwise put the arms back.
            if (armL != null) armL.localRotation = armLRest * Quaternion.Euler(armLAngle, 0f, 0f);
            if (armR != null) armR.localRotation = armRRest * Quaternion.Euler(armRAngle, 0f, 0f);
            if (roarIcon == null || !roarIcon.activeSelf || viewCamera == null) return;
            Vector3 facing = viewCamera.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f) roarIcon.transform.rotation = Quaternion.LookRotation(facing);
        }
    }
}
