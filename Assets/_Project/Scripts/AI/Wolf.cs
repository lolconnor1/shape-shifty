using System;
using System.Collections.Generic;
using ShapeShifty.Audio;
using ShapeShifty.Dialogue;
using ShapeShifty.Interaction;
using ShapeShifty.Player;
using ShapeShifty.Shapes;
using UnityEngine;

namespace ShapeShifty.AI
{
    // A wolf in its enclosure. Prowls about near its den (or lies there, with no roam
    // radius) and deals with the player two ways. Anything that isn't a wolf it spots on
    // sight (its VisionCone, which ignores wolves) and charges: reaching them ends the level.
    // A wolf-shaped player it can't see through, but can smell: inside its scent range a
    // sniff meter fills, and once full it stalks them, closing in to pounce.
    // A howl (PlayerHowl) in earshot stops it in its tracks: it throws its head back and
    // howls along for as long as the player does and a good while after, up to a limit that
    // shrinks each time it has heard a howl recently. It forgets any scent it had, and takes
    // a moment to start sniffing again.
    // A thrown Bone landing in range draws it over to gnaw, and it carries it home to lie
    // down with for a long while. A player carrying a bone smells of it from further away,
    // and a wolf that reaches them takes the bone instead of biting.
    [RequireComponent(typeof(VisionCone))]
    public class Wolf : MonoBehaviour
    {
        public enum State { Prowling, Stalking, Pouncing, Charging, Howling, Fetching, Gnawing, Carrying, Resting, Done }

        [Header("Prowling")]
        [Tooltip("The spot the wolf stays near, and where it lies down with a bone.")]
        [SerializeField] Transform den;
        [Tooltip("How far from the den it prowls. 0 keeps it lying at the den.")]
        [SerializeField] float roamRadius = 4f;
        [SerializeField] float walkSpeed = 1.6f;
        [SerializeField] float turnSpeed = 240f;
        [Tooltip("Seconds stood still between prowls (min, max).")]
        [SerializeField] Vector2 pauseTime = new Vector2(2f, 6f);
        [Tooltip("Pays the player no mind at all: for a wolf in a crate.")]
        [SerializeField] bool tame;
        [Tooltip("Layers it walks round and stands on. Ignore Raycast (the invisible level bounds) is left out.")]
        [SerializeField] LayerMask obstacleMask = ~(1 << 2);

        [Header("Scent")]
        [Tooltip("How close (m) the player has to be to be smelt.")]
        [SerializeField] float scentRange = 6f;
        [Tooltip("Scent range while the player is carrying a bone.")]
        [SerializeField] float boneScentRange = 10f;
        [Tooltip("Seconds up close to be sure of the scent. Longer at the edge of the range, shorter if the player is sprinting.")]
        [SerializeField] float sniffTime = 2f;
        [Tooltip("Seconds out of range for the scent to fade away.")]
        [SerializeField] float forgetTime = 4f;
        [Tooltip("How fast it follows its nose towards the player while it's still making up its mind. Slower than the player walks.")]
        [SerializeField] float trackSpeed = 1.8f;
        [Tooltip("How close (m) it creeps while sniffing before it stands its ground and sniffs.")]
        [SerializeField] float trackStopDistance = 2.8f;
        [Tooltip("Once stalking, the scent only fades beyond this distance (m).")]
        [SerializeField] float loseRange = 16f;
        [Tooltip("Faster than the player can run: once it's on the scent, only a howl or a bone shakes it.")]
        [SerializeField] float stalkSpeed = 6.5f;
        [Tooltip("Close enough (m) to pounce.")]
        [SerializeField] float pounceRange = 2.5f;
        [SerializeField] float pounceTime = 0.4f;
        [Tooltip("Landing this close (m) to the player is a bite.")]
        [SerializeField] float biteRange = 1.2f;

        [Header("Sight")]
        [Tooltip("How fast it runs at something it has seen that isn't a wolf.")]
        [SerializeField] float chargeSpeed = 7.5f;

        [Header("Howling")]
        [Tooltip("How far (m) it can hear the player howl.")]
        [SerializeField] float hearingRange = 12f;
        [Tooltip("Longest it goes on howling after the player stops (s): twice as long as they howled, up to this.")]
        [SerializeField] float maxReply = 8f;
        [SerializeField] float minReply = 3.5f;
        [Tooltip("Seconds after howling before it starts sniffing again.")]
        [SerializeField] float noseRest = 2f;
        [Tooltip("Each howl heard within Boredom Time of the last cuts the longest reply to this fraction of itself.")]
        [SerializeField, Range(0.1f, 1f)] float boredomFactor = 0.8f;
        [Tooltip("Seconds without hearing a howl before it's interested again.")]
        [SerializeField] float boredomTime = 45f;

        [Header("Bones")]
        [Tooltip("How far (m) it notices a bone landing.")]
        [SerializeField] float boneRange = 15f;
        [SerializeField] float fetchSpeed = 8f;
        [SerializeField] float gnawTime = 6f;
        [Tooltip("How long (s) it lies at its den with a bone before it's finished with it.")]
        [SerializeField] float restTime = 60f;

        [Header("Visual")]
        [Tooltip("The model: crouched, tipped and bobbed with what the wolf is doing.")]
        [SerializeField] Transform body;
        [Tooltip("Under the model: thrown back to howl, dropped to sniff.")]
        [SerializeField] Transform head;
        [Tooltip("Where a carried bone sits.")]
        [SerializeField] Transform mouth;
        [Tooltip("Shown above the head while it has a scent. Turned to face the camera.")]
        [SerializeField] GameObject scentIcon;
        [Tooltip("Scaled sideways from its left edge to show how sure it is of the scent.")]
        [SerializeField] Transform scentFill;
        [SerializeField] Renderer scentFillRenderer;
        [SerializeField] Color scentFaintColor = new Color(0.6f, 0.9f, 0.4f);
        [SerializeField] Color scentSureColor = new Color(1f, 0.15f, 0.1f);
        [Tooltip("Shown above the head while it howls. Turned to face the camera.")]
        [SerializeField] GameObject howlIcon;

        public static readonly List<Wolf> All = new List<Wolf>();
        // A wolf got the player.
        public static event Action<Wolf> PlayerCaught;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public State Current { get; private set; }
        // 0 = nothing, 1 = sure of the player's scent.
        public float Scent { get; private set; }
        public bool IsHowling => Current == State.Howling;
        public Bone HeldBone => bone;

        VisionCone vision;
        Scannable scannable;
        Talkable talkable;
        Transform player;
        CharacterController playerController;
        PlayerHowl howl;
        PlayerCarry carry;
        Transform viewCamera;
        MaterialPropertyBlock block;

        Vector3 destination;
        bool walking, returning, moved;
        // Where it started, and where it last smelt the player.
        Vector3 homePosition, scentTrail;
        Quaternion homeRotation;
        float pauseTimer, phase;
        float replyCap, howlUntil, noseBack, lastHeard = float.NegativeInfinity;
        int boredom, lastHowlCount;
        Vector3 pounceFrom, pounceTo;
        float pounceTimer, timer;
        // The bone it's after or has, and one it couldn't get to.
        Bone bone, uninteresting;
        float nextLook, fetchBest, fetchStuck;
        Vector3 bodyRest;
        Quaternion headRest;
        float crouch, bodyPitch, headPitch;
        readonly RaycastHit[] groundHits = new RaycastHit[8];
        Vector3 lastGrounded;

        void Awake()
        {
            vision = GetComponent<VisionCone>();
            scannable = GetComponent<Scannable>();
            talkable = GetComponent<Talkable>();
            block = new MaterialPropertyBlock();
            if (body != null) bodyRest = body.localPosition;
            if (head != null) headRest = head.localRotation;
            if (scentIcon != null) scentIcon.SetActive(false);
            if (howlIcon != null) howlIcon.SetActive(false);
        }

        void OnEnable()
        {
            All.Add(this);
            Bone.Landed += OnBoneLanded;
        }

        void OnDisable()
        {
            All.Remove(this);
            Bone.Landed -= OnBoneLanded;
        }

        void Start()
        {
            viewCamera = Camera.main != null ? Camera.main.transform : null;
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
                playerController = playerObject.GetComponent<CharacterController>();
                howl = playerObject.GetComponent<PlayerHowl>();
                carry = playerObject.GetComponent<PlayerCarry>();
                if (howl != null) lastHowlCount = howl.HowlCount;
            }
            pauseTimer = UnityEngine.Random.Range(pauseTime.x, pauseTime.y);
            lastGrounded = transform.position;
            homePosition = transform.position;
            homeRotation = transform.rotation;
            if (tame) vision.enabled = false;
        }

        void Update()
        {
            moved = false;
            if (scannable != null && scannable.IsBeingScanned) return; // frozen by the scanner
            if (talkable != null && talkable.IsTalking) { Pose(); return; } // Talkable turns it to the player
            if (tame || Current == State.Done) { Pose(); return; }
            if (player == null) { Prowl(); Pose(); return; }

            if (Time.time - lastHeard > boredomTime) boredom = 0;
            ListenForHowl();
            LookForBones();

            switch (Current)
            {
                case State.Prowling: Prowl(); Smell(); Watch(); break;
                case State.Stalking: Stalk(); Smell(); Watch(); break;
                case State.Pouncing: Pounce(); break;
                case State.Charging: Charge(); break;
                case State.Howling: HowlAlong(); break;
                case State.Fetching: Fetch(); break;
                case State.Gnawing: Gnaw(); break;
                case State.Carrying: Carry(); break;
                case State.Resting: Rest(); break;
            }
            Pose();
        }

        // ---------- Senses ----------

        float PlayerDistance()
        {
            Vector3 to = player.position - transform.position;
            to.y = 0f;
            return to.magnitude;
        }

        // The sniff meter fills in range (faster up close and when the player runs about) and
        // drains out of it. Full means stalking; empty means giving up.
        void Smell()
        {
            if (Time.time < noseBack) return;
            float range = carry != null && carry.HasBone ? boneScentRange : scentRange;
            float distance = PlayerDistance();
            if (distance < range)
            {
                scentTrail = player.position;
                float nearness = Mathf.Lerp(1f, 0.4f, distance / range);
                float speed = playerController != null ? new Vector2(playerController.velocity.x, playerController.velocity.z).magnitude : 0f;
                // Walking off calmly doesn't give you away; sprinting does.
                float running = Mathf.Lerp(1f, 2.5f, Mathf.InverseLerp(5f, 8f, speed));
                Scent = Mathf.Min(1f, Scent + nearness * running * Time.deltaTime / sniffTime);
            }
            else if (!(Current == State.Stalking && distance < loseRange))
            {
                Scent = Mathf.Max(0f, Scent - Time.deltaTime / forgetTime);
            }

            if (Current == State.Prowling && Scent >= 1f) Current = State.Stalking;
            else if (Current == State.Stalking && Scent <= 0f) Current = State.Prowling;
        }

        void Watch()
        {
            if (vision.enabled && vision.IsSpotted) Current = State.Charging;
        }

        void ListenForHowl()
        {
            if (howl == null || howl.HowlCount == lastHowlCount || !howl.IsHowling) return;
            if (PlayerDistance() > Mathf.Min(hearingRange, howl.Range)) return;
            lastHowlCount = howl.HowlCount;
            bool canHear = Current == State.Prowling || Current == State.Stalking || Current == State.Charging || Current == State.Fetching;
            if (!canHear) return;
            if (Time.time - lastHeard <= boredomTime) boredom++;
            lastHeard = Time.time;
            replyCap = Mathf.Max(minReply, maxReply * Mathf.Pow(boredomFactor, boredom));
            howlUntil = Time.time + minReply;
            walking = false;
            bone = null;
            Current = State.Howling;
        }

        void OnBoneLanded(Bone landed)
        {
            if (tame || Current == State.Done) return;
            if (landed == uninteresting) return;
            // Even mid-charge: nobody can resist a bone.
            bool free = Current == State.Prowling || Current == State.Stalking || Current == State.Charging || Current == State.Howling;
            if (!free) return;
            Vector3 to = landed.transform.position - transform.position;
            to.y = 0f;
            if (to.magnitude > boneRange) return;
            bone = landed;
            walking = false;
            fetchBest = float.PositiveInfinity;
            fetchStuck = 0f;
            Current = State.Fetching;
        }

        // A thrown bone lying about unclaimed goes on tempting any wolf that comes near it or
        // has nothing better to do, not just the ones about when it landed.
        void LookForBones()
        {
            if (Time.time < nextLook || Current == State.Howling) return;
            nextLook = Time.time + 0.3f;
            foreach (var b in Bone.All) if (b.IsLure) OnBoneLanded(b);
        }

        // ---------- States ----------

        void Prowl()
        {
            if (Scent > 0f && player != null) { Track(); return; }
            if (!walking)
            {
                pauseTimer -= Time.deltaTime;
                if (pauseTimer > 0f) return;
                // Followed a scent away from home: heads back first.
                Vector3 home = Home();
                Vector3 away = home - transform.position;
                away.y = 0f;
                if (away.magnitude > Mathf.Max(roamRadius + 1f, 1f))
                {
                    destination = home;
                    walking = returning = true;
                    return;
                }
                if (roamRadius <= 0f)
                {
                    // A wolf lying at its den settles back the way it was facing.
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, homeRotation, turnSpeed * 0.5f * Time.deltaTime);
                    return;
                }
                walking = TryPickDestination(out destination);
                if (!walking) pauseTimer = 0.5f;
                return;
            }
            if (MoveTowards(destination, walkSpeed, returning))
            {
                walking = returning = false;
                pauseTimer = UnityEngine.Random.Range(pauseTime.x, pauseTime.y);
            }
        }

        // Caught a whiff but not sure yet: follows its nose towards the player (or where it
        // last smelt them), slower than they walk, and stands sniffing once it's close.
        void Track()
        {
            walking = returning = false;
            pauseTimer = 1f;
            float range = carry != null && carry.HasBone ? boneScentRange : scentRange;
            float distance = PlayerDistance();
            Vector3 toPlayer = Flat(player.position - transform.position);
            if (distance < trackStopDistance) { Turn(toPlayer); return; }
            Vector3 target = distance < range ? player.position : scentTrail;
            if (MoveTowards(target, trackSpeed, true)) Turn(toPlayer);
        }

        Vector3 Home() => roamRadius <= 0f || den == null ? homePosition : den.position;

        void Stalk()
        {
            float distance = PlayerDistance();
            if (distance < pounceRange && Vector3.Angle(Flat(transform.forward), Flat(player.position - transform.position)) < 40f)
            {
                // Leads a running player: lands where they'll be, not where they were.
                Vector3 lead = playerController != null ? playerController.velocity : Vector3.zero;
                lead.y = 0f;
                pounceFrom = transform.position;
                pounceTo = player.position + lead * pounceTime;
                pounceTimer = 0f;
                Current = State.Pouncing;
                Sfx.Play(Sound.Bark, transform.position, 1f, 0.7f);
                return;
            }
            // Lost them: stands sniffing after them while the scent fades, rather than
            // running on and never losing it.
            if (distance > loseRange) { Turn(Flat(player.position - transform.position)); return; }
            MoveTowards(player.position, stalkSpeed, true);
        }

        // A lunge at where the player was.
        void Pounce()
        {
            pounceTimer += Time.deltaTime;
            float k = Mathf.Clamp01(pounceTimer / pounceTime);
            transform.position = Vector3.Lerp(pounceFrom, pounceTo, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.5f);
            moved = true;
            if (k < 1f) return;
            SnapToGround();
            if (PlayerDistance() < biteRange) Bite();
            else Current = Scent > 0f ? State.Stalking : State.Prowling;
        }

        void Charge()
        {
            Vector3 target = vision.CanSeeTarget ? player.position : vision.LastSeenPosition;
            bool there = MoveTowards(target, chargeSpeed, true);
            if (PlayerDistance() < biteRange) Bite();
            else if (there && !vision.IsSpotted) Current = Scent >= 1f ? State.Stalking : State.Prowling;
        }

        void HowlAlong()
        {
            vision.enabled = false;
            Scent = Mathf.MoveTowards(Scent, 0f, Time.deltaTime * 0.8f);
            // Keeps going as long as the player does, and then for twice as long as they did,
            // between the shortest and longest reply.
            if (howl != null && howl.IsHowling && howl.HowlCount == lastHowlCount)
                howlUntil = Time.time + Mathf.Clamp(howl.HowlTime * 2f, minReply, replyCap);
            if (Time.time < howlUntil) return;
            vision.enabled = true;
            Scent = 0f;
            noseBack = Time.time + noseRest;
            pauseTimer = noseRest;
            Current = State.Prowling;
        }

        void Fetch()
        {
            vision.enabled = false;
            if (bone == null || bone.IsHeld || (bone.ClaimedBy != null && bone.ClaimedBy != this))
            {
                GiveUpBone();
                return;
            }
            // Near enough to get its teeth into: it may be lying against a rock.
            Vector3 to = bone.transform.position - transform.position;
            to.y = 0f;
            if (to.magnitude > 1f)
            {
                MoveTowards(bone.transform.position, fetchSpeed, true);
                // Can't get any closer: forgets about it.
                if (to.magnitude < fetchBest - 0.1f) { fetchBest = to.magnitude; fetchStuck = 0f; }
                else fetchStuck += Time.deltaTime;
                if (fetchStuck > 3f) { uninteresting = bone; GiveUpBone(); }
                return;
            }
            Turn(Flat(to));
            bone.ClaimedBy = this;
            timer = gnawTime;
            Current = State.Gnawing;
        }

        void Gnaw()
        {
            if (bone == null) { GiveUpBone(); return; }
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            bone.PickUp(mouth != null ? mouth : transform);
            Current = State.Carrying;
        }

        void Carry()
        {
            if (bone == null) { GiveUpBone(); return; }
            if (!MoveTowards(den != null ? den.position : transform.position, walkSpeed * 1.4f, true)) return;
            timer = restTime;
            Current = State.Resting;
        }

        void Rest()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            if (bone != null) bone.Consume();
            bone = null;
            vision.enabled = true;
            pauseTimer = 1f;
            Current = State.Prowling;
        }

        void GiveUpBone()
        {
            bone = null;
            vision.enabled = true;
            Current = Scent >= 1f ? State.Stalking : State.Prowling;
        }

        // Got the player: takes their bone if they have one, otherwise that's that.
        void Bite()
        {
            if (carry != null && carry.HasBone)
            {
                bone = carry.Surrender();
                bone.PickUp(mouth != null ? mouth : transform);
                Scent = 0f;
                vision.enabled = false;
                Current = State.Carrying;
                return;
            }
            Current = State.Done;
            Debug.Log($"{name} got the player!", this);
            PlayerCaught?.Invoke(this);
        }

        // ---------- Moving ----------

        // Steps towards a spot, going round things in the way if asked; true once there.
        bool MoveTowards(Vector3 target, float speed, bool avoid)
        {
            Vector3 to = target - transform.position;
            to.y = 0f;
            if (to.magnitude < 0.2f) return true;
            Vector3 direction = to.normalized;
            if (avoid) direction = Steer(direction);
            Turn(direction);
            if (Vector3.Angle(Flat(transform.forward), direction) < 45f)
            {
                transform.position += Flat(transform.forward) * (speed * Time.deltaTime);
                SnapToGround();
                moved = true;
                phase += Time.deltaTime * speed * 4f;
            }
            return false;
        }

        // Swings round whatever's in the way, trying ever wider angles either side.
        Vector3 Steer(Vector3 direction)
        {
            if (Clear(direction)) return direction;
            for (int angle = 30; angle <= 150; angle += 30)
            {
                Vector3 left = Quaternion.Euler(0f, -angle, 0f) * direction;
                if (Clear(left)) return left;
                Vector3 right = Quaternion.Euler(0f, angle, 0f) * direction;
                if (Clear(right)) return right;
            }
            return direction;
        }

        bool Clear(Vector3 direction)
        {
            Vector3 from = transform.position + Vector3.up * 0.5f;
            return !Physics.SphereCast(from, 0.35f, direction, out RaycastHit hit, 1.6f, obstacleMask, QueryTriggerInteraction.Ignore)
                || hit.transform.CompareTag("Player");
        }

        void Turn(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
        }

        // Drops onto whatever's underneath, checked from shoulder height so a den roof
        // overhead doesn't count, and never onto the player. Won't step up more than a
        // small ledge: anything taller is walked round, not climbed.
        void SnapToGround()
        {
            const float From = 0.6f, MaxStep = 0.45f;
            int count = Physics.RaycastNonAlloc(transform.position + Vector3.up * From, Vector3.down, groundHits, 4f, obstacleMask, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
                if (groundHits[i].distance < best && !groundHits[i].transform.CompareTag("Player")) best = groundHits[i].distance;
            if (best == float.PositiveInfinity) return;
            float groundY = transform.position.y + From - best;
            if (groundY - transform.position.y > MaxStep) transform.position = lastGrounded;
            else transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
            lastGrounded = transform.position;
        }

        bool TryPickDestination(out Vector3 point)
        {
            Vector3 home = den != null ? den.position : transform.position;
            Vector3 from = transform.position + Vector3.up * 0.4f;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * roamRadius;
                Vector3 candidate = home + new Vector3(offset.x, 0f, offset.y);
                Vector3 path = candidate - transform.position;
                path.y = 0f;
                float length = path.magnitude;
                if (length < 1f) continue;
                if (Physics.SphereCast(from, 0.35f, path / length, out _, length, obstacleMask, QueryTriggerInteraction.Ignore)) continue;
                point = candidate;
                return true;
            }
            point = transform.position;
            return false;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.normalized;
        }

        // ---------- Looks ----------

        void Pose()
        {
            if (body == null) return;
            float targetCrouch = 0f, targetBodyPitch = 0f, targetHeadPitch = 0f;
            switch (Current)
            {
                case State.Prowling when Scent > 0f: targetCrouch = 0.05f; targetHeadPitch = 28f; break; // nose down, sniffing
                case State.Stalking: targetCrouch = 0.12f; targetHeadPitch = 18f; break;
                case State.Howling: targetHeadPitch = -55f; targetBodyPitch = -10f; break;
                case State.Pouncing: targetBodyPitch = -20f; break;
                case State.Charging: targetHeadPitch = 10f; break;
                case State.Gnawing: targetCrouch = 0.1f; targetHeadPitch = 35f + Mathf.Sin(Time.time * 18f) * 6f; break;
                case State.Resting: targetCrouch = 0.3f; targetHeadPitch = 12f; break;
            }
            if (tame) targetCrouch = 0.3f;
            float rate = Time.deltaTime * 6f;
            crouch = Mathf.Lerp(crouch, targetCrouch, rate);
            bodyPitch = Mathf.Lerp(bodyPitch, targetBodyPitch, rate);
            headPitch = Mathf.Lerp(headPitch, targetHeadPitch, rate);
            float bob = moved ? Mathf.Abs(Mathf.Sin(phase)) * 0.03f : 0f;
            float sway = moved ? Mathf.Sin(phase) * 3f : 0f;
            body.SetLocalPositionAndRotation(bodyRest + Vector3.up * (bob - crouch), Quaternion.Euler(bodyPitch, 0f, sway));
        }

        // After the camera has moved, so the icons face where it is this frame. And after the
        // model's Animator, which would otherwise put the head back.
        void LateUpdate()
        {
            if (head != null) head.localRotation = headRest * Quaternion.Euler(headPitch, 0f, 0f);
            bool sniffing = Scent > 0f && (Current == State.Prowling || Current == State.Stalking || Current == State.Pouncing);
            if (scentIcon != null)
            {
                if (scentIcon.activeSelf != sniffing) scentIcon.SetActive(sniffing);
                if (sniffing)
                {
                    FaceCamera(scentIcon.transform);
                    if (scentFill != null) scentFill.localScale = new Vector3(Mathf.Max(Scent, 0.001f), 1f, 1f);
                    if (scentFillRenderer != null)
                    {
                        block.SetColor(BaseColorId, Color.Lerp(scentFaintColor, scentSureColor, Scent));
                        scentFillRenderer.SetPropertyBlock(block);
                    }
                }
            }
            if (howlIcon != null)
            {
                if (howlIcon.activeSelf != IsHowling) howlIcon.SetActive(IsHowling);
                if (IsHowling)
                {
                    FaceCamera(howlIcon.transform);
                    howlIcon.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 9f) * 0.08f);
                }
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
