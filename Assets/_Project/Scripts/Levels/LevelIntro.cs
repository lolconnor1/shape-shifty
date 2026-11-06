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
    // hands over control. It can open on a shot of the player arriving (say, popping out of
    // a vent) and pan over to the exit from there, through optional points (e.g. up and over
    // the walls in between). If the exit is indoors, the camera can
    // back out along a path of points (e.g. out through a doorway) so the glide home doesn't
    // pass through walls. Black bars and a caption frame it like a cutscene, and the level
    // can fade in from black. Plays once per level per session, so restarting after being
    // caught goes straight into the action.
    // Space, Enter, Esc, a click or the gamepad's A button skips it.
    public class LevelIntro : MonoBehaviour
    {
        [Tooltip("What the camera circles: the level exit, or wherever the way out is.")]
        [SerializeField] Transform exit;
        [Tooltip("Switched on for the fly-by. Kept switched off in the scene.")]
        [SerializeField] CinemachineCamera introCamera;

        [Header("Opening Shot (optional)")]
        [Tooltip("If set, the intro starts with the camera here, looking along its forward (blue arrow), then pans over to the exit.")]
        [SerializeField] Transform openingShot;
        [SerializeField] float openingTime = 2.5f;
        [Tooltip("If set, the player comes out from here to where they start during the opening shot, like climbing out of a vent.")]
        [SerializeField] Transform playerEntrance;
        [Tooltip("Seconds into the opening shot before the player comes out.")]
        [SerializeField] float entranceDelay = 0.8f;
        [SerializeField] float entranceTime = 0.5f;
        [Tooltip("How high (m) the player hops on the way out.")]
        [SerializeField] float entranceHop = 0.12f;
        [Tooltip("Seconds to pan from the opening shot over to the exit.")]
        [SerializeField] float panTime = 2.5f;
        [Tooltip("Optional points the pan passes through, in order, e.g. up and over walls between the player and the exit.")]
        [SerializeField] Transform[] panVia;

        [Header("Circling the Exit")]
        [SerializeField] float radius = 12f;
        [SerializeField] float height = 7f;
        [Tooltip("How far above the exit the camera looks.")]
        [SerializeField] float lookHeight = 1.5f;
        [Tooltip("Where the camera starts and ends up, in degrees around the exit. 0 is south of it, looking north; 90 is east of it.")]
        [SerializeField] float startAngle = -120f;
        [SerializeField] float endAngle = 0f;
        [SerializeField] float circleTime = 5f;

        [Header("Backing Out")]
        [Tooltip("Optional points the camera backs out through after circling, in order, facing back the way it came. Leave empty to glide straight back to the player.")]
        [SerializeField] Transform[] leaveVia;
        [SerializeField] float leaveTime = 3f;
        [Tooltip("How far (degrees) the camera tilts down while backing out.")]
        [SerializeField] float leavePitch = 8f;

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
        [Tooltip("Black overlay over everything, faded out at the start of the level.")]
        [SerializeField] CanvasGroup screenFade;
        [Tooltip("Seconds to fade in from black when the level starts. 0 starts without a fade.")]
        [SerializeField] float fadeInTime;

        // Longest step any one frame advances the intro, so the slow first frames after a
        // level loads can't skip the fade or the opening.
        const float MaxStep = 1f / 30f;

        static readonly HashSet<string> played = new HashSet<string>();

        static float Step => Mathf.Min(Time.deltaTime, MaxStep);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlaySession() => played.Clear();

        InputAction skipAction;
        float shown; // how far the bars and caption are in, 0..1

        void Awake()
        {
            introCamera.enabled = false;
            caption.text = captionText;
            ShowScreen(0f);
            if (screenFade != null) screenFade.alpha = fadeInTime > 0f ? 1f : 0f;
            skipAction = new InputAction("Skip", InputActionType.Button);
            foreach (var path in new[] { "<Keyboard>/space", "<Keyboard>/enter", "<Keyboard>/escape", "<Mouse>/leftButton", "<Gamepad>/buttonSouth", "<Gamepad>/start" })
                skipAction.AddBinding(path);
        }

        void OnDestroy() => skipAction.Dispose();

        IEnumerator Start()
        {
            if (screenFade != null && fadeInTime > 0f) StartCoroutine(FadeIn());
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
            if (openingShot != null)
            {
                introCamera.transform.SetPositionAndRotation(openingShot.position, openingShot.rotation);
                var controller = player.GetComponent<CharacterController>();
                Vector3 start = player.transform.position;
                if (playerEntrance != null)
                {
                    controller.enabled = false;
                    player.transform.position = playerEntrance.position;
                    player.GetComponent<PlayerController>().FaceTowards(start - playerEntrance.position);
                }
                for (float t = 0f; t < openingTime && !skipped; t += Step)
                {
                    if (playerEntrance != null) ComeOut(player.transform, start, t);
                    FadeBars(1f);
                    yield return null;
                    skipped = skipAction.WasPressedThisFrame();
                }
                if (playerEntrance != null)
                {
                    player.transform.position = start;
                    controller.enabled = true;
                }

                // Pan over to where the circling starts.
                Vector3 fromPosition = introCamera.transform.position;
                Quaternion fromRotation = introCamera.transform.rotation;
                PlaceCamera(0f);
                Vector3 toPosition = introCamera.transform.position;
                Quaternion toRotation = introCamera.transform.rotation;
                introCamera.transform.SetPositionAndRotation(fromPosition, fromRotation);
                var panPath = new List<Vector3> { fromPosition };
                foreach (var point in panVia) panPath.Add(point.position);
                panPath.Add(toPosition);
                for (float t = 0f; t < panTime && !skipped; t += Step)
                {
                    float k = Mathf.SmoothStep(0f, 1f, t / panTime);
                    introCamera.transform.SetPositionAndRotation(PointOnPath(panPath, k), Quaternion.Slerp(fromRotation, toRotation, k));
                    FadeBars(1f);
                    yield return null;
                    skipped = skipAction.WasPressedThisFrame();
                }
            }

            for (float t = 0f; t < circleTime && !skipped; t += Step)
            {
                PlaceCamera(Mathf.SmoothStep(0f, 1f, t / circleTime));
                FadeBars(1f);
                yield return null;
                skipped = skipAction.WasPressedThisFrame();
            }
            if (leaveVia.Length > 0 && !skipped)
            {
                var path = new List<Vector3> { introCamera.transform.position };
                foreach (var point in leaveVia) path.Add(point.position);
                Quaternion startRotation = introCamera.transform.rotation;
                for (float t = 0f; t < leaveTime && !skipped; t += Step)
                {
                    BackOut(path, Mathf.SmoothStep(0f, 1f, t / leaveTime), startRotation);
                    FadeBars(1f);
                    yield return null;
                    skipped = skipAction.WasPressedThisFrame();
                }
            }
            skipAction.Disable();

            // Hand back to the player's camera; the brain blends there from wherever we are.
            float back = skipped ? skippedReturnTime : returnTime;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, back);
            introCamera.enabled = false;
            for (float t = 0f; t < back; t += Step)
            {
                FadeBars(0f);
                yield return null;
            }

            ShowScreen(0f);
            brain.DefaultBlend = blend;
            foreach (var canvas in hideDuring) canvas.enabled = true;
            PlayerControls.SetEnabled(player, true);
        }

        IEnumerator FadeIn()
        {
            for (float t = 0f; t < fadeInTime; t += Step)
            {
                screenFade.alpha = 1f - t / fadeInTime;
                yield return null;
            }
            screenFade.alpha = 0f;
        }

        // Moves the player from the entrance to where they start, with a little hop.
        void ComeOut(Transform player, Vector3 start, float time)
        {
            float k = Mathf.Clamp01((time - entranceDelay) / entranceTime);
            player.position = Vector3.Lerp(playerEntrance.position, start, Mathf.SmoothStep(0f, 1f, k))
                + Vector3.up * (Mathf.Sin(k * Mathf.PI) * entranceHop);
        }

        void PlaceCamera(float progress)
        {
            float angle = Mathf.Lerp(startAngle, endAngle, progress) * Mathf.Deg2Rad;
            Vector3 position = exit.position + new Vector3(Mathf.Sin(angle) * radius, height, -Mathf.Cos(angle) * radius);
            Vector3 lookAt = exit.position + Vector3.up * lookHeight;
            introCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
        }

        // Moves the camera along a smooth curve through path (progress 0..1), looking back the
        // way it came. It turns from its starting rotation to that over the first third.
        void BackOut(List<Vector3> path, float progress, Quaternion startRotation)
        {
            Vector3 position = PointOnPath(path, progress);
            Vector3 behind = PointOnPath(path, Mathf.Max(0f, progress - 0.02f));
            Vector3 back = behind - position;
            back.y = 0f;
            Quaternion facingBack = back.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(back) * Quaternion.Euler(leavePitch, 0f, 0f)
                : introCamera.transform.rotation;
            float turn = Mathf.SmoothStep(0f, 1f, progress * 3f);
            introCamera.transform.SetPositionAndRotation(position, Quaternion.Slerp(startRotation, facingBack, turn));
        }

        // Catmull-Rom curve through every point, at roughly even speed along its length.
        static Vector3 PointOnPath(List<Vector3> points, float progress)
        {
            float total = 0f;
            for (int i = 1; i < points.Count; i++) total += Vector3.Distance(points[i - 1], points[i]);
            float distance = progress * total;
            for (int i = 1; i < points.Count; i++)
            {
                float length = Vector3.Distance(points[i - 1], points[i]);
                if (distance <= length || i == points.Count - 1)
                {
                    float t = length > 0f ? Mathf.Clamp01(distance / length) : 1f;
                    Vector3 p0 = points[Mathf.Max(i - 2, 0)], p1 = points[i - 1], p2 = points[i], p3 = points[Mathf.Min(i + 1, points.Count - 1)];
                    return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (3f * p1 - p0 - 3f * p2 + p3) * t * t * t);
                }
                distance -= length;
            }
            return points[points.Count - 1];
        }

        void FadeBars(float target) => ShowScreen(Mathf.MoveTowards(shown, target, Step * 2f));

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
