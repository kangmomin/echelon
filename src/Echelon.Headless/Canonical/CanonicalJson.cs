using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Echelon.Core;
using Newtonsoft.Json.Linq;

namespace Echelon.Headless
{
    internal static class CanonicalJson
    {
        internal const long MaxSafeInteger = 9007199254740991L;

        internal static SortedDictionary<string, object?> Object(params (string Key, object? Value)[] fields)
        {
            var output = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            foreach ((string key, object? value) in fields)
                output.Add(key, value);
            return output;
        }

        internal static List<object?> Array(params object?[] values)
        {
            return new List<object?>(values);
        }

        internal static object Integer(long value)
        {
            return value > MaxSafeInteger || value < -MaxSafeInteger ? (object)Int64(value) : value;
        }

        internal static SortedDictionary<string, object?> Int64(long value)
        {
            return Object(("$i64", value.ToString(CultureInfo.InvariantCulture)));
        }

        internal static SortedDictionary<string, object?> Float64(double value)
        {
            return Object(("$f64", BitConverter.DoubleToInt64Bits(value).ToString("x16", CultureInfo.InvariantCulture)));
        }

        internal static List<object?> Vec2(SimVec2 value)
        {
            return Array(Float64(value.X), Float64(value.Y));
        }

        internal static List<object?> Vec2I(SimVec2I value)
        {
            return Array((long)value.X, (long)value.Y);
        }

        internal static List<object?> Vec3(float x, float y, float z)
        {
            return Array(Float64(x), Float64(y), Float64(z));
        }

        internal static object? Normalize(object? value)
        {
            if (value == null)
                return null;
            if (value is JToken token)
                return NormalizeToken(token);
            if (value is string text)
                return text;
            if (value is bool boolean)
                return boolean;
            if (value is double number)
                return Float64(number);
            if (value is float single)
                return Float64(single);
            if (value is decimal decimalNumber)
                return Float64((double)decimalNumber);
            if (value is Enum enumeration)
                return Integer(Convert.ToInt64(enumeration, CultureInfo.InvariantCulture));
            if (value is long integer)
                return Integer(integer);
            if (value is int int32)
                return Integer(int32);
            if (value is short int16)
                return Integer(int16);
            if (value is sbyte int8)
                return Integer(int8);
            if (value is byte uint8)
                return Integer(uint8);
            if (value is ushort uint16)
                return Integer(uint16);
            if (value is uint uint32)
                return Integer(uint32);
            if (value is ulong uint64)
                return Integer(checked((long)uint64));
            if (value is SimVec2 vector)
                return Vec2(vector);
            if (value is SimVec2I vectorI)
                return Vec2I(vectorI);
            if (value is IDictionary dictionary)
                return NormalizeDictionary(dictionary);
            if (value is IEnumerable sequence)
                return NormalizeSequence(sequence);
            throw new InvalidOperationException("Unsupported canonical value type: " + value.GetType().FullName);
        }

        internal static byte[] Bytes(object? value)
        {
            var builder = new StringBuilder();
            WriteCanonical(builder, Normalize(value));
            return new UTF8Encoding(false).GetBytes(builder.ToString());
        }

        internal static string Sha256(byte[] bytes)
        {
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }

        internal static string PrettyNumber(double value)
        {
            string text = value.ToString("G15", CultureInfo.InvariantCulture);
            if (!text.Contains(".") && !text.Contains("E") && !text.Contains("e") &&
                !text.Contains("Infinity") && !text.Contains("NaN"))
                text += ".0";
            return text;
        }

        internal static void QuoteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20)
                            builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            builder.Append(character);
                        break;
                }
            }
            builder.Append('"');
        }

        private static object NormalizeDictionary(IDictionary dictionary)
        {
            var output = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
            {
                if (!(entry.Key is string key))
                    throw new InvalidOperationException("Canonical object key is not a string: " + entry.Key);
                output.Add(key, Normalize(entry.Value));
            }
            return output;
        }

        private static object NormalizeSequence(IEnumerable sequence)
        {
            var output = new List<object?>();
            foreach (object? item in sequence)
                output.Add(Normalize(item));
            return output;
        }

        private static object? NormalizeToken(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var output = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                    foreach (KeyValuePair<string, JToken?> pair in (JObject)token)
                        output.Add(pair.Key, NormalizeToken(pair.Value ?? JValue.CreateNull()));
                    return output;
                case JTokenType.Array:
                    var array = new List<object?>();
                    foreach (JToken item in (JArray)token)
                        array.Add(NormalizeToken(item));
                    return array;
                case JTokenType.Integer:
                    return Integer(token.Value<long>());
                case JTokenType.Float:
                    return Float64(token.Value<double>());
                case JTokenType.String:
                    return token.Value<string>() ?? "";
                case JTokenType.Boolean:
                    return token.Value<bool>();
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return null;
                case JTokenType.Raw:
                    return token.Value<string>() ?? "";
                default:
                    object? raw = ((JValue)token).Value;
                    if (raw == null)
                        return null;
                    if (raw is string || raw is SimVec2 || raw is SimVec2I || raw is bool ||
                        raw is double || raw is float || raw is long || raw is int)
                        return Normalize(raw);
                    throw new InvalidOperationException("Unsupported canonical JSON token: " + token.Type);
            }
        }

        private static void WriteCanonical(StringBuilder builder, object? value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }
            if (value is string text)
            {
                QuoteString(builder, text);
                return;
            }
            if (value is bool boolean)
            {
                builder.Append(boolean ? "true" : "false");
                return;
            }
            if (value is long integer)
            {
                builder.Append(integer.ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (value is SortedDictionary<string, object?> dictionary)
            {
                builder.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, object?> pair in dictionary)
                {
                    if (!first)
                        builder.Append(',');
                    first = false;
                    QuoteString(builder, pair.Key);
                    builder.Append(':');
                    WriteCanonical(builder, pair.Value);
                }
                builder.Append('}');
                return;
            }
            if (value is List<object?> array)
            {
                builder.Append('[');
                for (int i = 0; i < array.Count; ++i)
                {
                    if (i != 0)
                        builder.Append(',');
                    WriteCanonical(builder, array[i]);
                }
                builder.Append(']');
                return;
            }
            throw new InvalidOperationException("Unnormalized canonical value: " + value.GetType().FullName);
        }
    }
}
