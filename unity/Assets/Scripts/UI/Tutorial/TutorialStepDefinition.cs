namespace Echelon.Unity.UI.Tutorial
{
    public enum TutorialStepId
    {
        Welcome = 0,
        Camera = 1,
        CompanySelection = 2,
        Subselection = 3,
        ControlGroups = 4,
        Formation = 5,
        TimeControls = 6,
        MovementOrder = 7,
        CommandDelay = 8,
        RulesOfEngagement = 9,
        AttackMove = 10,
        FireSupport = 11,
        EnemyDestruction = 12,
        Completion = 13
    }

    public sealed class TutorialStepDefinition
    {
        public TutorialStepDefinition(
            TutorialStepId id,
            string title,
            string body,
            bool requiresCompletion)
        {
            Id = id;
            Title = title;
            Body = body;
            RequiresCompletion = requiresCompletion;
        }

        public TutorialStepId Id { get; }
        public string Title { get; }
        public string Body { get; }
        public bool RequiresCompletion { get; }
        public bool AllowsNext => !RequiresCompletion;
        public bool IsFinal => Id == TutorialStepId.Completion;
    }
}
