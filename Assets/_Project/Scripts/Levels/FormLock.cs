using ShapeShifty.Interaction;
using ShapeShifty.Player;
using ShapeShifty.Shapes;
using UnityEngine;

namespace ShapeShifty.Levels
{
    // For a level that has to be played in one form (the monkey jungle: only a monkey can
    // throw). Once the player has taken that form they're kept in it for the rest of the
    // level, and a gate (optional) opens to let them on in.
    public class FormLock : MonoBehaviour
    {
        [SerializeField] ShapeDefinition form;
        [Tooltip("Opened once the player is in the form.")]
        [SerializeField] Hatch gate;

        PlayerShapeshifter shapeshifter;

        void Awake()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) shapeshifter = player.GetComponent<PlayerShapeshifter>();
        }

        void OnEnable()
        {
            if (shapeshifter != null) shapeshifter.Changed += Check;
        }

        void OnDisable()
        {
            if (shapeshifter != null) shapeshifter.Changed -= Check;
        }

        void Start() => Check();

        void Check()
        {
            if (shapeshifter == null || shapeshifter.Locked || shapeshifter.IsShifting || shapeshifter.Current != form) return;
            shapeshifter.Locked = true;
            if (gate != null) gate.Open();
        }
    }
}
