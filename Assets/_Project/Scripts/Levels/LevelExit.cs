using System.Collections;
using ShapeShifty.Player;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace ShapeShifty.Levels
{
    // The way out of a level, marked with a column of light so it can be spotted from far
    // away. Fires onReached the first time the player steps into its trigger, then (if a
    // next scene is set) takes the controls away, fades to black and loads it.
    [RequireComponent(typeof(Collider))]
    public class LevelExit : MonoBehaviour
    {
        [Tooltip("Scene loaded after reaching the exit. Leave empty to stay put, e.g. while the next level isn't built yet.")]
        [SerializeField] string nextScene;
        [Tooltip("Black overlay faded in before the next scene loads.")]
        [SerializeField] CanvasGroup screenFade;
        [SerializeField] float fadeOutTime = 1f;
        public UnityEvent onReached;

        bool reached;

        void OnTriggerEnter(Collider other)
        {
            if (reached || !other.CompareTag("Player")) return;
            reached = true;
            Debug.Log("Player reached the level exit.", this);
            onReached.Invoke();
            if (!string.IsNullOrEmpty(nextScene)) StartCoroutine(GoToNextScene(other.gameObject));
        }

        IEnumerator GoToNextScene(GameObject player)
        {
            PlayerControls.SetEnabled(player, false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (screenFade != null)
            {
                for (float t = 0f; t < fadeOutTime; t += Time.deltaTime)
                {
                    screenFade.alpha = t / fadeOutTime;
                    yield return null;
                }
                screenFade.alpha = 1f;
            }
            SceneManager.LoadScene(nextScene);
        }
    }
}
