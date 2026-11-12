using System.Collections;
using ShapeShifty.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Levels
{
    // The drain in the bottom of the penguin pool: the way out of Level 2. It's covered by
    // shutters (see DrainShutter) that each open when one of the level's puzzles is solved,
    // lighting a lamp for each. Once every shutter is open the water drains away, the exit
    // in the pit under the drain is switched on, and the player can drop down it.
    public class PoolDrain : MonoBehaviour
    {
        [SerializeField] DrainShutter[] shutters;
        [Tooltip("One per shutter, lit as it opens. Several sets can be listed one after another, e.g. lamps on the drain and a panel by the door.")]
        [SerializeField] Lamp[] lamps;
        [Tooltip("Played each time a shutter opens.")]
        [SerializeField] ParticleSystem bubbles;

        [Header("Draining")]
        [SerializeField] WaterLevel water;
        [SerializeField] float drainTime = 10f;
        [Tooltip("Switched on once the water has gone, e.g. the level exit in the pit.")]
        [SerializeField] GameObject exit;
        public UnityEvent onDrained;

        public int OpenCount { get; private set; }
        public bool IsDrained { get; private set; }

        void Awake()
        {
            if (exit != null) exit.SetActive(false);
        }

        // Called by a shutter as it starts to open.
        public void ShutterOpened(DrainShutter shutter)
        {
            int index = System.Array.IndexOf(shutters, shutter);
            if (index >= 0)
                for (int i = index; i < lamps.Length; i += shutters.Length)
                    if (lamps[i] != null) lamps[i].On();
            if (bubbles != null) bubbles.Play();
            OpenCount++;
            if (OpenCount >= shutters.Length) StartCoroutine(Drain());
        }

        IEnumerator Drain()
        {
            Debug.Log("Every shutter is open: the pool is draining.", this);
            yield return water.MoveTo(water.FloorY, drainTime);
            water.Hide();
            IsDrained = true;
            if (exit != null) exit.SetActive(true);
            onDrained.Invoke();
        }
    }
}
