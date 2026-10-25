using UnityEngine;

namespace Echelon.Unity.UI.Tutorial
{
    public enum TutorialIntentKind
    {
        CameraFocus = 0,
        RevealEnemy = 1,
        FireMode = 2,
        QuitToMenu = 3
    }

    public enum TutorialFireMode
    {
        None = 0,
        Mortar = 1,
        Artillery = 2,
        CloseAirSupport = 3
    }

    public readonly struct TutorialIntent
    {
        private TutorialIntent(
            TutorialIntentKind kind,
            string unitId,
            Vector2 simPosition,
            float headingRadians,
            TutorialFireMode fireMode,
            bool smoke)
        {
            Kind = kind;
            UnitId = unitId;
            SimPosition = simPosition;
            HeadingRadians = headingRadians;
            FireMode = fireMode;
            Smoke = smoke;
        }

        public TutorialIntentKind Kind { get; }
        public string UnitId { get; }
        public Vector2 SimPosition { get; }
        public float HeadingRadians { get; }
        public TutorialFireMode FireMode { get; }
        public bool Smoke { get; }

        public static TutorialIntent FocusCamera(Vector2 simPosition)
        {
            return new TutorialIntent(
                TutorialIntentKind.CameraFocus,
                string.Empty,
                simPosition,
                0f,
                TutorialFireMode.None,
                false);
        }

        public static TutorialIntent RevealEnemyAt(string unitId, Vector2 simPosition, float headingRadians)
        {
            return new TutorialIntent(
                TutorialIntentKind.RevealEnemy,
                unitId,
                simPosition,
                headingRadians,
                TutorialFireMode.None,
                false);
        }

        public static TutorialIntent SetFireMode(TutorialFireMode fireMode, bool smoke)
        {
            return new TutorialIntent(
                TutorialIntentKind.FireMode,
                string.Empty,
                Vector2.zero,
                0f,
                fireMode,
                smoke);
        }

        public static TutorialIntent QuitToMenu()
        {
            return new TutorialIntent(
                TutorialIntentKind.QuitToMenu,
                string.Empty,
                Vector2.zero,
                0f,
                TutorialFireMode.None,
                false);
        }
    }
}
