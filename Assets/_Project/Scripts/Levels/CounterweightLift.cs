using System;
using ShapeShifty.Audio;
using ShapeShifty.Player;
using UnityEngine;

namespace ShapeShifty.Levels
{
    // Two lift cages on one rope over a pulley: whichever side is heavier sinks and hauls the
    // other one up. Cage A rests at the top and B at the bottom, because B weighs a little
    // more empty. The player adds their current form's weight (ShapeDefinition.weight) to
    // the cage they're standing in, so changing shape mid-lift sets it moving. Cargo B is
    // anything else loaded into B (see IngotCart). Carries the player like a MovingPlatform.
    [DefaultExecutionOrder(-50)]
    public class CounterweightLift : MonoBehaviour
    {
        [SerializeField] Transform cageA;
        [SerializeField] Transform cageB;
        [Tooltip("Heights (y) of cage A at the top and the bottom.")]
        [SerializeField] Vector2 aTopBottom = new Vector2(5f, 0f);
        [Tooltip("Heights (y) of cage B at the bottom and the top.")]
        [SerializeField] Vector2 bBottomTop = new Vector2(-3f, 5f);
        [SerializeField] float cageWeightA = 0f;
        [SerializeField] float cageWeightB = 35f;
        [Tooltip("Seconds for a full trip.")]
        [SerializeField] float tripTime = 4f;
        [Tooltip("Ropes from each cage's top up to the pulley: stretched to fit each frame.")]
        [SerializeField] Transform ropeA;
        [SerializeField] Transform ropeB;
        [SerializeField] float pulleyHeight = 9f;
        [SerializeField] float ropeTopOffset = 2.4f;

        public float CargoB { get; set; }
        // 0: A at the top, B at the bottom. 1: the other way round.
        public float Travel { get; private set; }
        public bool BAtBottom => Travel <= 0.001f;
        public bool BAtTop => Travel >= 0.999f;
        public Transform CageB => cageB;
        public event Action ReachedTop;

        PlayerController player;
        PlayerShapeshifter shifter;
        bool moving;

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                player = p.GetComponent<PlayerController>();
                shifter = p.GetComponent<PlayerShapeshifter>();
            }
            Place();
        }

        bool Riding(Transform cage) => player != null && player.Ground != null && player.Ground.transform.IsChildOf(cage);

        float PlayerWeight => shifter != null && shifter.Current != null ? shifter.Current.weight : 0f;

        void Update()
        {
            float loadA = cageWeightA + (Riding(cageA) ? PlayerWeight : 0f);
            float loadB = cageWeightB + CargoB + (Riding(cageB) ? PlayerWeight : 0f);
            float dir = loadA > loadB + 0.01f ? 1f : loadA < loadB - 0.01f ? -1f : 0f;
            float before = Travel;
            Travel = Mathf.Clamp01(Travel + dir * Time.deltaTime / tripTime);
            bool nowMoving = Travel != before;
            if (nowMoving && !moving) Sfx.Play(Sound.HatchMove, cageA.position);
            if (!nowMoving && moving) Sfx.Play(Sound.HatchShut, Travel > 0.5f ? cageA.position : cageB.position, 0.6f);
            moving = nowMoving;
            if (!nowMoving) return;

            Vector3 a0 = cageA.position, b0 = cageB.position;
            bool onA = Riding(cageA), onB = Riding(cageB);
            Place();
            if (onA) player.transform.position += cageA.position - a0;
            else if (onB) player.transform.position += cageB.position - b0;
            Physics.SyncTransforms();
            if (BAtTop && before < 0.999f) ReachedTop?.Invoke();
        }

        void Place()
        {
            float t = Mathf.SmoothStep(0f, 1f, Travel);
            var a = cageA.position; a.y = Mathf.Lerp(aTopBottom.x, aTopBottom.y, t); cageA.position = a;
            var b = cageB.position; b.y = Mathf.Lerp(bBottomTop.x, bBottomTop.y, t); cageB.position = b;
            Stretch(ropeA, cageA);
            Stretch(ropeB, cageB);
        }

        void Stretch(Transform rope, Transform cage)
        {
            if (rope == null) return;
            float bottom = cage.position.y + ropeTopOffset;
            float length = Mathf.Max(0.05f, pulleyHeight - bottom);
            rope.position = new Vector3(cage.position.x, bottom + length * 0.5f, cage.position.z);
            var s = rope.localScale; s.y = length * 0.5f; rope.localScale = s; // a cylinder is 2 tall
        }
    }
}
