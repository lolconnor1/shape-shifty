using System.Collections;
using ShapeShifty.Audio;
using ShapeShifty.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Levels
{
    // A cart of gold ingots parked on rails, held by a brake (an Interactable). Let off, it
    // rolls down to Rail End and waits there until the CounterweightLift's cage B is at the
    // bottom, then rolls on and adds its weight to it. When B reaches the top, the ingots tip
    // into the weighing pan, which sinks (On Delivered). Set Delivered puts it all straight
    // into that state, e.g. after a restart.
    public class IngotCart : MonoBehaviour
    {
        [SerializeField] Interactable brake;
        [Tooltip("Turned when the brake's let off.")]
        [SerializeField] Transform brakeHandle;
        [SerializeField] Transform railEnd;
        [SerializeField] CounterweightLift lift;
        [Tooltip("Where the cart sits in cage B (a child of it).")]
        [SerializeField] Transform seatInB;
        [SerializeField] float weight = 40f;
        [SerializeField] float rollTime = 3f;
        [SerializeField] Transform ingots;
        [SerializeField] Transform pan;
        [Tooltip("Where the ingots end up, in the pan (a child of it).")]
        [SerializeField] Transform panSeat;
        [SerializeField] Vector3 panDrop = new Vector3(0f, -0.4f, 0f);
        public UnityEvent onReleased = new UnityEvent();
        public UnityEvent onDelivered = new UnityEvent();

        enum State { Parked, Rolling, Waiting, Loaded, Delivered }
        State state;
        Vector3 start;
        float roll;

        void Awake()
        {
            brake.Interacted += Release;
            start = transform.position;
        }

        void OnEnable() { if (lift != null) lift.ReachedTop += OnLiftTop; }
        void OnDisable() { if (lift != null) lift.ReachedTop -= OnLiftTop; }

        public void Release()
        {
            if (state != State.Parked) return;
            state = State.Rolling;
            brake.enabled = false;
            if (brakeHandle != null) brakeHandle.localRotation *= Quaternion.Euler(0f, 0f, 70f);
            Sfx.Play(Sound.Lever, brake.transform.position);
            onReleased.Invoke();
        }

        void Update()
        {
            switch (state)
            {
                case State.Rolling:
                    roll = Mathf.MoveTowards(roll, 1f, Time.deltaTime / rollTime);
                    transform.position = Vector3.Lerp(start, railEnd.position, roll * roll);
                    if (roll >= 1f) state = State.Waiting;
                    break;
                case State.Waiting:
                    if (!lift.BAtBottom) break;
                    transform.SetParent(seatInB, true);
                    StartCoroutine(Slide(transform, transform.localPosition, Vector3.zero, Quaternion.identity, 0.8f));
                    lift.CargoB = weight;
                    Sfx.Play(Sound.HatchShut, transform.position);
                    state = State.Loaded;
                    break;
            }
        }

        void OnLiftTop()
        {
            if (state != State.Loaded) return;
            state = State.Delivered;
            StartCoroutine(Tip());
        }

        IEnumerator Tip()
        {
            yield return new WaitForSeconds(0.4f);
            ingots.SetParent(panSeat, true);
            Vector3 from = ingots.localPosition;
            Quaternion rot = ingots.localRotation;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.9f)
            {
                ingots.localPosition = Vector3.Lerp(from, Vector3.zero, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.2f;
                ingots.localRotation = Quaternion.Slerp(rot, Quaternion.identity, t);
                yield return null;
            }
            ingots.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            lift.CargoB = 0f;
            Sfx.Play(Sound.PlateDown, pan.position);
            Vector3 panRest = pan.localPosition;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
            {
                pan.localPosition = panRest + panDrop * Mathf.SmoothStep(0f, 1f, t);
                yield return null;
            }
            pan.localPosition = panRest + panDrop;
            onDelivered.Invoke();
        }

        public void SetDelivered()
        {
            if (state == State.Delivered) return;
            state = State.Delivered;
            brake.enabled = false;
            transform.SetParent(seatInB, false);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            ingots.SetParent(panSeat, false);
            ingots.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            pan.localPosition += panDrop;
            lift.CargoB = 0f;
        }

        static IEnumerator Slide(Transform t, Vector3 from, Vector3 to, Quaternion rot, float time)
        {
            for (float k = 0f; k < 1f; k += Time.deltaTime / time)
            {
                if (t == null) yield break;
                t.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, k));
                yield return null;
            }
            if (t != null) t.SetLocalPositionAndRotation(to, rot);
        }
    }
}
