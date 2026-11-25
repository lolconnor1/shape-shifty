using System;
using ShapeShifty.AI;
using ShapeShifty.Audio;
using ShapeShifty.Interaction;
using ShapeShifty.Player;
using ShapeShifty.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace ShapeShifty.Levels
{
    // The Golden Goose's cage in the middle of the vault, and the level's progress: a lock per
    // puzzle, each with a lamp on the plinth. OpenLock lights the lock's lamp and runs its
    // onOpened, which should also leave its puzzle solved (lasers off and so on), because a
    // restart puts the opened locks back that way. With every lock open the cage lifts and the
    // Golden Goose herself can be grabbed, which sets off the EscapeAlarm.
    // Restarting after being caught keeps the locks already opened; loading the level any
    // other way starts it afresh.
    public class GooseVault : MonoBehaviour
    {
        [Serializable]
        public class Lock
        {
            public string name;
            public Renderer lamp;
            public UnityEvent onOpened;
            [NonSerialized] public bool open;
        }

        [SerializeField] Lock[] locks;
        [SerializeField] Material lampOff;
        [SerializeField] Material lampOn;
        [Tooltip("Lifts the cage off the plinth.")]
        [SerializeField] Hatch cage;
        [Tooltip("Grabbing the goose. Kept disabled until the cage is open.")]
        [FormerlySerializedAs("egg")]
        [SerializeField] Interactable grab;
        [SerializeField] GoldenGoose goose;
        [SerializeField] EscapeAlarm alarm;

        public int OpenCount { get; private set; }
        public bool GooseTaken { get; private set; }

        static string savedScene, lastLoaded;
        static bool[] saved;
        ShapeHud hud;
        bool restoring;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlaySession()
        {
            savedScene = lastLoaded = null;
            saved = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Anything but the same level loading again (a restart) forgets the opened locks.
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            if (scene.path != lastLoaded) savedScene = null;
            lastLoaded = scene.path;
        }

        void Awake()
        {
            hud = FindFirstObjectByType<ShapeHud>();
            grab.enabled = false;
            grab.Interacted += GrabGoose;
            foreach (var l in locks) if (l.lamp != null) l.lamp.sharedMaterial = lampOff;
        }

        void Start()
        {
            if (savedScene != gameObject.scene.path || saved == null || saved.Length != locks.Length) return;
            restoring = true;
            for (int i = 0; i < locks.Length; i++) if (saved[i]) OpenLock(i);
            restoring = false;
            if (hud != null && OpenCount > 0) hud.ShowMessage($"{OpenCount} of {locks.Length} locks still open");
        }

        public void OpenLock(int index)
        {
            var l = locks[index];
            if (l.open) return;
            l.open = true;
            OpenCount++;
            if (l.lamp != null) l.lamp.sharedMaterial = lampOn;

            // This scene is the one loaded now, even when sceneLoaded never said so (the first
            // scene of an editor Play session), so a restart counts as loading it again.
            savedScene = lastLoaded = gameObject.scene.path;
            if (saved == null || saved.Length != locks.Length) saved = new bool[locks.Length];
            saved[index] = true;

            l.onOpened.Invoke();
            Sfx.Play(Sound.Solved);
            bool all = OpenCount == locks.Length;
            if (all)
            {
                cage.Open();
                grab.enabled = true;
            }
            if (hud == null || restoring) return;
            hud.ShowMessage(all
                ? $"{l.name} done! All {locks.Length} locks are open: grab the Golden Goose!"
                : $"{l.name} done! {OpenCount} of {locks.Length} locks open");
        }

        void GrabGoose()
        {
            if (GooseTaken) return;
            GooseTaken = true;
            grab.enabled = false;
            var player = GameObject.FindWithTag("Player");
            if (player != null) goose.Grab(player.GetComponent<PlayerShapeshifter>());
            alarm.Begin();
        }
    }
}
