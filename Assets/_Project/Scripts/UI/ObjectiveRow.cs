using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShapeShifty.UI
{
    // One task on the ObjectiveHud.
    public class ObjectiveRow : MonoBehaviour
    {
        [Tooltip("Blank space before the box, for parts of a task.")]
        [SerializeField] GameObject indent;
        [SerializeField] Image box;
        [SerializeField] Image tick;
        [SerializeField] TMP_Text label;
        [SerializeField] Image arrow;
        [SerializeField] Color openColor = new Color(0.97f, 0.97f, 0.97f);
        [SerializeField] Color doneColor = new Color(0.55f, 0.85f, 0.5f);
        [SerializeField] Color arrowColor = new Color(1f, 0.82f, 0.35f);
        [SerializeField] float fontSize = 26f;
        [SerializeField] float partFontSize = 22f;

        float arrivedAt = float.NegativeInfinity;
        string shown;
        bool shownDone;

        // Just been given: lit up for a moment so it's noticed.
        public void Arrive()
        {
            arrivedAt = Time.unscaledTime;
            arrow.color = arrowColor;
        }

        public void Show(string text, bool done, bool isPart)
        {
            if (text != shown || done != shownDone)
            {
                shown = text;
                shownDone = done;
                label.text = done ? $"<s>{text}</s>" : text;
                label.fontSize = isPart ? partFontSize : fontSize;
                indent.SetActive(isPart);
                tick.enabled = done;
            }
            float flash = Mathf.Clamp01(1f - (Time.unscaledTime - arrivedAt) / 1.5f);
            Color color = done ? doneColor : Color.Lerp(openColor, arrowColor, flash);
            label.color = color;
            box.color = color;
        }

        public void HideArrow() => arrow.enabled = false;

        // Degrees clockwise from straight ahead.
        public void PointArrow(float angle)
        {
            arrow.enabled = true;
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }
    }
}
