using System.Collections.Generic;

namespace Echelon.Unity.UI.Tutorial
{
    public static class TutorialContent
    {
        public const int StepCount = 14;

        private static readonly IReadOnlyList<TutorialStepDefinition> AllSteps = new[]
        {
            Step(
                TutorialStepId.Welcome,
                "Welcome",
                Text(
                    "In ECHELON you command <b>companies and platoons</b>, not individual vehicles. Orders travel by radio and take <b>tens of seconds</b> to execute.",
                    "In this tutorial (about 10 minutes), practice the camera, selection, orders, formations, and fire support. Open the guide at any time with " + Key("/") + "."),
                false),
            Step(
                TutorialStepId.Camera,
                "Camera",
                "Pan with " + Key("W A S D") + " (or the arrow keys), zoom with the " + Key("mouse wheel")
                    + ", and rotate or tilt by dragging with the " + Key("middle mouse button") + "."),
            Step(
                TutorialStepId.CompanySelection,
                "Company selection",
                "Press " + Key("F1") + " or left-click the <b>A Company</b> symbol. <b>Double-clicking</b> a platoon symbol also selects its parent company.\n"
                    + "You can also click its name in the organization panel on the left."),
            Step(
                TutorialStepId.Subselection,
                "Subselection",
                "Press " + Key("Tab") + " to select the company's <b>platoons</b>, then " + Key("Shift+Tab") + " to select the company again.\n"
                    + "Normally command the company. Direct platoon orders <b>detach</b> that platoon from company control; use them only when necessary."),
            Step(
                TutorialStepId.ControlGroups,
                "Control groups",
                "With A Company selected, press " + Key("Ctrl+1") + " to assign group 1, " + Key("Esc") + " to clear the selection, then "
                    + Key("1") + " to recall it. A quick double press also moves the camera to the group."),
            Step(
                TutorialStepId.Formation,
                "Formation",
                "Press " + Key("Alt+3") + " to change A Company to <b>platoon line</b>, or use the formation buttons at the bottom of the screen.\n"
                    + "Line formation has the greatest frontal firepower and resists artillery, but is slow. Column is fast, but fewer vehicles can fire forward.\n"
                    + "<color=#a6a9b3>Formation orders travel by radio too, so the change happens after a delay.</color>"),
            Step(
                TutorialStepId.TimeControls,
                "Time controls",
                "Press " + Key("Space") + " to pause, " + Key("=") + " to increase speed (" + Key("-") + " decreases it), then "
                    + Key("Space") + " again to resume.\nYou can issue orders while paused. Pause whenever you need time to think."),
            Step(
                TutorialStepId.MovementOrder,
                "Movement order",
                "With A Company selected, " + Key("right-click") + " the <color=#ffd94d><b>yellow marker</b></color> across the river.\n"
                    + "Before clicking, <b>\"executes in about N seconds\"</b> beside the cursor shows command delay. Hold and drag the right button to choose the arrival facing."),
            Step(
                TutorialStepId.CommandDelay,
                "Command delay",
                "Orders travel by radio from <b>battalion → company → platoon</b>. Dotted lines and numbers on the map show the remaining time.\n"
                    + "Wait until a platoon receives the order and starts moving (you can press " + Key("=") + " to increase speed)."),
            Step(
                TutorialStepId.RulesOfEngagement,
                "Rules of engagement",
                "While moving, press " + Key("H") + " for <b>hold fire</b> (do not fire first; useful for ambushes), then "
                    + Key("H") + " again to restore <b>fire at will</b>.\nThe Fire entry in the selection panel changes after the order travels through the radio network."),
            Step(
                TutorialStepId.AttackMove,
                "Attack move",
                "Recon report: an <color=#ff7a70><b>enemy BMP-3 platoon</b></color> has appeared on BRAVO ridge.\n"
                    + "Press " + Key("Q") + ", then " + Key("right-click") + " the red marker. Attack move engages enemies encountered along the way.\n"
                    + "The same method issues " + Key("F") + " fast move, " + Key("B") + " withdraw, " + Key("Y") + " seize, and "
                    + Key("J") + " defend."),
            Step(
                TutorialStepId.FireSupport,
                "Fire support",
                "Press " + Key("T") + " to enter <b>mortar</b> request mode, then " + Key("left-click") + " near the red marker.\n"
                    + "Press " + Key("T") + " again for artillery, " + Key("G") + " for smoke, or " + Key("P") + " for close air support. "
                    + "A friendly observer who can see the target makes the mission faster and more accurate."),
            Step(
                TutorialStepId.EnemyDestruction,
                "Destroy the enemy platoon",
                "Destroy the enemy platoon. Watch notifications at the upper right and combat strength (%) in the organization panel.\n"
                    + "Losses and suppression lower morale until troops <b>refuse attacks or rout</b>. Press " + Key("=") + " to increase speed if this takes a while."),
            Step(
                TutorialStepId.Completion,
                "Tutorial complete",
                "Well done. You have practiced every basic control.\n"
                    + "• Battalion organization, supply, and victory conditions are covered in the " + Key("/") + " guide.\n"
                    + "• Try <b>Skirmish</b> in the menu to build your own battalion and fight the AI (up to 40 minutes).",
                false)
        };

        public static IReadOnlyList<TutorialStepDefinition> Steps => AllSteps;

        public static TutorialStepDefinition Get(TutorialStepId id)
        {
            return AllSteps[(int)id];
        }

        private static TutorialStepDefinition Step(
            TutorialStepId id,
            string title,
            string body,
            bool requiresCompletion = true)
        {
            return new TutorialStepDefinition(id, title, body, requiresCompletion);
        }

        private static string Key(string key)
        {
            return "<color=#f2cc59><b>" + key + "</b></color>";
        }

        private static string Text(params string[] lines)
        {
            return string.Join("\n", lines);
        }
    }
}
