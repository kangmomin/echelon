using UnityEngine;

namespace Echelon.Unity.UI.Tutorial
{
    public readonly struct TutorialWorldMarker
    {
        public TutorialWorldMarker(Vector2 simPosition, Color color, string text)
        {
            SimPosition = simPosition;
            Color = color;
            Text = text;
        }

        public Vector2 SimPosition { get; }
        public Color Color { get; }
        public string Text { get; }
        public float Radius => 90f;
        public float BeamHeight => 160f;
    }
}
