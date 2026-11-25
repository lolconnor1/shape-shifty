using System;
using ShapeShifty.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShapeShifty.Player
{
    // Flinging poo, for forms that can (ShapeDefinition.canThrow): Attack throws a lump at
    // whatever is under the middle of the screen, arcing it so it lands there. While in such
    // a form the body turns with the camera, so the player strafes like in a shooter.
    // Throws come out of a supply that a PooPile tops back up.
    [RequireComponent(typeof(PlayerShapeshifter), typeof(PlayerController))]
    public class PlayerThrower : MonoBehaviour
    {
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] PooProjectile pooPrefab;
        [Tooltip("The camera: throws go where its middle is pointing.")]
        [SerializeField] Transform aimCamera;
        [SerializeField] float throwSpeed = 26f;
        [SerializeField] float gravity = 9f;
        [SerializeField] float cooldown = 0.3f;
        [SerializeField] float range = 50f;
        [SerializeField] int maxAmmo = 20;
        [Tooltip("What the aim ray stops at. Ignore Raycast (the invisible level bounds) is left out.")]
        [SerializeField] LayerMask aimMask = ~(1 << 2);
        [Tooltip("Degrees back the arm (a child of the model called \"ArmR\", if it has one) winds up before flinging over the top.")]
        [SerializeField] float armSwing = 150f;
        [SerializeField] float swingTime = 0.25f;

        public bool CanThrow => shapeshifter.Current != null && shapeshifter.Current.canThrow && !shapeshifter.IsShifting;
        public int Ammo { get; private set; }
        public int MaxAmmo => maxAmmo;
        // What's under the crosshair, if anything, and where.
        public Health AimedAt { get; private set; }
        public Bullseye AimedAtTarget { get; private set; }
        public Vector3 AimPoint { get; private set; }

        public event Action Thrown;
        // Attack pressed with nothing left to throw.
        public event Action OutOfAmmo;
        // Attack pressed in a form that can't throw, while it would mean something.
        public event Action CantThrow;
        public event Action Refilled;

        PlayerShapeshifter shapeshifter;
        PlayerController mover;
        CharacterController controller;
        PlayerCarry carry;
        InputAction attackAction;
        float readyAt, swingStart = -99f;
        Transform arm;
        Quaternion armRest;
        object armOf;
        readonly RaycastHit[] hits = new RaycastHit[12];

        void Awake()
        {
            shapeshifter = GetComponent<PlayerShapeshifter>();
            mover = GetComponent<PlayerController>();
            controller = GetComponent<CharacterController>();
            carry = GetComponent<PlayerCarry>();
            attackAction = inputActions.FindActionMap("Player", true).FindAction("Attack", true);
            Ammo = maxAmmo;
            if (aimCamera == null && Camera.main != null) aimCamera = Camera.main.transform;
        }

        void OnEnable() => attackAction.Enable();

        void OnDisable()
        {
            if (mover != null) mover.FacesCamera = false;
        }

        // A poo pile: back up to full. False if already full.
        public bool Refill()
        {
            if (Ammo >= maxAmmo) return false;
            Ammo = maxAmmo;
            Refilled?.Invoke();
            return true;
        }

        void Update()
        {
            bool can = CanThrow;
            mover.FacesCamera = can;
            if (!can)
            {
                AimedAt = null;
                AimedAtTarget = null;
                if (attackAction.WasPressedThisFrame() && (carry == null || !carry.HasBone) && (Health.AnyFoes || Bullseye.All.Count > 0)) CantThrow?.Invoke();
                return;
            }
            Aim();
            if (!attackAction.WasPressedThisFrame() || Time.time < readyAt) return;
            if (carry != null && carry.HasBone) return; // that click throws the bone
            if (Ammo <= 0) { OutOfAmmo?.Invoke(); readyAt = Time.time + cooldown; return; }
            Throw();
        }

        void Aim()
        {
            if (aimCamera == null) return;
            Ray ray = new Ray(aimCamera.position, aimCamera.forward);
            int count = Physics.RaycastNonAlloc(ray, hits, range, aimMask, QueryTriggerInteraction.Ignore);
            float best = range;
            Collider found = null;
            // Nearest hit that isn't the player and isn't between the camera and the player.
            float behind = Vector3.Dot(transform.position - aimCamera.position, aimCamera.forward);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].transform.IsChildOf(transform)) continue;
                if (hits[i].distance < behind * 0.8f) continue;
                if (hits[i].distance < best) { best = hits[i].distance; found = hits[i].collider; }
            }
            AimPoint = ray.GetPoint(best);
            AimedAt = found != null ? found.GetComponentInParent<Health>() : null;
            if (AimedAt != null && (AimedAt.Team == Team.Player || AimedAt.IsDead)) AimedAt = null;
            AimedAtTarget = found != null ? found.GetComponentInParent<Bullseye>() : null;
        }

        void Throw()
        {
            readyAt = Time.time + cooldown;
            Ammo--;
            Vector3 hand = HandPosition();
            Vector3 velocity = PooProjectile.Aim(hand, AimPoint, throwSpeed, gravity);
            var poo = Instantiate(pooPrefab, hand, Quaternion.identity);
            poo.Launch(velocity, Team.Player, transform, gravity);
            swingStart = Time.time;
            Thrown?.Invoke();
        }

        // Up by the right shoulder, a little in front.
        Vector3 HandPosition()
        {
            Vector3 forward = aimCamera != null ? aimCamera.forward : transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return transform.position + Vector3.up * (controller.height * 0.85f) + right * (controller.radius * 0.8f) + forward * (controller.radius + 0.2f);
        }

        // After the model's Animator, which would otherwise put the arm back.
        void LateUpdate() => UpdateArm();

        void UpdateArm()
        {
            if (!ReferenceEquals(armOf, shapeshifter.Current))
            {
                armOf = shapeshifter.Current;
                arm = null;
                foreach (var t in GetComponentsInChildren<Transform>())
                    if (t.name == "ArmR" && t.gameObject.activeInHierarchy) { arm = t; armRest = t.localRotation; break; }
            }
            if (arm == null) return;
            float k = (Time.time - swingStart) / swingTime;
            // An overarm fling: back and up behind the shoulder, then over the top and on
            // round, down in front, back to hanging.
            float angle = k < 0f || k > 1f ? 0f : k < 0.2f ? Mathf.Lerp(0f, armSwing, k / 0.2f) : Mathf.Lerp(armSwing, 360f, (k - 0.2f) / 0.8f);
            arm.localRotation = armRest * Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
