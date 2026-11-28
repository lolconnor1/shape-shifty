using System.Collections.Generic;
using ShapeShifty.Audio;
using ShapeShifty.Interaction;
using ShapeShifty.Player;
using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Levels
{
    // Takes a KeyCard brought within Reach (carried, dropped or thrown), lights up and runs
    // On Accepted. Set Done puts it straight into that state, e.g. after a restart.
    public class CardReader : MonoBehaviour
    {
        [SerializeField] float reach = 1.4f;
        [SerializeField] Renderer lamp;
        [SerializeField] Material lampOff;
        [SerializeField] Material lampOn;
        public UnityEvent onAccepted = new UnityEvent();

        bool done;

        void Awake() { if (lamp != null) lamp.sharedMaterial = lampOff; }

        void Update()
        {
            if (done) return;
            foreach (var card in KeyCard.All)
            {
                Vector3 d = card.transform.position - transform.position;
                d.y *= 0.5f;
                if (d.magnitude > reach) continue;
                Take(card);
                Sfx.Play(Sound.TaskDone, transform.position);
                onAccepted.Invoke();
                return;
            }
        }

        public void SetDone()
        {
            if (done) return;
            done = true;
            if (lamp != null) lamp.sharedMaterial = lampOn;
            foreach (var card in KeyCard.All.ToArray()) Take(card);
        }

        void Take(KeyCard card)
        {
            done = true;
            if (lamp != null) lamp.sharedMaterial = lampOn;
            var player = GameObject.FindWithTag("Player");
            var bone = card.GetComponent<Bone>();
            if (player != null && player.TryGetComponent(out PlayerCarry carry) && carry.Held == bone) carry.Surrender();
            card.gameObject.SetActive(false);
            Destroy(card.gameObject);
        }
    }
}
