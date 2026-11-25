using ShapeShifty.Audio;
using UnityEngine;

namespace ShapeShifty.Interaction
{
    // A cover, gate or door that slides between closed and open, e.g. when a lever is pulled.
    // Hook Open and Close up to a lever's events. Grinds while it moves and clanks shut.
    public class Hatch : MonoBehaviour
    {
        [Tooltip("The part that moves. Its position in the scene is the closed position.")]
        [SerializeField] Transform cover;
        [Tooltip("How far it moves (in its parent's space) to open.")]
        [SerializeField] Vector3 openOffset = new Vector3(0f, 0.8f, 0f);
        [SerializeField] float moveTime = 0.6f;
        [SerializeField] bool isOpen;

        public bool IsOpen => isOpen;

        Vector3 closedPosition;
        float openAmount; // 0 = closed, 1 = open

        void Awake()
        {
            closedPosition = cover.localPosition;
            openAmount = isOpen ? 1f : 0f;
            Apply();
        }

        public void Open() => isOpen = true;
        public void Close() => isOpen = false;

        void Update()
        {
            float target = isOpen ? 1f : 0f;
            if (openAmount == target) return;
            if (openAmount == 1f - target) Sfx.Play(Sound.HatchMove, cover.position);
            openAmount = Mathf.MoveTowards(openAmount, target, Time.deltaTime / moveTime);
            Apply();
            if (openAmount == 0f) Sfx.Play(Sound.HatchShut, cover.position);
        }

        void Apply() => cover.localPosition = closedPosition + openOffset * Mathf.SmoothStep(0f, 1f, openAmount);
    }
}
