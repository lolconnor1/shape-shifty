using ShapeShifty.AI;
using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Levels
{
    // A job a member of staff gets up and goes to do when something happens, like wiping a
    // camera that's been splatted. Send() has them walk the Path from their post (the first
    // point should be next to it) to the last point, face that point's forward (blue arrow)
    // and work there for Work Time, then walk the path back and carry on as before. Sent again
    // while they're still on it, it's ignored. Other errands queue up behind it.
    public class Errand : MonoBehaviour
    {
        [SerializeField] EmployeePatrol who;
        [Tooltip("From their post to where the job is. Straight lines between points: keep them clear of walls.")]
        [SerializeField] Transform[] path;
        [SerializeField] float speed = 2f;
        [SerializeField] float workTime = 6f;
        [Tooltip("Said when they set off. Optional.")]
        [SerializeField] string setOffLine;
        [Tooltip("Said on arriving. Optional.")]
        [SerializeField] string workLine;
        public UnityEvent onArrived = new UnityEvent();
        [Tooltip("The job's done: they're heading back.")]
        public UnityEvent onDone = new UnityEvent();

        public bool Pending { get; private set; }

        public void Send()
        {
            if (Pending || who == null || !who.isActiveAndEnabled) return;
            Pending = true;
            var chatter = who.GetComponent<Chatter>();
            if (chatter != null) chatter.Say(setOffLine);
            var points = new Vector3[path.Length];
            for (int i = 0; i < path.Length; i++) points[i] = path[i].position;
            who.RunErrand(new EmployeePatrol.ErrandJob
            {
                path = points,
                facing = path[path.Length - 1].forward,
                speed = speed,
                workTime = workTime,
                arrived = () =>
                {
                    if (chatter != null) chatter.Say(workLine, Mathf.Min(workTime, 4f));
                    onArrived.Invoke();
                },
                done = () =>
                {
                    Pending = false;
                    onDone.Invoke();
                },
            });
        }

        void OnDrawGizmos()
        {
            if (path == null) return;
            Gizmos.color = new Color(1f, 0.6f, 0.2f);
            for (int i = 0; i + 1 < path.Length; i++)
                if (path[i] != null && path[i + 1] != null) Gizmos.DrawLine(path[i].position + Vector3.up * 0.2f, path[i + 1].position + Vector3.up * 0.2f);
        }
    }
}
