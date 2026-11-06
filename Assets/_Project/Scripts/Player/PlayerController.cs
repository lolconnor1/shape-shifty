using UnityEngine;
using UnityEngine.InputSystem;

namespace ShapeShifty.Player
{
    // Third-person movement: walks relative to where the camera is looking, turns the
    // body toward the direction of travel, and handles gravity, jumping and sprinting.
    // Only the body model turns; this root stays unrotated so the camera target
    // (a sibling of the body) is never dragged around by the body's rotation.
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

        [Header("References")]
        [Tooltip("The camera target rotated by PlayerLook; movement is relative to its yaw.")]
        [SerializeField] Transform cameraTarget;
        [Tooltip("The visual model, turned to face the direction of travel.")]
        [SerializeField] Transform body;

        CharacterController controller;
        InputActionMap playerMap;
        InputAction moveAction, jumpAction, sprintAction;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        float turnVelocity;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            playerMap = inputActions.FindActionMap("Player", true);
            moveAction = playerMap.FindAction("Move", true);
            jumpAction = playerMap.FindAction("Jump", true);
            sprintAction = playerMap.FindAction("Sprint", true);
        }

        void OnEnable() => playerMap.Enable();
        void OnDisable() => playerMap.Disable();

        // Turns the model to face a direction straight away, e.g. during a cutscene.
        public void FaceTowards(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) body.rotation = Quaternion.LookRotation(direction);
        }

        // Called by PlayerShapeshifter whenever the player changes form.
        public void SetMovement(float moveSpeed, float sprintSpeed, float jumpHeight)
        {
            this.moveSpeed = moveSpeed;
            this.sprintSpeed = sprintSpeed;
            this.jumpHeight = jumpHeight;
        }

        void Update()
        {
            Vector2 input = moveAction.ReadValue<Vector2>();
            float speed = sprintAction.IsPressed() ? sprintSpeed : moveSpeed;
            Vector3 targetVelocity = Vector3.zero;

            if (input.sqrMagnitude > 0.01f)
            {
                float targetYaw = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg + cameraTarget.eulerAngles.y;
                float yaw = Mathf.SmoothDampAngle(body.eulerAngles.y, targetYaw, ref turnVelocity, turnSmoothTime);
                body.rotation = Quaternion.Euler(0f, yaw, 0f);
                targetVelocity = Quaternion.Euler(0f, targetYaw, 0f) * Vector3.forward * (Mathf.Clamp01(input.magnitude) * speed);
            }
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, acceleration * Time.deltaTime);

            if (controller.isGrounded)
            {
                if (verticalVelocity < 0f) verticalVelocity = -2f; // keep snapped to slopes
                if (jumpAction.WasPressedThisFrame()) verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            verticalVelocity += gravity * Time.deltaTime;

            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        }
    }
}
