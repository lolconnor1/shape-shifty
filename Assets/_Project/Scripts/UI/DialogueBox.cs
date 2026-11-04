using ShapeShifty.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShapeShifty.UI
{
    // Shows the conversation the player is having with an animal: who's speaking, the line
    // typing out, and a reminder to press Interact once the line has finished.
    public class DialogueBox : MonoBehaviour
    {
        [SerializeField] PlayerTalker talker;
        [Tooltip("Used to show the right key in the continue hint.")]
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text speaker;
        [SerializeField] TMP_Text text;
        [SerializeField] TMP_Text continueHint;
        [SerializeField] string playerName = "You";
        [SerializeField] Color animalNameColor = new Color(1f, 0.82f, 0.35f);
        [SerializeField] Color playerNameColor = new Color(0.55f, 0.85f, 1f);

        void Awake()
        {
            var interact = inputActions.FindActionMap("Player", true).FindAction("Interact", true);
            continueHint.text = interact.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard&Mouse")) + " to continue";
            panel.SetActive(false);
        }

        // After PlayerTalker has updated this frame.
        void LateUpdate()
        {
            bool talking = talker.IsTalking;
            if (panel.activeSelf != talking) panel.SetActive(talking);
            if (!talking) return;

            var line = talker.CurrentLine;
            speaker.text = line.byPlayer ? playerName : talker.Target.Species.displayName;
            speaker.color = line.byPlayer ? playerNameColor : animalNameColor;
            if (text.text != line.text) text.text = line.text;
            text.maxVisibleCharacters = talker.VisibleCharacters;
            continueHint.gameObject.SetActive(talker.LineFinished);
        }
    }
}
