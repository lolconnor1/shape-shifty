using UnityEngine;
using UnityEngine.InputSystem;

namespace ShapeShifty.Player
{
    // Mouse / right-stick look. Rotates the camera target that the Cinemachine
    // third-person camera follows, independently of the player's body.
    // Esc frees the cursor; clicking the game view locks it again.
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] Transform cameraTarget;

        [Header("Sensitivity")]
        [Tooltip("Degrees per pixel of mouse movement.")]
        [SerializeField] float mouseSensitivity = 0.1f;
        [Tooltip("Degrees per second at full stick deflection.")]
        [SerializeField] float stickSensitivity = 180f;
        [SerializeField] bool invertY;

        [Header("Limits")]
        [SerializeField] float minPitch = -40f;
        [SerializeField] float maxPitch = 70f;

        InputAction lookAction;
        float yaw, pitch;

        void Awake()
        {
            lookAction = inputActions.FindActionMap("Player", true).FindAction("Look", true);
            Vector3 start = cameraTarget.eulerAngles;
            yaw = start.y;
            pitch = start.x > 180f ? start.x - 360f : start.x;
        }

        void OnEnable()
        {
            lookAction.Enable();
            SetCursorLocked(true);
        }

        void OnDisable() => SetCursorLocked(false);

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                SetCursorLocked(true);

            Vector2 look = lookAction.ReadValue<Vector2>();
            bool fromGamepad = lookAction.activeControl?.device is Gamepad;
            if (fromGamepad)
                look *= stickSensitivity * Time.deltaTime;
            else
                look = Cursor.lockState == CursorLockMode.Locked ? look * mouseSensitivity : Vector2.zero;

            yaw += look.x;
            pitch = Mathf.Clamp(pitch + (invertY ? look.y : -look.y), minPitch, maxPitch);
            cameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
