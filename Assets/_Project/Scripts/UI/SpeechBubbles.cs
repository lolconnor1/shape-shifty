using System.Collections.Generic;
using ShapeShifty.Dialogue;
using ShapeShifty.Player;
using UnityEngine;

namespace ShapeShifty.UI
{
    // A speech bubble over every animal the player could talk to in the form they're in, so
    // it's clear there's something to hear. Bobs over an animal they haven't talked to yet;
    // small and faint over one they have.
    public class SpeechBubbles : MonoBehaviour
    {
        [SerializeField] PlayerShapeshifter shapeshifter;
        [SerializeField] Sprite bubble;
        [Tooltip("How far off (m) bubbles can be seen.")]
        [SerializeField] float range = 30f;
        [SerializeField] float size = 0.5f;
        [Tooltip("Gap (m) between the animal's head and the bubble.")]
        [SerializeField] float gap = 0.12f;

        class Bubble
        {
            public SpriteRenderer sprite;
            public float top; // above the animal's origin
            public float shown;
        }

        readonly Dictionary<Talkable, Bubble> bubbles = new Dictionary<Talkable, Bubble>();
        Transform view;

        void Start() => view = Camera.main != null ? Camera.main.transform : null;

        void LateUpdate()
        {
            if (view == null) return;
            foreach (var talkable in Talkable.Active)
            {
                if (!talkable.HasSomethingToSay) continue;
                float distance = Vector3.Distance(talkable.transform.position, shapeshifter.transform.position);
                bool show = talkable.Species == shapeshifter.Current && !shapeshifter.IsShifting && !talkable.IsTalking && distance < range;
                if (!bubbles.TryGetValue(talkable, out var b))
                {
                    if (!show) continue;
                    bubbles[talkable] = b = Make(talkable);
                }
                b.shown = Mathf.MoveTowards(b.shown, show ? 1f : 0f, Time.deltaTime * 4f);
                bool visible = b.shown > 0f;
                if (b.sprite.enabled != visible) b.sprite.enabled = visible;
                if (!visible) continue;

                bool heard = talkable.WasHeard;
                float bob = heard ? 0f : Mathf.Sin(Time.time * 3f) * 0.05f;
                var t = b.sprite.transform;
                // Bigger further off, so it can still be made out.
                float scale = size * (heard ? 0.6f : 1f) * Mathf.Lerp(0.7f, 1.8f, distance / range) * Mathf.SmoothStep(0f, 1f, b.shown);
                t.position = talkable.transform.position + Vector3.up * (b.top + gap + bob);
                t.rotation = Quaternion.LookRotation(view.forward);
                t.localScale = Vector3.one * scale;
                b.sprite.color = new Color(1f, 1f, 1f, heard ? 0.45f : 0.95f);
            }
        }

        // Sits just above the top of the animal's model (the sprite's pivot is its tail).
        Bubble Make(Talkable talkable)
        {
            float top = 0.2f;
            foreach (var r in talkable.GetComponentsInChildren<SkinnedMeshRenderer>())
                top = Mathf.Max(top, Mathf.Min(r.bounds.max.y - talkable.transform.position.y, 2.4f));
            var go = new GameObject("SpeechBubble " + talkable.name);
            go.transform.SetParent(transform.root.parent, false);
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = bubble;
            sprite.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sprite.receiveShadows = false;
            return new Bubble { sprite = sprite, top = top };
        }
    }
}
