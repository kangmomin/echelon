using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Echelon.Core
{
    public sealed class DataCatalog
    {
        private readonly List<string> _formationOrder = new List<string>();
        private readonly List<string> _toeOrder = new List<string>();

        public Dictionary<string, ElementDef> Elements { get; } = new Dictionary<string, ElementDef>();
        public Dictionary<string, WeaponDef> Weapons { get; } = new Dictionary<string, WeaponDef>();
        public Dictionary<string, FormationDef> Formations { get; } = new Dictionary<string, FormationDef>();
        public Dictionary<string, TOETemplate> ToeTemplates { get; } = new Dictionary<string, TOETemplate>();
        public Dictionary<string, ScenarioDef> Scenarios { get; } = new Dictionary<string, ScenarioDef>();
        public Dictionary<string, UnitTemplate> UnitTemplates { get; } = new Dictionary<string, UnitTemplate>();
        public Dictionary<string, MapDef> Maps { get; } = new Dictionary<string, MapDef>();
        public Dictionary<string, ForceSpec> Forces { get; } = new Dictionary<string, ForceSpec>();
        public List<string> Errors { get; internal set; } = new List<string>();
        public List<string> Warnings { get; internal set; } = new List<string>();

        public bool IsValid()
        {
            return Errors.Count == 0;
        }

        public ElementDef? GetElement(string id)
        {
            return Elements.TryGetValue(id, out ElementDef? value) ? value : null;
        }

        public WeaponDef? GetWeapon(string id)
        {
            return Weapons.TryGetValue(id, out WeaponDef? value) ? value : null;
        }

        public FormationDef? GetFormation(string id)
        {
            return Formations.TryGetValue(id, out FormationDef? value) ? value : null;
        }

        public TOETemplate? GetToe(string id)
        {
            return ToeTemplates.TryGetValue(id, out TOETemplate? value) ? value : null;
        }

        public ScenarioDef? GetScenario(string id)
        {
            return Scenarios.TryGetValue(id, out ScenarioDef? value) ? value : null;
        }

        public UnitTemplate? GetUnitTemplate(string id)
        {
            return UnitTemplates.TryGetValue(id, out UnitTemplate? value) ? value : null;
        }

        public MapDef? GetMap(string id)
        {
            return Maps.TryGetValue(id, out MapDef? value) ? value : null;
        }

        public List<UnitTemplate> UnitTemplatesFor(string faction, string echelon = "")
        {
            return UnitTemplates.Values
                .Where(template => template.Faction == faction && (echelon == "" || template.Echelon == echelon))
                .OrderBy(template => template.Id, System.StringComparer.Ordinal)
                .ToList();
        }

        public List<FormationDef> FormationsFor(string echelon)
        {
            List<FormationDef> result = new List<FormationDef>();
            foreach (string id in _formationOrder)
            {
                FormationDef? formation = GetFormation(id);
                if (formation != null && formation.Echelon == echelon)
                {
                    result.Add(formation);
                }
            }

            if (_formationOrder.Count == 0)
            {
                result.AddRange(Formations.Values.Where(formation => formation.Echelon == echelon));
            }

            return result;
        }

        public string Summary()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "elements {0}, weapons {1}, formations {2}, units {3}, toe {4}, maps {5}, scenarios {6}, forces {7}, errors {8}, warnings {9}",
                Elements.Count, Weapons.Count, Formations.Count, UnitTemplates.Count, ToeTemplates.Count,
                Maps.Count, Scenarios.Count, Forces.Count, Errors.Count, Warnings.Count);
        }

        public ForceSpec? DefaultForce(string faction)
        {
            foreach (string key in Forces.Keys.OrderBy(key => key, System.StringComparer.Ordinal))
            {
                ForceSpec force = Forces[key];
                if (force.Faction == faction)
                {
                    return force.DuplicateSpec();
                }
            }

            foreach (string id in _toeOrder)
            {
                TOETemplate? toe = GetToe(id);
                if (toe != null && toe.Faction == faction)
                {
                    return ForceSpec.FromToe(this, toe.Id);
                }
            }

            if (_toeOrder.Count == 0)
            {
                foreach (TOETemplate toe in ToeTemplates.Values.OrderBy(toe => toe.Id, System.StringComparer.Ordinal))
                {
                    if (toe.Faction == faction)
                    {
                        return ForceSpec.FromToe(this, toe.Id);
                    }
                }
            }

            return null;
        }

        internal void NoteLoaded(object definition)
        {
            if (definition is FormationDef formation)
            {
                _formationOrder.Add(formation.Id);
            }
            else if (definition is TOETemplate toe)
            {
                _toeOrder.Add(toe.Id);
            }
        }
    }
}
