using ShapeShifty.Combat;
using ShapeShifty.UI;
using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Levels
{
    // A row of Bullseyes that all have to be hit within a time limit. The first hit starts the
    // clock; if it runs out first, they all go dark again. Once all are hit it's done for good.
    public class ShootingGallery : MonoBehaviour
    {
        [SerializeField] Bullseye[] targets;
        [Tooltip("Seconds from the first hit to hit the rest.")]
        [SerializeField] float timeLimit = 12f;
        public UnityEvent onCompleted;

        public bool IsCompleted { get; private set; }

        float deadline = -1f;
        ShapeHud hud;

        void Awake() => hud = FindFirstObjectByType<ShapeHud>();

        void OnEnable() { foreach (var t in targets) t.Hit += OnHit; }
        void OnDisable() { foreach (var t in targets) t.Hit -= OnHit; }

        void OnHit(Bullseye target)
        {
            if (IsCompleted) return;
            int lit = 0;
            foreach (var t in targets) if (t.IsLit) lit++;
            if (lit == targets.Length) { Complete(); return; }
            if (deadline < 0f) deadline = Time.time + timeLimit;
            if (hud != null) hud.ShowMessage($"{lit} of {targets.Length}! {Mathf.CeilToInt(deadline - Time.time)} seconds to hit the rest");
        }

        // Also used to put it back when a restart remembers it was done.
        public void Complete()
        {
            if (IsCompleted) return;
            IsCompleted = true;
            deadline = -1f;
            foreach (var t in targets) if (!t.IsLit) t.TakeHit(Team.Player, -t.transform.forward);
            onCompleted.Invoke();
        }

        void Update()
        {
            if (IsCompleted || deadline < 0f || Time.time < deadline) return;
            deadline = -1f;
            foreach (var t in targets) t.ResetTarget();
            if (hud != null) hud.ShowMessage("Too slow! The targets reset");
        }
    }
}
