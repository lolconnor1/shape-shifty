using System.Collections.Generic;
using ShapeShifty.AI;
using ShapeShifty.Player;
using UnityEngine;

namespace ShapeShifty.Interaction
{
    // Water spreading across the floor, e.g. from a valve's spout. Flood() makes it spread out
    // to full size, stay a while and then dry up. While it covers the bare end of a live wire
    // (see BareWire) it's electrified: it flickers and crackles, and anyone who walks into it
    // gets a shock. Employees are out of action for a good while (see EmployeePatrol.Stun); the
    // player, in any form, for a few seconds (see PlayerStun). Each can be shocked again after
    // leaving the water and coming back in. Any dead wire it covers while it's live gets power,
    // and so does whatever that wire leads to.
    public class WaterPuddle : MonoBehaviour
    {
        [Tooltip("A flat disc centred on this object, scaled to the puddle's size.")]
        [SerializeField] Transform water;
        [SerializeField] Renderer waterRenderer;
        [SerializeField] float maxRadius = 3f;
        [Tooltip("How long (s) it takes to spread to full size.")]
        [SerializeField] float spreadTime = 3f;
        [Tooltip("How long (s) it stays at full size before drying up.")]
        [SerializeField] float wetTime = 25f;
        [SerializeField] float dryTime = 4f;
        [SerializeField] Color wetColor = new Color(0.25f, 0.45f, 0.65f, 0.55f);

        [Header("Electricity")]
        [Tooltip("How long (s) a shocked employee is out of action.")]
        [SerializeField] float stunTime = 15f;
        [Tooltip("How long (s) the player is stunned if they step in while it's live.")]
        [SerializeField] float playerStunTime = 3f;
        [Tooltip("How long (s) sparks keep crackling round someone after they're shocked.")]
        [SerializeField] float crackleTime = 1.5f;
        [Tooltip("Crackle across the water while it's live and burst round anyone shocked. Simulated in world space, emitted from code.")]
        [SerializeField] ParticleSystem sparks;
        [SerializeField] Color liveColor = new Color(0.6f, 0.9f, 1f, 0.7f);
        [ColorUsage(false, true)]
        [SerializeField] Color liveGlow = new Color(0.4f, 0.9f, 1.6f);

        public bool IsWet => radius > 0f;
        public bool IsLive { get; private set; }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // Someone just shocked, crackling until the time is up.
        struct Zap
        {
            public Transform target;
            public float height;
            public float until;
        }

        readonly HashSet<EmployeePatrol> shocked = new HashSet<EmployeePatrol>();
        readonly List<Zap> zaps = new List<Zap>();
        MaterialPropertyBlock block;
        PlayerStun player;
        CharacterController playerController;
        bool playerShocked;
        float radius;
        float wetTimer;
        float crackleTimer;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            water.gameObject.SetActive(false);
        }

        void Start()
        {
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject == null) return;
            player = playerObject.GetComponent<PlayerStun>();
            playerController = playerObject.GetComponent<CharacterController>();
        }

        // Spreads out (or tops up a puddle that's already there), then dries after a while.
        public void Flood() => wetTimer = spreadTime + wetTime;

        void Update()
        {
            wetTimer = Mathf.Max(0f, wetTimer - Time.deltaTime);
            float target = wetTimer > 0f ? maxRadius : 0f;
            float rate = maxRadius / (target > radius ? spreadTime : dryTime);
            radius = Mathf.MoveTowards(radius, target, rate * Time.deltaTime);

            water.gameObject.SetActive(radius > 0f);
            water.localScale = new Vector3(radius * 2f, water.localScale.y, radius * 2f);
            IsLive = BareWire.All.Exists(wire => wire.IsLive && Covers(wire.transform.position));
            if (IsLive)
                foreach (var wire in BareWire.All)
                    if (!wire.IsLive && Covers(wire.transform.position)) wire.Feed();

            ShockEmployees();
            ShockPlayer();
            Crackle();
            if (radius > 0f) UpdateLook();
        }

        // Standing in the water, rather than above it (e.g. riding something overhead).
        bool InWater(Vector3 feet) => feet.y < transform.position.y + 0.3f && FlatDistance(feet) < radius - 0.1f;

        // Lying in the water: a wire end on the floor under it.
        bool Covers(Vector3 point) => Mathf.Abs(point.y - transform.position.y) < 0.3f && FlatDistance(point) < radius;

        void ShockEmployees()
        {
            foreach (var employee in EmployeePatrol.All)
            {
                if (!InWater(employee.transform.position))
                {
                    shocked.Remove(employee);
                    continue;
                }
                if (IsLive && shocked.Add(employee))
                {
                    employee.Stun(stunTime);
                    StartZap(employee.transform, 1.7f);
                }
            }
        }

        void ShockPlayer()
        {
            if (player == null) return;
            if (!InWater(player.transform.position))
            {
                playerShocked = false;
                return;
            }
            if (IsLive && !playerShocked)
            {
                playerShocked = true;
                player.Stun(playerStunTime);
                StartZap(player.transform, playerController != null ? playerController.height : 1f);
            }
        }

        void StartZap(Transform target, float height)
        {
            zaps.Add(new Zap { target = target, height = height, until = Time.time + crackleTime });
            for (int i = 0; i < 12; i++) Spark(AroundZap(target, height), 4);
        }

        void Crackle()
        {
            zaps.RemoveAll(zap => zap.target == null || Time.time > zap.until);
            foreach (var zap in zaps)
                if (Random.value < 0.5f) Spark(AroundZap(zap.target, zap.height), Random.Range(1, 4));
        }

        static Vector3 AroundZap(Transform target, float height) =>
            target.position + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.05f, height), Random.Range(-0.3f, 0.3f));

        void UpdateLook()
        {
            float flicker = 0f;
            if (IsLive)
            {
                flicker = Mathf.PerlinNoise(Time.time * 14f, 0.37f);
                flicker = flicker > 0.55f ? flicker : flicker * 0.3f;
                // Little arcs dancing over the surface.
                crackleTimer -= Time.deltaTime;
                if (crackleTimer <= 0f)
                {
                    crackleTimer = Random.Range(0.03f, 0.12f);
                    Vector2 p = Random.insideUnitCircle * (radius * 0.95f);
                    Spark(transform.position + new Vector3(p.x, 0.03f, p.y), Random.Range(1, 4));
                }
            }
            block.SetColor(BaseColorId, Color.Lerp(wetColor, liveColor, flicker));
            block.SetColor(EmissionColorId, liveGlow * flicker);
            waterRenderer.SetPropertyBlock(block);
        }

        void Spark(Vector3 position, int count)
        {
            if (sparks == null) return;
            var emit = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true };
            sparks.Emit(emit, count);
        }

        float FlatDistance(Vector3 point)
        {
            Vector3 offset = point - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }
    }
}
