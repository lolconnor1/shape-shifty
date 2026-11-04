using System.Collections.Generic;
using ShapeShifty.Player;
using ShapeShifty.Shapes;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShapeShifty.UI
{
    // On-screen feedback for shapeshifting: an aiming dot with a bar that fills up while
    // scanning an animal, a prompt under it (also used for talking to animals and using
    // things like levers), and the bar of known forms along the bottom of the screen. The bar
    // stays hidden until there's more than one form, and dims after a few seconds untouched.
    public class ShapeHud : MonoBehaviour
    {
        [SerializeField] PlayerScanner scanner;
        [SerializeField] PlayerShapeshifter shapeshifter;
        [SerializeField] PlayerTalker talker;
        [SerializeField] PlayerInteractor interactor;
        [Tooltip("Used to show the right keys in the prompts.")]
        [SerializeField] InputActionAsset inputActions;

        [Header("Scanning")]
        [SerializeField] GameObject reticle;
        [SerializeField] Image reticleFill;
        [SerializeField] GameObject promptPanel;
        [SerializeField] TMP_Text prompt;
        [SerializeField] float messageTime = 3f;

        [Header("Shape Bar")]
        [SerializeField] CanvasGroup bar;
        [Tooltip("Copied once per form. Kept inactive.")]
        [SerializeField] ShapeSlot slotTemplate;
        [SerializeField] TMP_Text barHint;
        [SerializeField] float barShowTime = 3f;
        [SerializeField, Range(0f, 1f)] float barIdleAlpha = 0.4f;

        readonly List<ShapeSlot> slots = new List<ShapeSlot>();
        string scanKey, shiftKey, interactKey;
        string message;
        float messageUntil;
        float barShownUntil;

        void Awake()
        {
            var map = inputActions.FindActionMap("Player", true);
            scanKey = KeyName(map.FindAction("Scan", true));
            shiftKey = KeyName(map.FindAction("Shift", true));
            interactKey = KeyName(map.FindAction("Interact", true));
            barHint.text = $"Scroll to choose  ·  {shiftKey} to change";
            slotTemplate.gameObject.SetActive(false);
            bar.alpha = 0f;
        }

        void OnEnable()
        {
            scanner.Scanned += OnScanned;
            shapeshifter.Changed += RefreshBar;
            shapeshifter.ShiftBlocked += OnShiftBlocked;
        }

        void OnDisable()
        {
            scanner.Scanned -= OnScanned;
            shapeshifter.Changed -= RefreshBar;
            shapeshifter.ShiftBlocked -= OnShiftBlocked;
        }

        void Start() => RefreshBar();

        void Update()
        {
            Scannable target = scanner.Target;
            reticle.SetActive(target != null);
            if (target != null) reticleFill.fillAmount = scanner.Progress;

            string text = null;
            if (talker.IsTalking)
                text = null; // the dialogue box is showing
            else if (Time.unscaledTime < messageUntil)
                text = message;
            else if (target != null)
                text = scanner.Progress > 0f ? $"Scanning {target.Shape.displayName}..." : $"Hold {scanKey} to scan {target.Shape.displayName}";
            else if (talker.Target != null)
                text = $"Press {interactKey} to talk to the {talker.Target.Species.displayName.ToLower()}";
            else if (interactor.Target != null)
                text = interactor.CanUseTarget ? $"Press {interactKey} to {interactor.Target.Action}" : interactor.Target.WrongShapeHint;
            promptPanel.SetActive(text != null);
            if (text != null) prompt.text = text;

            float alpha = shapeshifter.Available.Count < 2 ? 0f
                : Time.unscaledTime < barShownUntil || shapeshifter.IsShifting ? 1f
                : barIdleAlpha;
            bar.alpha = Mathf.MoveTowards(bar.alpha, alpha, Time.unscaledDeltaTime * 4f);
        }

        void OnScanned(ShapeDefinition shape) => ShowMessage($"{shape.displayName} scanned! Press {shiftKey} to become one");

        void OnShiftBlocked(ShapeDefinition shape) => ShowMessage($"No room to become a {shape.displayName.ToLower()} here");

        void ShowMessage(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + messageTime;
        }

        void RefreshBar()
        {
            var forms = shapeshifter.Available;
            while (slots.Count < forms.Count)
            {
                var slot = Instantiate(slotTemplate, slotTemplate.transform.parent);
                slot.gameObject.SetActive(true);
                slots.Add(slot);
            }
            for (int i = 0; i < slots.Count; i++)
            {
                bool used = i < forms.Count;
                slots[i].gameObject.SetActive(used);
                if (used) slots[i].Show(forms[i], i == shapeshifter.SelectedIndex, forms[i] == shapeshifter.Current);
            }
            barShownUntil = Time.unscaledTime + barShowTime;
        }

        static string KeyName(InputAction action) =>
            action.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard&Mouse"));
    }
}
