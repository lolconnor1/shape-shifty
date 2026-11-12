using System;
using System.Collections;
using System.Collections.Generic;
using ShapeShifty.Shapes;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShapeShifty.Player
{
    // Turns the player into any form they've scanned. Previous / Next (scroll wheel, d-pad)
    // choose a form and Shift changes into it. Changing takes a moment: the old model shrinks
    // away in a puff, the collider, speeds and camera switch over, and the new model grows in.
    // After each change there's a short cooldown before the next one.
    // Employees who see it happen get suspicious fast (see VisionCone).
    [RequireComponent(typeof(CharacterController), typeof(PlayerController))]
    public class PlayerShapeshifter : MonoBehaviour
    {
        [SerializeField] InputActionAsset inputActions;

        [Header("Forms")]
        [Tooltip("The player's own form: always available, and the one every level starts in.")]
        [SerializeField] ShapeDefinition baseShape;
        [Tooltip("The base shape's model, already placed under Visual.")]
        [SerializeField] GameObject baseModel;
        [Tooltip("Parent for the form models. The transform PlayerController turns to face the direction of travel.")]
        [SerializeField] Transform visual;

        [Header("Camera")]
        [SerializeField] Transform cameraTarget;
        [SerializeField] CinemachineThirdPersonFollow follow;
        [SerializeField] float cameraBlendTime = 0.5f;

        [Header("Shifting")]
        [SerializeField] float shiftDuration = 0.4f;
        [Tooltip("Seconds after a change finishes before the player can change again.")]
        [SerializeField] float cooldown = 2f;
        [SerializeField] ParticleSystem puff;
        [Tooltip("Layers that stop the player growing into a bigger form. Ignore Raycast (the invisible level bounds) is left out.")]
        [SerializeField] LayerMask obstacleMask = ~(1 << 2);

        public ShapeDefinition Current { get; private set; }
        // How suspicious employees find the current form right here (see BlendInZone).
        public float Suspicion => BlendInZone.Hides(Current, transform.position) ? 0f : Current.suspicion;
        public ShapeDefinition Selected => available[SelectedIndex];
        public int SelectedIndex { get; private set; }
        public IReadOnlyList<ShapeDefinition> Available => available;
        public bool IsShifting { get; private set; }
        // Seconds until the player can change again; 0 when ready.
        public float CooldownLeft => Mathf.Max(0f, readyAt - Time.time);

        // Selection, the list of forms, or the current form changed.
        public event Action Changed;
        // Shift was pressed but the selected form doesn't fit here.
        public event Action<ShapeDefinition> ShiftBlocked;
        // Shift was pressed before the cooldown from the last change ran out.
        public event Action ShiftTooSoon;

        readonly List<ShapeDefinition> available = new List<ShapeDefinition>();
        readonly Dictionary<ShapeDefinition, GameObject> models = new Dictionary<ShapeDefinition, GameObject>();
        readonly Collider[] overlaps = new Collider[8];
        CharacterController controller;
        PlayerController mover;
        InputAction previousAction, nextAction, shiftAction;
        GameObject currentModel;
        Coroutine cameraBlend;
        float readyAt;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            mover = GetComponent<PlayerController>();
            var map = inputActions.FindActionMap("Player", true);
            previousAction = map.FindAction("Previous", true);
            nextAction = map.FindAction("Next", true);
            shiftAction = map.FindAction("Shift", true);

            models[baseShape] = baseModel;
            currentModel = baseModel;
            Current = baseShape;
            RebuildAvailable();
            ApplyForm(baseShape);
            SnapCamera(baseShape);
        }

        void OnEnable()
        {
            previousAction.Enable();
            nextAction.Enable();
            shiftAction.Enable();
            UnlockedShapes.Unlocked += OnUnlocked;
        }

        void OnDisable() => UnlockedShapes.Unlocked -= OnUnlocked;

        void Update()
        {
            if (previousAction.WasPressedThisFrame()) Select(SelectedIndex - 1);
            if (nextAction.WasPressedThisFrame()) Select(SelectedIndex + 1);
            if (shiftAction.WasPressedThisFrame() && !IsShifting && Selected != Current)
            {
                if (CooldownLeft > 0f) ShiftTooSoon?.Invoke();
                else if (HasRoomFor(Selected)) StartCoroutine(Shift(Selected));
                else ShiftBlocked?.Invoke(Selected);
            }
        }

        public void Select(int index)
        {
            SelectedIndex = (index % available.Count + available.Count) % available.Count;
            Changed?.Invoke();
        }

        // Switches straight into a known form with no effect, e.g. arriving in a level as a rat.
        public void StartAs(ShapeDefinition shape)
        {
            int index = available.IndexOf(shape);
            if (index < 0 || shape == Current) return;
            currentModel.SetActive(false);
            currentModel = GetModel(shape);
            currentModel.SetActive(true);
            ApplyForm(shape);
            Current = shape;
            SnapCamera(shape);
            Select(index);
        }

        void OnUnlocked(ShapeDefinition shape)
        {
            RebuildAvailable();
            Select(available.IndexOf(shape)); // ready to change into straight away
        }

        void RebuildAvailable()
        {
            available.Clear();
            available.Add(baseShape);
            foreach (var shape in UnlockedShapes.All)
                if (shape != baseShape) available.Add(shape);
        }

        IEnumerator Shift(ShapeDefinition shape)
        {
            IsShifting = true;
            Changed?.Invoke();
            puff.transform.localPosition = Vector3.up * (Mathf.Max(Current.height, shape.height) * 0.5f);
            puff.Play();

            float half = shiftDuration * 0.5f;
            yield return ScaleVisual(1f, 0f, half);

            currentModel.SetActive(false);
            currentModel = GetModel(shape);
            currentModel.SetActive(true);
            ApplyForm(shape);
            Current = shape;
            if (cameraBlend != null) StopCoroutine(cameraBlend);
            cameraBlend = StartCoroutine(BlendCamera(shape));

            yield return ScaleVisual(0f, 1f, half);
            IsShifting = false;
            readyAt = Time.time + cooldown;
            Changed?.Invoke();
        }

        IEnumerator ScaleVisual(float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                visual.localScale = Vector3.one * Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            visual.localScale = Vector3.one * to;
        }

        GameObject GetModel(ShapeDefinition shape)
        {
            if (!models.TryGetValue(shape, out var model))
            {
                model = Instantiate(shape.model, visual);
                model.name = shape.model.name;
                models[shape] = model;
            }
            return model;
        }

        void ApplyForm(ShapeDefinition shape)
        {
            // Step offset can't be taller than the capsule, so lower it before shrinking.
            controller.stepOffset = Mathf.Min(controller.stepOffset, shape.stepOffset);
            controller.height = shape.height;
            controller.radius = shape.radius;
            controller.center = Vector3.up * (shape.height * 0.5f);
            controller.skinWidth = shape.skinWidth;
            controller.stepOffset = shape.stepOffset;
            mover.SetForm(shape);
        }

        // Growing could push the player into a ceiling or wall, so check the bigger capsule
        // is clear first. Only above step height: bumps below that are fine to stand on.
        bool HasRoomFor(ShapeDefinition shape)
        {
            if (shape.height <= controller.height && shape.radius <= controller.radius) return true;
            Vector3 feet = transform.position;
            Vector3 bottom = feet + Vector3.up * (shape.radius + shape.stepOffset);
            Vector3 top = feet + Vector3.up * Mathf.Max(shape.height - shape.radius, shape.radius + shape.stepOffset);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, shape.radius, overlaps, obstacleMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (overlaps[i] != controller) return false;
            return true;
        }

        IEnumerator BlendCamera(ShapeDefinition shape)
        {
            float fromHeight = cameraTarget.localPosition.y;
            float fromDistance = follow.CameraDistance;
            Vector3 fromShoulder = follow.ShoulderOffset;
            float fromArm = follow.VerticalArmLength;
            for (float t = 0f; t < cameraBlendTime; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / cameraBlendTime);
                SetCamera(Mathf.Lerp(fromHeight, shape.cameraHeight, k), Mathf.Lerp(fromDistance, shape.cameraDistance, k),
                    Vector3.Lerp(fromShoulder, shape.shoulderOffset, k), Mathf.Lerp(fromArm, shape.verticalArmLength, k));
                yield return null;
            }
            SnapCamera(shape);
            cameraBlend = null;
        }

        void SnapCamera(ShapeDefinition shape) =>
            SetCamera(shape.cameraHeight, shape.cameraDistance, shape.shoulderOffset, shape.verticalArmLength);

        void SetCamera(float height, float distance, Vector3 shoulder, float arm)
        {
            Vector3 target = cameraTarget.localPosition;
            cameraTarget.localPosition = new Vector3(target.x, height, target.z);
            follow.CameraDistance = distance;
            follow.ShoulderOffset = shoulder;
            follow.VerticalArmLength = arm;
        }
    }
}
