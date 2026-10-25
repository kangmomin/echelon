using System;
using System.Collections.Generic;
using System.Linq;
using Echelon.Core;
using UnityEngine;

namespace Echelon.Unity.Platform
{
    public sealed class UnityCatalogSource : ICatalogSource
    {
        private readonly Dictionary<string, TextAsset> _files = new Dictionary<string, TextAsset>(StringComparer.Ordinal);

        public UnityCatalogSource()
        {
            LoadCategory(CatalogCategories.Weapons);
            LoadCategory(CatalogCategories.Elements);
            LoadCategory(CatalogCategories.Formations);
            LoadCategory(CatalogCategories.Units);
            LoadCategory(CatalogCategories.Maps);
            LoadCategory(CatalogCategories.Toe);
            LoadCategory(CatalogCategories.Scenarios);
            LoadCategory(CatalogCategories.Forces);
        }

        public IEnumerable<CatalogJsonFile> EnumerateJson(string category)
        {
            if (category == CatalogCategories.Formations)
            {
                string key = Key(category, "formations.json");
                if (_files.ContainsKey(key))
                    yield return new CatalogJsonFile(category, "formations.json", key);
                yield break;
            }
            ValidateCategory(category);
            string prefix = category + "/";
            foreach (TextAsset asset in _files
                         .Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                         .Select(pair => pair.Value)
                         .OrderBy(asset => asset.name, StringComparer.Ordinal))
            {
                string name = asset.name + ".json";
                yield return new CatalogJsonFile(category, name, Key(category, name));
            }
        }

        public string ReadText(CatalogJsonFile file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));
            if (!_files.TryGetValue(Key(file.Category, file.Name), out TextAsset asset))
                throw new ArgumentException("Catalog JSON is not included: " + file.Context, nameof(file));
            return asset.text;
        }

        private void LoadCategory(string category)
        {
            if (category == CatalogCategories.Formations)
            {
                TextAsset formations = Resources.Load<TextAsset>("GameData/formations");
                if (formations != null)
                    _files[Key(category, "formations.json")] = formations;
                return;
            }
            ValidateCategory(category);
            foreach (TextAsset asset in Resources.LoadAll<TextAsset>("GameData/" + category))
                _files[Key(category, asset.name + ".json")] = asset;
        }

        private static string Key(string category, string name)
        {
            return category + "/" + name;
        }

        private static void ValidateCategory(string category)
        {
            if (category == CatalogCategories.Formations)
                return;
            if (category != CatalogCategories.Weapons && category != CatalogCategories.Elements &&
                category != CatalogCategories.Units && category != CatalogCategories.Maps &&
                category != CatalogCategories.Toe && category != CatalogCategories.Scenarios &&
                category != CatalogCategories.Forces)
            {
                throw new ArgumentException("Unknown catalog category '" + category + "'.", nameof(category));
            }
        }
    }
}
