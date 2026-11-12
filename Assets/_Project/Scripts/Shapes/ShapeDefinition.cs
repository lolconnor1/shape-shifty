using UnityEngine;

namespace ShapeShifty.Shapes
{
    // One form the player can take: how it looks, how big its collider is, how it moves,
    // where the camera sits, and how suspicious employees find it. To add an animal, make
    // one of these (Create > Shape-Shifty > Shape) and put a Scannable on the animal.
    [CreateAssetMenu(fileName = "Shape", menuName = "Shape-Shifty/Shape")]
    public class ShapeDefinition : ScriptableObject
    {
        public string displayName = "Shape";
        [Tooltip("Shown in the shape bar. The name is shown instead while this is empty.")]
        public Sprite icon;
        [Tooltip("Placed under the player's Visual while in this form. Should face +Z with its feet at 0.")]
        public GameObject model;
        [Tooltip("Where the scan beam comes out, relative to the model (+Z is forward).")]
        public Vector3 scanOrigin = new Vector3(0f, 1.2f, 0.4f);

        [Header("Collider")]
        public float height = 1.8f;
        public float radius = 0.4f;
        public float stepOffset = 0.35f;
        public float skinWidth = 0.08f;

        [Header("Movement")]
        public float moveSpeed = 5f;
        public float sprintSpeed = 8f;
        public float jumpHeight = 1.2f;

        [Header("Camera")]
        [Tooltip("Height above the feet of the point the camera orbits.")]
        public float cameraHeight = 1.5f;
        public float cameraDistance = 3.5f;
        public Vector3 shoulderOffset = new Vector3(1f, 0.15f, 0f);
        public float verticalArmLength = 0.3f;

        [Header("Disguise")]
        [Tooltip("How quickly employees get suspicious of this form compared with a person. 0 = ignored completely. Ignored completely anyway inside a BlendInZone for this form.")]
        [Range(0f, 1f)] public float suspicion = 1f;

        [Header("Swimming")]
        [Tooltip("Swims in deep water (see DeepWater) instead of sinking to the bottom. Crouch dives, Jump swims up, and Jump at the surface leaps out.")]
        public bool canSwim;
        public float swimSpeed = 6f;
        [Tooltip("How high Jump throws a swimmer out of the water.")]
        public float leapHeight = 1.4f;

        [Header("Breath")]
        [Tooltip("Seconds this form can stay under deep water before drowning. Swimmers never drown.")]
        public float breathTime = 4f;

        [Header("Belly Slide")]
        [Tooltip("Crouch flops onto the belly and slides for as long as it's held: quick and hard to steer, and faster still on ice (see IcySurface) and downhill.")]
        public bool canBellySlide;
        [Tooltip("Speed a slide starts at and can be paddled up to. Ice and slopes take it higher.")]
        public float slideSpeed = 7f;
        [Tooltip("Collider height while sliding. No less than twice the radius.")]
        public float slideHeight = 0.45f;
    }
}
