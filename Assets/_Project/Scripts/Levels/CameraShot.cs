using System.Collections;
using ShapeShifty.Player;
using ShapeShifty.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace ShapeShifty.Levels
{
    // A quick cutaway to something the player has set off but might not see, like the guard
    // dogs' kennel opening. Place this where the camera should be, looking along its forward
    // (blue arrow), and hook Play up to whatever sets it off. The player's controls are off
    // while it runs, and nobody can spot them and they don't lose breath (see Playing). Shots
    // asked for during another wait their turn. Space, Enter or a click cuts it short.
    public class CameraShot : MonoBehaviour
    {
        [Tooltip("Seconds it holds on the shot, after gliding there.")]
        [SerializeField] float holdTime = 2f;
        [Tooltip("Seconds to glide there, and back.")]
        [SerializeField] float blendTime = 0.7f;
        [SerializeField] float fieldOfView = 50f;
        [Tooltip("Only plays the first time it's asked.")]
        [SerializeField] bool once = true;
        [Tooltip("Shown under the aiming dot during the shot. Optional.")]
        [SerializeField] string message;
        public UnityEvent onStarted = new UnityEvent();
        [Tooltip("Seconds after the shot before On Done, e.g. a head start on what was shown.")]
        [SerializeField] float doneDelay;
        [Tooltip("Back with the player, or skipped. Runs even if the shot couldn't play.")]
        public UnityEvent onDone = new UnityEvent();

        // A shot is on screen, anywhere.
        public static bool Playing { get; private set; }

        bool played, mine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlaySession() => Playing = false;

        // Another scene loading mid-shot.
        void OnDestroy()
        {
            if (mine) Playing = false;
        }

        public void Play()
        {
            if (played && once) return;
            played = true;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            while (Playing) yield return null;
            var player = GameObject.FindWithTag("Player");
            var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            // Not while something else (the level intro, the caught screen) has the controls.
            if (player == null || brain == null || Time.timeScale == 0f || !player.GetComponent<PlayerController>().enabled)
            {
                onStarted.Invoke();
                onDone.Invoke();
                yield break;
            }

            Playing = mine = true;
            PlayerControls.SetEnabled(player, false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            var hud = FindFirstObjectByType<ShapeHud>();
            if (hud != null && !string.IsNullOrEmpty(message)) hud.ShowMessage(message, holdTime + blendTime * 2f);

            var blend = brain.DefaultBlend;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendTime);
            var cam = new GameObject("CameraShot Camera").AddComponent<CinemachineCamera>();
            cam.transform.SetParent(transform, false);
            var lens = LensSettings.Default;
            lens.FieldOfView = fieldOfView;
            cam.Lens = lens;
            cam.Priority = 100;

            onStarted.Invoke();
            yield return Wait(blendTime + holdTime);
            Destroy(cam.gameObject);
            yield return Wait(blendTime);

            brain.DefaultBlend = blend;
            Playing = mine = false;
            // Not if they were caught meanwhile: the caught screen has the controls.
            if (Time.timeScale > 0f) PlayerControls.SetEnabled(player, true);
            if (doneDelay > 0f) yield return new WaitForSeconds(doneDelay);
            onDone.Invoke();
        }

        IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                yield return null;
                if (t > 0.3f && Skipped()) yield break;
            }
        }

        static bool Skipped()
        {
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            return (keys != null && (keys.spaceKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame))
                || (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (pad != null && pad.buttonSouth.wasPressedThisFrame);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawFrustum(Vector3.zero, fieldOfView, 4f, 0.2f, 16f / 9f);
        }
    }
}
