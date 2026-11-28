using System.Collections;
using ShapeShifty.AI;
using ShapeShifty.Audio;
using ShapeShifty.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Levels
{
    // The getaway, set off by grabbing the Golden Goose. The lights pulse red and the beacons
    // spin, a lockdown clock counts down on screen (reaching zero ends the attempt: "Locked
    // in!"), the goose panics, and after a moment the guard dogs come out of their kennel.
    // Everything else the alarm does (the way in slamming shut, the way out opening, the
    // shutter run re-arming) hangs off onBegin.
    public class EscapeAlarm : MonoBehaviour
    {
        [SerializeField] float lockdownTime = 90f;
        [SerializeField] CanvasGroup clockPanel;
        [SerializeField] TMP_Text clockText;
        [SerializeField] Color clockColor = Color.white;
        [SerializeField] Color clockUrgentColor = new Color(1f, 0.3f, 0.25f);

        [Header("Chaos")]
        [SerializeField] GuardDog[] dogs;
        [SerializeField] float dogDelay = 0.8f;
        [Tooltip("A look at the kennel as it opens. The dogs come out once it's over (after Head Start), and the clock waits for it.")]
        [SerializeField] CameraShot dogShot;
        [Tooltip("Seconds after the shot before the dogs come out: the player's head start.")]
        [SerializeField] float headStart = 2.2f;
        [SerializeField] GoldenGoose goose;
        [Tooltip("The siren blares for this long (s), then drops to a murmur so everything else can be heard.")]
        [SerializeField] float sirenTime = 3.5f;
        [SerializeField, Range(0f, 1f)] float sirenMurmur = 0.12f;

        [Header("Lights")]
        [Tooltip("Pulses towards the alarm colour, as does the ambient light.")]
        [SerializeField] Light mainLight;
        [SerializeField] Color alarmColor = new Color(1f, 0.12f, 0.08f);
        [Tooltip("Pulses a second.")]
        [SerializeField] float pulseRate = 1.4f;
        [Tooltip("Hidden until the alarm, then spun round like the lamp on a police car.")]
        [SerializeField] Transform[] beacons;
        [SerializeField] float beaconSpeed = 400f;

        public UnityEvent onBegin;

        public bool IsRunning { get; private set; }

        float deadline;
        Color sky, equator, ground, lightColor;
        ShapeHud hud;
        AudioSource siren;

        void Awake()
        {
            hud = FindFirstObjectByType<ShapeHud>();
            if (clockPanel != null) clockPanel.alpha = 0f;
            foreach (var b in beacons) if (b != null) b.gameObject.SetActive(false);
        }

        public void Begin()
        {
            if (IsRunning) return;
            IsRunning = true;
            deadline = Time.time + lockdownTime;
            sky = RenderSettings.ambientSkyColor;
            equator = RenderSettings.ambientEquatorColor;
            ground = RenderSettings.ambientGroundColor;
            if (mainLight != null) lightColor = mainLight.color;
            foreach (var b in beacons) if (b != null) b.gameObject.SetActive(true);
            if (goose != null) goose.Panic();
            siren = Sfx.Loop(Sound.Alarm, 0.6f);
            onBegin.Invoke();
            if (hud != null) hud.ShowMessage("ALARM! Get out before the lockdown!");
            StartCoroutine(LetTheDogsOut());
        }

        IEnumerator LetTheDogsOut()
        {
            yield return new WaitForSeconds(dogDelay);
            Sfx.Play(Sound.Growl);
            if (dogShot != null)
            {
                bool over = false;
                dogShot.onDone.AddListener(() => over = true);
                dogShot.Play();
                float nextBark = 0f;
                while (!over)
                {
                    if (Time.time >= nextBark)
                    {
                        nextBark = Time.time + 0.55f;
                        var dog = dogs[Random.Range(0, dogs.Length)];
                        if (dog != null) dog.Bark();
                    }
                    yield return null;
                }
            }
            if (hud != null) hud.ShowMessage("The guard dogs are loose. RUN!");
            yield return new WaitForSeconds(headStart);
            foreach (var dog in dogs) if (dog != null) dog.Release();
            yield return new WaitForSeconds(sirenTime);
            Sfx.Fade(siren, Sound.Alarm, sirenMurmur, 2f);
        }

        // Out of the zoo: the clock stops.
        public void Escaped()
        {
            IsRunning = false;
            Sfx.Stop(siren);
            if (clockPanel != null) clockPanel.alpha = 0f;
        }

        void Update()
        {
            if (!IsRunning) return;
            if (CameraShot.Playing) deadline += Time.deltaTime;
            float left = Mathf.Max(0f, deadline - Time.time);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseRate * Mathf.PI * 2f);
            float k = 0.3f + 0.45f * pulse;
            RenderSettings.ambientSkyColor = Color.Lerp(sky, alarmColor * 0.7f, k);
            RenderSettings.ambientEquatorColor = Color.Lerp(equator, alarmColor * 0.55f, k);
            RenderSettings.ambientGroundColor = Color.Lerp(ground, alarmColor * 0.25f, k);
            if (mainLight != null) mainLight.color = Color.Lerp(lightColor, alarmColor, k);
            foreach (var b in beacons) if (b != null) b.Rotate(Vector3.up, beaconSpeed * Time.deltaTime, Space.World);

            if (clockPanel != null) clockPanel.alpha = 1f;
            if (clockText != null)
            {
                int seconds = Mathf.CeilToInt(left);
                clockText.text = $"LOCKDOWN IN {seconds / 60}:{seconds % 60:00}";
                clockText.color = left < 15f && Mathf.Repeat(Time.time * 2f, 1f) < 0.5f ? clockUrgentColor : clockColor;
            }
            if (left > 0f) return;
            IsRunning = false;
            var caught = FindFirstObjectByType<CaughtScreen>();
            if (caught != null) caught.Show("Locked in!");
        }
    }
}
