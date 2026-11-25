using System;
using ShapeShifty.Audio;
using ShapeShifty.Player;
using UnityEngine;

namespace ShapeShifty.Combat
{
    // Walked into by the player (needs a trigger collider). A poo pile tops a thrower back up
    // to a full supply and never runs out. A banana heals and is eaten.
    public class Pickup : MonoBehaviour
    {
        public enum Kind { PooPile, Banana }

        [SerializeField] Kind kind = Kind.Banana;
        [Tooltip("Health a banana gives back.")]
        [SerializeField] float heal = 2f;
        [Tooltip("Spun and bobbed so it catches the eye (bananas).")]
        [SerializeField] Transform show;

        // Picked up: what kind, and where.
        public static event Action<Kind, Vector3> Collected;

        Vector3 showRest;

        void Awake()
        {
            if (show != null) showRest = show.localPosition;
        }

        void Update()
        {
            if (show == null) return;
            show.localPosition = showRest + Vector3.up * (Mathf.Sin(Time.time * 2.5f) * 0.08f);
            show.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
        }

        void OnTriggerStay(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (kind == Kind.PooPile)
            {
                if (other.TryGetComponent(out PlayerThrower thrower) && thrower.CanThrow && thrower.Refill())
                {
                    Collected?.Invoke(kind, transform.position);
                    Sfx.Play(Sound.Pickup, transform.position, 1f, 0.75f);
                }
                return;
            }
            if (other.TryGetComponent(out Health health) && health.Heal(heal))
            {
                Collected?.Invoke(kind, transform.position);
                gameObject.SetActive(false);
            }
        }
    }
}
