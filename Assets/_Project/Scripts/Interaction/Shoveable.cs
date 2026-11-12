using UnityEngine;

namespace ShapeShifty.Interaction
{
    // Something heavy the player can only move by hitting it hard: a belly-slider ramming it at
    // full tilt sets it rolling, and a swimmer can nudge it along under water. Walking into it
    // does nothing. Sits still (kinematic) until the first good hit. Needs a Rigidbody.
    [RequireComponent(typeof(Rigidbody))]
    public class Shoveable : MonoBehaviour
    {
        [Tooltip("How fast (m/s) a sliding player has to hit it to shift it.")]
        [SerializeField] float minSlideSpeed = 8f;
        [Tooltip("How fast (m/s) a swimming player has to hit it to shift it.")]
        [SerializeField] float minSwimSpeed = 4f;
        [Tooltip("Speed it's sent off at, as a fraction of the player's.")]
        [SerializeField] float shove = 0.5f;
        [Tooltip("Damping while in deep water, so it sinks more or less straight down instead of rolling on across the bottom.")]
        [SerializeField] float waterDamping = 3f;

        public bool IsLoose { get; private set; }

        Rigidbody body;
        float dryDamping, dryAngularDamping;
        float lastShove = -1f;
        bool wet;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            IsLoose = !body.isKinematic;
            dryDamping = body.linearDamping;
            dryAngularDamping = body.angularDamping;
        }

        void FixedUpdate()
        {
            if (!IsLoose) return;
            bool inWater = DeepWater.At(transform.position) != null;
            if (inWater == wet) return;
            wet = inWater;
            body.linearDamping = wet ? waterDamping : dryDamping;
            body.angularDamping = wet ? waterDamping : dryAngularDamping;
        }

        // Called by PlayerController when a sliding or swimming player runs into it.
        public void Shove(Vector3 playerVelocity, bool swimming)
        {
            playerVelocity.y = 0f;
            float speed = playerVelocity.magnitude;
            if (speed < (swimming ? minSwimSpeed : minSlideSpeed)) return;
            if (Time.time - lastShove < 0.3f) return; // one hit, not one per frame of contact
            lastShove = Time.time;
            if (!IsLoose)
            {
                IsLoose = true;
                body.isKinematic = false;
            }
            body.AddForce(playerVelocity / speed * (speed * shove), ForceMode.VelocityChange);
        }
    }
}
