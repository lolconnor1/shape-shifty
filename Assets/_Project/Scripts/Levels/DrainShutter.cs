using UnityEngine;

namespace ShapeShifty.Levels
{
    // One of the shutters covering the pool drain. Open() slides it away under the pool floor
    // and tells the drain; hook it up to whatever solves its puzzle, like a lever's
    // onSwitchedOn. Once open it stays open.
    public class DrainShutter : MonoBehaviour
    {
        [SerializeField] PoolDrain drain;
        [Tooltip("How far it slides, in its own space, to get out of the way.")]
        [SerializeField] Vector3 openOffset = new Vector3(0f, -0.08f, 1.4f);
        [SerializeField] float openTime = 1.5f;

        public bool IsOpen { get; private set; }

        Vector3 closedPosition;
        float slide; // 0 = closed, 1 = open

        void Awake() => closedPosition = transform.localPosition;

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            drain.ShutterOpened(this);
        }

        void Update()
        {
            if (!IsOpen || slide >= 1f) return;
            slide = Mathf.MoveTowards(slide, 1f, Time.deltaTime / openTime);
            float t = slide * slide * (3f - 2f * slide);
            transform.localPosition = closedPosition + transform.localRotation * openOffset * t;
        }
    }
}
