using System;
using System.Collections.Generic;
using ShapeShifty.Audio;
using ShapeShifty.Player;
using ShapeShifty.Shapes;
using UnityEngine;

namespace ShapeShifty.Levels
{
    // What the player is meant to be doing in this level, shown in the corner of the screen
    // (see ObjectiveHud) with an arrow towards each. A task is done when Complete is called
    // with its id (hook that up to whatever does it), or by itself when the player has
    // scanned a form, become one, or reached a place. Tasks can wait for another to be done
    // before they show, and a task with parts is done when all its parts are. Finishing a
    // task finishes whatever it was waiting for too, so doing things early or restarting
    // from a checkpoint never leaves an old task behind.
    public class Objectives : MonoBehaviour
    {
        public enum Kind { WhenTold, Scan, Become, Reach }

        [Serializable]
        public class Task
        {
            [Tooltip("Passed to Complete. Also what After and Part Of refer to.")]
            public string id;
            [Tooltip("What's wanted, not how to do it.")]
            public string text;
            public Kind kind;
            [Tooltip("For Scan and Become.")]
            public ShapeDefinition form;
            [Tooltip("What the arrow points to, and for Reach, where to get to.")]
            public Transform target;
            [Tooltip("For Reach: how near the target counts (m, ignoring height).")]
            public float radius = 4f;
            [Tooltip("Shown once this task is done. Empty: shown from the start.")]
            public string after;
            [Tooltip("The task this is one part of. Empty: a task of its own.")]
            public string partOf;

            [NonSerialized] public bool done;
            [NonSerialized] public float doneAt;
            [NonSerialized] public int parts, partsDone;
        }

        [SerializeField] List<Task> tasks = new List<Task>();

        public static Objectives Current { get; private set; }
        public IReadOnlyList<Task> Tasks => tasks;

        Transform player;
        PlayerShapeshifter shapeshifter;

        void Awake()
        {
            Current = this;
            CountParts();
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p == null) return;
            player = p.transform;
            shapeshifter = p.GetComponent<PlayerShapeshifter>();
        }

        // On the list now: not waiting for another task.
        public bool IsShown(Task task)
        {
            if (!string.IsNullOrEmpty(task.partOf))
            {
                var whole = Find(task.partOf);
                return whole != null && !whole.done && IsShown(whole);
            }
            if (string.IsNullOrEmpty(task.after)) return true;
            var before = Find(task.after);
            return before == null || before.done;
        }

        public void Complete(string id)
        {
            var task = Find(id);
            if (task == null) { Debug.LogWarning($"No task called {id}.", this); return; }
            if (Finish(task)) Sfx.Play(Sound.TaskDone);
        }

        void Update()
        {
            if (shapeshifter == null) return;
            foreach (var task in tasks)
            {
                if (task.done || !IsShown(task)) continue;
                bool done = false;
                switch (task.kind)
                {
                    case Kind.Scan: done = UnlockedShapes.Contains(task.form); break;
                    case Kind.Become: done = shapeshifter.Current == task.form && !shapeshifter.IsShifting; break;
                    case Kind.Reach:
                        if (task.target == null) break;
                        Vector3 to = task.target.position - player.position;
                        to.y = 0f;
                        done = to.magnitude < task.radius;
                        break;
                }
                if (done) { Complete(task.id); return; }
            }
        }

        bool Finish(Task task)
        {
            if (task.done) return false;
            task.done = true;
            task.doneAt = Time.unscaledTime;
            if (!string.IsNullOrEmpty(task.after))
            {
                var before = Find(task.after);
                if (before != null) Finish(before);
            }
            foreach (var part in tasks) if (part.partOf == task.id) Finish(part);
            CountParts();
            if (!string.IsNullOrEmpty(task.partOf))
            {
                var whole = Find(task.partOf);
                if (whole != null && whole.partsDone >= whole.parts) Finish(whole);
            }
            return true;
        }

        void CountParts()
        {
            foreach (var task in tasks) task.parts = task.partsDone = 0;
            foreach (var part in tasks)
            {
                if (string.IsNullOrEmpty(part.partOf)) continue;
                var whole = Find(part.partOf);
                if (whole == null) continue;
                whole.parts++;
                if (part.done) whole.partsDone++;
            }
        }

        Task Find(string id)
        {
            foreach (var task in tasks) if (task.id == id) return task;
            return null;
        }
    }
}
