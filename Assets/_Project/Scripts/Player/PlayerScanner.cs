using System;
using ShapeShifty.Shapes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShapeShifty.Player
{
    // Hold Scan while pointing the camera at a nearby animal to learn its form. Picks the
    // unscanned animal closest to the centre of the screen, then tracks it more loosely while
    // the button stays held so a running rat can be followed. The scan starts over if the
    // animal gets away, goes out of view, or the button is let go.
    public class PlayerScanner : MonoBehaviour
    {
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] float range = 5f;
        [Tooltip("How far off the centre of the screen (degrees) an animal can be to start a scan.")]
        [SerializeField] float pickAngle = 10f;
        [Tooltip("How far off centre it can drift once the scan has started.")]
        [SerializeField] float holdAngle = 20f;
        [Tooltip("Where the view to the animal is checked from, e.g. the camera target at head height.")]
        [SerializeField] Transform eye;
        [Tooltip("Layers that block the view. Ignore Raycast (the invisible level bounds) is left out.")]
        [SerializeField] LayerMask obstacleMask = ~(1 << 2);

        // The animal being scanned, or the one that would be if Scan were pressed now.
        public Scannable Target { get; private set; }
        public float Progress { get; private set; }
        public bool IsScanning => scanning != null;
        public event Action<ShapeDefinition> Scanned;

        InputAction scanAction;
        Scannable scanning;
        Transform view;
        Collider ownCollider;
        readonly RaycastHit[] hits = new RaycastHit[8];

        void Awake()
        {
            scanAction = inputActions.FindActionMap("Player", true).FindAction("Scan", true);
            ownCollider = GetComponent<Collider>();
        }

        void Start() => view = Camera.main != null ? Camera.main.transform : null;

        void OnEnable() => scanAction.Enable();

        void OnDisable()
        {
            SetScanning(null);
            Target = null;
            Progress = 0f;
        }

        void Update()
        {
            if (view == null) return;

            bool holding = scanAction.IsPressed();
            if (!(holding && Progress > 0f && Target != null && CanScan(Target, holdAngle, out _)))
            {
                Target = FindTarget();
                Progress = 0f;
            }
            SetScanning(holding ? Target : null);
            if (scanning == null) return;

            Progress += Time.deltaTime / Target.ScanTime;
            if (Progress >= 1f)
            {
                ShapeDefinition shape = Target.Shape;
                SetScanning(null);
                Target = null;
                Progress = 0f;
                UnlockedShapes.Unlock(shape);
                Scanned?.Invoke(shape);
            }
        }

        // Lets the animal know it's being scanned (so it holds still), and when that stops.
        void SetScanning(Scannable scannable)
        {
            if (scanning == scannable) return;
            if (scanning != null) scanning.SetBeingScanned(false);
            scanning = scannable;
            if (scanning != null) scanning.SetBeingScanned(true);
        }

        Scannable FindTarget()
        {
            Scannable best = null;
            float bestAngle = float.MaxValue;
            foreach (var scannable in Scannable.Active)
            {
                if (scannable.Shape == null || UnlockedShapes.Contains(scannable.Shape)) continue;
                if (CanScan(scannable, pickAngle, out float angle) && angle < bestAngle)
                {
                    best = scannable;
                    bestAngle = angle;
                }
            }
            return best;
        }

        bool CanScan(Scannable scannable, float maxAngle, out float angle)
        {
            angle = 0f;
            Vector3 point = scannable.AimPoint;
            if ((point - transform.position).sqrMagnitude > range * range) return false;
            angle = Vector3.Angle(view.forward, point - view.position);
            if (angle > maxAngle) return false;

            Vector3 toPoint = point - eye.position;
            float distance = toPoint.magnitude;
            int count = Physics.RaycastNonAlloc(eye.position, toPoint / distance, hits, distance, obstacleMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = hits[i].collider;
                if (hit != ownCollider && !hit.transform.IsChildOf(scannable.transform)) return false;
            }
            return true;
        }
    }
}
