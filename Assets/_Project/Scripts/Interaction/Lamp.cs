using UnityEngine;

namespace ShapeShifty.Interaction
{
    // A warning or status light. Hook On and Off up to whatever it shows, e.g. a wire's power.
    public class Lamp : MonoBehaviour
    {
        [SerializeField] Renderer bulb;
        [SerializeField] Material offMaterial;
        [SerializeField] Material onMaterial;
        [SerializeField] bool isOn;

        void Awake() => Apply();

        public void On()
        {
            isOn = true;
            Apply();
        }

        public void Off()
        {
            isOn = false;
            Apply();
        }

        void Apply()
        {
            if (bulb != null) bulb.sharedMaterial = isOn ? onMaterial : offMaterial;
        }
    }
}
