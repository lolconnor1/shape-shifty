using ShapeShifty.Audio;
using ShapeShifty.Player;
using UnityEngine;
using UnityEngine.Events;

namespace ShapeShifty.Interaction
{
    // A pane of glass a wolf can howl to pieces. While the player howls within Range it hums
    // (shivers), and cracks spread across it, one more at each step towards Break Time
    // seconds of howling in all. Stopping early leaves the cracks where they are. At Break
    // Time it shatters: the pane and its collider go, shards fall, On Shattered goes off.
    public class AcousticGlass : MonoBehaviour
    {
        [SerializeField] Renderer pane;
        [SerializeField] Collider paneCollider;
        [Tooltip("Shown one after another as the glass weakens.")]
        [SerializeField] GameObject[] cracks;
        [SerializeField] float range = 9f;
        [SerializeField] float breakTime = 3f;
        [SerializeField] Material shardMaterial;
        [SerializeField] int shardCount = 24;
        public UnityEvent onShattered = new UnityEvent();

        PlayerHowl howl;
        Transform player;
        Vector3 rest;
        float stress;
        int shownCracks;

        public bool Shattered { get; private set; }

        void Awake()
        {
            rest = pane.transform.localPosition;
            foreach (var c in cracks) if (c != null) c.SetActive(false);
        }

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p == null) return;
            player = p.transform;
            howl = p.GetComponent<PlayerHowl>();
        }

        void Update()
        {
            if (Shattered || howl == null) return;
            bool hearing = howl.IsHowling && (paneCollider.ClosestPoint(player.position) - player.position).magnitude < range;
            if (!hearing)
            {
                pane.transform.localPosition = rest;
                return;
            }
            stress += Time.deltaTime;
            float shiver = 0.012f + 0.03f * stress / breakTime;
            pane.transform.localPosition = rest + new Vector3(Random.Range(-shiver, shiver), Random.Range(-shiver, shiver), Random.Range(-shiver, shiver));
            int want = Mathf.Min(cracks.Length, Mathf.FloorToInt(stress / breakTime * (cracks.Length + 1)));
            while (shownCracks < want)
            {
                if (cracks[shownCracks] != null) cracks[shownCracks].SetActive(true);
                shownCracks++;
                Sfx.Play(Sound.GlassCrack, pane.bounds.center);
            }
            if (stress >= breakTime) Shatter();
        }

        public void Shatter() => Shatter(true);
        public void ShatterQuietly() => Shatter(false);

        // Loud: with the crash and the shards. Quiet, for putting it back that way on a restart.
        public void Shatter(bool loud)
        {
            if (Shattered) return;
            Shattered = true;
            Bounds b = pane.bounds;
            pane.gameObject.SetActive(false);
            paneCollider.enabled = false;
            foreach (var c in cracks) if (c != null) c.SetActive(false);
            if (loud)
            {
                Sfx.Play(Sound.GlassBreak, b.center);
                Sfx.Play(Sound.GlassBreak, b.center, 0.7f, 0.8f);
                Vector3 away = player != null ? Vector3.ProjectOnPlane(b.center - player.position, Vector3.up).normalized : transform.forward;
                for (int i = 0; i < shardCount; i++)
                {
                    var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    shard.name = "Shard";
                    shard.layer = 2;
                    shard.transform.position = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y), Random.Range(b.min.z, b.max.z));
                    shard.transform.rotation = Random.rotation;
                    shard.transform.localScale = new Vector3(Random.Range(0.1f, 0.35f), Random.Range(0.1f, 0.35f), 0.03f);
                    if (shardMaterial != null) shard.GetComponent<Renderer>().sharedMaterial = shardMaterial;
                    var rb = shard.AddComponent<Rigidbody>();
                    rb.mass = 0.2f;
                    rb.linearVelocity = away * Random.Range(1f, 3.5f) + Vector3.up * Random.Range(0f, 1.5f);
                    rb.angularVelocity = Random.insideUnitSphere * 10f;
                    Destroy(shard, Random.Range(5f, 7f));
                }
            }
            onShattered.Invoke();
        }
    }
}
