using ShapeShifty.Player;
using ShapeShifty.Shapes;
using UnityEngine;

namespace ShapeShifty.Levels
{
    // How the player starts a level: which forms they must already have to have got here
    // (unlocked if missing, so the level also works when opened on its own), and which form
    // they arrive in. Runs early so the forms are in place before anything else looks.
    [DefaultExecutionOrder(-100)]
    public class LevelStart : MonoBehaviour
    {
        [Tooltip("Forms the player needs for this level. Normally already scanned in an earlier level.")]
        [SerializeField] ShapeDefinition[] requiredForms;
        [Tooltip("The form the player arrives in. Leave empty to start in their own form.")]
        [SerializeField] ShapeDefinition startForm;

        void Awake()
        {
            foreach (var form in requiredForms)
                if (!UnlockedShapes.Contains(form)) UnlockedShapes.Unlock(form);
        }

        void Start()
        {
            if (startForm == null) return;
            var player = GameObject.FindWithTag("Player");
            if (player != null) player.GetComponent<PlayerShapeshifter>().StartAs(startForm);
        }
    }
}
