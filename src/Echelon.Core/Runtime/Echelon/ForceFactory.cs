using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class ForceFactory
    {
        public static readonly string[] CompanyLetters = { "A", "B", "C", "D", "E", "F" };
        public const double HqOffsetBehindM = 150.0;
        private const int EngineerMinefieldsPerPlatoon = EngineerSystem.MinefieldsPerPlatoon;

        public ForceFactory(Battle battle)
        {
            Battle = battle ?? throw new ArgumentNullException(nameof(battle));
            Catalog = battle.Catalog;
        }

        public Battle Battle { get; }
        public DataCatalog Catalog { get; }

        public TaskForce CreateTaskForce(string side, TOETemplate toe, SimVec2 pos, double heading)
        {
            if (toe == null)
            {
                throw new ArgumentNullException(nameof(toe));
            }

            TaskForce tf = new TaskForce
            {
                Side = side,
                Toe = toe,
                Faction = toe.Faction,
                DisplayName = toe.DisplayName,
                ShortName = "TF",
                TemplateId = toe.Id,
                Kind = "tf",
                CommandCapacityPerMin = toe.CommandCapacityPerMin
            };
            tf.Guide.Reset(pos, heading);
            Battle.RegisterUnit(tf);

            List<string> ids = new List<string>();
            for (int i = 0; i < toe.HqCount; ++i)
            {
                ids.Add(toe.HqElement);
            }

            Platoon cp = SpawnSection(ids, side, "cp", "대대 지휘소", "CP", pos, heading, tf);
            cp.IsHqSection = true;
            tf.CpSection = cp;
            tf.Commander = cp.Elements[0];
            Battle.TaskForces[side] = tf;
            return tf;
        }

        public Platoon SpawnPlatoon(UnitTemplate template, string side, SimVec2 pos, double heading, Unit parent,
            string display = "", string shortName = "", string loadout = "")
        {
            RequireEchelon(template, "platoon");
            Platoon p = new Platoon
            {
                Side = side,
                TemplateId = template.Id,
                Kind = template.Kind,
                Skill = template.Skill,
                Loadout = loadout != "" ? loadout : template.Loadouts.Count > 0 ? template.Loadouts[0].Id : "",
                DisplayName = display != "" ? display : template.DisplayName,
                ShortName = shortName != "" ? shortName : template.DisplayName,
                Formation = FormationOr(template.DefaultFormation, "wedge"),
                Parent = parent
            };
            p.Guide.Reset(pos, heading);
            Battle.RegisterUnit(p);

            foreach (string elementId in template.ElementsFor(p.Loadout))
            {
                AddElement(p, elementId);
            }

            List<Element> vehicles = p.Vehicles();
            int seat = 0;
            foreach (string squadId in template.PassengersFor(p.Loadout))
            {
                Element squad = AddElement(p, squadId);
                while (seat < vehicles.Count && vehicles[seat].Passengers.Count >= vehicles[seat].Def.TransportCapacity)
                {
                    ++seat;
                }

                if (seat < vehicles.Count)
                {
                    squad.MountedIn = vehicles[seat];
                    vehicles[seat].Passengers.Add(squad);
                }
            }

            p.Mounted = p.HasSquads() && p.HasTransport();
            if (p.Kind == "engineer")
            {
                p.MinesLeft = EngineerMinefieldsPerPlatoon;
            }

            p.Commander = p.Elements[0];
            p.InitialElementCount = p.Elements.Count;
            PlaceElements(p);
            return p;
        }

        public Platoon SpawnSection(IEnumerable<string> elementIds, string side, string kind, string display,
            string shortName, SimVec2 pos, double heading, Unit parent)
        {
            Platoon p = new Platoon
            {
                Side = side,
                Kind = kind,
                DisplayName = display,
                ShortName = shortName,
                Formation = FormationOr("line", "wedge"),
                Parent = parent
            };
            p.Guide.Reset(pos, heading);
            Battle.RegisterUnit(p);
            foreach (string elementId in elementIds)
            {
                AddElement(p, elementId);
            }

            p.Mounted = false;
            p.Commander = p.Elements.Count > 0 ? p.Elements[0] : null;
            p.InitialElementCount = p.Elements.Count;
            PlaceElements(p);
            return p;
        }

        public Company SpawnCompany(UnitTemplate template, string side, SimVec2 pos, double heading, Unit parent,
            int index, IReadOnlyList<UnitTemplate>? platoonTemplates = null, IReadOnlyList<string>? loadouts = null)
        {
            RequireEchelon(template, "company");
            string letter = CompanyLetters[index % CompanyLetters.Length];
            Company c = new Company
            {
                Side = side,
                TemplateId = template.Id,
                Kind = template.Kind,
                Skill = template.Skill,
                DisplayName = letter + " 중대 (" + template.DisplayName + ")",
                ShortName = letter,
                Formation = FormationOr(template.DefaultFormation, "co_wedge"),
                Parent = parent
            };
            c.Guide.Reset(pos, heading);
            Battle.RegisterUnit(c);

            SimVec2 hqPos = FormationSolver.LocalToWorld(new SimVec2(0.0, -HqOffsetBehindM), pos, heading);
            c.Hq = SpawnSection(template.HqElements, side, "hq", letter + " 중대본부", letter + "/HQ",
                Battle.Terrain.NearestPassable(hqPos, Terrain.MobilityVehicle), heading, c);
            c.Hq.IsHqSection = true;
            c.Commander = c.Hq.Elements[0];
            c.Xo = c.Hq.Elements.Count > 1 ? c.Hq.Elements[1] : null;

            List<UnitTemplate> platoons = platoonTemplates != null
                ? new List<UnitTemplate>(platoonTemplates)
                : new List<UnitTemplate>();
            if (platoons.Count == 0)
            {
                foreach (string platoonId in template.Platoons)
                {
                    platoons.Add(RequiredUnitTemplate(platoonId));
                }
            }

            List<SimVec2> slots = FormationSolver.WorldSlots(c.Formation!, platoons.Count, pos, heading);
            for (int i = 0; i < platoons.Count; ++i)
            {
                string loadout = loadouts != null && i < loadouts.Count ? loadouts[i] : "";
                Platoon p = SpawnPlatoon(platoons[i], side,
                    Battle.Terrain.NearestPassable(slots[i], Terrain.MobilityVehicle), heading, c,
                    letter + " 중대 " + (i + 1) + "소대", letter + "/" + (i + 1), loadout);
                c.Platoons.Add(p);
            }

            c.InitialElementCount = c.AllElements().Count;
            return c;
        }

        public TaskForce BuildTaskForce(ForceSpec spec, string side)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            TOETemplate toe = Catalog.GetToe(spec.Toe) ??
                throw new InvalidOperationException("편제표 없음: " + spec.Toe);
            Battle.Scenario.Sides.TryGetValue(side, out ScenarioDef.SideSetup? setup);
            MapRect zone = setup != null ? setup.DeployZone : new MapRect(SimVec2.Zero, Battle.Terrain.SizeM);
            ForceSpec deploySpec = spec.DuplicateSpec();
            Deployment.AutoFill(deploySpec, zone, Battle.Terrain.SizeM, new VehicleDeploymentTerrain(Battle.Terrain));
            SimVec2 entry = zone.Center;
            if (setup != null && setup.EntryPoints.Count > 0)
            {
                entry = setup.EntryPoints[SimMath.Clamp(spec.EntryPoint, 0, setup.EntryPoints.Count - 1)];
            }

            entry = Battle.Terrain.NearestPassable(Battle.Terrain.ClampToMap(entry, 30.0), Terrain.MobilityVehicle);
            double heading = Deployment.Facing(zone, Battle.Terrain.SizeM);
            ForceRules.Report report = ForceRules.Evaluate(Catalog, spec, setup != null ? setup.Budget : 100000);

            DeploymentPose cp = deploySpec.Deploy["cp"];
            TaskForce tf = CreateTaskForce(side, toe, cp.Position, cp.Heading);
            tf.DisplayName = spec.Name != "" ? spec.Name : toe.DisplayName;
            tf.ArtilleryMissions = spec.ArtilleryMissions;
            tf.CasSorties = spec.CasSorties;
            tf.EntryPos = entry;

            for (int i = 0; i < spec.Companies.Count; ++i)
            {
                CompanySpec companySpec = spec.Companies[i];
                UnitTemplate unitTemplate = RequiredUnitTemplate(companySpec.Template);
                List<UnitTemplate> platoons = new List<UnitTemplate>();
                List<string> loadouts = new List<string>();
                foreach (PlatoonSpec platoonSpec in companySpec.Platoons)
                {
                    platoons.Add(RequiredUnitTemplate(platoonSpec.Template));
                    loadouts.Add(platoonSpec.Loadout);
                }

                int wave = companySpec.Wave();
                string key = "co:" + companySpec.Slot;
                DeploymentPose at = deploySpec.Deploy.TryGetValue(key, out DeploymentPose saved)
                    ? saved
                    : new DeploymentPose(entry, heading);
                Company c = SpawnCompany(unitTemplate, side, at.Position, at.Heading, tf, i, platoons, loadouts);
                c.AdHoc = report.AdHoc.Contains(companySpec.Slot);
                c.Phase = wave;
                c.Hq!.Phase = wave;
                tf.Companies.Add(c);
                for (int j = 0; j < c.Platoons.Count; ++j)
                {
                    PlatoonSpec platoonSpec = companySpec.Platoons[j];
                    Platoon p = c.Platoons[j];
                    p.Phase = platoonSpec.Wave;
                    p.AdHoc = c.AdHoc;
                    if (platoonSpec.Home != companySpec.Slot && platoonSpec.Home != "")
                    {
                        int homeIndex = SlotIndex(spec, platoonSpec.Home);
                        string fromLetter = homeIndex >= 0 ? CompanyLetters[homeIndex % CompanyLetters.Length] : "?";
                        p.DisplayName += " (" + fromLetter + " 중대에서 배속)";
                    }

                    if (platoonSpec.Wave > 0)
                    {
                        SetOffMap(p, entry, heading);
                    }
                }

                if (wave > 0)
                {
                    SetOffMap(c, entry, heading);
                    SetOffMap(c.Hq, entry, heading);
                }
            }

            List<PlatoonSpec> organicSpecs = new List<PlatoonSpec>();
            List<string> organicKeys = new List<string>();
            for (int i = 0; i < spec.Organic.Count; ++i)
            {
                organicSpecs.Add(spec.Organic[i]);
                organicKeys.Add("org:" + i);
            }

            for (int i = 0; i < spec.Attachments.Count; ++i)
            {
                organicSpecs.Add(spec.Attachments[i]);
                organicKeys.Add("att:" + i);
            }

            for (int i = 0; i < organicSpecs.Count; ++i)
            {
                PlatoonSpec platoonSpec = organicSpecs[i];
                DeploymentPose at = deploySpec.Deploy.TryGetValue(organicKeys[i], out DeploymentPose saved)
                    ? saved
                    : new DeploymentPose(entry, heading);
                Platoon p = SpawnPlatoon(RequiredUnitTemplate(platoonSpec.Template), side, at.Position, at.Heading, tf,
                    "", "", platoonSpec.Loadout);
                p.Phase = platoonSpec.Wave;
                if (organicKeys[i].StartsWith("att:", StringComparison.Ordinal))
                {
                    p.Attached = true;
                }

                tf.Organic.Add(p);
                if (platoonSpec.Wave > 0)
                {
                    SetOffMap(p, entry, heading);
                }
            }

            return tf;
        }

        public void SetOffMap(Unit unit, SimVec2 entry, double heading)
        {
            unit.OnMap = false;
            unit.Guide.Reset(entry, heading);
            foreach (Element e in unit.AllElements())
            {
                e.OnMap = false;
                e.Pos = entry;
                e.PrevPos = entry;
                e.Heading = heading;
                e.PrevHeading = heading;
                e.Velocity = SimVec2.Zero;
                e.HasMoveTarget = false;
            }
        }

        public void Redeploy(Unit unit, SimVec2 pos, double heading)
        {
            if (unit is Company c)
            {
                c.Guide.Reset(pos, heading);
                SimVec2 hqPos = FormationSolver.LocalToWorld(new SimVec2(0.0, -HqOffsetBehindM), pos, heading);
                if (c.Hq != null && c.Hq.OnMap)
                {
                    c.Hq.Guide.Reset(Battle.Terrain.NearestPassable(hqPos, Terrain.MobilityVehicle), heading);
                    PlaceElements(c.Hq);
                }

                List<Platoon> platoons = new List<Platoon>();
                foreach (Platoon p in c.Platoons)
                {
                    if (p.OnMap)
                    {
                        platoons.Add(p);
                    }
                }

                List<SimVec2> slots = FormationSolver.WorldSlots(c.Formation!, platoons.Count, pos, heading);
                for (int i = 0; i < platoons.Count; ++i)
                {
                    platoons[i].Guide.Reset(Battle.Terrain.NearestPassable(slots[i], Terrain.MobilityVehicle), heading);
                    PlaceElements(platoons[i]);
                }
            }
            else if (unit is Platoon p)
            {
                p.Guide.Reset(Battle.Terrain.NearestPassable(pos, Terrain.MobilityVehicle), heading);
                PlaceElements(p);
            }
            else if (unit is TaskForce tf)
            {
                tf.Guide.Reset(pos, heading);
                if (tf.CpSection != null)
                {
                    tf.CpSection.Guide.Reset(Battle.Terrain.NearestPassable(pos, Terrain.MobilityVehicle), heading);
                    PlaceElements(tf.CpSection);
                }
            }
        }

        public void PlaceElements(Platoon p)
        {
            List<Element> members = p.FormationMembers();
            List<SimVec2> slots = FormationSolver.WorldSlots(p.Formation!, members.Count, p.Guide.Pos, p.Guide.Heading);
            for (int i = 0; i < members.Count; ++i)
            {
                Element e = members[i];
                e.Pos = Battle.Terrain.NearestPassable(slots[i], e.Mobility());
                e.PrevPos = e.Pos;
                e.Heading = p.Guide.Heading;
                e.PrevHeading = e.Heading;
                e.RestHeading = e.Heading;
                e.MoveTarget = e.Pos;
                e.HasMoveTarget = true;
                foreach (Element passenger in e.Passengers)
                {
                    passenger.Pos = e.Pos;
                    passenger.PrevPos = e.Pos;
                    passenger.Heading = e.Heading;
                }
            }
        }

        private static int SlotIndex(ForceSpec spec, string slotId)
        {
            for (int i = 0; i < spec.Companies.Count; ++i)
            {
                if (spec.Companies[i].Slot == slotId)
                {
                    return i;
                }
            }

            return -1;
        }

        private Element AddElement(Platoon p, string elementId)
        {
            ElementDef def = Catalog.GetElement(elementId) ??
                throw new InvalidOperationException("엘리먼트 정의 없음: " + elementId);
            Element e = new Element
            {
                Def = def,
                Side = p.Side,
                Platoon = p,
                IndexInPlatoon = p.Elements.Count,
                Hp = def.Hp,
                SupplyLeft = def.SupplyCapacity
            };
            foreach (KeyValuePair<string, int> pair in def.Ammo)
            {
                e.Ammo[pair.Key] = pair.Value;
            }

            foreach (string weaponId in def.Weapons)
            {
                e.Reload[weaponId] = 0.0;
            }

            e.Pos = p.Guide.Pos;
            e.PrevPos = e.Pos;
            p.Elements.Add(e);
            Battle.RegisterElement(e);
            return e;
        }

        private FormationDef FormationOr(string id, string fallback)
        {
            FormationDef? formation = id != "" ? Catalog.GetFormation(id) : null;
            formation = formation ?? Catalog.GetFormation(fallback);
            return formation ?? throw new InvalidOperationException("대형 정의 없음: " + (id != "" ? id : fallback));
        }

        private UnitTemplate RequiredUnitTemplate(string id)
        {
            return Catalog.GetUnitTemplate(id) ?? throw new InvalidOperationException("제대 템플릿 없음: " + id);
        }

        private static void RequireEchelon(UnitTemplate template, string echelon)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (template.Echelon != echelon)
            {
                throw new ArgumentException("Expected a " + echelon + " template.", nameof(template));
            }
        }

        private sealed class VehicleDeploymentTerrain : IDeploymentTerrain
        {
            private readonly Terrain _terrain;

            internal VehicleDeploymentTerrain(Terrain terrain)
            {
                _terrain = terrain;
            }

            public bool IsPassable(SimVec2 point)
            {
                return _terrain.IsPassable(point, Terrain.MobilityVehicle);
            }

            public SimVec2 NearestPassable(SimVec2 point)
            {
                return _terrain.NearestPassable(point, Terrain.MobilityVehicle);
            }
        }
    }
}
