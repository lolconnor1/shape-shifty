using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShapeShifty.Audio
{
    // Plays sound effects from any script without it needing an AudioSource:
    // Sfx.Play(Sound.Jump, position) for a sound in the world, or Sfx.Play(Sound.UiClick) for
    // one with no place (menus, stingers). Loop starts one that repeats until Stop, e.g. a siren.
    // Made when the game starts with a pool of voices, and kept across scenes; loops stop when
    // a scene loads.
    public class Sfx : MonoBehaviour
    {
        const int Voices = 24;
        // The same sound asked for again this soon is skipped, e.g. a splash hitting five things.
        const float RepeatGap = 0.04f;
        // At most this many of any one sound at once: a pack barking is a din, not twelve barks.
        const int MostAtOnce = 3;
        const float SettleTime = 0.3f;

        static Sfx instance;

        SoundBank bank;
        AudioSource[] voices;
        float[] lastPlayed;
        int next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Create()
        {
            var bank = Resources.Load<SoundBank>("SoundBank");
            if (bank == null)
            {
                Debug.LogWarning("No SoundBank in a Resources folder: the game will be silent.");
                return;
            }
            var host = new GameObject("Sfx");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<Sfx>();
            instance.bank = bank;
        }

        void Awake()
        {
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var voice = new GameObject("Voice").AddComponent<AudioSource>();
                voice.transform.SetParent(transform);
                voice.playOnAwake = false;
                voice.dopplerLevel = 0f;
                voice.rolloffMode = AudioRolloffMode.Linear;
                voice.minDistance = 6f; // the player, a few metres from the camera, is heard at full volume
                voice.maxDistance = 45f;
                voices[i] = voice;
            }
            lastPlayed = new float[System.Enum.GetValues(typeof(Sound)).Length];
            for (int i = 0; i < lastPlayed.Length; i++) lastPlayed[i] = float.NegativeInfinity;
            SceneManager.sceneLoaded += (_, _) => StopLoops();
        }

        // A sound at a point in the world: quieter further from the camera.
        public static AudioSource Play(Sound sound, Vector3 position, float volume = 1f, float pitch = 1f) =>
            instance != null ? instance.Emit(sound, position, true, volume, pitch, false) : null;

        // A sound with no place, heard the same wherever the camera is.
        public static AudioSource Play(Sound sound, float volume = 1f, float pitch = 1f) =>
            instance != null ? instance.Emit(sound, Vector3.zero, false, volume, pitch, false) : null;

        // Repeats until passed to Stop (or the next scene loads).
        public static AudioSource Loop(Sound sound, float volume = 1f) =>
            instance != null ? instance.Emit(sound, Vector3.zero, false, volume, 1f, true) : null;

        public static void Stop(AudioSource voice)
        {
            if (voice == null) return;
            voice.Stop();
            voice.loop = false;
        }

        // Turns a loop down (or up) to a volume, as given to Loop, over some seconds.
        public static void Fade(AudioSource voice, Sound sound, float volume, float seconds)
        {
            if (voice == null || instance == null) return;
            var entry = instance.bank.Get(sound);
            if (entry != null) instance.StartCoroutine(instance.FadeTo(voice, entry.volume * volume * instance.bank.masterVolume, seconds));
        }

        System.Collections.IEnumerator FadeTo(AudioSource voice, float volume, float seconds)
        {
            var clip = voice.clip;
            float from = voice.volume;
            for (float t = 0f; t < seconds && voice.loop && voice.clip == clip; t += Time.deltaTime)
            {
                voice.volume = Mathf.Lerp(from, volume, t / seconds);
                yield return null;
            }
        }

        public static void StopLoops()
        {
            if (instance == null) return;
            foreach (var voice in instance.voices) if (voice.loop) Stop(voice);
        }

        AudioSource Emit(Sound sound, Vector3 position, bool spatial, float volume, float pitch, bool loop)
        {
            var entry = bank.Get(sound);
            if (entry == null) return null;
            // Just after a scene loads, things being put back how they were (doors, solved
            // puzzles after a restart) aren't heard.
            if (Time.timeSinceLevelLoad < SettleTime) return null;
            if (!loop)
            {
                if (Time.unscaledTime - lastPlayed[(int)sound] < Mathf.Max(RepeatGap, entry.minGap)) return null;
                int playing = 0;
                foreach (var v in voices)
                    if (v.isPlaying && System.Array.IndexOf(entry.clips, v.clip) >= 0 && ++playing >= MostAtOnce) return null;
            }
            lastPlayed[(int)sound] = Time.unscaledTime;

            var voice = FreeVoice();
            if (voice == null) return null;
            voice.transform.position = position;
            voice.spatialBlend = spatial ? 1f : 0f;
            voice.clip = entry.clips[Random.Range(0, entry.clips.Length)];
            voice.volume = entry.volume * volume * bank.masterVolume;
            voice.pitch = pitch * (1f + Random.Range(-entry.pitchJitter, entry.pitchJitter));
            voice.loop = loop;
            voice.Play();
            return voice;
        }

        // An idle voice if there is one, otherwise the one that started longest ago. Loops
        // are never cut off.
        AudioSource FreeVoice()
        {
            AudioSource fallback = null;
            for (int i = 0; i < Voices; i++)
            {
                var voice = voices[(next + i) % Voices];
                if (voice.loop) continue;
                if (!voice.isPlaying)
                {
                    next = (next + i + 1) % Voices;
                    return voice;
                }
                fallback ??= voice;
            }
            next = (next + 1) % Voices;
            return fallback;
        }
    }
}
