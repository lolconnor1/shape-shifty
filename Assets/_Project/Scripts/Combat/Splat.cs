using System.Collections.Generic;
using UnityEngine;

namespace ShapeShifty.Combat
{
    // A poo splat left where a throw landed: pops out flat against the surface, stays a while,
    // then shrinks away. Only so many are kept about; the oldest go first.
    public class Splat : MonoBehaviour
    {
        [SerializeField] float lifetime = 8f;
        [SerializeField] float growTime = 0.08f;
        [SerializeField] float shrinkTime = 0.6f;
        [Tooltip("Lifetime on a character: they'd look a mess otherwise.")]
        [SerializeField] float characterLifetime = 2.5f;
        const int MaxSplats = 50;

        static readonly Queue<Splat> live = new Queue<Splat>();

        float size, age, life;

        public void Begin(float splatSize, Transform stickTo)
        {
            size = splatSize;
            life = stickTo != null ? characterLifetime : lifetime;
            if (stickTo != null) transform.SetParent(stickTo, true);
            transform.localScale = Vector3.zero;
            live.Enqueue(this);
            while (live.Count > MaxSplats)
            {
                var oldest = live.Dequeue();
                if (oldest != null) Destroy(oldest.gameObject);
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            float k = age < growTime ? age / growTime : age > life ? 1f - (age - life) / shrinkTime : 1f;
            if (k <= 0f) { Destroy(gameObject); return; }
            Vector3 scale = new Vector3(size, size, size) * Mathf.Clamp01(k);
            // Keep its world size whatever it's stuck to.
            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(scale.x / Mathf.Max(0.001f, parentScale.x), scale.y / Mathf.Max(0.001f, parentScale.y), scale.z / Mathf.Max(0.001f, parentScale.z));
        }
    }
}
