using System;
using System.Collections.Generic;
using System.Globalization;

namespace Echelon.Core
{
    public interface IScoringBattle
    {
        SimWorld World { get; }
        ScenarioDef Scenario { get; }
        bool HasResult { get; }

        TaskForce? GetTaskForce(string side);
        IEnumerable<Element> ElementsNear(SimVec2 position, double radius);
        void SetResult(Dictionary<string, object> result);
    }

    public interface IBattleStatsBattle
    {
        SimWorld World { get; }
        ScoringSystem? Scoring { get; }
        IReadOnlyDictionary<string, object> Result { get; }

        TaskForce? GetTaskForce(string side);
        Unit? GetUnit(int unitId);
        Element? GetElement(int elementId);
    }

    internal static class VictoryData
    {
        internal static Dictionary<string, object> CopyDictionary(IEnumerable<KeyValuePair<string, object>> source)
        {
            Dictionary<string, object> copy = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> pair in source)
            {
                copy[pair.Key] = CopyValue(pair.Value);
            }

            return copy;
        }

        internal static object CopyValue(object value)
        {
            if (value is Dictionary<string, object> dictionary)
            {
                return CopyDictionary(dictionary);
            }

            if (value is IReadOnlyDictionary<string, object> readOnlyDictionary)
            {
                return CopyDictionary(readOnlyDictionary);
            }

            if (value is List<Dictionary<string, object>> dictionaries)
            {
                List<Dictionary<string, object>> copy = new List<Dictionary<string, object>>();
                foreach (Dictionary<string, object> entry in dictionaries)
                {
                    copy.Add(CopyDictionary(entry));
                }

                return copy;
            }

            if (value is List<object> list)
            {
                List<object> copy = new List<object>();
                foreach (object entry in list)
                {
                    copy.Add(CopyValue(entry));
                }

                return copy;
            }

            if (value is object[] array)
            {
                object[] copy = new object[array.Length];
                for (int i = 0; i < array.Length; ++i)
                {
                    copy[i] = CopyValue(array[i]);
                }

                return copy;
            }

            return value;
        }

        internal static double Number(object value)
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        internal static long Integer(object value)
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        internal static string Text(object value)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        }
    }
}
