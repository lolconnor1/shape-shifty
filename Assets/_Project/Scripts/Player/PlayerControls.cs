using UnityEngine;

namespace ShapeShifty.Player
{
    // Turns all of the player's controls off or back on in one go, for cutscenes and the
    // caught screen.
    public static class PlayerControls
    {
        public static void SetEnabled(GameObject player, bool enabled)
        {
            player.GetComponent<PlayerController>().enabled = enabled;
            player.GetComponent<PlayerLook>().enabled = enabled; // also frees or locks the cursor
            if (player.TryGetComponent(out PlayerScanner scanner)) scanner.enabled = enabled;
            if (player.TryGetComponent(out PlayerShapeshifter shapeshifter)) shapeshifter.enabled = enabled;
            if (player.TryGetComponent(out PlayerTalker talker)) talker.enabled = enabled;
            if (player.TryGetComponent(out PlayerInteractor interactor)) interactor.enabled = enabled;
        }
    }
}
