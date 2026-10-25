using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Echelon.Core
{
    public sealed class DefReader
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public string Context { get; set; } = "";

        public void Error(string message)
        {
            Errors.Add(string.IsNullOrEmpty(Context) ? message : Context + ": " + message);
        }

        public void Warn(string message)
        {
            Warnings.Add(string.IsNullOrEmpty(Context) ? message : Context + ": " + message);
        }

        public bool HasErrors()
        {
            return Errors.Count != 0;
        }

        public static bool IsNumber(JToken? value)
        {
            if (value == null || (value.Type != JTokenType.Integer && value.Type != JTokenType.Float))
            {
                return false;
            }

            try
            {
                double number = value.Type == JTokenType.Integer ? value.Value<long>() : value.Value<double>();
                return !double.IsNaN(number) && !double.IsInfinity(number);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is FormatException ||
                                               exception is OverflowException || exception is InvalidCastException)
            {
                return false;
            }
        }

        public string ReqString(JObject data, string key)
        {
            return Require(data, key) ? AsString(data[key], key) : "";
        }

        public double ReqNumber(JObject data, string key)
        {
            return Require(data, key) ? AsNumber(data[key], key) : 0.0;
        }

        public int ReqInt(JObject data, string key)
        {
            return Require(data, key) ? AsInt(data[key], key) : 0;
        }

        public bool ReqBool(JObject data, string key)
        {
            return Require(data, key) ? AsBool(data[key], key) : false;
        }

        public JObject ReqObject(JObject data, string key)
        {
            return Require(data, key) ? AsObject(data[key], key) : new JObject();
        }

        public JArray ReqArray(JObject data, string key)
        {
            return Require(data, key) ? AsArray(data[key], key) : new JArray();
        }

        public List<string> ReqStringArray(JObject data, string key)
        {
            return Require(data, key) ? AsStringArray(data[key], key) : new List<string>();
        }

        public SimVec2 ReqVec2(JObject data, string key)
        {
            return Require(data, key) ? AsVec2(data[key], key) : SimVec2.Zero;
        }

        public string OptString(JObject data, string key, string defaultValue = "")
        {
            return Has(data, key) ? AsString(data[key], key) : defaultValue;
        }

        public double OptNumber(JObject data, string key, double defaultValue = 0.0)
        {
            return Has(data, key) ? AsNumber(data[key], key) : defaultValue;
        }

        public int OptInt(JObject data, string key, int defaultValue = 0)
        {
            return Has(data, key) ? AsInt(data[key], key) : defaultValue;
        }

        public bool OptBool(JObject data, string key, bool defaultValue = false)
        {
            return Has(data, key) ? AsBool(data[key], key) : defaultValue;
        }

        public JObject OptObject(JObject data, string key)
        {
            return Has(data, key) ? AsObject(data[key], key) : new JObject();
        }

        public JArray OptArray(JObject data, string key)
        {
            return Has(data, key) ? AsArray(data[key], key) : new JArray();
        }

        public List<string> OptStringArray(JObject data, string key)
        {
            return Has(data, key) ? AsStringArray(data[key], key) : new List<string>();
        }

        public void CheckRange(double value, double minimum, double maximum, string what)
        {
            if (value < minimum || value > maximum || double.IsNaN(value))
            {
                Error(string.Format(CultureInfo.InvariantCulture,
                    "'{0}' = {1}, 허용 범위 {2} ~ {3}", what, value, minimum, maximum));
            }
        }

        public void CheckOneOf(string value, IEnumerable<string> allowed, string what)
        {
            ICollection<string> collection = allowed as ICollection<string> ?? new List<string>(allowed);
            if (!collection.Contains(value))
            {
                Error("'" + what + "' = '" + value + "', 허용값 [" + string.Join(", ", collection) + "]");
            }
        }

        public SimVec2 AsVec2(JToken? value, string what)
        {
            if (!(value is JArray array))
            {
                Error("'" + what + "'는 [x, y] 배열이어야 함");
                return SimVec2.Zero;
            }

            if (array.Count != 2 || !IsNumber(array[0]) || !IsNumber(array[1]))
            {
                Error("'" + what + "'는 숫자 2개짜리 [x, y] 배열이어야 함");
                return SimVec2.Zero;
            }

            double x = NumberValue(array[0]);
            double y = NumberValue(array[1]);
            if (x < -float.MaxValue || x > float.MaxValue || y < -float.MaxValue || y > float.MaxValue)
            {
                Error("'" + what + "'의 좌표가 float 범위를 벗어남");
                return SimVec2.Zero;
            }

            return new SimVec2(x, y);
        }

        public JObject AsObject(JToken? value, string what)
        {
            if (value is JObject obj)
            {
                return obj;
            }

            Error("'" + what + "'는 객체({})여야 함");
            return new JObject();
        }

        public JArray AsArray(JToken? value, string what)
        {
            if (value is JArray array)
            {
                return array;
            }

            Error("'" + what + "'는 배열이어야 함");
            return new JArray();
        }

        public string AsString(JToken? value, string what)
        {
            if (value != null && value.Type == JTokenType.String)
            {
                return value.Value<string>() ?? "";
            }

            Error("'" + what + "'는 문자열이어야 함");
            return "";
        }

        public bool AsBool(JToken? value, string what)
        {
            if (value != null && value.Type == JTokenType.Boolean)
            {
                return value.Value<bool>();
            }

            Error("'" + what + "'는 true/false 여야 함");
            return false;
        }

        public double AsNumber(JToken? value, string what)
        {
            if (!IsNumber(value))
            {
                Error("'" + what + "'는 숫자여야 함");
                return 0.0;
            }

            return NumberValue(value!);
        }

        public int AsInt(JToken? value, string what)
        {
            if (!IsNumber(value))
            {
                Error("'" + what + "'는 정수여야 함");
                return 0;
            }

            double number = NumberValue(value!);
            if (number != Math.Floor(number))
            {
                Error(string.Format(CultureInfo.InvariantCulture, "'{0}'는 정수여야 함 (값 {1})", what, number));
            }

            if (number < int.MinValue || number > int.MaxValue)
            {
                Error("'" + what + "'는 32비트 정수 범위를 벗어남");
                return 0;
            }

            return (int)number;
        }

        public List<string> AsStringArray(JToken? value, string what)
        {
            List<string> result = new List<string>();
            foreach (JToken item in AsArray(value, what))
            {
                if (item.Type != JTokenType.String)
                {
                    Error("'" + what + "'의 원소는 문자열이어야 함");
                    continue;
                }

                result.Add(item.Value<string>() ?? "");
            }

            return result;
        }

        private static bool Has(JObject data, string key)
        {
            return data.Property(key) != null;
        }

        private bool Require(JObject data, string key)
        {
            if (Has(data, key))
            {
                return true;
            }

            Error("필수 필드 누락 '" + key + "'");
            return false;
        }

        private static double NumberValue(JToken value)
        {
            return value.Type == JTokenType.Integer
                ? value.Value<long>()
                : value.Value<double>();
        }
    }
}
