using UnityEngine;

namespace ShapeShifty.Player
{
    // How a scan looks while it runs: a flickering bolt of electricity from the player to
    // the animal, sparks where it hits, and the player's model turned to face the animal.
    public class ScanBeam : MonoBehaviour
    {
        [SerializeField] PlayerScanner scanner;
        [SerializeField] PlayerShapeshifter shapeshifter;
        [Tooltip("The player's model root, turned to face the animal while scanning.")]
        [SerializeField] Transform visual;
        [SerializeField] float turnSpeed = 720f;

        [Header("Bolt")]
        [Tooltip("Thin, bright centre of the bolt.")]
        [SerializeField] LineRenderer core;
        [Tooltip("Wider, dimmer glow around the centre. Its width flickers.")]
        [SerializeField] LineRenderer glow;
        [SerializeField] int segments = 14;
        [Tooltip("How far (m) the bolt zigzags off a straight line, at its middle.")]
        [SerializeField] float jaggedness = 0.12f;
        [Tooltip("Seconds between new zigzag shapes.")]
        [SerializeField] float flickerInterval = 0.05f;
        [Tooltip("Plays at the animal while the scan runs.")]
        [SerializeField] ParticleSystem sparks;

        Vector3[] points;
        Vector2[] offsets;
        float nextFlicker;
        float glowWidth;
        bool shown = true;

        void Awake()
        {
            points = new Vector3[segments + 1];
            offsets = new Vector2[segments + 1];
            core.positionCount = glow.positionCount = segments + 1;
            glowWidth = glow.widthMultiplier;
            Show(false);
        }

        // After PlayerController has turned the model to face the way it's moving, so
        // facing the animal wins while scanning.
        void LateUpdate()
        {
            bool scanning = scanner.IsScanning && scanner.Target != null;
            Show(scanning);
            if (!scanning) return;

            Vector3 target = scanner.Target.AimPoint;
            Vector3 facing = target - visual.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
                visual.rotation = Quaternion.RotateTowards(visual.rotation, Quaternion.LookRotation(facing), turnSpeed * Time.deltaTime);

            if (Time.time >= nextFlicker)
            {
                for (int i = 0; i < offsets.Length; i++) offsets[i] = Random.insideUnitCircle;
                glow.widthMultiplier = glowWidth * Random.Range(0.6f, 1.4f);
                nextFlicker = Time.time + flickerInterval;
            }

            Vector3 from = visual.TransformPoint(shapeshifter.Current.scanOrigin);
            Vector3 span = target - from;
            if (span.sqrMagnitude < 0.0001f) return;
            Quaternion frame = Quaternion.LookRotation(span);
            Vector3 side = frame * Vector3.right;
            Vector3 up = frame * Vector3.up;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float amount = Mathf.Sin(t * Mathf.PI) * jaggedness; // pinned at both ends
                points[i] = from + span * t + (side * offsets[i].x + up * offsets[i].y) * amount;
            }
            core.SetPositions(points);
            glow.SetPositions(points);
            sparks.transform.position = target;
        }

        void Show(bool on)
        {
            if (shown == on) return;
            shown = on;
            core.enabled = glow.enabled = on;
            if (on) sparks.Play();
            else sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
