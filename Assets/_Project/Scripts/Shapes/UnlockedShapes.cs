using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShapeShifty.Shapes
{
    // The forms the player has scanned so far, in the order they were scanned. Kept for the
    // whole play session, so forms carry over from level to level. Forms scanned during a
    // level only count once it's beaten: restarting forgets them (ForgetSinceLevelStart).
    // Not saved to disk yet, so quitting the game forgets everything.
    public static class UnlockedShapes
    {
        static readonly List<ShapeDefinition> shapes = new List<ShapeDefinition>();
        static int countAtLevelStart;

        public static IReadOnlyList<ShapeDefinition> All => shapes;
        public static event Action<ShapeDefinition> Unlocked;

        public static bool Contains(ShapeDefinition shape) => shapes.Contains(shape);

        public static void Unlock(ShapeDefinition shape)
        {
            if (shape == null || shapes.Contains(shape)) return;
            shapes.Add(shape);
            Unlocked?.Invoke(shape);
        }

        // Call before reloading a level to retry it.
        public static void ForgetSinceLevelStart() =>
            shapes.RemoveRange(countAtLevelStart, shapes.Count - countAtLevelStart);

        // Call when going back to the main menu, which starts a fresh run.
        public static void ForgetAll()
        {
            shapes.Clear();
            countAtLevelStart = 0;
        }

        // Every scene load starts a level with whatever forms the player has at that point.
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => countAtLevelStart = shapes.Count;

        // Start every Play session in the editor fresh, even with domain reload turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlaySession()
        {
            shapes.Clear();
            countAtLevelStart = 0;
            Unlocked = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
    }
}
