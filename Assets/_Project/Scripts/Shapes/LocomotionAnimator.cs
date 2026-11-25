using ShapeShifty.Player;
using UnityEngine;

namespace ShapeShifty.Shapes
{
    // Plays an animated model's idle cycle and its moving gaits (walk, run, and for some a
    // trot between) to match how fast it's actually moving, and its air pose while the
    // player is off the ground. Works the same on the player's forms and on NPCs: it watches
    // how far the model moves each frame, so whatever moves the character doesn't need to
    // know about it.
    // The Animator's controller is made by CharacterModelImporter from the model's clips.
    [RequireComponent(typeof(Animator))]
    public class LocomotionAnimator : MonoBehaviour
    {
        static readonly int GaitId = Animator.StringToHash("Gait");
        static readonly int PlaySpeedId = Animator.StringToHash("PlaySpeed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");

        [System.Serializable]
        public struct Gait
        {
            [Tooltip("Ground speed (m/s) this cycle's stride was made for.")]
            public float speed;
            [Tooltip("Blend into this gait from the slower one all the way between their speeds. Off when their legs don't line up (a trot and a gallop): the slower one plays faster up to halfway, then it switches over quickly.")]
            public bool blend;
        }

        [Tooltip("The moving gaits, slowest first, in the order the controller has them (Walk, Trot, Run).")]
        [SerializeField] Gait[] gaits = { new Gait { speed = 1.8f, blend = true }, new Gait { speed = 5f, blend = true } };
        [Tooltip("Seconds off the ground before the air pose shows, so stairs and bumps don't flicker it.")]
        [SerializeField] float airDelay = 0.15f;

        Animator animator;
        PlayerController player;
        CharacterController controller;
        Vector3 lastPosition;
        float speed, airTime;

        void Awake()
        {
            animator = GetComponent<Animator>();
            player = GetComponentInParent<PlayerController>();
            if (player != null) controller = player.GetComponent<CharacterController>();
        }

        void OnEnable()
        {
            lastPosition = transform.position;
            speed = 0f;
            airTime = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            // The player's controller knows its real speed, but while a cutscene has it switched
            // off, whatever moves the player instead is watched like an NPC.
            bool driven = player != null && player.enabled;
            Vector3 moved = driven ? controller.velocity : (transform.position - lastPosition) / dt;
            lastPosition = transform.position;
            moved.y = 0f;
            float target = moved.magnitude;
            // Lying down to slide or swim: hold still rather than run on the spot.
            if (player != null && (player.IsSliding || player.IsSwimming)) target = 0f;
            speed = Mathf.Lerp(speed, target, 1f - Mathf.Exp(-12f * dt));

            float gait, playSpeed;
            GaitAt(speed, out gait, out playSpeed);
            animator.SetFloat(GaitId, gait);
            animator.SetFloat(PlaySpeedId, playSpeed);

            bool grounded = !driven || controller.isGrounded || player.IsSliding || player.IsSwimming;
            airTime = grounded ? 0f : airTime + dt;
            animator.SetBool(GroundedId, airTime < airDelay);
        }

        // Gait 0 is idle and 1, 2... the moving gaits. Slower than the first plays it slower
        // rather than shuffling half-way between standing and walking. In between gaits, the
        // play speed keeps the feet moving at the ground speed.
        void GaitAt(float speed, out float gait, out float playSpeed)
        {
            float first = gaits[0].speed;
            if (speed < first)
            {
                gait = Mathf.Clamp01(speed / (first * 0.35f));
                playSpeed = Mathf.Lerp(1f, Mathf.Max(0.5f, speed / first), gait);
                return;
            }
            for (int i = 0; i < gaits.Length - 1; i++)
            {
                float slow = gaits[i].speed, fast = gaits[i + 1].speed;
                if (speed >= fast) continue;
                float t = (speed - slow) / (fast - slow);
                if (!gaits[i + 1].blend) t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.58f, t));
                gait = 1f + i + t;
                playSpeed = speed / Mathf.Lerp(slow, fast, t);
                return;
            }
            gait = gaits.Length;
            playSpeed = speed / gaits[gaits.Length - 1].speed;
        }
    }
}
