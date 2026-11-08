using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Interaction
{
    // The bare end of a wire, lying where water can reach it. A live one is on the mains: it
    // electrifies any puddle that spreads over it (see WaterPuddle). SwitchOn and SwitchOff
    // connect it to the mains and cut it off again, e.g. from a pressure plate. A dead one leads
    // off to something that needs power, like a door or a lamp: while a live puddle covers it
    // too, the water connects the two and it has power. Hook onPowered and onUnpowered up to
    // whatever it leads to. Either sparks now and then while it has power.
    public class BareWire : MonoBehaviour
    {
        [Tooltip("On the mains, so live. Off: dead until water connects it to a live wire.")]
        [SerializeField] bool live;
        [Tooltip("Spits sparks now and then while it has power. Simulated in world space, emitted from code.")]
        [SerializeField] ParticleSystem sparks;
        [Tooltip("Seconds between spits of sparks.")]
        [SerializeField] Vector2 sparkInterval = new Vector2(0.4f, 1.6f);
        public UnityEvent onPowered;
        public UnityEvent onUnpowered;

        public static readonly List<BareWire> All = new List<BareWire>();

        public bool IsLive => live;
        public bool IsPowered { get; private set; }

        int fedFrame = -1;
        float sparkTimer;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void SwitchOn() => live = true;
        public void SwitchOff() => live = false;

        // Called by a live puddle covering this wire, every frame it does.
        public void Feed() => fedFrame = Time.frameCount;

        // After the puddles have had their Update, so a wire knows whether one fed it this frame.
        void LateUpdate()
        {
            bool powered = !live && fedFrame == Time.frameCount;
            if (powered != IsPowered)
            {
                IsPowered = powered;
                (powered ? onPowered : onUnpowered).Invoke();
            }
            if (live || powered) Spark();
        }

        void Spark()
        {
            if (sparks == null) return;
            sparkTimer -= Time.deltaTime;
            if (sparkTimer > 0f) return;
            sparkTimer = Random.Range(sparkInterval.x, sparkInterval.y);
            var emit = new ParticleSystem.EmitParams { position = transform.position, applyShapeToPosition = true };
            sparks.Emit(emit, Random.Range(3, 8));
        }
    }
}
