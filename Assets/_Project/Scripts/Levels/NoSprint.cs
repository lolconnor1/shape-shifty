using ShapeShifty.Player;
using ShapeShifty.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShapeShifty.Levels
{
    // For a level that has to be crept through: no sprinting in any form. Says so, now and
    // then, when the player tries.
    public class NoSprint : MonoBehaviour
    {
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] string hint = "Nobody runs round a wolf pack. Not twice";
        [SerializeField] float hintEvery = 12f;

        PlayerController mover;
        ShapeHud hud;
        InputAction sprint;
        float nextHint;

        void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) mover = player.GetComponent<PlayerController>();
            if (mover != null) mover.NoSprint = true;
            hud = FindFirstObjectByType<ShapeHud>();
            sprint = inputActions.FindActionMap("Player", true).FindAction("Sprint", true);
        }

        void Update()
        {
            if (mover == null || hud == null || !mover.enabled || Time.time < nextHint || !sprint.WasPressedThisFrame()) return;
            nextHint = Time.time + hintEvery;
            hud.ShowMessage(hint);
        }
    }
}
