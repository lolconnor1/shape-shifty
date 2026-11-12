using System.Collections.Generic;
using ShapeShifty.Dialogue;
using ShapeShifty.Player;
using ShapeShifty.Shapes;
using UnityEngine;

namespace ShapeShifty.AI
{
    // A penguin that gets bowled over. Stands about near its spot (PenguinWander idles it)
    // until a belly-slider, a PenguinBowler or the player, comes through fast enough: then
    // it's knocked onto its back, skids along, lies there a moment, gets up and waddles back.
    // A skidding pin bowls over the next one along. Freezes while being scanned or talked to.
    public class PenguinPin : MonoBehaviour
    {
        [Tooltip("Where it stands. Waddles back here after being knocked over.")]
        [SerializeField] Transform spot;
        [SerializeField] Transform body;
        [Tooltip("Idles it about between knocks; switched off while it's down.")]
        [SerializeField] PenguinWander wander;

        [Header("Being Knocked Over")]
        [Tooltip("How fast (m/s) a slider has to hit it to knock it over.")]
        [SerializeField] float minHitSpeed = 2.5f;
        [Tooltip("How close (m) a sliding player has to come.")]
        [SerializeField] float playerHitRadius = 0.7f;
        [Tooltip("Speed it skids off at, as a fraction of the hitter's.")]
        [SerializeField] float knockback = 0.4f;
        [SerializeField] float skidFriction = 4f;
        [Tooltip("A pin still skidding this fast knocks over the next one it reaches.")]
        [SerializeField] float chainSpeed = 1.5f;
        [SerializeField] float chainRadius = 0.6f;
        [Tooltip("Seconds flat on its back before getting up.")]
        [SerializeField] float downTime = 2f;
        [Tooltip("Where the body sits while on its back, so it lies centred on this object.")]
        [SerializeField] Vector3 downOffset = new Vector3(0f, 0.05f, 0.4f);

        [Header("Getting Back")]
        [SerializeField] float walkSpeed = 1.1f;
        [SerializeField] float turnSpeed = 240f;
        [SerializeField] float waddleAngle = 10f;
        [SerializeField] float waddleRate = 9f;
        [SerializeField] LayerMask groundMask = ~(1 << 2);

        public static readonly List<PenguinPin> Active = new List<PenguinPin>();

        public bool IsDown => state == State.Down;

        enum State { Standing, Down, Returning }

        State state;
        Vector3 skid;
        float timer;
        float phase;
        float flat; // 0 stood up, 1 flat on its back
        Scannable scannable;
        Talkable talkable;
        PlayerController player;

        void Awake()
        {
            scannable = GetComponent<Scannable>();
            talkable = GetComponent<Talkable>();
        }

        void Start()
        {
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null) player = playerObject.GetComponent<PlayerController>();
        }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        // A slider's come through at this velocity. Ignored if it's too slow or the pin's already down.
        public void Topple(Vector3 velocity)
        {
            velocity.y = 0f;
            if (IsDown || velocity.magnitude < minHitSpeed) return;
            state = State.Down;
            if (wander != null) wander.enabled = false;
            skid = velocity * knockback;
            timer = downTime;
            phase = 0f;
            // On its back with its head the way it was hit: the body lies out behind it.
            transform.rotation = Quaternion.LookRotation(-skid);
        }

        void Update()
        {
            if (scannable != null && scannable.IsBeingScanned) return; // frozen by the scanner
            if (talkable != null && talkable.IsTalking) return;      // Talkable turns it to face the player

            switch (state)
            {
                case State.Standing:
                    WatchForPlayer();
                    return; // PenguinWander has the body
                case State.Down:
                    transform.position = SnapToGround(transform.position + skid * Time.deltaTime);
                    skid = Vector3.MoveTowards(skid, Vector3.zero, skidFriction * Time.deltaTime);
                    if (skid.magnitude > chainSpeed) Chain();
                    timer -= Time.deltaTime;
                    if (timer <= 0f) state = State.Returning;
                    break;
                case State.Returning:
                    if (flat <= 0f && WalkTowards(spot.position))
                    {
                        state = State.Standing;
                        body.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                        if (wander != null) wander.enabled = true;
                        return;
                    }
                    break;
            }
            Pose();
        }

        void WatchForPlayer()
        {
            if (player == null || !player.IsSliding) return;
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < playerHitRadius * playerHitRadius) Topple(player.HorizontalVelocity);
        }

        void Chain()
        {
            foreach (var other in Active)
            {
                if (other == this || other.IsDown) continue;
                Vector3 toOther = other.transform.position - transform.position;
                toOther.y = 0f;
                if (toOther.sqrMagnitude > chainRadius * chainRadius) continue;
                other.Topple(skid.normalized * Mathf.Max(skid.magnitude, minHitSpeed));
            }
        }

        bool WalkTowards(Vector3 target)
        {
            Vector3 toTarget = target - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f) return true;
            var facing = Quaternion.LookRotation(toTarget);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * Time.deltaTime);
            if (Quaternion.Angle(transform.rotation, facing) < 30f)
            {
                Vector3 step = Vector3.ClampMagnitude(toTarget, walkSpeed * Time.deltaTime);
                transform.position = SnapToGround(transform.position + step);
                phase += Time.deltaTime * waddleRate;
            }
            return false;
        }

        Vector3 SnapToGround(Vector3 position)
        {
            if (Physics.Raycast(position + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 3f, groundMask, QueryTriggerInteraction.Ignore))
                position.y = hit.point.y;
            return position;
        }

        void Pose()
        {
            flat = Mathf.MoveTowards(flat, IsDown ? 1f : 0f, Time.deltaTime / 0.3f);
            float roll;
            if (IsDown)
            {
                // Flails, then settles.
                phase += Time.deltaTime;
                roll = Mathf.Sin(phase * 14f) * 8f * Mathf.Clamp01(1.5f - phase);
            }
            else roll = Mathf.Sin(phase) * waddleAngle * (1f - flat);
            body.SetLocalPositionAndRotation(downOffset * flat, Quaternion.Euler(-80f * flat, 0f, roll));
        }
    }
}
