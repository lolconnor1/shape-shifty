using System.Collections;
using ShapeShifty.Audio;
using ShapeShifty.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShapeShifty.UI
{
    // The end of the game, shown when the player gets out of the zoo: fades in over the
    // world, takes the controls away, hands back the cursor and offers the way to the menu.
    public class EndScreen : MonoBehaviour
    {
        [SerializeField] CanvasGroup panel;
        [Tooltip("Selected when the screen opens, so keyboard and gamepad can confirm straight away.")]
        [SerializeField] GameObject firstSelected;
        [SerializeField] float fadeTime = 1.2f;

        bool shown;

        void Awake()
        {
            panel.alpha = 0f;
            panel.gameObject.SetActive(false);
        }

        public void Show()
        {
            if (shown) return;
            shown = true;
            var player = GameObject.FindWithTag("Player");
            if (player != null) PlayerControls.SetEnabled(player, false); // also frees the cursor
            panel.gameObject.SetActive(true);
            Sfx.Play(Sound.LevelDone);
            StartCoroutine(FadeIn());
        }

        IEnumerator FadeIn()
        {
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                panel.alpha = t / fadeTime;
                yield return null;
            }
            panel.alpha = 1f;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(firstSelected);
        }

        // Main Menu button. A fresh run from there, so every scanned form is forgotten.
        public void GoToMainMenu() => MainMenu.Open();
    }
}
