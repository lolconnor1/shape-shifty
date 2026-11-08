using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Interaction
{
    // A basket hanging from an overhead rail, riding a MovingPlatform, with a load inside.
    // Dump() stops it where it is and swings its floor open, dropping the load (and anyone
    // riding in it). Whatever lands on a pressure plate stays there; anything that misses
    // shrinks away after a few seconds. Then the floor swings shut and, if Refill is on, a
    // fresh load appears and the basket carries on. Once a load has landed on a plate, the job's
    // done: the basket carries on empty and doesn't refill again.
    public class HangingBasket : MonoBehaviour
    {
        [SerializeField] MovingPlatform platform;
        [Tooltip("The hinges the floor halves hang from. Each swings round its own forward (blue arrow) to open.")]
        [SerializeField] Transform[] floorHinges;
        [SerializeField] float openAngle = 100f;
        [SerializeField] float swingTime = 0.35f;
        [Tooltip("Parent of the load: kinematic Rigidbodies resting on the floor, not interpolated (so they keep up with the basket). Copied for each refill.")]
        [SerializeField] Transform load;
        [Tooltip("Where things go once they've dropped out, so they stop moving with the basket.")]
        [SerializeField] Transform droppedParent;
        [Tooltip("How long (s) dropped things that missed a pressure plate stay before shrinking away.")]
        [SerializeField] float clearDelay = 4f;
        [Tooltip("Refill and carry on after dumping. Off: the basket stays where it stopped, empty.")]
        [SerializeField] bool refill = true;
        [Tooltip("How long (s) after dumping before the floor shuts again (then it refills and carries on).")]
        [SerializeField] float refillDelay = 3f;
        public UnityEvent onDumped;

        public bool IsDumping { get; private set; }

        Quaternion[] hingeClosed;
        GameObject loadTemplate;

        void Awake()
        {
            hingeClosed = new Quaternion[floorHinges.Length];
            for (int i = 0; i < floorHinges.Length; i++) hingeClosed[i] = floorHinges[i].localRotation;
            if (load != null)
            {
                loadTemplate = Instantiate(load.gameObject, load.parent);
                loadTemplate.name = load.name;
                loadTemplate.transform.SetLocalPositionAndRotation(load.localPosition, load.localRotation);
                loadTemplate.SetActive(false);
            }
        }

        public void Dump()
        {
            if (!IsDumping) StartCoroutine(Dumping());
        }

        IEnumerator Dumping()
        {
            IsDumping = true;
            platform.Stop();
            var dropped = new List<Rigidbody>();
            if (load != null)
            {
                foreach (var body in load.GetComponentsInChildren<Rigidbody>())
                {
                    body.transform.SetParent(droppedParent, true);
                    body.isKinematic = false;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    dropped.Add(body);
                }
                Destroy(load.gameObject);
                load = null;
            }
            yield return Swing(0f, 1f);
            onDumped.Invoke();
            StartCoroutine(ClearMissed(dropped));

            yield return new WaitForSeconds(refillDelay);
            yield return Swing(1f, 0f);
            bool landed = dropped.Exists(body => body != null && PressurePlate.IsOnAny(body));
            if (landed) refill = false;
            if (refill)
            {
                load = Instantiate(loadTemplate, loadTemplate.transform.parent).transform;
                load.name = loadTemplate.name;
                load.SetLocalPositionAndRotation(loadTemplate.transform.localPosition, loadTemplate.transform.localRotation);
                load.gameObject.SetActive(true);
            }
            if (refill || landed) platform.Resume();
            IsDumping = false;
        }

        IEnumerator Swing(float from, float to)
        {
            for (float t = 0f; t < swingTime; t += Time.deltaTime)
            {
                SetOpen(Mathf.Lerp(from, to, t / swingTime));
                yield return null;
            }
            SetOpen(to);
        }

        void SetOpen(float amount)
        {
            float angle = -openAngle * Mathf.SmoothStep(0f, 1f, amount);
            for (int i = 0; i < floorHinges.Length; i++)
                floorHinges[i].localRotation = hingeClosed[i] * Quaternion.Euler(0f, 0f, angle);
        }

        IEnumerator ClearMissed(List<Rigidbody> dropped)
        {
            yield return new WaitForSeconds(clearDelay);
            var missed = dropped.FindAll(body => body != null && !PressurePlate.IsOnAny(body));
            var sizes = missed.ConvertAll(body => body.transform.localScale);
            const float ShrinkTime = 0.4f;
            for (float t = 0f; t < ShrinkTime; t += Time.deltaTime)
            {
                for (int i = 0; i < missed.Count; i++)
                    if (missed[i] != null) missed[i].transform.localScale = sizes[i] * (1f - t / ShrinkTime);
                yield return null;
            }
            foreach (var body in missed)
                if (body != null) Destroy(body.gameObject);
        }
    }
}
