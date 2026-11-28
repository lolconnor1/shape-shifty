using ShapeShifty.AI;
using ShapeShifty.Combat;
using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Levels
{
    // A security camera wired to a monitor in the security office. A poo on the lens (its
    // Bullseye) blinds it: the lens gets a splat, the camera stops seeing, its monitor goes
    // brown, and On Blinded goes off (e.g. sending the watchman to wipe it). Wipe() cleans it.
    // Power Off kills the camera and its monitor for good.
    [RequireComponent(typeof(SecurityCamera), typeof(VisionCone))]
    public class CameraFeed : MonoBehaviour
    {
        [SerializeField] Bullseye lens;
        [Tooltip("Shown on the lens while it's mucky.")]
        [SerializeField] GameObject splat;
        [SerializeField] Renderer monitor;
        [SerializeField] Material feedOn;
        [SerializeField] Material feedMucky;
        [SerializeField] Material feedOff;
        public UnityEvent onBlinded = new UnityEvent();

        SecurityCamera securityCamera;
        VisionCone vision;
        bool powered = true;

        public bool Blinded { get; private set; }

        void Awake()
        {
            securityCamera = GetComponent<SecurityCamera>();
            vision = GetComponent<VisionCone>();
            if (splat != null) splat.SetActive(false);
            Show(feedOn);
        }

        void OnEnable() { if (lens != null) lens.Hit += OnLensHit; }
        void OnDisable() { if (lens != null) lens.Hit -= OnLensHit; }

        void OnLensHit(Bullseye target) => Blind();

        public void Blind()
        {
            if (Blinded || !powered) return;
            Blinded = true;
            if (splat != null) splat.SetActive(true);
            vision.enabled = false;
            Show(feedMucky);
            onBlinded.Invoke();
        }

        public void Wipe()
        {
            if (!Blinded) return;
            Blinded = false;
            if (splat != null) splat.SetActive(false);
            if (lens != null) lens.ResetTarget();
            if (!powered) return;
            vision.enabled = true;
            Show(feedOn);
        }

        public void PowerOff()
        {
            if (!powered) return;
            powered = false;
            securityCamera.SwitchOff();
            Show(feedOff);
        }

        void Show(Material material)
        {
            if (monitor != null && material != null) monitor.sharedMaterial = material;
        }
    }
}
