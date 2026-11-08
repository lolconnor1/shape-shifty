using UnityEngine;

namespace ShapeShifty.Interaction
{
    // A flat conveyor belt that carries the player along while they stand on it. Its texture
    // scrolls so you can see which way it runs. Hook SwitchOn and SwitchOff up to a lever to
    // start and stop it.
    public class Conveyor : MonoBehaviour
    {
        [Tooltip("The belt: a box whose top is the surface. Its forward (blue arrow) is the way it runs.")]
        [SerializeField] Transform belt;
        [SerializeField] float speed = 1.5f;
        [SerializeField] bool running = true;
        [Tooltip("Scrolled along with the belt. Leave empty for a belt that doesn't show it moving.")]
        [SerializeField] Renderer beltRenderer;
        [Tooltip("How many times the texture repeats per metre of belt.")]
        [SerializeField] float texturePerMetre = 1f;

        static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        public bool IsRunning => running;

        CharacterController player;
        MaterialPropertyBlock block;
        float scroll;

        void Start()
        {
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null) player = playerObject.GetComponent<CharacterController>();
            block = new MaterialPropertyBlock();
            Scroll(0f);
        }

        public void SwitchOn() => running = true;
        public void SwitchOff() => running = false;

        void Update()
        {
            if (!running) return;
            Scroll(speed * Time.deltaTime);
            // A little downwards too, so the player stays grounded and can still jump.
            if (player != null && player.enabled && IsOnBelt(player.transform.position))
                player.Move((belt.forward * speed + Vector3.down) * Time.deltaTime);
        }

        // A little past the ends counts too, so someone standing still gets carried right off
        // rather than left balanced on the edge.
        bool IsOnBelt(Vector3 feet)
        {
            Vector3 local = belt.InverseTransformPoint(feet);
            float above = feet.y - belt.TransformPoint(0f, 0.5f, 0f).y;
            float overhang = (player.radius + 0.1f) / belt.lossyScale.z;
            return Mathf.Abs(local.x) <= 0.5f && Mathf.Abs(local.z) <= 0.5f + overhang && above > -player.radius && above < 0.2f;
        }

        void Scroll(float metres)
        {
            if (beltRenderer == null) return;
            float length = belt.lossyScale.z;
            // A cube's top face runs its texture from its front edge (+z) to its back, so a bigger
            // offset moves the pattern forward.
            scroll = Mathf.Repeat(scroll + metres * texturePerMetre, 1f);
            beltRenderer.GetPropertyBlock(block);
            block.SetVector(BaseMapStId, new Vector4(1f, length * texturePerMetre, 0f, scroll));
            beltRenderer.SetPropertyBlock(block);
        }
    }
}
