using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    internal static class DataTestAssets
    {
        private static DataCatalog? _catalog;

        private static readonly Lazy<string> Root = new Lazy<string>(FindRepositoryRoot);

        internal static string RepositoryRoot => Root.Value;
        internal static string DataRoot => Path.Combine(RepositoryRoot, "data");
        internal static string GoldenRoot => Path.Combine(RepositoryRoot, "tests", "golden");
        internal static DataCatalog Catalog => _catalog ??= FreshCatalog();

        internal static DataCatalog FreshCatalog()
        {
            return DataLoader.LoadCatalog(DataRoot);
        }

        internal static ForceSpec Us()
        {
            ForceSpec? spec = Catalog.DefaultForce("US");
            Assert.That(spec, Is.Not.Null);
            return spec!;
        }

        internal static ForceSpec Ru()
        {
            ForceSpec? spec = Catalog.DefaultForce("RU");
            Assert.That(spec, Is.Not.Null);
            return spec!;
        }

        internal static string ReadData(string category, string name)
        {
            return File.ReadAllText(Path.Combine(DataRoot, category, name));
        }

        internal static bool HasMessage(IEnumerable<string> messages, string fragment)
        {
            return messages.Any(message => message.Contains(fragment));
        }

        internal static string Join(IEnumerable<string> messages)
        {
            return string.Join("\n", messages);
        }

        private static string FindRepositoryRoot()
        {
            string? overrideRoot = Environment.GetEnvironmentVariable("ECHELON_REPOSITORY_ROOT");
            if (!string.IsNullOrEmpty(overrideRoot) &&
                File.Exists(Path.Combine(overrideRoot, "data", "formations.json")))
            {
                return Path.GetFullPath(overrideRoot);
            }

            string[] starts =
            {
                TestContext.CurrentContext.TestDirectory,
                Directory.GetCurrentDirectory()
            };
            foreach (string start in starts)
            {
                DirectoryInfo? current = new DirectoryInfo(start);
                while (current != null)
                {
                    if (File.Exists(Path.Combine(current.FullName, "data", "formations.json")) &&
                        File.Exists(Path.Combine(current.FullName, "tests", "golden", "map01.json")))
                    {
                        return current.FullName;
                    }

                    current = current.Parent;
                }
            }

            throw new DirectoryNotFoundException("Could not locate the Echelon repository data directory.");
        }
    }

    internal sealed class InMemoryCatalogSource : ICatalogSource
    {
        private readonly Dictionary<CatalogJsonFile, string> _documents = new Dictionary<CatalogJsonFile, string>();

        internal void Add(string category, string name, string text)
        {
            CatalogJsonFile file = new CatalogJsonFile(category, name, "memory://" + category + "/" + name);
            _documents[file] = text;
        }

        public IEnumerable<CatalogJsonFile> EnumerateJson(string category)
        {
            return _documents.Keys.Where(file => file.Category == category)
                .OrderBy(file => file.Name, StringComparer.Ordinal).ToList();
        }

        public string ReadText(CatalogJsonFile file)
        {
            if (!_documents.TryGetValue(file, out string? text))
            {
                throw new FileNotFoundException(file.Context);
            }

            return text;
        }
    }
}
