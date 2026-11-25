using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShapeShifty.Player
{
    // Howling, for forms that can (see ShapeDefinition.canHowl). Holding Howl, or howling
    // into the microphone (see MicrophoneLevel), for a moment starts a howl: the player
    // stands still with their head thrown back for as long as they keep it up, and every
    // Wolf in earshot answers. A ring spreads out along the ground to show how far it
    // carries. Microphone (M) switches the mic on, or tries again after a refusal.
    [RequireComponent(typeof(PlayerController), typeof(PlayerShapeshifter))]
    public class PlayerHowl : MonoBehaviour
    {
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] MicrophoneLevel microphone;
        [Tooltip("Switch the microphone on as soon as the player is in a form that can howl.")]
        [SerializeField] bool listenWhenAble = true;

        [Header("Howling")]
        [Tooltip("Seconds of noise, or of holding the key, before it counts as a howl, so a cough doesn't.")]
        [SerializeField] float startTime = 0.35f;
        [Tooltip("Seconds of quiet before a howl is over, so a breath mid-howl doesn't end it.")]
        [SerializeField] float stopTime = 0.4f;
        [Tooltip("Longest one howl can go on (s).")]
        [SerializeField] float maxTime = 6f;
        [Tooltip("How far (m) a howl carries. Wolves also have their own hearing range.")]
        [SerializeField] float range = 18f;

        [Header("Visual")]
        [Tooltip("Degrees the body tips back while howling.")]
        [SerializeField] float leanBack = 12f;
        [Tooltip("Degrees a model's \"Head\" child (if it has one) tips back while howling.")]
        [SerializeField] float headBack = 50f;
        [Tooltip("Spread out along the ground from the player, once a second, while howling. Scaled to Range.")]
        [SerializeField] Transform ring;
        [SerializeField] float ringTime = 1f;

        public bool IsHowling { get; private set; }
        // How long the current howl has gone on (s).
        public float HowlTime { get; private set; }
        // Goes up by one each time a howl starts, so listeners can tell a new one from the last.
        public int HowlCount { get; private set; }
        public float Range => range;
        public bool CanHowl => shapeshifter.Current.canHowl && !shapeshifter.IsShifting && !mover.IsSliding && !mover.IsSwimming;
        public MicrophoneLevel Microphone => microphone;

        public event Action Started;
        public event Action Stopped;

        PlayerController mover;
        PlayerShapeshifter shapeshifter;
        InputAction howlAction, microphoneAction;
        Transform head;
        Quaternion headRest;
        Renderer ringRenderer;
        float buildUp, quiet, ringPhase;
        bool wasAble;

        void Awake()
        {
            mover = GetComponent<PlayerController>();
            shapeshifter = GetComponent<PlayerShapeshifter>();
            var map = inputActions.FindActionMap("Player", true);
            howlAction = map.FindAction("Howl", true);
            microphoneAction = map.FindAction("Microphone", true);
            if (ring != null)
            {
                ringRenderer = ring.GetComponent<Renderer>();
                ring.gameObject.SetActive(false);
            }
        }

        void OnEnable()
        {
            howlAction.Enable();
            microphoneAction.Enable();
        }

        void OnDisable()
        {
            if (IsHowling) End();
            buildUp = 0f;
        }

        void Update()
        {
            bool able = shapeshifter.Current.canHowl;
            if (able && !wasAble && listenWhenAble && microphone != null) microphone.Listen();
            wasAble = able;
            if (microphone != null && microphoneAction.WasPressedThisFrame())
            {
                if (microphone.State == MicrophoneLevel.Status.Listening) microphone.Stop();
                else microphone.Listen();
            }

            bool wanting = CanHowl && (howlAction.IsPressed() || (microphone != null && microphone.IsLoud));
            if (!IsHowling)
            {
                buildUp = wanting ? buildUp + Time.deltaTime : 0f;
                if (buildUp >= startTime) Begin();
                return;
            }

            HowlTime += Time.deltaTime;
            quiet = wanting ? 0f : quiet + Time.deltaTime;
            if (quiet >= stopTime || HowlTime >= maxTime || !CanHowl) End();
            else UpdateRing();
        }

        // After the model's Animator, which would otherwise put the head back.
        void LateUpdate()
        {
            if (IsHowling && head != null) head.localRotation = headRest * Quaternion.Euler(-headBack, 0f, 0f);
        }

        void Begin()
        {
            IsHowling = true;
            HowlTime = 0f;
            quiet = 0f;
            buildUp = 0f;
            HowlCount++;
            mover.Rooted = true;
            mover.HeadBack = leanBack;
            head = FindHead();
            if (head != null) headRest = head.localRotation;
            ringPhase = 0f;
            if (ring != null) ring.gameObject.SetActive(true);
            Started?.Invoke();
        }

        void End()
        {
            IsHowling = false;
            mover.Rooted = false;
            mover.HeadBack = 0f;
            if (head != null) head.localRotation = headRest;
            head = null;
            if (ring != null) ring.gameObject.SetActive(false);
            Stopped?.Invoke();
        }

        // The ring grows out to the howl's range and fades, then starts again.
        void UpdateRing()
        {
            if (ring == null) return;
            ringPhase = Mathf.Repeat(ringPhase + Time.deltaTime / ringTime, 1f);
            float size = Mathf.Lerp(1f, range * 2f, ringPhase);
            ring.localScale = new Vector3(size, ring.localScale.y, size);
            if (ringRenderer != null && ringRenderer.material.HasProperty("_BaseColor"))
            {
                Color color = ringRenderer.material.GetColor("_BaseColor");
                color.a = (1f - ringPhase) * 0.5f;
                ringRenderer.material.SetColor("_BaseColor", color);
            }
        }

        // The current model's head, if it has one called that.
        Transform FindHead()
        {
            foreach (var t in shapeshifter.GetComponentsInChildren<Transform>())
                if (t.name == "Head" && t.gameObject.activeInHierarchy) return t;
            return null;
        }
    }
}
