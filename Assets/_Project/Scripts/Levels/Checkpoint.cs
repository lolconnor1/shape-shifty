using System;
using ShapeShifty.Player;
using ShapeShifty.Shapes;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShapeShifty.Levels
{
    // A point partway through a long level (needs a trigger collider across the way). Walking
    // through it remembers it, and restarting after being caught then starts the player here,
    // in the form they had, with everything they'd already got past cleared out of the way.
    // Only a restart keeps it: loading any other scene, or this one fresh from elsewhere,
    // starts the level from the beginning again.
    public class Checkpoint : MonoBehaviour
    {
        [Tooltip("Where this comes along the level: 1 for the first, then 2, ... Later ones replace earlier ones.")]
        [SerializeField] int order = 1;
        [Tooltip("Where the player starts on a restart, facing its forward (blue arrow).")]
        [SerializeField] Transform respawnPoint;
        [Tooltip("Switched off on a restart from here or any later checkpoint: what's been got past, e.g. the Greys in the section before.")]
        [SerializeField] GameObject[] behind;

        // The player reached a checkpoint.
        public static event Action<Checkpoint> Reached;

        static string savedScene;
        static int savedOrder;
        static ShapeDefinition savedForm;
        static string lastLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlaySession()
        {
            savedScene = lastLoaded = null;
            savedOrder = 0;
            savedForm = null;
            Reached = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Anything but the same level loading again (a restart) forgets the checkpoint.
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            if (scene.path != lastLoaded) savedScene = null;
            lastLoaded = scene.path;
        }

        bool Saved => savedScene == gameObject.scene.path;

        void Start()
        {
            if (!Saved || order > savedOrder) return;
            foreach (var g in behind) if (g != null) g.SetActive(false);
            if (order == savedOrder) Respawn();
        }

        void Respawn()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return;
            if (savedForm != null)
            {
                // Forms scanned during the attempt are forgotten on a restart; this one was got.
                UnlockedShapes.Unlock(savedForm);
                player.GetComponent<PlayerShapeshifter>().StartAs(savedForm);
            }
            Transform at = respawnPoint != null ? respawnPoint : transform;
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = at.position;
            controller.enabled = true;
            player.GetComponent<PlayerController>().FaceTowards(at.forward);
            foreach (var cam in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
                cam.PreviousStateIsValid = false; // cut straight there rather than fly
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player") || (Saved && order <= savedOrder)) return;
            // This scene is the one loaded now, even when sceneLoaded never said so (the first
            // scene of an editor Play session), so a restart counts as loading it again.
            savedScene = lastLoaded = gameObject.scene.path;
            savedOrder = order;
            savedForm = other.GetComponent<PlayerShapeshifter>().Current;
            Reached?.Invoke(this);
        }
    }
}
