using System;
using System.Collections.Generic;
using ShapeShifty.Audio;
using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Combat
{
    // A target for the player's poo, working like a button that can be pressed from far away.
    // Only a hit on its face counts (its forward, the blue arrow, points at the thrower), and
    // its lamp stays lit until ResetTarget, e.g. when a timed set of targets runs out of time.
    public class Bullseye : MonoBehaviour
    {
        [SerializeField] Renderer lamp;
        [SerializeField] Material lampOff;
        [SerializeField] Material lampOn;
        [Tooltip("Wobbles back like a hit fairground target.")]
        [SerializeField] Transform face;
        public UnityEvent onHit;

        public static readonly List<Bullseye> All = new List<Bullseye>();

        public bool IsLit { get; private set; }

        public event Action<Bullseye> Hit;

        float wobble;
        Quaternion faceRest;

        void Awake()
        {
            if (face != null) faceRest = face.localRotation;
            ShowLit(false);
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        // Called by a poo that splats on it.
        public void TakeHit(Team thrownBy, Vector3 travelling)
        {
            if (thrownBy != Team.Player || Vector3.Dot(travelling, transform.forward) > 0f) return;
            wobble = 1f;
            ShowLit(true);
            Sfx.Play(Sound.Ding, transform.position);
            Hit?.Invoke(this);
            onHit.Invoke();
        }

        public void ResetTarget() => ShowLit(false);

        void ShowLit(bool lit)
        {
            IsLit = lit;
            if (lamp != null && lampOn != null && lampOff != null) lamp.sharedMaterial = lit ? lampOn : lampOff;
        }

        void Update()
        {
            if (face == null || wobble <= 0f) return;
            wobble = Mathf.MoveTowards(wobble, 0f, Time.deltaTime * 2.5f);
            face.localRotation = faceRest * Quaternion.Euler(Mathf.Sin(wobble * 18f) * wobble * 25f, 0f, 0f);
        }
    }
}
