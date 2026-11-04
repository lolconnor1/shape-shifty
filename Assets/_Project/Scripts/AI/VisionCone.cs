using ShapeShifty.Player;
using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.AI
{
    // Sees the player when they're inside a horizontal cone with a clear line of sight,
    // and fills a detection meter while they stay seen. How fast it fills depends on
    // distance, how fast the player moves, and their current form: animals that belong
    // here barely register, but changing form in plain sight is very noticeable.
    // Also draws the cone on the ground, cut short by anything waist-high or taller, so
    // cover that hides the player visibly blocks the cone too. The cone tints from calm to
    // alarmed as detection rises, and fades out with distance as detection weakens. It
    // disappears while the player is in a form this employee ignores, and comes back as
    // soon as they start changing out of it.
    public class VisionCone : MonoBehaviour
    {
        [Header("Sight")]
        [SerializeField] float range = 18f;
        [SerializeField, Range(1f, 180f)] float viewAngle = 110f;
        [SerializeField] float eyeHeight = 1.6f;
        [Tooltip("Height cover must reach to block the drawn cone. Also the point on the player that has to be visible, if they have no CharacterController (otherwise its centre is used).")]
        [SerializeField] float targetHeight = 1f;
        [Tooltip("Layers that block sight. Ignore Raycast (the invisible level bounds) is left out.")]
        [SerializeField] LayerMask obstacleMask = ~(1 << 2);

        [Header("Detection")]
        [Tooltip("Seconds of continuous sight to be spotted up close at a normal pace.")]
        [SerializeField] float timeToSpot = 1f;
        [Tooltip("Detection speed at the far edge of the cone, relative to up close. Falls off evenly in between, and the cone fades the same way.")]
        [SerializeField, Range(0.05f, 1f)] float edgeStrength = 0.15f;
        [Tooltip("Detection speed multiplier while the player stands still.")]
        [SerializeField] float stillMultiplier = 0.4f;
        [Tooltip("Detection speed multiplier at Fast Speed and above (sprinting).")]
        [SerializeField] float fastMultiplier = 2f;
        [Tooltip("Player speed (m/s) that counts as fully fast. Speeds in between blend the two multipliers.")]
        [SerializeField] float fastSpeed = 8f;
        [Tooltip("Detection speed multiplier while the player is seen changing form. Replaces the movement and disguise multipliers.")]
        [SerializeField] float shiftingMultiplier = 4f;
        [Tooltip("Detection lost per second while the player is out of sight or not suspicious.")]
        [SerializeField] float decayRate = 0.35f;
        public UnityEvent onSpotted;

        [Header("Cone Visual")]
        [SerializeField] MeshFilter coneMesh;
        [SerializeField] int rayCount = 40;
        [Tooltip("Colour right next to the employee; the cone fades out towards its edge.")]
        [SerializeField] Color calmColor = new Color(1f, 0.95f, 0.6f, 0.4f);
        [SerializeField] Color alarmedColor = new Color(1f, 0.15f, 0.1f, 0.6f);
        [Tooltip("How quickly (per second) the cone fades out and back in when the player takes or leaves a form this employee ignores.")]
        [SerializeField] float hideSpeed = 4f;

        // 0 = unaware, 1 = spotted. Stays spotted until it decays all the way back to 0.
        public float Detection { get; private set; }
        public bool IsSpotted { get; private set; }
        public bool CanSeeTarget { get; private set; }
        public Vector3 LastSeenPosition { get; private set; }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        Transform target;
        CharacterController targetController;
        PlayerShapeshifter targetShapeshifter;
        Mesh mesh;
        // Held here as well as in the property block: a property block doesn't count as a use,
        // so loading a scene (e.g. restarting) would otherwise unload the texture.
        Texture2D fadeTexture;
        Vector3[] vertices;
        Vector2[] uvs;
        Renderer coneRenderer;
        MaterialPropertyBlock block;
        float coneVisibility = 1f;

        void Awake()
        {
            mesh = new Mesh { name = "VisionCone" };
            mesh.MarkDynamic();
            vertices = new Vector3[rayCount + 2];
            uvs = new Vector2[rayCount + 2];
            var triangles = new int[rayCount * 3];
            for (int i = 0; i < rayCount; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            coneMesh.sharedMesh = mesh;
            coneRenderer = coneMesh.GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            fadeTexture = BuildFadeTexture();
            block.SetTexture(BaseMapId, fadeTexture);
        }

        void OnDestroy()
        {
            Destroy(mesh);
            Destroy(fadeTexture);
        }

        // Alpha ramp along U (0 = at the employee, 1 = full range). Follows Falloff, squared so
        // the cone visibly fades out towards its edge rather than ending at a faint tint.
        Texture2D BuildFadeTexture()
        {
            const int Size = 64;
            var texture = new Texture2D(Size, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "VisionConeFade" };
            for (int x = 0; x < Size; x++)
            {
                float strength = Falloff(x / (Size - 1f));
                texture.SetPixel(x, 0, new Color(1f, 1f, 1f, strength * strength));
            }
            texture.Apply();
            return texture;
        }

        // How strongly the employee notices things at a fraction of their range: 1 up close, edgeStrength at the edge.
        float Falloff(float distanceFraction) => Mathf.Lerp(1f, edgeStrength, distanceFraction);

        void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return;
            target = player.transform;
            targetController = player.GetComponent<CharacterController>();
            targetShapeshifter = player.GetComponent<PlayerShapeshifter>();
        }

        void Update()
        {
            float distanceFraction = 0f;
            CanSeeTarget = target != null && CheckSight(out distanceFraction);
            float rate = CanSeeTarget ? Falloff(distanceFraction) * Noticeability() / timeToSpot : 0f;
            if (rate > 0f)
            {
                LastSeenPosition = target.position;
                Detection = Mathf.Min(1f, Detection + rate * Time.deltaTime);
                if (Detection >= 1f && !IsSpotted)
                {
                    IsSpotted = true;
                    Debug.Log($"{name} spotted the player!", this);
                    onSpotted.Invoke();
                }
            }
            else
            {
                Detection = Mathf.Max(0f, Detection - decayRate * Time.deltaTime);
                if (Detection <= 0f) IsSpotted = false;
            }
        }

        void LateUpdate() => DrawCone();

        // How much seeing the player right now counts, before distance falloff.
        float Noticeability()
        {
            if (targetShapeshifter == null) return MovementMultiplier();
            if (targetShapeshifter.IsShifting) return shiftingMultiplier;
            return MovementMultiplier() * targetShapeshifter.Current.suspicion;
        }

        bool TargetIgnored() =>
            targetShapeshifter != null && !targetShapeshifter.IsShifting && targetShapeshifter.Current.suspicion <= 0f;

        // Moving targets are easier to notice: slow when still, fast when sprinting.
        float MovementMultiplier()
        {
            if (targetController == null) return 1f;
            Vector3 velocity = targetController.velocity;
            velocity.y = 0f;
            return Mathf.Lerp(stillMultiplier, fastMultiplier, velocity.magnitude / fastSpeed);
        }

        // distanceFraction: 0 right next to the employee, 1 at the edge of the range.
        bool CheckSight(out float distanceFraction)
        {
            distanceFraction = 0f;
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            // The middle of the player's collider, so small forms hide behind low cover.
            Vector3 point = targetController != null ? target.TransformPoint(targetController.center) : target.position + Vector3.up * targetHeight;
            Vector3 toTarget = point - eye;
            Vector3 flat = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            if (flat.magnitude > range) return false;
            if (Vector3.Angle(Flat(transform.forward), flat) > viewAngle * 0.5f) return false;

            float distance = toTarget.magnitude;
            if (Physics.Raycast(eye, toTarget / distance, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(target))
                return false;

            distanceFraction = flat.magnitude / range;
            return true;
        }

        void DrawCone()
        {
            coneVisibility = Mathf.MoveTowards(coneVisibility, TargetIgnored() ? 0f : 1f, hideSpeed * Time.deltaTime);
            coneRenderer.enabled = coneVisibility > 0f;
            if (!coneRenderer.enabled) return;

            Vector3 origin = transform.position + Vector3.up * targetHeight;
            Vector3 forward = Flat(transform.forward);
            for (int i = 0; i <= rayCount; i++)
            {
                float angle = -viewAngle * 0.5f + viewAngle * i / rayCount;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
                float distance = Physics.Raycast(origin, dir, out RaycastHit hit, range, obstacleMask, QueryTriggerInteraction.Ignore)
                    ? hit.distance : range;
                vertices[i + 1] = coneMesh.transform.InverseTransformDirection(dir) * distance;
                uvs[i + 1] = new Vector2(distance / range, 0.5f);
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.RecalculateBounds();

            Color color = Color.Lerp(calmColor, alarmedColor, Detection);
            color.a *= coneVisibility;
            block.SetColor(BaseColorId, color);
            coneRenderer.SetPropertyBlock(block);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.normalized;
        }
    }
}
