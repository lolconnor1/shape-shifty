using System.Collections;
using System.Collections.Generic;
using ShapeShifty.Player;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShapeShifty.Levels
{
    // A short fly-by when a level starts: cuts to a camera circling the level exit so the
    // player knows where they're headed, then glides back to the player's own camera and
    // hands over control. Black bars and a caption frame it like a cutscene. Plays once per
    // level per session, so restarting after being caught goes straight into the action.
    // Space, Enter, Esc, a click or the gamepad's A button skips it.
    public class LevelIntro : MonoBehaviour
    {
        [SerializeField] Transform exit;
        [Tooltip("Switched on for the fly-by. Kept switched off in the scene.")]
        [SerializeField] CinemachineCamera introCamera;

        [Header("Circling the Exit")]
        [SerializeField] float radius = 12f;
        [SerializeField] float height = 7f;
        [Tooltip("How far above the exit the camera looks.")]
        [SerializeField] float lookHeight = 1.5f;
        [Tooltip("Where the camera starts and ends up, in degrees around the exit. 0 is south of it, looking north; 90 is east of it.")]
        [SerializeField] float startAngle = -120f;
        [SerializeField] float endAngle = 0f;
        [SerializeField] float circleTime = 5f;

        [Header("Back to the Player")]
        [SerializeField] float returnTime = 2.5f;
        [SerializeField] float skippedReturnTime = 0.8f;

        [Header("Screen")]
        [SerializeField] RectTransform topBar;
        [SerializeField] RectTransform bottomBar;
        [SerializeField] float barHeight = 120f;
        [SerializeField] CanvasGroup captionGroup;
        [SerializeField] TMP_Text caption;
        [Tooltip("What the player has to do, shown while circling the exit.")]
        [SerializeField, TextArea] string captionText = "Reach the exit";
        [Tooltip("Hidden while the intro plays, like the HUD.")]
        [SerializeField] Canvas[] hideDuring;

        static readonly HashSet<string> played = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlaySession() => played.Clear();

        InputAction skipAction;
        float shown; // how far the bars and caption are in, 0..1

        void Awake()
        {
            introCamera.enabled = false;
            caption.text = captionText;
            ShowScreen(0f);
            skipAction = new InputAction("Skip", InputActionType.Button);
            foreach (var path in new[] { "<Keyboard>/space", "<Keyboard>/enter", "<Keyboard>/escape", "<Mouse>/leftButton", "<Gamepad>/buttonSouth", "<Gamepad>/start" })
                skipAction.AddBinding(path);
        }

        void OnDestroy() => skipAction.Dispose();

        IEnumerator Start()
        {
            if (exit == null || !played.Add(gameObject.scene.path)) yield break;

            var player = GameObject.FindWithTag("Player");
            PlayerControls.SetEnabled(player, false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            foreach (var canvas in hideDuring) canvas.enabled = false;

            var brain = Camera.main.GetComponent<CinemachineBrain>();
            var blend = brain.DefaultBlend;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            introCamera.enabled = true;
            skipAction.Enable();

            bool skipped = false;
            for (float t = 0f; t < circleTime; t += Time.deltaTime)
            {
                PlaceCamera(Mathf.SmoothStep(0f, 1f, t / circleTime));
                Fade(1f);
                yield return null;
                if (skipAction.WasPressedThisFrame())
                {
                    skipped = true;
                    break;
                }
            }
            skipAction.Disable();

            // Hand back to the player's camera; the brain blends there from wherever we are.
            float back = skipped ? skippedReturnTime : returnTime;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, back);
            introCamera.enabled = false;
            for (float t = 0f; t < back; t += Time.deltaTime)
            {
                Fade(0f);
                yield return null;
            }

            ShowScreen(0f);
            brain.DefaultBlend = blend;
            foreach (var canvas in hideDuring) canvas.enabled = true;
            PlayerControls.SetEnabled(player, true);
        }

        void PlaceCamera(float progress)
        {
            float angle = Mathf.Lerp(startAngle, endAngle, progress) * Mathf.Deg2Rad;
            Vector3 position = exit.position + new Vector3(Mathf.Sin(angle) * radius, height, -Mathf.Cos(angle) * radius);
            Vector3 lookAt = exit.position + Vector3.up * lookHeight;
            introCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
        }

        void Fade(float target) => ShowScreen(Mathf.MoveTowards(shown, target, Time.deltaTime * 2f));

        void ShowScreen(float amount)
        {
            shown = amount;
            float eased = Mathf.SmoothStep(0f, 1f, amount);
            topBar.sizeDelta = new Vector2(topBar.sizeDelta.x, barHeight * eased);
            bottomBar.sizeDelta = new Vector2(bottomBar.sizeDelta.x, barHeight * eased);
            captionGroup.alpha = eased;
        }
    }
}
