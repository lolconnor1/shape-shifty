using System.Collections.Generic;
using ShapeShifty.Levels;
using UnityEngine;

namespace ShapeShifty.UI
{
    // The level's tasks (see Objectives) in the top right corner. Each has a box that's ticked
    // when it's done and an arrow that turns to point the way there, like a compass; parts of
    // a task are listed under it. A finished task stays ticked for a few seconds and goes.
    public class ObjectiveHud : MonoBehaviour
    {
        [SerializeField] CanvasGroup panel;
        [Tooltip("Copied once per task. Kept inactive.")]
        [SerializeField] ObjectiveRow rowTemplate;
        [Tooltip("Seconds a finished task stays on the list.")]
        [SerializeField] float lingerTime = 3.5f;

        readonly Dictionary<Objectives.Task, ObjectiveRow> rows = new Dictionary<Objectives.Task, ObjectiveRow>();
        Transform player, view;

        void Awake()
        {
            rowTemplate.gameObject.SetActive(false);
            panel.alpha = 0f;
        }

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
            view = Camera.main != null ? Camera.main.transform : null;
        }

        void LateUpdate()
        {
            var objectives = Objectives.Current;
            bool any = false;
            if (objectives != null)
                foreach (var task in objectives.Tasks) any |= Show(objectives, task);
            panel.alpha = Mathf.MoveTowards(panel.alpha, any ? 1f : 0f, Time.unscaledDeltaTime * 3f);
        }

        bool Show(Objectives objectives, Objectives.Task task)
        {
            // Parts stay, ticked, for as long as the rest of the task does.
            bool isPart = !string.IsNullOrEmpty(task.partOf);
            bool shown = objectives.IsShown(task) && (isPart || !task.done || Time.unscaledTime - task.doneAt < lingerTime);
            rows.TryGetValue(task, out var row);
            if (!shown)
            {
                if (row != null && row.gameObject.activeSelf) row.gameObject.SetActive(false);
                return false;
            }
            if (row == null)
            {
                row = Instantiate(rowTemplate, rowTemplate.transform.parent);
                rows[task] = row;
                row.gameObject.SetActive(true);
                row.Arrive();
                PlaceInOrder(objectives);
            }
            string text = task.parts > 0 ? $"{task.text}  {task.partsDone}/{task.parts}" : task.text;
            row.Show(text, task.done, isPart);

            if (task.done || task.target == null || player == null || view == null)
            {
                row.HideArrow();
                return true;
            }
            Vector3 to = task.target.position - player.position;
            Vector3 forward = view.forward;
            to.y = forward.y = 0f;
            if (to.magnitude > 2.5f) row.PointArrow(Vector3.SignedAngle(forward, to, Vector3.up));
            else row.HideArrow(); // right there
            return true;
        }

        // As they're listed on the level, after the template.
        void PlaceInOrder(Objectives objectives)
        {
            int index = rowTemplate.transform.GetSiblingIndex() + 1;
            foreach (var task in objectives.Tasks)
                if (rows.TryGetValue(task, out var row)) row.transform.SetSiblingIndex(index++);
        }
    }
}
