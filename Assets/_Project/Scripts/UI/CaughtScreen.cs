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
            if (player != null)
            {
                player.GetComponent<PlayerController>().enabled = false;
                player.GetComponent<PlayerLook>().enabled = false; // also unlocks the cursor
                if (player.TryGetComponent(out PlayerScanner scanner)) scanner.enabled = false;
                if (player.TryGetComponent(out PlayerShapeshifter shapeshifter)) shapeshifter.enabled = false;
                if (player.TryGetComponent(out PlayerTalker talker)) talker.enabled = false;
            }

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

        // Main Menu button. There's no main menu yet, so this does nothing for now.
        public void GoToMainMenu()
        {
            Debug.Log("Main menu isn't built yet.");
        }
    }
}
