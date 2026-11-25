using ShapeShifty.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShapeShifty.Player
{
    // Carries one bone at a time (see Bone), held out in front at mouth height for whatever
    // form the player is in, and lobs it the way the camera's looking with Throw.
    [RequireComponent(typeof(CharacterController), typeof(PlayerShapeshifter))]
    public class PlayerCarry : MonoBehaviour
    {
        [SerializeField] InputActionAsset inputActions;
        [Tooltip("Where the camera looks from: a throw goes the way it's pointing.")]
        [SerializeField] Transform cameraTarget;
        [Tooltip("The model: the held bone follows it.")]
        [SerializeField] Transform body;
        [SerializeField] float throwSpeed = 11f;
        [Tooltip("Degrees above the camera's line a throw sets off at.")]
        [SerializeField, Range(0f, 60f)] float throwAngle = 20f;

        public Bone Held { get; private set; }
        public bool HasBone => Held != null;

        CharacterController controller;
        PlayerShapeshifter shapeshifter;
        InputAction throwAction;
        Transform hold;
        // The current model's "Mouth", if it has one: a wolf carries the bone in its jaws.
        Transform mouth;
        object mouthOf;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            shapeshifter = GetComponent<PlayerShapeshifter>();
            throwAction = inputActions.FindActionMap("Player", true).FindAction("Attack", true);
            hold = new GameObject("Carry").transform;
            hold.SetParent(body, false);
        }

        void OnEnable() => throwAction.Enable();

        // Called by a bone the player interacts with.
        public void Take(Bone bone)
        {
            if (HasBone || bone.IsHeld) return;
            Held = bone;
            PlaceHold();
            bone.PickUp(HoldPoint());
        }

        // A wolf has snatched the bone: hands it over.
        public Bone Surrender()
        {
            Bone bone = Held;
            Held = null;
            return bone;
        }

        void Update()
        {
            PlaceHold();
            if (!HasBone) return;
            // Changed form: moves the bone to the new model's mouth (or out in front).
            Transform point = HoldPoint();
            if (Held.Holder != point) Held.PickUp(point);
            if (throwAction.WasPressedThisFrame()) Throw();
        }

        // Out in front, at mouth height for the current form.
        void PlaceHold() => hold.localPosition = new Vector3(0f, controller.height * 0.7f, controller.radius + 0.25f);

        Transform HoldPoint()
        {
            if (!ReferenceEquals(mouthOf, shapeshifter.Current))
            {
                mouthOf = shapeshifter.Current;
                mouth = null;
                foreach (var t in body.GetComponentsInChildren<Transform>())
                    if (t.name == "Mouth" && t.gameObject.activeInHierarchy) { mouth = t; break; }
            }
            return mouth != null && mouth.gameObject.activeInHierarchy ? mouth : hold;
        }

        void Throw()
        {
            Bone bone = Held;
            Held = null;
            Vector3 forward = cameraTarget.forward;
            Vector3 flat = forward;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) flat = body.forward;
            flat.Normalize();
            Vector3 direction = Quaternion.AngleAxis(-throwAngle, Vector3.Cross(Vector3.up, flat)) * forward;
            // From just ahead of the player, so it doesn't start inside their collider.
            bone.transform.position = hold.position + flat * 0.3f;
            Vector3 carried = controller.velocity;
            carried.y = 0f;
            bone.Throw(direction.normalized * throwSpeed + carried * 0.5f);
        }
    }
}
