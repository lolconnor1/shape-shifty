using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShapeShifty.UI
{
    // For testing: F1 (or a gamepad's Back / Select button) goes back to the main menu from
    // any level. Made when the game starts and kept across scenes, so no scene needs one.
    public class MenuShortcut : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var shortcut = new GameObject("MenuShortcut");
            DontDestroyOnLoad(shortcut);
            shortcut.AddComponent<MenuShortcut>();
        }

        void Update()
        {
            if (SceneManager.GetActiveScene().name == MainMenu.SceneName) return;
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            if ((keyboard != null && keyboard.f1Key.wasPressedThisFrame) || (gamepad != null && gamepad.selectButton.wasPressedThisFrame))
                MainMenu.Open();
        }
    }
}
