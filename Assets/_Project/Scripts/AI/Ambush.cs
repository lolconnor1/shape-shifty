using System;
using System.Collections;
using UnityEngine;

namespace ShapeShifty.AI
{
    // Greys hiding up in the trees (needs a trigger collider): kept out of the level until the
    // player walks in, then they drop down around them one after another and pile in.
    public class Ambush : MonoBehaviour
    {
        [SerializeField] Monkey[] monkeys;
        [Tooltip("Where each one lands, in the same order.")]
        [SerializeField] Transform[] landings;
        [Tooltip("Seconds between one dropping and the next.")]
        [SerializeField] float stagger = 0.35f;

        // An ambush has been sprung.
        public static event Action<Ambush> Sprung;

        bool sprung;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlaySession() => Sprung = null;

        void Awake()
        {
            foreach (var m in monkeys) if (m != null) m.gameObject.SetActive(false);
        }

        void OnTriggerEnter(Collider other)
        {
            if (sprung || !other.CompareTag("Player")) return;
            sprung = true;
            StartCoroutine(Spring());
            Sprung?.Invoke(this);
        }

        IEnumerator Spring()
        {
            for (int i = 0; i < monkeys.Length; i++)
            {
                if (monkeys[i] == null) continue;
                monkeys[i].DropIn(i < landings.Length && landings[i] != null ? landings[i].position : monkeys[i].transform.position);
                yield return new WaitForSeconds(stagger);
            }
        }
    }
}
