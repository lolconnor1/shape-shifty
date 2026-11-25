using ShapeShifty.AI;
using ShapeShifty.Audio;
using ShapeShifty.Player;
using ShapeShifty.Shapes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ShapeShifty.UI
{
    // Ends the level as soon as any employee spots the player, the player drowns, a wolf or
    // guard dog gets them, or they take one splat too many (or anything else calls Show):
    // freezes the game, hands the cursor back, and shows the "Caught!" (or "Drowned!",
    // "Mauled!", "Splattered!", ...) screen with Restart and Main Menu buttons.
    public class CaughtScreen : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text title;
        [Tooltip("Selected when the screen opens, so keyboard and gamepad can confirm straight away.")]
        [SerializeField] GameObject firstSelected;

        bool caught;

        void Awake() => panel.SetActive(false);

        void Start()
        {
            foreach (var vision in FindObjectsByType<VisionCone>(FindObjectsSortMode.None))
                if (vision.CatchesOnSpotted) vision.onSpotted.AddListener(() => Show("Caught!"));
            var player = GameObject.FindWithTag("Player");
            if (player != null && player.TryGetComponent(out PlayerBreath breath))
                breath.Drowned += () => Show("Drowned!");
            if (player != null && player.TryGetComponent(out ShapeShifty.Combat.Health health))
                health.Died += () => Show("Splattered!");
        }

        void OnEnable()
        {
            Wolf.PlayerCaught += OnMauled;
            GuardDog.PlayerCaught += OnDogged;
        }

        void OnDisable()
        {
            Wolf.PlayerCaught -= OnMauled;
            GuardDog.PlayerCaught -= OnDogged;
        }

        void OnMauled(Wolf wolf) => Show("Mauled!");
        void OnDogged(GuardDog dog) => Show("Caught by the guard dogs!");

        // Also called by anything else that ends the attempt, e.g. guard dogs or a lockdown.
        public void Show(string heading)
        {
            if (caught) return;
            caught = true;

            Time.timeScale = 0f;
            Sfx.StopLoops();
            Sfx.Play(Sound.Caught);
            var player = GameObject.FindWithTag("Player");
            if (player != null) PlayerControls.SetEnabled(player, false); // also frees the cursor

            title.text = heading;
            panel.SetActive(true);
            EventSystem.current.SetSelectedGameObject(firstSelected);
        }

        // Restart button. Forms scanned during this attempt are lost.
        public void Restart()
        {
            Sfx.Play(Sound.UiClick);
            UnlockedShapes.ForgetSinceLevelStart();
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // Main Menu button. Going back there starts a fresh run, so every scanned form is lost.
        public void GoToMainMenu()
        {
            Sfx.Play(Sound.UiClick);
            MainMenu.Open();
        }
    }
}
