using ShapeShifty.AI;
using ShapeShifty.Interaction;
using UnityEngine;

namespace ShapeShifty.Levels
{
    // A door (Hatch) that opens for staff walking up to it and shuts behind them. The player
    // can't open it: they need another way in.
    [RequireComponent(typeof(Hatch))]
    public class StaffDoor : MonoBehaviour
    {
        [SerializeField] float range = 2.6f;

        Hatch hatch;

        void Awake() => hatch = GetComponent<Hatch>();

        void Update()
        {
            bool near = false;
            foreach (var e in EmployeePatrol.All)
            {
                Vector3 d = e.transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude < range * range) { near = true; break; }
            }
            if (near && !hatch.IsOpen) hatch.Open();
            else if (!near && hatch.IsOpen) hatch.Close();
        }
    }
}
