using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Echelon.Core
{
    public static class DataLoader
    {
        public static DataCatalog LoadCatalog(string rootPath)
        {
            return LoadCatalog(new FileCatalogSource(rootPath));
        }

        public static DataCatalog LoadCatalog(ICatalogSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            DataCatalog catalog = new DataCatalog();
            DefReader reader = new DefReader();

            foreach (CatalogJsonFile file in Enumerate(source, CatalogCategories.Weapons, reader))
            {
                JObject? data = ReadObject(source, file, reader);
                if (data != null)
                {
                    Put(catalog, catalog.Weapons, WeaponDef.FromJObject(data, reader), value => value.Id, reader);
                }
            }

            foreach (CatalogJsonFile file in Enumerate(source, CatalogCategories.Elements, reader))
            {
                JObject? data = ReadObject(source, file, reader);
                if (data != null)
                {
                    Put(catalog, catalog.Elements, ElementDef.FromJObject(data, reader), value => value.Id, reader);
                }
            }

            List<CatalogJsonFile> formationFiles = Enumerate(source, CatalogCategories.Formations, reader);
            if (formationFiles.Count == 0)
            {
                reader.Context = "formations.json";
                reader.Error("파일 없음");
            }

            foreach (CatalogJsonFile file in formationFiles)
            {
                JObject? data = ReadObject(source, file, reader);
                if (data == null)
                {
                    continue;
                }

                reader.Context = file.Context;
                JArray formations = reader.ReqArray(data, "formations");
                for (int i = 0; i < formations.Count; ++i)
                {
                    JObject item = reader.AsObject(formations[i], "formations[" + i + "]");
                    if (item.Count == 0)
                    {
                        continue;
                    }

                    string id = i.ToString();
                    JToken? idToken = item["id"];
                    if (idToken?.Type == JTokenType.String)
                    {
                        id = idToken.Value<string>() ?? id;
                    }

                    reader.Context = file.Context + "#" + id;
                    Put(catalog, catalog.Formations, FormationDef.FromJObject(item, reader), value => value.Id, reader);
                }
            }

            foreach (CatalogJsonFile file in Enumerate(source, CatalogCategories.Units, reader))
            {
                JObject? data = ReadObject(source, file, reader);
                if (data != null)
                {
                    Put(catalog, catalog.UnitTemplates, UnitTemplate.FromJObject(data, reader), value => value.Id, reader);
                }
            }

            foreach (CatalogJsonFile file in Enumerate(source, CatalogCategories.Maps, reader))
            {
                JObject? data = ReadObject(source, file, reader);
                if (data != null)
                {
                    Put(catalog, catalog.Maps, MapDef.FromJObject(data, reader), value => value.Id, reader);
                }
            }

            foreach (CatalogJsonFile file in Enumerate(source, CatalogCategories.Toe, reader))
            {
                JObject? data = ReadObject(source, file, reader);
                if (data != null)
                {
                    Put(catalog, catalog.ToeTemplates, TOETemplate.FromJObject(data, reader), value => value.Id, reader);
                }
            }

            foreach (CatalogJsonFile file in Enumerate(source, CatalogCategories.Scenarios, reader))
            {
                JObject? data = ReadObject(source, file, reader);
                if (data != null)
                {
                    Put(catalog, catalog.Scenarios, ScenarioDef.FromJObject(data, reader), value => value.Id, reader);
                }
            }

            foreach (CatalogJsonFile file in Enumerate(source, CatalogCategories.Forces, reader))
            {
                JObject? data = ReadObject(source, file, reader);
                if (data == null)
                {
                    continue;
                }

                reader.Context = file.Context;
                ForceSpec spec = ForceSpec.FromJObject(data, reader);
                string id = Path.GetFileNameWithoutExtension(file.Name);
                if (catalog.Forces.ContainsKey(id))
                {
                    reader.Error("id '" + id + "' 가 중복 정의됨");
                }
                else
                {
                    catalog.Forces[id] = spec;
                }
            }

            ValidateReferences(catalog, reader);
            catalog.Errors = new List<string>(reader.Errors);
            catalog.Warnings = new List<string>(reader.Warnings);
            return catalog;
        }

        public static void ValidateReferences(DataCatalog catalog, DefReader reader)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            foreach (ElementDef element in catalog.Elements.Values)
            {
                reader.Context = "element '" + element.Id + "'";
                foreach (string weaponId in element.Weapons)
                {
                    if (!catalog.Weapons.ContainsKey(weaponId))
                    {
                        reader.Error("무기 '" + weaponId + "' 정의가 없음");
                    }
                }
            }

            foreach (FormationDef formation in catalog.Formations.Values)
            {
                reader.Context = "formation '" + formation.Id + "'";
                if (formation.Echelon == "platoon" && (!formation.Slots.ContainsKey(3) || !formation.Slots.ContainsKey(4)))
                {
                    reader.Error("소대 대형은 3·4 엘리먼트 슬롯을 모두 정의해야 함");
                }
            }

            foreach (UnitTemplate template in catalog.UnitTemplates.Values)
            {
                reader.Context = "unit '" + template.Id + "'";
                int cost = 0;
                List<string> ids = new List<string>(template.Elements);
                ids.AddRange(template.Passengers);
                ids.AddRange(template.HqElements);
                foreach (UnitTemplate.Loadout loadout in template.Loadouts)
                {
                    ids.AddRange(loadout.Replace.Values);
                }

                foreach (string elementId in ids)
                {
                    ElementDef? element = catalog.GetElement(elementId);
                    if (element == null)
                    {
                        reader.Error("엘리먼트 '" + elementId + "' 정의가 없음");
                    }
                    else if (element.Faction != template.Faction)
                    {
                        reader.Error("엘리먼트 '" + elementId + "' 의 진영(" + element.Faction + ")이 템플릿 진영과 다름");
                    }
                }

                foreach (string elementId in template.Elements)
                {
                    ElementDef? element = catalog.GetElement(elementId);
                    cost += element?.Cost ?? 0;
                }

                foreach (string elementId in template.Passengers)
                {
                    ElementDef? element = catalog.GetElement(elementId);
                    cost += element?.Cost ?? 0;
                }

                if (template.Passengers.Count != 0)
                {
                    int seats = 0;
                    foreach (string elementId in template.Elements)
                    {
                        seats += catalog.GetElement(elementId)?.TransportCapacity ?? 0;
                    }

                    if (seats < template.Passengers.Count)
                    {
                        reader.Error(string.Format("탑승 분대 {0} 개를 태울 좌석이 {1} 개뿐", template.Passengers.Count, seats));
                    }
                }

                if (template.DefaultFormation != "" && catalog.GetFormation(template.DefaultFormation) == null)
                {
                    reader.Error("default_formation '" + template.DefaultFormation + "' 가 없음");
                }

                template.Cost = cost;
            }

            foreach (UnitTemplate template in catalog.UnitTemplates.Values)
            {
                if (template.Echelon != "company")
                {
                    continue;
                }

                reader.Context = "unit '" + template.Id + "'";
                int cost = 0;
                foreach (string elementId in template.HqElements)
                {
                    cost += catalog.GetElement(elementId)?.Cost ?? 0;
                }

                foreach (string platoonId in template.Platoons)
                {
                    UnitTemplate? platoon = catalog.GetUnitTemplate(platoonId);
                    if (platoon == null)
                    {
                        reader.Error("소대 템플릿 '" + platoonId + "' 가 없음");
                    }
                    else if (platoon.Echelon != "platoon" || platoon.Faction != template.Faction)
                    {
                        reader.Error("'" + platoonId + "' 는 같은 진영의 소대 템플릿이어야 함");
                    }
                    else
                    {
                        cost += platoon.Cost;
                    }
                }

                template.Cost = cost;
            }

            foreach (TOETemplate toe in catalog.ToeTemplates.Values)
            {
                reader.Context = "toe '" + toe.Id + "'";
                ElementDef? hq = catalog.GetElement(toe.HqElement);
                if (hq == null)
                {
                    reader.Error("hq.element '" + toe.HqElement + "' 정의가 없음");
                }
                else if (hq.Faction != toe.Faction)
                {
                    reader.Error("hq.element '" + toe.HqElement + "' 의 진영(" + hq.Faction + ")이 템플릿 진영(" + toe.Faction + ")과 다름");
                }

                foreach (TOETemplate.CompanySlot slot in toe.CompanySlots)
                {
                    foreach (string reference in slot.Allowed)
                    {
                        CheckUnitRef(catalog, reader, reference, "company", toe.Faction);
                    }
                }

                foreach (string reference in toe.Organic)
                {
                    CheckUnitRef(catalog, reader, reference, "platoon", toe.Faction);
                }

                foreach (string weaponId in new[] { toe.ArtilleryWeapon, toe.CasWeapon })
                {
                    if (weaponId != "" && catalog.GetWeapon(weaponId) == null)
                    {
                        reader.Error("support 무기 '" + weaponId + "' 정의가 없음");
                    }
                }

                foreach (TOETemplate.AttachmentSlot slot in toe.AttachmentSlots)
                {
                    foreach (string reference in slot.Allowed)
                    {
                        CheckUnitRef(catalog, reader, reference, "platoon", toe.Faction);
                    }
                }
            }

            foreach (ScenarioDef scenario in catalog.Scenarios.Values)
            {
                reader.Context = "scenario '" + scenario.Id + "'";
                foreach (KeyValuePair<string, ScenarioDef.SideSetup> pair in scenario.Sides)
                {
                    ScenarioDef.SideSetup side = pair.Value;
                    TOETemplate? toe = catalog.GetToe(side.Toe);
                    if (toe == null)
                    {
                        reader.Error("sides." + pair.Key + ".toe '" + side.Toe + "' 정의가 없음");
                    }
                    else if (toe.Faction != side.Faction)
                    {
                        reader.Error("sides." + pair.Key + " 의 진영(" + side.Faction + ")과 toe 진영(" + toe.Faction + ")이 다름");
                    }
                }

                foreach (KeyValuePair<string, ScenarioDef.SideSetup> pair in scenario.Sides)
                {
                    ScenarioDef.SideSetup side = pair.Value;
                    foreach (KeyValuePair<string, ForceSpec> forcePair in catalog.Forces)
                    {
                        ForceSpec spec = forcePair.Value;
                        if (spec.Toe != side.Toe)
                        {
                            continue;
                        }

                        ForceRules.Report report = ForceRules.Evaluate(catalog, spec, side.Budget);
                        foreach (string message in report.Errors)
                        {
                            reader.Error("기본 편성 '" + forcePair.Key + "' (" + pair.Key + "): " + message);
                        }
                    }
                }

                MapDef? map = catalog.GetMap(scenario.MapId);
                if (map == null)
                {
                    reader.Error("지도 '" + scenario.MapId + "' 정의가 없음");
                }
                else if (map.SizeM != scenario.SizeM)
                {
                    reader.Error("size_m 이 지도(" + map.SizeM + ")와 다름");
                }
            }

            reader.Context = "";
        }

        private static void CheckUnitRef(DataCatalog catalog, DefReader reader, string reference,
            string echelon, string faction)
        {
            UnitTemplate? template = catalog.GetUnitTemplate(reference);
            if (template == null)
            {
                reader.Error("편제 템플릿 '" + reference + "' 가 없음");
            }
            else if (template.Echelon != echelon)
            {
                reader.Error("'" + reference + "' 는 " + echelon + " 템플릿이어야 함");
            }
            else if (template.Faction != faction)
            {
                reader.Error("'" + reference + "' 의 진영이 다름");
            }
        }

        private static List<CatalogJsonFile> Enumerate(ICatalogSource source, string category, DefReader reader)
        {
            try
            {
                return source.EnumerateJson(category).OrderBy(file => file.Name, StringComparer.Ordinal).ToList();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                reader.Context = category;
                reader.Error("카탈로그 열거 실패: " + exception.Message);
                return new List<CatalogJsonFile>();
            }
        }

        private static JObject? ReadObject(ICatalogSource source, CatalogJsonFile file, DefReader reader)
        {
            reader.Context = file.Context;
            string text;
            try
            {
                text = source.ReadText(file);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                reader.Error("파일 읽기 실패: " + exception.Message);
                return null;
            }

            JToken token;
            try
            {
                token = JToken.Parse(text);
            }
            catch (JsonException exception)
            {
                int line = exception is JsonReaderException lineException ? lineException.LineNumber : 0;
                reader.Error("JSON 파싱 오류 " + line + "행: " + exception.Message);
                return null;
            }

            if (!(token is JObject obj))
            {
                reader.Error("최상위가 객체({})여야 함");
                return null;
            }

            reader.Context = file.Context;
            return obj;
        }

        private static void Put<T>(DataCatalog catalog, Dictionary<string, T> target, T definition,
            Func<T, string> id, DefReader reader) where T : class
        {
            string definitionId = id(definition);
            if (definitionId == "")
            {
                return;
            }

            if (target.ContainsKey(definitionId))
            {
                reader.Error("id '" + definitionId + "' 가 중복 정의됨");
                return;
            }

            target[definitionId] = definition;
            catalog.NoteLoaded(definition);
        }
    }
}
