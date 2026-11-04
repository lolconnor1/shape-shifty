using System;
using ShapeShifty.Dialogue;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShapeShifty.Player
{
    // Lets the player chat with animals of the kind they've turned into. Interact (E) next
    // to one starts its conversation; after that it finishes typing out the current line,
    // or moves on to the next. Walking away, changing form or running out of lines ends it.
    [RequireComponent(typeof(PlayerShapeshifter))]
    public class PlayerTalker : MonoBehaviour
    {
        [SerializeField] InputActionAsset inputActions;
        [Tooltip("How close (m) an animal has to be to start talking.")]
        [SerializeField] float range = 1.5f;
        [Tooltip("Walking further than this (m) from the animal ends the conversation.")]
        [SerializeField] float leaveDistance = 3f;
        [Tooltip("Characters per second as a line types out.")]
        [SerializeField] float typingSpeed = 45f;

        // The animal being talked to, or the one Interact would start talking to.
        public Talkable Target { get; private set; }
        public bool IsTalking => lineIndex >= 0;
        public Conversation.Line CurrentLine => Target.Conversation.lines[lineIndex];
        public int VisibleCharacters => Mathf.FloorToInt(typed);
        public bool LineFinished => typed >= CurrentLine.text.Length;

        InputAction interactAction;
        PlayerShapeshifter shapeshifter;
        int lineIndex = -1;
        float typed;

        void Awake()
        {
            interactAction = inputActions.FindActionMap("Player", true).FindAction("Interact", true);
            shapeshifter = GetComponent<PlayerShapeshifter>();
        }

        void OnEnable() => interactAction.Enable();

        void OnDisable() => End();

        void Update()
        {
            if (IsTalking)
            {
                if (!CanKeepTalking())
                {
                    End();
                    return;
                }
                typed = Mathf.Min(typed + typingSpeed * Time.deltaTime, CurrentLine.text.Length);
                if (interactAction.WasPressedThisFrame()) Advance();
                return;
            }

            Target = FindTarget();
            if (Target != null && interactAction.WasPressedThisFrame())
            {
                lineIndex = 0;
                typed = 0f;
                Target.StartTalking(transform);
            }
        }

        void Advance()
        {
            if (!LineFinished)
            {
                typed = CurrentLine.text.Length;
                return;
            }
            if (lineIndex + 1 < Target.Conversation.lines.Length)
            {
                lineIndex++;
                typed = 0f;
            }
            else End();
        }

        void End()
        {
            if (!IsTalking) return;
            if (Target != null) Target.StopTalking();
            lineIndex = -1;
        }

        bool CanKeepTalking() =>
            Target != null && Target.isActiveAndEnabled && !shapeshifter.IsShifting && shapeshifter.Current == Target.Species
            && (Target.transform.position - transform.position).sqrMagnitude <= leaveDistance * leaveDistance;

        Talkable FindTarget()
        {
            if (shapeshifter.IsShifting) return null;
            Talkable best = null;
            float bestDistance = range * range;
            foreach (var talkable in Talkable.Active)
            {
                if (talkable.Species != shapeshifter.Current || !talkable.HasSomethingToSay) continue;
                float distance = (talkable.transform.position - transform.position).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    best = talkable;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
