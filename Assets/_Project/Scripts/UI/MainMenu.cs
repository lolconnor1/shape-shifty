using System.IO;
using System.Text.RegularExpressions;
using ShapeShifty.Shapes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShapeShifty.UI
{
    // A placeholder menu for testing: one button for every other scene in the build settings,
    // so adding a scene there adds a button here. Coming back here starts a fresh run, so
    // every scanned form is forgotten.
    public class MainMenu : MonoBehaviour
    {
        public const string SceneName = "MainMenu";

        [Tooltip("Copied once for each scene. Keep it switched off.")]
        [SerializeField] Button buttonTemplate;

        void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UnlockedShapes.ForgetAll();

            Button first = null;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scene = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                if (scene == SceneName) continue;
                var button = Instantiate(buttonTemplate, buttonTemplate.transform.parent);
                button.name = scene + "Button";
                button.GetComponentInChildren<TMP_Text>().text = Nicify(scene);
                button.onClick.AddListener(() => SceneManager.LoadScene(scene));
                button.gameObject.SetActive(true);
                if (first == null) first = button;
            }
            if (first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        // Unpauses (the caught screen freezes time) and comes back here.
        public static void Open()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneName);
        }

        // "Level_01" -> "Level 01", "TestHall" -> "Test Hall".
        static string Nicify(string scene) =>
            Regex.Replace(scene.Replace('_', ' '), "(?<=[a-z])(?=[A-Z0-9])", " ");
    }
}
