using System.Collections.Generic;
using ShapeShifty.Shapes;
using UnityEngine;

namespace ShapeShifty.Dialogue
{
    // An animal the player can talk to after turning into the same kind of animal. Holds
    // still (its movement script checks IsTalking) and turns to face the player while they
    // chat. Without a Conversation it has nothing to say.
    public class Talkable : MonoBehaviour
    {
        [Tooltip("The form the player has to be in to talk to this animal.")]
        [SerializeField] ShapeDefinition species;
        [Tooltip("What it says. Usually set per level on the animal in the scene.")]
        [SerializeField] Conversation conversation;
        [SerializeField] float turnSpeed = 540f;

        public static readonly List<Talkable> Active = new List<Talkable>();

        public ShapeDefinition Species => species;
        public Conversation Conversation => conversation;
        public bool HasSomethingToSay => conversation != null && conversation.lines != null && conversation.lines.Length > 0;
        public bool IsTalking => listener != null;

        Transform listener;

        void OnEnable() => Active.Add(this);

        void OnDisable()
        {
            Active.Remove(this);
            listener = null;
        }

        // Called by the player when a conversation starts and ends.
        public void StartTalking(Transform with) => listener = with;
        public void StopTalking() => listener = null;

        void Update()
        {
            if (listener == null) return;
            Vector3 toListener = listener.position - transform.position;
            toListener.y = 0f;
            if (toListener.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toListener), turnSpeed * Time.deltaTime);
        }
    }
}
