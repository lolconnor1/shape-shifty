using System.Collections;
using UnityEngine;

namespace ShapeShifty.Interaction
{
    // The level of a body of deep water, so it can be drained and refilled. Moves the water's
    // volume (a cube or cylinder primitive under an unscaled parent), shrinking it from the top
    // down so its bottom stays on the floor, and the separate surface mesh if there is one.
    public class WaterLevel : MonoBehaviour
    {
        [Tooltip("The water's volume. Defaults to this object.")]
        [SerializeField] Transform volume;
        [Tooltip("Height of the volume's mesh at scale 1: 1 for a cube, 2 for a cylinder.")]
        [SerializeField] float meshHeight = 1f;
        [Tooltip("A separate surface mesh, moved up and down with the level. Optional.")]
        [SerializeField] Transform surface;

        public float Level { get; private set; }
        public float FloorY { get; private set; }

        void Awake()
        {
            if (volume == null) volume = transform;
            float half = volume.localScale.y * meshHeight / 2f;
            Level = volume.localPosition.y + half;
            FloorY = volume.localPosition.y - half;
        }

        public void SetLevel(float y)
        {
            y = Mathf.Max(y, FloorY + 0.01f);
            if (surface != null) surface.position += Vector3.up * (y - Level);
            Level = y;
            Vector3 position = volume.localPosition, scale = volume.localScale;
            position.y = (y + FloorY) / 2f;
            scale.y = (y - FloorY) / meshHeight;
            volume.localPosition = position;
            volume.localScale = scale;
        }

        public Coroutine MoveTo(float y, float seconds) => StartCoroutine(Move(y, seconds));

        IEnumerator Move(float y, float seconds)
        {
            float from = Level;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                SetLevel(Mathf.Lerp(from, y, t / seconds));
                yield return null;
            }
            SetLevel(y);
        }

        // Gone for good, e.g. once the pool has drained.
        public void Hide()
        {
            if (surface != null) surface.gameObject.SetActive(false);
            volume.gameObject.SetActive(false);
        }
    }
}
