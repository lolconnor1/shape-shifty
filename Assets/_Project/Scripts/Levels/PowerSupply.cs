using ShapeShifty.Audio;
using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Levels
{
    // A generator that keeps the lights (and whatever else is hooked to On Power Off) running
    // until Power Off is called, e.g. by a poo on its breaker. Its fan spins down and its
    // lamps and lights go out. Only happens once.
    public class PowerSupply : MonoBehaviour
    {
        [SerializeField] Light[] lights;
        [SerializeField] Renderer[] lamps;
        [SerializeField] Material lampOff;
        [SerializeField] Transform fan;
        [SerializeField] float fanSpeed = 540f;
        public UnityEvent onPowerOff = new UnityEvent();

        float spin = 1f;
        public bool IsOff { get; private set; }

        public void PowerOff()
        {
            if (IsOff) return;
            IsOff = true;
            Sfx.Play(Sound.PowerDown, transform.position);
            foreach (var l in lights) if (l != null) l.enabled = false;
            foreach (var r in lamps) if (r != null && lampOff != null) r.sharedMaterial = lampOff;
            onPowerOff.Invoke();
        }

        void Update()
        {
            if (IsOff) spin = Mathf.MoveTowards(spin, 0f, Time.deltaTime * 0.4f);
            if (fan != null && spin > 0f) fan.Rotate(0f, 0f, fanSpeed * spin * Time.deltaTime, Space.Self);
        }
    }
}
