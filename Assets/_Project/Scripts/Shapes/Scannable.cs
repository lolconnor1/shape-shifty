using System.Collections.Generic;
using UnityEngine;

namespace ShapeShifty.Shapes
{
    // Marks an animal the player can scan to learn its form. Needs no collider: the
    // scanner picks scannables by where the camera is pointing. While a scan is running the
    // animal is frozen (its movement script checks IsBeingScanned) and its model shudders.
    public class Scannable : MonoBehaviour
    {
        [SerializeField] ShapeDefinition shape;
        [Tooltip("Seconds the scan button has to be held.")]
        [SerializeField] float scanTime = 1.5f;
        [Tooltip("The point the scanner aims at and needs a clear view of, relative to this object.")]
        [SerializeField] Vector3 aimOffset = new Vector3(0f, 0.15f, 0f);

        [Header("While Scanned")]
        [Tooltip("Shaken while being scanned.")]
        [SerializeField] Transform model;
        [SerializeField] float shudder = 0.012f;

        public static readonly List<Scannable> Active = new List<Scannable>();

        public ShapeDefinition Shape => shape;
        public float ScanTime => scanTime;
        public Vector3 AimPoint => transform.TransformPoint(aimOffset);
        public bool IsBeingScanned { get; private set; }

        Vector3 modelRestPosition;

        void OnEnable() => Active.Add(this);

        void OnDisable()
        {
            Active.Remove(this);
            SetBeingScanned(false);
        }

        // Called by the player's scanner when a scan of this animal starts and stops.
        public void SetBeingScanned(bool scanned)
        {
            if (scanned == IsBeingScanned) return;
            IsBeingScanned = scanned;
            if (model == null) return;
            if (scanned) modelRestPosition = model.localPosition;
            else model.localPosition = modelRestPosition;
        }

        void LateUpdate()
        {
            if (IsBeingScanned && model != null)
                model.localPosition = modelRestPosition + Random.insideUnitSphere * shudder;
        }
    }
}
