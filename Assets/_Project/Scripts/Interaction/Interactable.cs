using System;
using System.Collections.Generic;
using ShapeShifty.Shapes;
using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Interaction
{
    // Something the player can use with Interact (E) while standing next to it, like a lever
    // or a button. Can be limited to one form, so only a human can pull a lever, say; in any
    // other form the player just gets a hint. Disable it to stop it being used.
    public class Interactable : MonoBehaviour
    {
        [Tooltip("The spot the player has to stand near. Defaults to this object.")]
        [SerializeField] Transform point;
        [Tooltip("Finishes the prompt \"Press E to ...\", e.g. \"pull the lever\".")]
        [SerializeField] string action = "use this";
        [Tooltip("Only this form can use it. Leave empty to let any form use it.")]
        [SerializeField] ShapeDefinition requiredShape;
        [Tooltip("Shown instead of the prompt when the player is in any other form.")]
        [SerializeField] string wrongShapeHint = "You can't use this in this form";
        [SerializeField] UnityEvent onInteract;

        public static readonly List<Interactable> Active = new List<Interactable>();

        public event Action Interacted;

        public Vector3 Point => (point != null ? point : transform).position;
        public string Action => action;
        public string WrongShapeHint => wrongShapeHint;

        public bool CanBeUsedBy(ShapeDefinition shape) => requiredShape == null || shape == requiredShape;

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        // Called by the player.
        public void Interact()
        {
            Interacted?.Invoke();
            onInteract.Invoke();
        }
    }
}
