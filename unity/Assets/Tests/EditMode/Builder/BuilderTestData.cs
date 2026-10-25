using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Echelon.Core;
using Echelon.Unity.UI.Builder;
using NUnit.Framework;
using UnityEngine;

namespace Echelon.Unity.Tests.Builder
{
    internal static class BuilderTestData
    {
        private static DataCatalog _catalog;

        internal static DataCatalog Catalog => _catalog ??= LoadCatalog();
        internal static ScenarioDef Scenario => Catalog.GetScenario("skirmish_01");

        internal static ForceBuilderController Us(MemoryForceStore store = null, ForceSpec initial = null)
        {
            return new ForceBuilderController(Catalog, Scenario, store ?? new MemoryForceStore(), initial);
        }

        internal static string Errors(ForceRules.Report report)
        {
            return string.Join("\n", report.Errors);
        }

        internal static int Round(double value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private static DataCatalog LoadCatalog()
        {
            string overrideRoot = Environment.GetEnvironmentVariable("ECHELON_REPOSITORY_ROOT");
            string root = !string.IsNullOrEmpty(overrideRoot)
                ? overrideRoot
                : Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            DataCatalog catalog = DataLoader.LoadCatalog(Path.Combine(root, "data"));
            Assert.That(catalog.Errors, Is.Empty, string.Join("\n", catalog.Errors));
            return catalog;
        }
    }

    internal sealed class MemoryForceStore : IForceStore
    {
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>(StringComparer.Ordinal);

        public string BasePath => "memory://forces";

        public string PathFor(string fileName)
        {
            return BasePath + "/" + SafeName(fileName);
        }

        public void Save(ForceSpec spec, string fileName)
        {
            _files[SafeName(fileName)] = spec.ToJson();
        }

        public ForceSpec LoadSpec(string fileName)
        {
            string name = SafeName(fileName);
            return _files.TryGetValue(name, out string json) ? ForceSpec.FromJson(json) : null;
        }

        public IReadOnlyList<string> ListSaved()
        {
            return _files.Keys.OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => BasePath + "/" + name).ToList();
        }

        private static string SafeName(string fileName)
        {
            string value = (fileName ?? string.Empty).Replace('\\', '/');
            int slash = value.LastIndexOf('/');
            if (slash >= 0)
                value = value.Substring(slash + 1);
            value = value.Trim();
            if (value.Length == 0)
                value = "tf";
            return value.EndsWith(".json", StringComparison.Ordinal) ? value : value + ".json";
        }
    }
}
