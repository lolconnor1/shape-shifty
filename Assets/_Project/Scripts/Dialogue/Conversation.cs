using System;
using UnityEngine;

namespace ShapeShifty.Dialogue
{
    // A short back-and-forth between the player and an animal, usually a hint about the
    // level or what that kind of animal can do. Make one with Create > Shape-Shifty >
    // Conversation and give it to a Talkable.
    [CreateAssetMenu(fileName = "Conversation", menuName = "Shape-Shifty/Conversation")]
    public class Conversation : ScriptableObject
    {
        [Serializable]
        public struct Line
        {
            [Tooltip("Said by the player rather than the animal.")]
            public bool byPlayer;
            [TextArea(2, 4)] public string text;
        }

        public Line[] lines;
    }
}
