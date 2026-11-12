using System;
using ShapeShifty.Interaction;
using UnityEngine;

namespace ShapeShifty.Player
{
    // Forms that can't swim (see ShapeDefinition.canSwim) hold their breath while their head
    // is under deep water, and drown if they stay under longer than the form's breathTime.
    // Breath comes back quickly once their head is out of the water, or straight away on
    // changing into a swimmer.
    [RequireComponent(typeof(CharacterController), typeof(PlayerShapeshifter))]
    public class PlayerBreath : MonoBehaviour
    {
        [Tooltip("Seconds to get all of their breath back once out of the water.")]
        [SerializeField] float recoverTime = 1f;
        [Tooltip("Where the head is, as a fraction of the collider's height.")]
        [SerializeField] float headHeight = 0.9f;

        // How much breath is left: 1 is full, 0 is drowned.
        public float Breath { get; private set; } = 1f;
        public bool IsUnderwater { get; private set; }
        public bool HasDrowned { get; private set; }

        public event Action Drowned;

        CharacterController controller;
        PlayerShapeshifter shapeshifter;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            shapeshifter = GetComponent<PlayerShapeshifter>();
        }

        void Update()
        {
            if (HasDrowned) return;
            Vector3 head = transform.position + Vector3.up * (controller.height * headHeight);
            IsUnderwater = !shapeshifter.Current.canSwim && DeepWater.At(head) != null;
            if (!IsUnderwater)
            {
                Breath = shapeshifter.Current.canSwim ? 1f : Mathf.MoveTowards(Breath, 1f, Time.deltaTime / recoverTime);
                return;
            }
            Breath = Mathf.MoveTowards(Breath, 0f, Time.deltaTime / shapeshifter.Current.breathTime);
            if (Breath > 0f) return;
            HasDrowned = true;
            Drowned?.Invoke();
        }
    }
}
