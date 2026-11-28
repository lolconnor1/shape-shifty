using TMPro;
using UnityEngine;

namespace ShapeShifty.AI
{
    // Lines a member of staff says out loud, shown over their head: muttering to themselves
    // now and then while the player is close enough to overhear (Idle Lines), and whatever
    // they're told to Say, e.g. on setting off on an errand. Overheard grumbles are how a level
    // hints at what staff will react to, without a sign spelling it out.
    public class Chatter : MonoBehaviour
    {
        [Tooltip("Said one after another, in order, while the player's within Hearing Range.")]
        [SerializeField, TextArea] string[] idleLines;
        [SerializeField] float hearingRange = 14f;
        [Tooltip("Seconds between idle lines (at random in this range).")]
        [SerializeField] Vector2 idleGap = new Vector2(7f, 11f);
        [SerializeField] float lineTime = 3.5f;
        [SerializeField] float height = 2.45f;

        TextMeshPro text;
        Transform player, viewCamera;
        EmployeePatrol patrol;
        float showUntil, nextIdle;
        int nextLine;

        void Awake()
        {
            patrol = GetComponent<EmployeePatrol>();
            var g = new GameObject("Speech");
            g.transform.SetParent(transform, false);
            g.transform.localPosition = Vector3.up * height;
            text = g.AddComponent<TextMeshPro>();
            text.fontSize = 2.2f;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(5f, 1.5f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.color = Color.white;
            text.outlineWidth = 0.25f;
            text.outlineColor = new Color32(20, 20, 25, 255);
            text.text = "";
            nextIdle = Time.time + Random.Range(2f, idleGap.y);
        }

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
            if (Camera.main != null) viewCamera = Camera.main.transform;
        }

        public void Say(string line) => Say(line, lineTime);

        public void Say(string line, float seconds)
        {
            if (string.IsNullOrEmpty(line)) return;
            text.text = line;
            showUntil = Time.time + seconds;
            nextIdle = Mathf.Max(nextIdle, showUntil + idleGap.x);
        }

        void Update()
        {
            bool busy = patrol != null && (patrol.IsChasing || patrol.IsStunned);
            if (busy) showUntil = 0f;
            else if (idleLines != null && idleLines.Length > 0 && Time.time >= nextIdle && player != null
                && (player.position - transform.position).sqrMagnitude < hearingRange * hearingRange)
            {
                Say(idleLines[nextLine]);
                nextLine = (nextLine + 1) % idleLines.Length;
                nextIdle = Time.time + Random.Range(idleGap.x, idleGap.y) + lineTime;
            }
            text.enabled = Time.time < showUntil;
        }

        void LateUpdate()
        {
            if (!text.enabled || viewCamera == null) return;
            text.transform.rotation = Quaternion.LookRotation(text.transform.position - viewCamera.position);
        }
    }
}
