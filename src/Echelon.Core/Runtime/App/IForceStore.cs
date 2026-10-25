using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Echelon.Core
{
    public interface IForceStore
    {
        string BasePath { get; }
        string PathFor(string fileName);
        void Save(ForceSpec spec, string fileName);
        ForceSpec? LoadSpec(string fileName);
        IReadOnlyList<string> ListSaved();
    }

    public sealed class FileForceStore : IForceStore
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public string BasePath { get; }

        public FileForceStore(string basePath)
        {
            if (string.IsNullOrWhiteSpace(basePath))
            {
                throw new ArgumentException("A force-store base path is required.", nameof(basePath));
            }

            BasePath = Path.GetFullPath(basePath);
        }

        public string PathFor(string fileName)
        {
            string safe = SanitizeFileName(fileName);
            return Path.Combine(BasePath, safe);
        }

        public void Save(ForceSpec spec, string fileName)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            string path = ResolvePath(fileName);
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, spec.ToJson(), Utf8WithoutBom);
        }

        public ForceSpec? LoadSpec(string fileName)
        {
            string path = ResolvePath(fileName);
            return File.Exists(path) ? ForceSpec.FromJson(File.ReadAllText(path)) : null;
        }

        public IReadOnlyList<string> ListSaved()
        {
            if (!Directory.Exists(BasePath))
            {
                return new List<string>();
            }

            return Directory.GetFiles(BasePath, "*.json", SearchOption.TopDirectoryOnly)
                .Where(path => path.EndsWith(".json", StringComparison.Ordinal))
                .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
                .ToList();
        }

        private string ResolvePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return PathFor(fileName);
            }

            if (!Path.IsPathRooted(fileName))
            {
                return PathFor(fileName);
            }

            string fullPath = Path.GetFullPath(fileName);
            string basePrefix = BasePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                                Path.DirectorySeparatorChar;
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!fullPath.StartsWith(basePrefix, comparison))
            {
                throw new UnauthorizedAccessException("Force files must remain under the configured base path.");
            }

            return fullPath;
        }

        private static string SanitizeFileName(string fileName)
        {
            string source = (fileName ?? "").Trim();
            HashSet<char> invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
            invalid.Add('/');
            invalid.Add('\\');
            invalid.Add(':');
            invalid.Add('*');
            invalid.Add('?');
            invalid.Add('"');
            invalid.Add('<');
            invalid.Add('>');
            invalid.Add('|');
            char[] characters = source.Select(character => invalid.Contains(character) || char.IsControl(character) ? '_' : character)
                .ToArray();
            string safe = new string(characters).Trim();
            if (safe == "")
            {
                safe = "tf";
            }

            if (!safe.EndsWith(".json", StringComparison.Ordinal))
            {
                safe += ".json";
            }

            return safe;
        }
    }
}
