using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Echelon.Core
{
    public sealed class CatalogJsonFile
    {
        public string Category { get; }
        public string Name { get; }
        public string Context { get; }

        public CatalogJsonFile(string category, string name, string context)
        {
            Category = category ?? throw new ArgumentNullException(nameof(category));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Context = context ?? name;
        }
    }

    public interface ICatalogSource
    {
        IEnumerable<CatalogJsonFile> EnumerateJson(string category);
        string ReadText(CatalogJsonFile file);
    }

    public static class CatalogCategories
    {
        public const string Weapons = "weapons";
        public const string Elements = "elements";
        public const string Formations = "formations";
        public const string Units = "units";
        public const string Maps = "maps";
        public const string Toe = "toe";
        public const string Scenarios = "scenarios";
        public const string Forces = "forces";
    }

    public sealed class FileCatalogSource : ICatalogSource
    {
        public string RootPath { get; }

        public FileCatalogSource(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("A catalog root path is required.", nameof(rootPath));
            }

            RootPath = Path.GetFullPath(rootPath);
        }

        public IEnumerable<CatalogJsonFile> EnumerateJson(string category)
        {
            if (category == CatalogCategories.Formations)
            {
                string formationPath = ResolvePath(CatalogCategories.Formations, "formations.json");
                if (File.Exists(formationPath))
                {
                    yield return new CatalogJsonFile(category, "formations.json", formationPath);
                }

                yield break;
            }

            string directory = ResolveDirectory(category);
            if (!Directory.Exists(directory))
            {
                yield break;
            }

            foreach (string path in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                         .Where(path => path.EndsWith(".json", StringComparison.Ordinal))
                         .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal))
            {
                yield return new CatalogJsonFile(category, Path.GetFileName(path), path);
            }
        }

        public string ReadText(CatalogJsonFile file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            return File.ReadAllText(ResolvePath(file.Category, file.Name));
        }

        private string ResolveDirectory(string category)
        {
            switch (category)
            {
                case CatalogCategories.Weapons:
                case CatalogCategories.Elements:
                case CatalogCategories.Units:
                case CatalogCategories.Maps:
                case CatalogCategories.Toe:
                case CatalogCategories.Scenarios:
                case CatalogCategories.Forces:
                    return Path.Combine(RootPath, category);
                default:
                    throw new ArgumentException("Unknown catalog category '" + category + "'.", nameof(category));
            }
        }

        private string ResolvePath(string category, string name)
        {
            if (string.IsNullOrEmpty(name) || Path.IsPathRooted(name) ||
                Path.GetFileName(name) != name || !name.EndsWith(".json", StringComparison.Ordinal))
            {
                throw new ArgumentException("Catalog files must be direct JSON file names.", nameof(name));
            }

            if (category == CatalogCategories.Formations)
            {
                if (name != "formations.json")
                {
                    throw new ArgumentException("The formations category contains only formations.json.", nameof(name));
                }

                return Path.Combine(RootPath, name);
            }

            return Path.Combine(ResolveDirectory(category), name);
        }
    }
}
