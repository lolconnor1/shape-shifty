using TMPro;
using UnityEngine;

namespace ShapeShifty.UI
{
    // A word that pops up over something in the world, drifts up and fades away, like a
    // goose's "HONK!". Spawn makes one; it tidies itself up.
    public class FloatingWord : MonoBehaviour
    {
        const float Life = 1.1f;

        TextMeshPro text;
        Color color;
        Vector3 drift;
        float age;

        public static void Spawn(Vector3 at, string word, Color color, float size = 4f)
        {
            var g = new GameObject("Word " + word);
            g.transform.position = at;
            var t = g.AddComponent<TextMeshPro>();
            t.text = word;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = new Vector2(4f, 1f);
            t.color = color;
            var w = g.AddComponent<FloatingWord>();
            w.text = t;
            w.color = color;
            w.drift = new Vector3(Random.Range(-0.3f, 0.3f), 1.1f, Random.Range(-0.3f, 0.3f));
            w.LateUpdate();
        }

        void LateUpdate()
        {
            age += Time.deltaTime;
            if (age > Life) { Destroy(gameObject); return; }
            transform.position += drift * Time.deltaTime;
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            float k = age / Life;
            text.color = new Color(color.r, color.g, color.b, 1f - k * k);
            transform.localScale = Vector3.one * (k < 0.12f ? Mathf.Lerp(0.4f, 1.15f, k / 0.12f) : Mathf.Lerp(1.15f, 1f, (k - 0.12f) * 4f));
        }
    }
}
