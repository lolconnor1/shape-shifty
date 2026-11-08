using UnityEngine;

namespace ShapeShifty.Player
{
    // Puts the player out of action for a moment, e.g. after stepping into live water: they
    // can't move, change form, scan, talk or use anything, and their body jolts about. The
    // camera can still look round. Everything comes back when it wears off.
    [RequireComponent(typeof(PlayerController))]
    public class PlayerStun : MonoBehaviour
    {
        [Tooltip("Jolted about while stunned: the model PlayerController turns.")]
        [SerializeField] Transform body;
        [SerializeField] float jolt = 0.04f;
        [SerializeField] float joltAngle = 8f;

        public bool IsStunned => timer > 0f;

        PlayerController mover;
        Behaviour[] actions;
        Vector3 bodyPosition;
        Quaternion bodyRotation;
        float timer;

        void Awake()
        {
            mover = GetComponent<PlayerController>();
            actions = new Behaviour[]
            {
                GetComponent<PlayerShapeshifter>(), GetComponent<PlayerScanner>(),
                GetComponent<PlayerTalker>(), GetComponent<PlayerInteractor>(),
            };
        }

        public void Stun(float seconds)
        {
            if (!IsStunned)
            {
                bodyPosition = body.localPosition;
                bodyRotation = body.localRotation;
                SetActions(false);
                mover.Frozen = true;
            }
            timer = Mathf.Max(timer, seconds);
        }

        void Update()
        {
            if (!IsStunned) return;
            timer -= Time.deltaTime;
            if (timer > 0f)
            {
                body.SetLocalPositionAndRotation(
                    bodyPosition + new Vector3(Random.Range(-jolt, jolt), 0f, Random.Range(-jolt, jolt)),
                    bodyRotation * Quaternion.Euler(Random.Range(-joltAngle, joltAngle), 0f, Random.Range(-joltAngle, joltAngle)));
                return;
            }
            body.SetLocalPositionAndRotation(bodyPosition, bodyRotation);
            mover.Frozen = false;
            SetActions(true);
        }

        void SetActions(bool enabled)
        {
            foreach (var action in actions)
                if (action != null) action.enabled = enabled;
        }
    }
}
