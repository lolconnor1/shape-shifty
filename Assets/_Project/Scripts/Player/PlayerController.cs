using ShapeShifty.Interaction;
using ShapeShifty.Shapes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShapeShifty.Player
{
    // Third-person movement: walks relative to where the camera is looking, turns the
    // body toward the direction of travel, and handles gravity, jumping and sprinting.
    // Only the body model turns; this root stays unrotated so the camera target
    // (a sibling of the body) is never dragged around by the body's rotation.
    // Some forms can do more (see ShapeDefinition). Swimmers swim in deep water instead of
    // sinking. Belly-sliders flop onto their front when Crouch is pressed and slide along
    // until it's let go, keeping their speed and steering slowly, faster on ice and downhill.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] InputActionAsset inputActions;

        [Header("Movement")]
        [Tooltip("Speeds and jump height are replaced by the current form's when the player has a PlayerShapeshifter.")]
        [SerializeField] float moveSpeed = 5f;
        [SerializeField] float sprintSpeed = 8f;
        [Tooltip("How quickly speed ramps up, down and changes direction (m/s²).")]
        [SerializeField] float acceleration = 30f;
        [SerializeField] float turnSmoothTime = 0.1f;
        [SerializeField] float jumpHeight = 1.2f;
        [SerializeField] float gravity = -20f;

        [Header("Swimming")]
        [Tooltip("How far under the surface a floating swimmer's feet are, as a fraction of its height.")]
        [SerializeField] float floatDepth = 0.6f;
        [Tooltip("How quickly swimming speed changes (m/s²). Water is slower to respond than ground.")]
        [SerializeField] float swimAcceleration = 12f;

        [Header("Belly Slide")]
        [Tooltip("Speed lost per second while sliding on ordinary ground (m/s²).")]
        [SerializeField] float slideFriction = 2.5f;
        [Tooltip("Speed lost per second while sliding on ice (m/s²).")]
        [SerializeField] float iceFriction = 0.3f;
        [Tooltip("How quickly holding a direction paddles a slide along, up to the form's slide speed (m/s²).")]
        [SerializeField] float slidePush = 6f;
        [Tooltip("Top speed on ice and downhill, as a multiple of the form's slide speed.")]
        [SerializeField] float slideTopSpeed = 1.8f;
        [Tooltip("Degrees per second a slide can turn.")]
        [SerializeField] float slideTurnRate = 90f;
        [Tooltip("A slide slower than this stops, and the player gets back up.")]
        [SerializeField] float slideStopSpeed = 0.8f;
        [Tooltip("Seconds to tip over onto the belly or back upright.")]
        [SerializeField] float poseTime = 0.12f;
        [Tooltip("Layers that stop a sliding player standing back up. Ignore Raycast (the invisible level bounds) is left out.")]
        [SerializeField] LayerMask obstacleMask = ~(1 << 2);

        [Header("References")]
        [Tooltip("The camera target rotated by PlayerLook; movement is relative to its yaw.")]
        [SerializeField] Transform cameraTarget;
        [Tooltip("The visual model, turned to face the direction of travel.")]
        [SerializeField] Transform body;

        // What the player is standing on, if anything. Moving platforms use it to carry them.
        public Collider Ground { get; private set; }
        // Set while the player is stunned: move and jump input are ignored, but gravity still
        // applies and the camera can still look round.
        public bool Frozen { get; set; }
        public bool IsSwimming { get; private set; }
        public bool IsSliding { get; private set; }
        // Speed and direction along the ground. Penguin pins check it to see if a slide was fast enough.
        public Vector3 HorizontalVelocity => horizontalVelocity;

        CharacterController controller;
        InputActionMap playerMap;
        InputAction moveAction, jumpAction, sprintAction, crouchAction;
        ShapeDefinition form;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        float bodyYaw, turnVelocity;
        float tilt, tiltVelocity;
        float leapEndsAt;
        Collider groundHit, lastGround;
        Vector3 groundNormal = Vector3.up, hitNormal;
        bool onIce;
        readonly Collider[] overlaps = new Collider[8];

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            playerMap = inputActions.FindActionMap("Player", true);
            moveAction = playerMap.FindAction("Move", true);
            jumpAction = playerMap.FindAction("Jump", true);
            sprintAction = playerMap.FindAction("Sprint", true);
            crouchAction = playerMap.FindAction("Crouch", true);
            bodyYaw = body.eulerAngles.y;
        }

        void OnEnable() => playerMap.Enable();
        void OnDisable() => playerMap.Disable();

        // Turns the model to face a direction straight away, e.g. during a cutscene.
        public void FaceTowards(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            bodyYaw = Quaternion.LookRotation(direction).eulerAngles.y;
            body.rotation = Quaternion.Euler(0f, bodyYaw, 0f) * Quaternion.Euler(tilt, 0f, 0f);
        }

        // Called by PlayerShapeshifter whenever the player changes form, after it has sized
        // the collider for the new form standing up.
        public void SetForm(ShapeDefinition shape)
        {
            form = shape;
            moveSpeed = shape.moveSpeed;
            sprintSpeed = shape.sprintSpeed;
            jumpHeight = shape.jumpHeight;
            IsSliding = false;
        }

        void Update()
        {
            Vector2 input = Frozen ? Vector2.zero : moveAction.ReadValue<Vector2>();

            // Swimmers are in the water while their lower body is under the surface, except
            // just after leaping out.
            DeepWater water = null;
            if (form != null && form.canSwim && Time.time >= leapEndsAt)
                water = DeepWater.At(transform.position + Vector3.up * (controller.height * 0.25f));
            IsSwimming = water != null;
            if (IsSwimming && IsSliding) GetUp(true);

            if (IsSwimming) Swim(input, water.SurfaceY);
            else if (IsSliding) Slide(input);
            else Walk(input);

            groundHit = null;
            hitNormal = Vector3.up;
            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
            Ground = controller.isGrounded ? groundHit : null;
            groundNormal = Ground != null ? hitNormal : Vector3.up;
            if (Ground != lastGround)
            {
                lastGround = Ground;
                onIce = Ground != null && Ground.GetComponent<IcySurface>() != null;
            }
            // Running into something takes the speed out of a slide.
            if (IsSliding)
            {
                Vector3 moved = controller.velocity;
                moved.y = 0f;
                if (moved.sqrMagnitude < horizontalVelocity.sqrMagnitude) horizontalVelocity = moved;
            }

            UpdatePose();
        }

        void Walk(Vector2 input)
        {
            float speed = sprintAction.IsPressed() ? sprintSpeed : moveSpeed;
            Vector3 targetVelocity = Vector3.zero;
            if (input.sqrMagnitude > 0.01f)
            {
                float targetYaw = InputYaw(input);
                bodyYaw = Mathf.SmoothDampAngle(bodyYaw, targetYaw, ref turnVelocity, turnSmoothTime);
                targetVelocity = Quaternion.Euler(0f, targetYaw, 0f) * Vector3.forward * (Mathf.Clamp01(input.magnitude) * speed);
            }
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, acceleration * Time.deltaTime);

            if (controller.isGrounded)
            {
                if (verticalVelocity < 0f) verticalVelocity = -2f; // keep snapped to slopes
                if (!Frozen && jumpAction.WasPressedThisFrame()) verticalVelocity = JumpSpeed(jumpHeight);
                else if (!Frozen && form != null && form.canBellySlide && crouchAction.WasPressedThisFrame()) StartSlide();
            }
            verticalVelocity += gravity * Time.deltaTime;
        }

        // Flops forward onto the belly with at least a push's worth of speed.
        void StartSlide()
        {
            IsSliding = true;
            Vector3 facing = Quaternion.Euler(0f, bodyYaw, 0f) * Vector3.forward;
            horizontalVelocity = facing * Mathf.Max(Vector3.Dot(horizontalVelocity, facing), form.slideSpeed * 0.6f);
            SetHeight(form.slideHeight);
        }

        void Slide(Vector2 input)
        {
            float speed = horizontalVelocity.magnitude;
            Vector3 direction = speed > 0.01f ? horizontalVelocity / speed : Quaternion.Euler(0f, bodyYaw, 0f) * Vector3.forward;
            if (input.sqrMagnitude > 0.01f)
            {
                // Steers slowly towards the stick, and paddles along when slower than a push.
                Vector3 wish = Quaternion.Euler(0f, InputYaw(input), 0f) * Vector3.forward;
                direction = Vector3.RotateTowards(direction, wish, slideTurnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);
                if (speed < form.slideSpeed && Vector3.Dot(direction, wish) > 0f)
                    speed = Mathf.Min(form.slideSpeed, speed + slidePush * Time.deltaTime);
            }
            if (controller.isGrounded)
            {
                // Downhill speeds it up and uphill slows it down; ice hardly slows it at all.
                Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal) * -gravity;
                speed += Vector3.Dot(new Vector3(downhill.x, 0f, downhill.z), direction) * Time.deltaTime;
                speed -= (onIce ? iceFriction : slideFriction) * Time.deltaTime;
            }
            speed = Mathf.Clamp(speed, 0f, form.slideSpeed * slideTopSpeed);
            // Pressed down harder the faster it goes, so it follows slopes instead of
            // skipping off them.
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f - speed;
            horizontalVelocity = direction * speed;
            bodyYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            verticalVelocity += gravity * Time.deltaTime;

            bool jump = !Frozen && controller.isGrounded && jumpAction.WasPressedThisFrame();
            if ((jump || Frozen || !crouchAction.IsPressed() || speed < slideStopSpeed) && GetUp(false) && jump)
                verticalVelocity = JumpSpeed(jumpHeight);
        }

        // Stands back up from a slide, if there's room. Forced, it stands up regardless.
        bool GetUp(bool force)
        {
            if (!force && !HasHeadroom(form.height)) return false;
            IsSliding = false;
            SetHeight(form.height);
            return true;
        }

        // Floats with its feet a little under the surface. Crouch dives, Jump swims back up,
        // and Jump at the surface leaps out.
        void Swim(Vector2 input, float surfaceY)
        {
            float speed = form.swimSpeed;
            Vector3 targetVelocity = Vector3.zero;
            if (input.sqrMagnitude > 0.01f)
            {
                float targetYaw = InputYaw(input);
                bodyYaw = Mathf.SmoothDampAngle(bodyYaw, targetYaw, ref turnVelocity, turnSmoothTime);
                targetVelocity = Quaternion.Euler(0f, targetYaw, 0f) * Vector3.forward * (Mathf.Clamp01(input.magnitude) * speed);
            }
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, swimAcceleration * Time.deltaTime);

            float floatY = surfaceY - controller.height * floatDepth;
            float belowFloat = floatY - transform.position.y;
            if (!Frozen && belowFloat < 0.1f && jumpAction.WasPressedThisFrame())
            {
                verticalVelocity = JumpSpeed(form.leapHeight);
                leapEndsAt = Time.time + 0.5f;
                return;
            }
            float targetVertical;
            if (!Frozen && crouchAction.IsPressed()) targetVertical = -speed * 0.7f;
            else if (!Frozen && jumpAction.IsPressed()) targetVertical = Mathf.Min(speed * 0.7f, belowFloat * 6f);
            else targetVertical = Mathf.Clamp(belowFloat * 4f, -3f, speed * 0.4f); // bob back up to the surface
            verticalVelocity = Mathf.MoveTowards(verticalVelocity, targetVertical, swimAcceleration * 2f * Time.deltaTime);
        }

        // Lies flat while sliding or swimming, tipping nose-down to dive and nose-up to rise,
        // and stands upright otherwise. Tips over around the middle of the collider rather
        // than the feet, so a lying body stays inside it.
        void UpdatePose()
        {
            if (Frozen) return; // PlayerStun shakes the body meanwhile
            float targetTilt = 0f;
            if (IsSliding) targetTilt = 85f;
            else if (IsSwimming) targetTilt = 80f - Mathf.Clamp(verticalVelocity / form.swimSpeed, -1f, 1f) * 50f;
            tilt = Mathf.SmoothDamp(tilt, targetTilt, ref tiltVelocity, poseTime);
            Quaternion rotation = Quaternion.Euler(0f, bodyYaw, 0f) * Quaternion.Euler(tilt, 0f, 0f);
            float middle = (form != null ? form.height : controller.height) * 0.5f;
            body.SetLocalPositionAndRotation(controller.center - rotation * (Vector3.up * middle), rotation);
        }

        void SetHeight(float height)
        {
            controller.height = height;
            controller.center = Vector3.up * (height * 0.5f);
        }

        // Checks a standing collider would fit here, above step height.
        bool HasHeadroom(float height)
        {
            float radius = controller.radius;
            Vector3 bottom = transform.position + Vector3.up * (radius + controller.stepOffset);
            Vector3 top = transform.position + Vector3.up * Mathf.Max(height - radius, radius + controller.stepOffset);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius * 0.95f, overlaps, obstacleMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (overlaps[i] != controller) return false;
            return true;
        }

        float InputYaw(Vector2 input) => Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg + cameraTarget.eulerAngles.y;
        float JumpSpeed(float height) => Mathf.Sqrt(height * -2f * gravity);

        // Called during Move for everything the controller touches.
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > 0.5f)
            {
                groundHit = hit.collider;
                hitNormal = hit.normal;
            }
            // Only a slide or a swim hits hard enough to move heavy things.
            if ((IsSliding || IsSwimming) && hit.collider.TryGetComponent(out Shoveable shoveable))
                shoveable.Shove(horizontalVelocity, IsSwimming);
        }
    }
}
