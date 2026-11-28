using ShapeShifty.Audio;
using UnityEngine;

namespace ShapeShifty.Interaction
{
    // A drawer that opens once (Interact) and lets what's inside fall out: Contents is kept
    // hidden until then and appears at Drop At.
    [RequireComponent(typeof(Interactable))]
    public class Drawer : MonoBehaviour
    {
        [SerializeField] Transform drawer;
        [SerializeField] Vector3 openOffset = new Vector3(0f, 0f, 0.35f);
        [SerializeField] GameObject contents;
        [SerializeField] Transform dropAt;

        Interactable interactable;
        Vector3 shut;
        float open;
        bool opened;

        void Awake()
        {
            interactable = GetComponent<Interactable>();
            interactable.Interacted += Open;
            if (drawer != null) shut = drawer.localPosition;
            if (contents != null) contents.SetActive(false);
        }

        void Open()
        {
            if (opened) return;
            opened = true;
            interactable.enabled = false;
            Sfx.Play(Sound.HatchMove, transform.position, 0.6f);
            if (contents == null) return;
            if (dropAt != null) contents.transform.SetPositionAndRotation(dropAt.position, dropAt.rotation);
            contents.SetActive(true);
        }

        void Update()
        {
            if (drawer == null || !opened || open >= 1f) return;
            open = Mathf.MoveTowards(open, 1f, Time.deltaTime * 3f);
            drawer.localPosition = shut + openOffset * Mathf.SmoothStep(0f, 1f, open);
        }
    }
}
