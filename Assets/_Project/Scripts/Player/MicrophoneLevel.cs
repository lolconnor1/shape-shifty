using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ShapeShifty.Player
{
    // Listens to the player's microphone and says how loud it is right now. Standalone
    // builds and the editor use Unity's Microphone class; WebGL has none, so a small
    // JavaScript plugin (Plugins/WebGL/WebMicrophone.jslib) asks the browser instead. Either
    // way nothing happens until Listen() is called, and the browser may ask permission.
    // The background noise is measured the whole time, so a noisy room isn't mistaken for
    // shouting: "loud" means well above whatever the room sounds like.
    public class MicrophoneLevel : MonoBehaviour
    {
        public enum Status { Off, Starting, Listening, Unavailable }

        [Tooltip("Quietest level (RMS, 0-1) that can ever count as loud, however quiet the room is.")]
        [SerializeField] float minLoudLevel = 0.04f;
        [Tooltip("How many times louder than the background noise counts as loud.")]
        [SerializeField] float loudMultiplier = 3f;
        [Tooltip("Seconds for the measured background level to creep up in a room that's got noisier.")]
        [SerializeField] float floorRiseTime = 20f;
        [Tooltip("Seconds for the measured background level to drop in a room that's gone quiet.")]
        [SerializeField] float floorFallTime = 1.5f;
        [SerializeField] int sampleRate = 16000;
        [Tooltip("How many of the latest samples the level is measured over.")]
        [SerializeField] int windowSize = 1024;

        public Status State { get; private set; }
        // The level this frame (RMS, 0-1).
        public float Level { get; private set; }
        // Where the background noise sits.
        public float NoiseFloor { get; private set; } = 0.02f;
        // The level needed to count as loud right now.
        public float LoudLevel => Mathf.Max(minLoudLevel, NoiseFloor * loudMultiplier);
        // For meters: 0 at the background level, 1 at the loud level and above.
        public float Loudness => Mathf.Clamp01((Level - NoiseFloor) / Mathf.Max(0.001f, LoudLevel - NoiseFloor));
        public bool IsLive => State == Status.Listening || simulatedLevel >= 0f;
        public bool IsLoud => IsLive && Level >= LoudLevel;

        // Tests can feed in a pretend level instead of a real microphone. Negative = off.
        [System.NonSerialized] public float simulatedLevel = -1f;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void WebMic_Start();
        [DllImport("__Internal")] static extern int WebMic_GetState();
        [DllImport("__Internal")] static extern float WebMic_GetLevel();
        [DllImport("__Internal")] static extern void WebMic_Stop();
#else
        AudioClip clip;
        float[] samples;
#endif

        // Switches the microphone on. Safe to call again: a refusal can be retried.
        public void Listen()
        {
            if (State == Status.Starting || State == Status.Listening) return;
            StartCoroutine(StartListening());
        }

        public void Stop()
        {
            if (State != Status.Listening) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            WebMic_Stop();
#else
            Microphone.End(null);
            Destroy(clip);
            clip = null;
#endif
            State = Status.Off;
            Level = 0f;
        }

        void OnDestroy() => Stop();

        IEnumerator StartListening()
        {
            State = Status.Starting;
#if UNITY_WEBGL && !UNITY_EDITOR
            WebMic_Start();
            while (WebMic_GetState() == 1) yield return null;
            State = WebMic_GetState() == 2 ? Status.Listening : Status.Unavailable;
#else
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone) || Microphone.devices.Length == 0)
            {
                State = Status.Unavailable;
                yield break;
            }
            // Only a rate the device supports (0-0 means any).
            Microphone.GetDeviceCaps(null, out int minRate, out int maxRate);
            int rate = maxRate > 0 ? Mathf.Clamp(sampleRate, minRate, maxRate) : sampleRate;
            clip = Microphone.Start(null, true, 1, rate);
            if (clip == null)
            {
                State = Status.Unavailable;
                yield break;
            }
            samples = new float[windowSize];
            State = Status.Listening;
#endif
        }

        void Update()
        {
            Level = simulatedLevel >= 0f ? simulatedLevel : State == Status.Listening ? Read() : 0f;
            if (!IsLive) return;
            // The background creeps up slowly and drops quickly, and never learns from a shout.
            if (Level < NoiseFloor) NoiseFloor = Mathf.Lerp(NoiseFloor, Level, Time.unscaledDeltaTime / floorFallTime);
            else if (Level < LoudLevel) NoiseFloor = Mathf.Lerp(NoiseFloor, Level, Time.unscaledDeltaTime / floorRiseTime);
        }

        float Read()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return WebMic_GetLevel();
#else
            int position = Microphone.GetPosition(null);
            if (clip == null || position < windowSize) return 0f;
            clip.GetData(samples, position - windowSize);
            float sum = 0f;
            for (int i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
            return Mathf.Sqrt(sum / samples.Length);
#endif
        }
    }
}
