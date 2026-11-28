using ShapeShifty.Audio;
using ShapeShifty.Player;
using ShapeShifty.UI;
using UnityEngine;

namespace ShapeShifty.AI
{
    // The Golden Goose, the prize. Sits on her nest, bobbing, until the player grabs her;
    // then she rides on top of the player for the rest of the getaway, whatever form they're
    // in (sized to suit it), flapping and honking her head off, which helps nobody.
    // Her model's Animator has her looking about; the wings are flapped here, after it.
    public class GoldenGoose : MonoBehaviour
    {
        [SerializeField] Transform body;
        [SerializeField] Transform wingL;
        [SerializeField] Transform wingR;
        [Tooltip("Her size while carried, for each metre of the carrier's height.")]
        [SerializeField] float carriedScale = 0.32f;
        [Tooltip("How high she sits while carried, for each metre of the carrier's height.")]
        [SerializeField] float carriedHeight = 0.92f;
        [SerializeField] Vector2 honkInterval = new Vector2(2.5f, 5f);

        public bool IsCarried => shapeshifter != null;

        PlayerShapeshifter shapeshifter;
        Vector3 bodyRest;
        Quaternion wingLRest, wingRRest;
        float nextHonk;
        bool panicking;

        void Awake()
        {
            bodyRest = body.localPosition;
            wingLRest = wingL.localRotation;
            wingRRest = wingR.localRotation;
        }

        // Picked up by the player: she goes on top of whatever they are.
        public void Grab(PlayerShapeshifter by)
        {
            if (IsCarried) return;
            shapeshifter = by;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            transform.SetParent(by.Visual, false);
            transform.localRotation = Quaternion.identity;
            Sit();
            Honk();
        }

        // The alarm's gone off.
        public void Panic() => panicking = true;

        void Update()
        {
            float t = Time.time;
            if (!IsCarried)
            {
                body.localPosition = bodyRest + Vector3.up * (Mathf.Sin(t * 1.6f) * 0.02f);
                return;
            }
            Sit();
            body.localPosition = bodyRest + Vector3.up * (Mathf.Abs(Mathf.Sin(t * 9f)) * 0.06f);
            if (t >= nextHonk) Honk();
        }

        // Bursts of flapping while she's carried, more of them once the alarm's going. Each
        // wing swings out about her length, whichever way its bone points.
        void LateUpdate()
        {
            if (!IsCarried) return;
            float t = Time.time;
            float flap = Mathf.Clamp01((Mathf.Sin(t * 1.7f) - (panicking ? 0.1f : 0.6f)) * 3f);
            float a = Mathf.Sin(t * 28f) * 55f * flap;
            Vector3 along = wingL.parent.InverseTransformDirection(transform.forward);
            wingL.localRotation = Quaternion.AngleAxis(a, along) * wingLRest;
            wingR.localRotation = Quaternion.AngleAxis(-a, along) * wingRRest;
        }

        // On top of the current form, at a size to suit it.
        void Sit()
        {
            var form = shapeshifter.Current;
            float height = form != null ? form.height : 1.8f;
            transform.localPosition = Vector3.up * (height * carriedHeight);
            transform.localScale = Vector3.one * (height * carriedScale);
        }

        void Honk()
        {
            nextHonk = Time.time + Random.Range(honkInterval.x, honkInterval.y) * (panicking ? 0.8f : 1f);
            // Small while she's carried: right by the camera, a big one would fill the screen.
            FloatingWord.Spawn(transform.position + Vector3.up * (transform.lossyScale.y * 1.2f), "HONK!", new Color(1f, 0.84f, 0.3f), IsCarried ? 1.2f : 4f);
            Sfx.Play(Sound.Honk, transform.position, IsCarried ? 0.6f : 1f);
        }
    }
}
