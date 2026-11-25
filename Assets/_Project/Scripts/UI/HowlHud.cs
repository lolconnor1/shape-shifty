using ShapeShifty.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShapeShifty.UI
{
    // How the howling's going, shown only in a form that can howl (see PlayerHowl): whether
    // the microphone is listening, how loud the player is against the level that counts as
    // a howl, and a big "AWOOO" while they're at it. Also a hint for throwing a carried bone.
    public class HowlHud : MonoBehaviour
    {
        [SerializeField] PlayerHowl howl;
        [SerializeField] PlayerShapeshifter shapeshifter;
        [SerializeField] PlayerCarry carry;
        [Tooltip("Used to show the right keys.")]
        [SerializeField] InputActionAsset inputActions;

        [Header("Microphone")]
        [SerializeField] CanvasGroup panel;
        [SerializeField] TMP_Text status;
        [SerializeField] Image levelFill;
        [SerializeField] Color quietColor = new Color(0.55f, 0.75f, 1f);
        [SerializeField] Color loudColor = new Color(1f, 0.85f, 0.3f);

        [Header("Howling")]
        [SerializeField] GameObject howlText;

        [Header("Carrying")]
        [SerializeField] GameObject carryHint;
        [SerializeField] TMP_Text carryText;

        string howlKey, microphoneKey, throwKey;
        MicrophoneLevel.Status shownStatus = (MicrophoneLevel.Status)(-1);

        void Awake()
        {
            var map = inputActions.FindActionMap("Player", true);
            howlKey = KeyName(map.FindAction("Howl", true));
            microphoneKey = KeyName(map.FindAction("Microphone", true));
            throwKey = KeyName(map.FindAction("Attack", true));
            panel.alpha = 0f;
            howlText.SetActive(false);
            carryHint.SetActive(false);
            carryText.text = throwKey.StartsWith("LMB") ? "Click to throw the bone" : $"{throwKey} to throw the bone";
        }

        void Update()
        {
            bool able = shapeshifter.Current.canHowl;
            panel.alpha = Mathf.MoveTowards(panel.alpha, able ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            var microphone = howl.Microphone;
            if (able && microphone != null)
            {
                ShowStatus(microphone.State);
                levelFill.fillAmount = microphone.IsLive ? microphone.Loudness : 0f;
                levelFill.color = microphone.IsLoud ? loudColor : quietColor;
            }
            if (howlText.activeSelf != howl.IsHowling) howlText.SetActive(howl.IsHowling);
            if (howl.IsHowling) howlText.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 8f) * 0.06f);
            bool carrying = carry != null && carry.HasBone;
            if (carryHint.activeSelf != carrying) carryHint.SetActive(carrying);
        }

        void ShowStatus(MicrophoneLevel.Status state)
        {
            if (state == shownStatus) return;
            shownStatus = state;
            switch (state)
            {
                case MicrophoneLevel.Status.Starting: status.text = "Asking for your microphone..."; break;
                case MicrophoneLevel.Status.Listening: status.text = $"Mic on: howl!  (or hold {howlKey})"; break;
                case MicrophoneLevel.Status.Unavailable: status.text = $"No mic: hold {howlKey} to howl  ({microphoneKey} to try the mic again)"; break;
                default: status.text = $"Hold {howlKey} to howl  ({microphoneKey} to howl for real)"; break;
            }
        }

        static string KeyName(InputAction action) =>
            action.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard&Mouse"));
    }
}
