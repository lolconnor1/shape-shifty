using ShapeShifty.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShapeShifty.Player
{
    // Uses the nearest Interactable when Interact (E) is pressed, as long as the player is in
    // a form that's allowed to use it. Talking shares the same key and comes first: while
    // there's an animal to talk to, this stands aside.
    [RequireComponent(typeof(PlayerShapeshifter))]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] PlayerTalker talker;
        [Tooltip("How close (m) the player has to stand to use something.")]
        [SerializeField] float range = 1.5f;
        [Tooltip("How far (m) above or below the player it can be.")]
        [SerializeField] float reachHeight = 2f;

        // The nearest thing in range, whether or not the current form can use it.
        public Interactable Target { get; private set; }
        public bool CanUseTarget => Target != null && Target.CanBeUsedBy(shapeshifter.Current);

        InputAction interactAction;
        PlayerShapeshifter shapeshifter;

        void Awake()
        {
            interactAction = inputActions.FindActionMap("Player", true).FindAction("Interact", true);
            shapeshifter = GetComponent<PlayerShapeshifter>();
        }

        void OnEnable() => interactAction.Enable();

        void OnDisable() => Target = null;

        void Update()
        {
            Target = talker.Target != null ? null : FindTarget();
            if (CanUseTarget && interactAction.WasPressedThisFrame()) Target.Interact();
        }

        Interactable FindTarget()
        {
            if (shapeshifter.IsShifting) return null;
            Interactable best = null;
            float bestDistance = range * range;
            foreach (var interactable in Interactable.Active)
            {
                Vector3 offset = interactable.Point - transform.position;
                if (Mathf.Abs(offset.y) > reachHeight) continue;
                offset.y = 0f;
                float distance = offset.sqrMagnitude;
                if (distance <= bestDistance)
                {
                    best = interactable;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
