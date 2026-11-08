using ShapeShifty.AI;
using ShapeShifty.Player;
using ShapeShifty.Shapes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ShapeShifty.UI
{
    // Ends the level as soon as any employee spots the player: freezes the game, hands the
    // cursor back, and shows the "Caught!" screen with Restart and Main Menu buttons.
    public class CaughtScreen : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [Tooltip("Selected when the screen opens, so keyboard and gamepad can confirm straight away.")]
        [SerializeField] GameObject firstSelected;

        bool caught;

        void Awake() => panel.SetActive(false);

        void Start()
        {
            foreach (var vision in FindObjectsByType<VisionCone>(FindObjectsSortMode.None))
                vision.onSpotted.AddListener(OnCaught);
        }

        void OnCaught()
        {
            if (caught) return;
            caught = true;

            Time.timeScale = 0f;
            var player = GameObject.FindWithTag("Player");
            if (player != null) PlayerControls.SetEnabled(player, false); // also frees the cursor

            panel.SetActive(true);
            EventSystem.current.SetSelectedGameObject(firstSelected);
        }

        // Restart button. Forms scanned during this attempt are lost.
        public void Restart()
        {
            UnlockedShapes.ForgetSinceLevelStart();
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // Main Menu button. Going back there starts a fresh run, so every scanned form is lost.
        public void GoToMainMenu() => MainMenu.Open();
    }
}
