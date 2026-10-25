using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Echelon.Core;
using UnityEngine;

namespace Echelon.Unity.Platform
{
    public static class UnityForceStore
    {
        public static IForceStore Create()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new PlayerPrefsForceStore();
#else
            return new FileForceStore(Path.Combine(Application.persistentDataPath, "forces"));
#endif
        }
    }

    public sealed class PlayerPrefsForceStore : IForceStore
    {
        private readonly string _prefsPrefix;
        private readonly string _indexKey;

        public string BasePath { get; }

        public PlayerPrefsForceStore(string virtualRoot = "playerprefs://forces", string prefsPrefix = "Echelon.Forces")
        {
            BasePath = virtualRoot.TrimEnd('/');
            _prefsPrefix = prefsPrefix;
            _indexKey = prefsPrefix + ".Index";
        }

        public string PathFor(string fileName)
        {
            return BasePath + "/" + ResolveName(fileName);
        }

        public void Save(ForceSpec spec, string fileName)
        {
            if (spec == null)
                throw new ArgumentNullException(nameof(spec));
            string safe = ResolveName(fileName);
            PlayerPrefs.SetString(PrefsKey(safe), spec.ToJson());
            List<string> names = ReadNames();
            if (!names.Contains(safe, StringComparer.Ordinal))
            {
                names.Add(safe);
                names.Sort(StringComparer.Ordinal);
                PlayerPrefs.SetString(_indexKey, string.Join("\n", names));
            }
            PlayerPrefs.Save();
        }

        public ForceSpec LoadSpec(string fileName)
        {
            string safe = ResolveName(fileName);
            string key = PrefsKey(safe);
            return PlayerPrefs.HasKey(key) ? ForceSpec.FromJson(PlayerPrefs.GetString(key)) : null;
        }

        public IReadOnlyList<string> ListSaved()
        {
            return ReadNames().Select(name => BasePath + "/" + name).ToList();
        }

        private List<string> ReadNames()
        {
            string value = PlayerPrefs.GetString(_indexKey, "");
            return value.Length == 0
                ? new List<string>()
                : value.Split('\n').Where(name => name.Length > 0).ToList();
        }

        private string PrefsKey(string safeName)
        {
            return _prefsPrefix + "." + safeName;
        }

        private string ResolveName(string fileName)
        {
            string source = (fileName ?? "").Trim();
            string prefix = BasePath + "/";
            if (source.StartsWith(prefix, StringComparison.Ordinal))
                source = source.Substring(prefix.Length);
            else if (Path.IsPathRooted(source))
                throw new UnauthorizedAccessException("Force files must remain under the configured base path.");
            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars()) { '/', '\\', ':' };
            char[] characters = source.Select(character => invalid.Contains(character) || char.IsControl(character) ? '_' : character).ToArray();
            string safe = new string(characters).Trim();
            if (safe == "")
                safe = "tf";
            if (!safe.EndsWith(".json", StringComparison.Ordinal))
                safe += ".json";
            return safe;
        }
    }
}
