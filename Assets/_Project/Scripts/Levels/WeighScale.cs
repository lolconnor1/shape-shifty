using ShapeShifty.Player;
using TMPro;
using UnityEngine;

namespace ShapeShifty.Levels
{
    // A floor scale: while the player stands on its plate (inside Plate's bounds) the dial
    // shows what their current form weighs (ShapeDefinition.weight), with the needle swinging
    // round to match. The way to find out that shape and weight go together.
    public class WeighScale : MonoBehaviour
    {
        [SerializeField] Collider plate;
        [SerializeField] TMP_Text readout;
        [SerializeField] Transform needle;
        [Tooltip("Weight at a full turn of the needle's sweep.")]
        [SerializeField] float fullScale = 100f;
        [SerializeField] float sweep = 270f;

        Transform player;
        PlayerShapeshifter shifter;
        float shown;
        string text;

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p == null) return;
            player = p.transform;
            shifter = p.GetComponent<PlayerShapeshifter>();
        }

        void Update()
        {
            if (player == null) return;
            Bounds b = plate.bounds;
            b.Expand(new Vector3(0f, 1f, 0f));
            bool on = b.Contains(player.position + Vector3.up * 0.1f);
            float target = on && shifter.Current != null ? shifter.Current.weight : 0f;
            shown = Mathf.MoveTowards(shown, target, Time.deltaTime * Mathf.Max(20f, Mathf.Abs(target - shown) * 3f));
            if (needle != null) needle.localRotation = Quaternion.Euler(0f, 0f, -sweep * Mathf.Clamp01(shown / fullScale) + sweep * 0.5f);
            string next = shown < 10f ? $"{shown:0.0} kg" : $"{shown:0} kg";
            if (next != text) readout.text = text = next;
        }
    }
}
