using System;
using UnityEngine;

namespace ShapeShifty.Audio
{
    // Every sound effect in the game. Scripts ask for one by name (Sfx.Play(Sound.Jump, ...));
    // the SoundBank says which clips it uses, so a clip can be swapped without touching code.
    public enum Sound
    {
        Footstep, Jump, Land, Splash,
        Shift, Denied, Scanned,
        Interact, Lever, HatchMove, HatchShut,
        Throw, Splat, Hurt, Squeak, Defeat, Pickup, Ding,
        Solved, Caught, LevelDone, UiClick, Talk,
        Bark, Honk, Alarm, Zap,
    }

    // The clips for each Sound, with how loud they play. The one in a Resources folder named
    // "SoundBank" is the one Sfx uses. Tools/make_sfx.py makes the placeholder clips.
    [CreateAssetMenu(menuName = "Shape-Shifty/Sound Bank")]
    public class SoundBank : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public Sound sound;
            [Tooltip("One is picked at random each time it plays.")]
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 0.8f;
            [Tooltip("Pitch varies by up to this much either way, so repeats don't sound identical.")]
            [Range(0f, 0.5f)] public float pitchJitter = 0.05f;
        }

        [Range(0f, 1f)] public float masterVolume = 1f;
        [SerializeField] Entry[] entries;

        Entry[] bySound;

        public Entry Get(Sound sound)
        {
            if (bySound == null)
            {
                bySound = new Entry[Enum.GetValues(typeof(Sound)).Length];
                foreach (var entry in entries) bySound[(int)entry.sound] = entry;
            }
            var found = bySound[(int)sound];
            return found != null && found.clips != null && found.clips.Length > 0 ? found : null;
        }

        void OnValidate() => bySound = null;
    }
}
