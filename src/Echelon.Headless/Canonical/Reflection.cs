using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Echelon.Headless
{
    internal static class SnapshotReflection
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static object Get(object target, string name)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            Type? type = target.GetType();
            while (type != null)
            {
                FieldInfo? field = type.GetField(name, Flags | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field.GetValue(target) ?? throw new InvalidOperationException(
                        $"Snapshot field {type.FullName}.{name} is null");
                PropertyInfo? property = type.GetProperty(name, Flags | BindingFlags.DeclaredOnly);
                if (property != null)
                    return property.GetValue(target) ?? throw new InvalidOperationException(
                        $"Snapshot property {type.FullName}.{name} is null");
                type = type.BaseType;
            }
            throw new MissingMemberException(target.GetType().FullName, name);
        }

        internal static T Get<T>(object target, string name)
        {
            object value = Get(target, name);
            if (value is T typed)
                return typed;
            throw new InvalidCastException($"Snapshot member {target.GetType().FullName}.{name} " +
                $"is {value.GetType().FullName}, not {typeof(T).FullName}");
        }

        internal static List<T> List<T>(object target, string name)
        {
            var output = new List<T>();
            foreach (object? item in (IEnumerable)Get(target, name))
            {
                if (item is T typed)
                    output.Add(typed);
                else
                    throw new InvalidCastException($"Snapshot sequence {target.GetType().FullName}.{name} " +
                        $"contains {item?.GetType().FullName ?? "null"}, not {typeof(T).FullName}");
            }
            return output;
        }

        internal static object? GetNullable(object target, string name)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            Type? type = target.GetType();
            while (type != null)
            {
                FieldInfo? field = type.GetField(name, Flags | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field.GetValue(target);
                PropertyInfo? property = type.GetProperty(name, Flags | BindingFlags.DeclaredOnly);
                if (property != null)
                    return property.GetValue(target);
                type = type.BaseType;
            }
            throw new MissingMemberException(target.GetType().FullName, name);
        }
    }
}
