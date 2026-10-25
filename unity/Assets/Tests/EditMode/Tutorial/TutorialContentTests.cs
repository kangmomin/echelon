using Echelon.Unity.UI.Tutorial;
using NUnit.Framework;
using UnityEngine;

namespace Echelon.Unity.Tests.UI.Tutorial
{
    public sealed class TutorialContentTests
    {
        [Test]
        public void Definitions_ContainFourteenOrderedSteps()
        {
            Assert.That(TutorialContent.Steps.Count, Is.EqualTo(TutorialContent.StepCount));
            Assert.That(TutorialContent.StepCount, Is.EqualTo(14));
            for (int i = 0; i < TutorialContent.Steps.Count; i++)
            {
                TutorialStepDefinition step = TutorialContent.Steps[i];
                Assert.That(step.Id, Is.EqualTo((TutorialStepId)i));
                Assert.That(TutorialContent.Get(step.Id), Is.SameAs(step));
                Assert.That(step.Title, Is.Not.Empty);
                Assert.That(step.Body, Is.Not.Empty);
                Assert.That(step.AllowsNext, Is.EqualTo(!step.RequiresCompletion));
            }
        }

        [Test]
        public void Definitions_OnlyWelcomeAndCompletionUseNextButton()
        {
            for (int i = 0; i < TutorialContent.Steps.Count; i++)
            {
                TutorialStepDefinition step = TutorialContent.Steps[i];
                bool manual = step.Id == TutorialStepId.Welcome || step.Id == TutorialStepId.Completion;
                Assert.That(step.AllowsNext, Is.EqualTo(manual), step.Id.ToString());
                Assert.That(step.IsFinal, Is.EqualTo(step.Id == TutorialStepId.Completion));
            }
        }

        [Test]
        public void ScenarioCoordinates_MatchGodotTutorialComposition()
        {
            Assert.That(TutorialController.TutorialStart, Is.EqualTo(new Vector2(750f, 650f)));
            Assert.That(TutorialController.TutorialWaypoint, Is.EqualTo(new Vector2(900f, 1500f)));
            Assert.That(TutorialController.TutorialEnemy, Is.EqualTo(new Vector2(1150f, 2050f)));
        }
    }
}
