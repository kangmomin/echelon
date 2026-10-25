namespace Echelon.Unity.UI.Common
{
    public readonly struct UiOption<T>
    {
        public UiOption(string label, T value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }
        public T Value { get; }
    }
}
