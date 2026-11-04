using ShapeShifty.Shapes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShapeShifty.UI
{
    // One form in the shape bar: its icon (or name, until it has an icon), highlighted when
    // selected, with a marker under the form the player is in right now.
    public class ShapeSlot : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text label;
        [SerializeField] Image currentMarker;

        [SerializeField] Color color = new Color(0.1f, 0.1f, 0.12f, 0.8f);
        [SerializeField] Color selectedColor = new Color(1f, 0.82f, 0.35f, 0.95f);
        [SerializeField] Color labelColor = new Color(0.95f, 0.95f, 0.95f);
        [SerializeField] Color selectedLabelColor = new Color(0.12f, 0.12f, 0.14f);

        public void Show(ShapeDefinition shape, bool selected, bool current)
        {
            icon.sprite = shape.icon;
            icon.gameObject.SetActive(shape.icon != null);
            label.text = shape.displayName;
            label.gameObject.SetActive(shape.icon == null);

            Color foreground = selected ? selectedLabelColor : labelColor;
            background.color = selected ? selectedColor : color;
            label.color = foreground;
            currentMarker.color = foreground;
            currentMarker.gameObject.SetActive(current);
        }
    }
}
