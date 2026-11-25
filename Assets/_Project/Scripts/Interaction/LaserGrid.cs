using ShapeShifty.Audio;
using ShapeShifty.Player;
using ShapeShifty.UI;
using UnityEngine;

namespace ShapeShifty.Interaction
{
    // Laser beams skimming a pool. Anything under the beams is zapped unless it's swimming
    // with its whole body below the surface, and anything that can't swim is zapped as soon
    // as it's in the water at all: a flash, a moment stunned, and it's back at the start.
    // Both zones are box colliders (triggers on Ignore Raycast) used only for their shape.
    // Switched off, the beams go dark and nothing is zapped.
    public class LaserGrid : MonoBehaviour
    {
        [Tooltip("Under the beams.")]
        [SerializeField] BoxCollider beamZone;
        [Tooltip("The whole pool: where a non-swimmer gets zapped.")]
        [SerializeField] BoxCollider poolZone;
        [SerializeField] DeepWater water;
        [Tooltip("How far under the surface the top of a swimmer has to be.")]
        [SerializeField] float clearance = 0.1f;
        [SerializeField] GameObject beams;
        [Tooltip("Where a zapped player is put back, facing its forward.")]
        [SerializeField] Transform respawn;
        [SerializeField] float stunTime = 1f;
        [SerializeField] bool isOn = true;

        public bool IsOn => isOn;

        GameObject player;
        CharacterController controller;
        PlayerController mover;
        PlayerShapeshifter shapeshifter;
        PlayerStun stun;
        ShapeHud hud;
        float flashUntil;

        void Start()
        {
            player = GameObject.FindWithTag("Player");
            if (player == null) return;
            controller = player.GetComponent<CharacterController>();
            mover = player.GetComponent<PlayerController>();
            shapeshifter = player.GetComponent<PlayerShapeshifter>();
            stun = player.GetComponent<PlayerStun>();
            hud = FindFirstObjectByType<ShapeHud>();
            beams.SetActive(isOn);
        }

        public void TurnOff()
        {
            isOn = false;
            if (beams != null) beams.SetActive(false);
        }

        void Update()
        {
            if (beams != null && isOn && Time.time < flashUntil) beams.SetActive(Mathf.Repeat(Time.time * 12f, 1f) < 0.5f);
            else if (beams != null) beams.SetActive(isOn);
            if (!isOn || controller == null || !controller.enabled) return;

            Bounds body = controller.bounds;
            var form = shapeshifter.Current;
            bool swimmer = form != null && form.canSwim;
            if (!swimmer && poolZone.bounds.Intersects(body)) { Zap("Zapped! Only a swimmer can slip under the lasers"); return; }
            if (!beamZone.bounds.Intersects(body)) return;
            if (swimmer && mover.IsSwimming && body.max.y < water.SurfaceY - clearance) return;
            Zap(swimmer ? "Zapped! Dive deeper to stay under the lasers" : "Zapped! The lasers caught you");
        }

        void Zap(string message)
        {
            controller.enabled = false;
            player.transform.position = respawn.position;
            controller.enabled = true;
            mover.FaceTowards(respawn.forward);
            if (stun != null) stun.Stun(stunTime);
            Sfx.Play(Sound.Zap);
            if (hud != null) hud.ShowMessage(message);
            flashUntil = Time.time + 0.6f;
        }
    }
}
