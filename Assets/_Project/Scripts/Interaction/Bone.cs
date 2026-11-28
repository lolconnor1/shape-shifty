using System;
using System.Collections.Generic;
using ShapeShifty.AI;
using ShapeShifty.Audio;
using ShapeShifty.Player;
using UnityEngine;

namespace ShapeShifty.Interaction
{
    // A bone. Lies about until the player picks it up (Interact), and once thrown it tumbles,
    // lands, and every Wolf in range comes running (Landed), then and for as long as it lies
    // there (IsLure). The first wolf there claims it,
    // gnaws it, carries it home and finishes it off. A wolf that catches a player carrying
    // one takes the bone instead of biting.
    [RequireComponent(typeof(Interactable), typeof(Rigidbody))]
    public class Bone : MonoBehaviour
    {
        [Tooltip("Seconds after a throw before it counts as landed even if it's still rolling about.")]
        [SerializeField] float settleTime = 1.5f;
        [Tooltip("How it sits in a mouth, relative to the holder.")]
        [SerializeField] Vector3 heldRotation = new Vector3(0f, 90f, 0f);

        public static readonly List<Bone> All = new List<Bone>();
        // A thrown bone has come to rest.
        public static event Action<Bone> Landed;

        // Who's holding it: the player's carry point or a wolf's mouth. Null when it's loose.
        public Transform Holder { get; private set; }
        // The wolf that got to it first, while it's being gnawed on the ground.
        public Wolf ClaimedBy { get; set; }
        public bool IsHeld => Holder != null;
        public bool InFlight { get; private set; }
        // Thrown, landed, and nobody's had it yet.
        public bool IsLure => thrown && !InFlight && !IsHeld && ClaimedBy == null;

        Interactable interactable;
        Rigidbody rb;
        Collider[] colliders;
        float flightTime, touchedAt;
        bool thrown, touching;

        void Awake()
        {
            interactable = GetComponent<Interactable>();
            rb = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();
            interactable.Interacted += OnInteract;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void OnInteract()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null && player.TryGetComponent(out PlayerCarry carry)) carry.Take(this);
        }

        public void PickUp(Transform holder)
        {
            Holder = holder;
            ClaimedBy = null;
            InFlight = thrown = false;
            rb.isKinematic = true;
            // Interpolation would keep pulling it back to where the physics last had it,
            // so it'd trail behind whoever's carrying it.
            rb.interpolation = RigidbodyInterpolation.None;
            SetColliders(false);
            interactable.enabled = false;
            transform.SetParent(holder, false);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(heldRotation));
        }

        public void Throw(Vector3 velocity)
        {
            Release();
            rb.linearVelocity = velocity;
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 8f;
            InFlight = thrown = true;
            touching = false;
            flightTime = 0f;
            touchedAt = -1f;
        }

        // Dropped where it is, e.g. by a wolf that's lost interest.
        public void Drop()
        {
            Release();
            ClaimedBy = null;
            interactable.enabled = true;
        }

        void Release()
        {
            transform.SetParent(null);
            Holder = null;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            SetColliders(true);
        }

        // Chewed up: gone for good.
        public void Consume() => Destroy(gameObject);

        void Update()
        {
            if (!InFlight) return;
            flightTime += Time.deltaTime;
            // A clatter on the ground is enough: it needn't have stopped rolling.
            if (touchedAt < 0f && flightTime > 0.2f && touching) touchedAt = flightTime;
            touching = false;
            if (flightTime > 0.3f && ((touchedAt >= 0f && flightTime > touchedAt + 0.25f) || rb.IsSleeping() || rb.linearVelocity.sqrMagnitude < 0.04f || flightTime > settleTime)) Land();
        }

        void OnCollisionStay(Collision collision) => touching = true;

        void Land()
        {
            InFlight = false;
            interactable.enabled = true;
            Sfx.Play(Sound.Land, transform.position, 0.6f, 1.6f);
            Landed?.Invoke(this);
        }

        void SetColliders(bool on)
        {
            foreach (var collider in colliders) collider.enabled = on;
        }
    }
}
