using ShapeShifty.Combat;
using ShapeShifty.Interaction;
using ShapeShifty.UI;
using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Levels
{
    // A long straight corridor of shutters that only the quickest form can get down. Hitting
    // the starter Bullseye opens the far door; a moment later the shutters start slamming
    // down one after another from the near end, faster than anything on two legs can run, and
    // the far door shuts when they get there. Getting through the far door first solves it,
    // and it stays open after that. A shutter never comes down on the player: it waits until
    // they've moved out from under it. After each run the shutters all go back up.
    // Armed (by the escape), the far door shuts again and a run starts by itself as soon as
    // the player runs in past the first shutter. Without a starter there's no test at all, only
    // the escape.
    public class ShutterRun : MonoBehaviour
    {
        [SerializeField] Bullseye starter;
        [Tooltip("In order from the near end. Open while the corridor is idle.")]
        [SerializeField] Hatch[] shutters;
        [SerializeField] Hatch farDoor;
        [Tooltip("Seconds from the hit until the first shutter drops.")]
        [SerializeField] float delay = 2f;
        [Tooltip("How fast the shutters drop along the corridor (m/s). A sprinting wolf is 12.")]
        [SerializeField] float waveSpeed = 11.3f;
        [Tooltip("Seconds after a run before the shutters go back up.")]
        [SerializeField] float resetDelay = 2.5f;
        [Tooltip("Half the corridor's width, to tell whether the player is in it.")]
        [SerializeField] float halfWidth = 2.5f;
        [Tooltip("When armed: seconds from running in past the first shutter until it drops.")]
        [SerializeField] float armedDelay = 0.5f;
        public UnityEvent onSucceeded;

        enum State { Idle, Running, Resetting }

        State state;
        bool solved, armed;
        float clock, startDelay, resetAt;
        int nextShutter;
        Vector3 origin, along;
        float length;
        float[] distances;
        Transform player;
        ShapeHud hud;

        void Awake()
        {
            origin = shutters[0].transform.position;
            Vector3 toEnd = farDoor.transform.position - origin;
            toEnd.y = 0f;
            length = toEnd.magnitude;
            along = toEnd / length;
            distances = new float[shutters.Length];
            for (int i = 0; i < shutters.Length; i++) distances[i] = Along(shutters[i].transform.position);
            hud = FindFirstObjectByType<ShapeHud>();
        }

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
            // No starter: there's no test, only the escape, so the far door stands open till then.
            if (starter == null) SetSolved();
        }

        void OnEnable() { if (starter != null) starter.Hit += OnStarterHit; }
        void OnDisable() { if (starter != null) starter.Hit -= OnStarterHit; }

        float Along(Vector3 point) => Vector3.Dot(point - origin, along);
        float Across(Vector3 point) { Vector3 d = point - origin; d.y = 0f; return Mathf.Abs(Vector3.Dot(d, Vector3.Cross(Vector3.up, along))); }

        // Solved before a restart: the far door is open from the start.
        public void SetSolved()
        {
            solved = true;
            if (state == State.Idle) farDoor.Open();
        }

        // The escape: the far door shuts and the next run starts as the player runs in.
        public void Arm()
        {
            armed = true;
            solved = false;
            if (state == State.Idle) farDoor.Close();
        }

        void OnStarterHit(Bullseye target)
        {
            if (state != State.Idle || solved || armed) { if (state == State.Idle) target.ResetTarget(); return; }
            Begin(delay);
            if (hud != null) hud.ShowMessage("The shutter test has started. RUN!");
        }

        void Begin(float wait)
        {
            state = State.Running;
            clock = 0f;
            startDelay = wait;
            nextShutter = 0;
            farDoor.Open();
        }

        void Update()
        {
            if (player == null) return;
            float s = Along(player.position);
            bool inCorridor = Across(player.position) < halfWidth;

            switch (state)
            {
                case State.Idle:
                    if (armed && !solved && inCorridor && s > 0.2f && s < 4f) Begin(armedDelay);
                    break;

                case State.Running:
                    clock += Time.deltaTime;
                    if (s > length + 0.6f && Across(player.position) < halfWidth + 3f) { Succeed(); break; }
                    float wave = (clock - startDelay) * waveSpeed;
                    while (nextShutter < shutters.Length && wave >= distances[nextShutter])
                    {
                        if (inCorridor && Mathf.Abs(s - distances[nextShutter]) < 0.9f) break; // not on the player
                        shutters[nextShutter++].Close();
                    }
                    if (wave >= length && !(inCorridor && Mathf.Abs(s - length) < 0.9f))
                    {
                        farDoor.Close();
                        if (hud != null) hud.ShowMessage("Too slow! The shutters beat you");
                        state = State.Resetting;
                        resetAt = Time.time + resetDelay;
                    }
                    break;

                case State.Resetting:
                    if (Time.time < resetAt) break;
                    foreach (var shutter in shutters) shutter.Open();
                    if (starter != null) starter.ResetTarget();
                    if (solved) farDoor.Open();
                    state = State.Idle;
                    break;
            }
        }

        void Succeed()
        {
            solved = true;
            state = State.Resetting;
            resetAt = Time.time + resetDelay;
            if (hud != null && !armed) hud.ShowMessage("Made it! Nothing's faster than a wolf");
            onSucceeded.Invoke();
        }
    }
}
