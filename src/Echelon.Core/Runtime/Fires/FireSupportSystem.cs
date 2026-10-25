using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class FireSupportSystem : SimSystem
    {
        public enum Asset
        {
            MORTAR,
            ARTILLERY,
            CAS
        }

        public enum Phase
        {
            PROCESSING,
            ADJUSTING,
            EFFECT,
            DONE,
            CANCELLED
        }

        public static readonly string[] AssetNames = { "박격포", "포병", "근접항공지원" };
        public static readonly string[] PhaseNames = { "처리 중", "수정 사격", "효력사", "완료", "취소" };
        public static readonly double[] FdcSec = { 30.0, 100.0, 75.0 };
        public static readonly double[] HopSec = { 10.0, 15.0, 15.0 };
        public static readonly double[] RandomSec = { 20.0, 30.0, 40.0 };
        public static readonly double[] SigmaM = { 35.0, 50.0, 20.0 };
        public const double AdjustSigmaMult = 3.0;
        public const double CorrectionSec = 15.0;
        public static readonly double[] ObserverSigma = { 1.0, 1.5, 2.0 };
        public static readonly double[] ObserverDelay = { 0.0, 10.0, 30.0 };
        public const int MortarRoundsPerTube = 6;
        public const int MortarSmokePerTube = 3;
        public const double MortarShellSpeed = 250.0;
        public const double ArtilleryVolleySec = 15.0;
        // 지도 위 포병 중대(자주포)가 쏘는 포병 임무. 지도 밖 임무를 쓰지 않고, 포대가 살아 있고 탄이 있는 동안
        // 몇 번이고 쏜다. 사격 지휘가 가까워 처리 시간이 짧다(D-050).
        public const double OnMapArtilleryFdcSec = 45.0;
        public const int ArtilleryRoundsPerGun = 3;
        public const int ArtillerySmokePerGun = 2;
        public const double ArtilleryShellSpeed = 450.0;
        public static readonly double[] SmokeRadius = { 35.0, 50.0, 0.0 };
        public static readonly double[] SmokeSec = { 120.0, 150.0, 0.0 };
        public const double TargetPointH = 1.5;
        public const double ObserveRadiusM = 0.0;

        public sealed class Mission
        {
            public int Id { get; set; }
            public string Side { get; set; } = "blue";
            public Asset Asset { get; set; } = Asset.MORTAR;
            public bool Smoke { get; set; }
            public SimVec2 Target { get; set; } = SimVec2.Zero;
            public int ObserverId { get; set; } = -1;
            public int ObserverQuality { get; set; } = 2;
            public Phase Phase { get; set; } = Phase.PROCESSING;
            public long RequestedTick { get; set; }
            public long ReadyTick { get; set; }
            public long NextRoundTick { get; set; }
            public int RoundsLeft { get; set; }
            public int RoundsFired { get; set; }
            public int ShooterUnitId { get; set; } = -1;
            public string Weapon { get; set; } = "";
            public double Sigma { get; set; }
            public double DelaySec { get; set; }
            public SimVec2 FirstImpact { get; set; } = SimVec2.Inf;

            public string Label()
            {
                return string.Format("{0}{1}", AssetNames[(int)Asset], Smoke ? " 연막" : "");
            }
        }

        public sealed class Shell
        {
            public int MissionId { get; set; }
            public SimVec2 Pos { get; set; }
            public long LandTick { get; set; }
            public string Weapon { get; set; } = "";
            public bool Smoke { get; set; }
            public Asset Asset { get; set; }
            public int SourceId { get; set; } = -1;
            public string Side { get; set; } = "";
        }

        private readonly IFireSupportBattle _battle;
        private readonly List<Shell> _shells = new List<Shell>();
        private int _nextId = 1;

        public FireSupportSystem(IFireSupportBattle battle)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
        }

        public List<Mission> Missions { get; private set; } = new List<Mission>();
        public IReadOnlyList<Shell> Shells => _shells;

        public override string GetSystemName()
        {
            return "fires";
        }

        public override long StateHash()
        {
            long hash = 0;
            foreach (Mission mission in Missions)
            {
                hash = SimHash.Append(hash, mission.Id);
                hash = SimHash.Append(hash, (int)mission.Phase);
                hash = SimHash.Append(hash, mission.RoundsLeft);
            }

            return hash;
        }

        public List<Mission> ActiveMissions(string side)
        {
            List<Mission> output = new List<Mission>();
            foreach (Mission mission in Missions)
            {
                if (mission.Side == side && mission.Phase != Phase.DONE && mission.Phase != Phase.CANCELLED)
                {
                    output.Add(mission);
                }
            }

            return output;
        }

        public Dictionary<string, object> Request(string side, Asset asset, SimVec2 target, bool smoke = false)
        {
            TaskForce? taskForce = _battle.GetTaskForce(side);
            if (taskForce == null || taskForce.Leader() == null)
            {
                return Fail("대대 지휘소가 없습니다.");
            }

            if (asset == Asset.CAS && smoke)
            {
                return Fail("항공은 연막을 쓸 수 없습니다.");
            }

            target = _battle.Terrain.ClampToMap(target, 10.0);
            long tick = _battle.World.Tick;
            Mission mission = new Mission
            {
                Side = side,
                Asset = asset,
                Smoke = smoke,
                Target = target,
                RequestedTick = tick
            };
            Element? observer = FindObserver(side, target, asset == Asset.CAS);
            if (observer != null)
            {
                mission.ObserverId = observer.Id;
                mission.ObserverQuality = ObserverQuality(observer, asset);
            }
            else if (asset == Asset.CAS)
            {
                return Fail("항공 통제관이나 관측반이 표적을 보고 있어야 합니다.");
            }

            int assetIndex = (int)asset;
            double delay = FdcSec[assetIndex] + ObserverDelay[mission.ObserverQuality];
            if (observer?.Platoon != null)
            {
                double path = CommsToTaskForce(observer.Platoon, taskForce, HopSec[assetIndex]);
                if (path < 0.0)
                {
                    return Fail("관측자 통신 두절");
                }

                delay += path;
            }

            TOETemplate? toe = taskForce.Toe;
            switch (asset)
            {
                case Asset.MORTAR:
                    Platoon? mortar = FindMortar(side, target);
                    if (mortar == null)
                    {
                        return Fail("사거리 안에 쏠 수 있는 박격포 소대가 없습니다.");
                    }

                    foreach (Mission other in ActiveMissions(side))
                    {
                        if (other.ShooterUnitId == mortar.Id)
                        {
                            return Fail(string.Format("{0} 가 다른 임무를 쏘는 중입니다.", mortar.Label()));
                        }
                    }

                    CommsLink link = _battle.LinkBetween(taskForce, mortar);
                    if (!link.Ok)
                    {
                        return Fail("박격포 소대 통신 두절");
                    }

                    delay += HopSec[assetIndex] * CommsNet.DelayFactor(link.Quality) +
                        link.Relays * CommsNet.RelayDelaySec;
                    mission.ShooterUnitId = mortar.Id;
                    mission.Weapon = IndirectWeapon(mortar);
                    mission.RoundsLeft = mortar.AliveElements().Count *
                        (smoke ? MortarSmokePerTube : MortarRoundsPerTube);
                    break;
                case Asset.ARTILLERY:
                    Platoon? battery = FindBattery(side, target, "artillery", true);
                    double batteryPath = battery != null ? CommsToTaskForce(battery, taskForce, HopSec[assetIndex]) : -1.0;
                    if (battery != null && batteryPath >= 0.0)
                    {
                        delay += OnMapArtilleryFdcSec - FdcSec[assetIndex] + batteryPath;
                        mission.ShooterUnitId = battery.Id;
                        mission.Weapon = IndirectWeapon(battery);
                        mission.RoundsLeft = battery.AliveElements().Count *
                            (smoke ? ArtillerySmokePerGun : ArtilleryRoundsPerGun);
                        break;
                    }

                    if (taskForce.ArtilleryMissions <= 0)
                    {
                        return Fail(battery != null ? "포병 중대 통신 두절" : "남은 포병 사격 임무가 없습니다.");
                    }

                    mission.Weapon = toe?.ArtilleryWeapon ?? "";
                    mission.RoundsLeft = toe != null ? toe.BatteryGuns * toe.RoundsPerGun : 18;
                    break;
                case Asset.CAS:
                    if (taskForce.CasSorties <= 0)
                    {
                        return Fail("남은 항공 출격이 없습니다.");
                    }

                    mission.Weapon = toe?.CasWeapon ?? "";
                    mission.RoundsLeft = toe?.CasBombs ?? 2;
                    break;
            }

            if (_battle.Catalog.GetWeapon(mission.Weapon) == null)
            {
                return Fail("화력 자산 무기 정의가 없습니다.");
            }

            if (!_battle.TrySpendCommand(side, 1.0))
            {
                return Fail("지휘 용량이 부족합니다.");
            }

            if (asset == Asset.ARTILLERY && mission.ShooterUnitId < 0)
            {
                --taskForce.ArtilleryMissions;
            }
            else if (asset == Asset.CAS)
            {
                --taskForce.CasSorties;
            }

            delay += _battle.World.Rng.NextRange(0.0, RandomSec[assetIndex]);
            mission.DelaySec = delay;
            mission.ReadyTick = tick + SecondsToTicks(delay);
            mission.Sigma = SigmaM[assetIndex] * ObserverSigma[mission.ObserverQuality];
            mission.Id = _nextId++;
            Missions.Add(mission);
            _battle.World.Events.EmitEvent("fire_requested", tick, new Dictionary<string, object>
            {
                ["side"] = side,
                ["mission"] = mission.Id,
                ["asset"] = (int)asset,
                ["smoke"] = smoke,
                ["pos"] = target,
                ["seconds"] = delay,
                ["observer"] = mission.ObserverId,
                ["quality"] = mission.ObserverQuality
            });
            return new Dictionary<string, object> { ["ok"] = true, ["mission"] = mission };
        }

        public Dictionary<string, object> Preview(string side, Asset asset, SimVec2 target)
        {
            TaskForce? taskForce = _battle.GetTaskForce(side);
            if (taskForce == null)
            {
                return new Dictionary<string, object> { ["error"] = "전투단 없음" };
            }

            Element? observer = FindObserver(side, target, asset == Asset.CAS);
            int quality = observer != null ? ObserverQuality(observer, asset) : 2;
            if (asset == Asset.CAS && observer == null)
            {
                return new Dictionary<string, object> { ["error"] = "표적을 보는 통제관 없음" };
            }

            int assetIndex = (int)asset;
            double delay = FdcSec[assetIndex] + ObserverDelay[quality] + RandomSec[assetIndex] * 0.5;
            if (observer?.Platoon != null)
            {
                double path = CommsToTaskForce(observer.Platoon, taskForce, HopSec[assetIndex]);
                if (path < 0.0)
                {
                    return new Dictionary<string, object> { ["error"] = "관측자 통신 두절" };
                }

                delay += path;
            }

            if (asset == Asset.MORTAR)
            {
                Platoon? mortar = FindMortar(side, target);
                if (mortar == null)
                {
                    return new Dictionary<string, object> { ["error"] = "박격포 사거리 밖" };
                }

                delay += HopSec[assetIndex];
            }
            else if (asset == Asset.ARTILLERY && FindBattery(side, target, "artillery", true) != null)
            {
                delay += OnMapArtilleryFdcSec - FdcSec[assetIndex] + HopSec[assetIndex];
            }
            else if (asset == Asset.ARTILLERY && taskForce.ArtilleryMissions <= 0)
            {
                return new Dictionary<string, object> { ["error"] = "포병 임무 없음" };
            }
            else if (asset == Asset.CAS && taskForce.CasSorties <= 0)
            {
                return new Dictionary<string, object> { ["error"] = "항공 출격 없음" };
            }

            return new Dictionary<string, object>
            {
                ["quality"] = quality,
                ["seconds"] = delay,
                ["observer"] = observer?.Id ?? -1
            };
        }

        public Element? FindObserver(string side, SimVec2 target, bool cas)
        {
            Element? best = null;
            int bestQuality = 99;
            double bestDistance = double.PositiveInfinity;
            foreach (Element element in _battle.Elements)
            {
                if (element.Side != side || !element.IsActive() || element.IsMounted())
                {
                    continue;
                }

                bool isForwardObserver = element.Def.ElementClass == "fo";
                if (cas && !isForwardObserver)
                {
                    continue;
                }

                double distance = element.Pos.DistanceTo(target);
                if (distance > element.Def.OpticsM)
                {
                    continue;
                }

                int quality = isForwardObserver ? 0 : 1;
                if (quality > bestQuality || (quality == bestQuality && distance >= bestDistance))
                {
                    continue;
                }

                if (!Los.Check(_battle.Terrain, _battle.Smokes, _battle.World.Tick, element.Pos,
                    Los.EyeHeight(element), target, TargetPointH))
                {
                    continue;
                }

                best = element;
                bestQuality = quality;
                bestDistance = distance;
            }

            return best;
        }

        public static int ObserverQuality(Element element, Asset asset)
        {
            if (element.Def.ElementClass != "fo")
            {
                return 1;
            }

            if (asset == Asset.CAS)
            {
                return element.Def.Id.Contains("jtac") ? 0 : 1;
            }

            return 0;
        }

        public Platoon? FindMortar(string side, SimVec2 target)
        {
            return FindBattery(side, target, "mortar", false);
        }

        /// <summary>사거리 안에서 탄이 남은 가장 가까운 지도 위 사격 소대. skipBusy 면 다른 임무를 쏘는 소대는 건너뛴다.</summary>
        public Platoon? FindBattery(string side, SimVec2 target, string kind, bool skipBusy)
        {
            TaskForce? taskForce = _battle.GetTaskForce(side);
            if (taskForce == null)
            {
                return null;
            }

            Platoon? best = null;
            double bestDistance = double.PositiveInfinity;
            foreach (Platoon platoon in taskForce.AllPlatoons())
            {
                if (platoon.Kind != kind || !platoon.OnMap || platoon.IsDestroyed())
                {
                    continue;
                }

                if (skipBusy && IsBusy(side, platoon))
                {
                    continue;
                }

                string weaponId = IndirectWeapon(platoon);
                WeaponDef? weapon = _battle.Catalog.GetWeapon(weaponId);
                if (weapon == null)
                {
                    continue;
                }

                bool hasAmmo = false;
                foreach (Element element in platoon.AliveElements())
                {
                    if (element.Ammo.TryGetValue(weaponId, out int ammo) && ammo > 0)
                    {
                        hasAmmo = true;
                    }
                }

                double distance = platoon.Centroid().DistanceTo(target);
                if (!hasAmmo || distance > weapon.RangeMaxM || distance < weapon.RangeMinM)
                {
                    continue;
                }

                if (distance < bestDistance)
                {
                    best = platoon;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private bool IsBusy(string side, Platoon platoon)
        {
            foreach (Mission other in ActiveMissions(side))
            {
                if (other.ShooterUnitId == platoon.Id)
                {
                    return true;
                }
            }

            return false;
        }

        public string IndirectWeapon(Platoon platoon)
        {
            foreach (Element element in platoon.Elements)
            {
                foreach (string weaponId in element.Def.Weapons)
                {
                    WeaponDef? weapon = _battle.Catalog.GetWeapon(weaponId);
                    if (weapon != null && weapon.Indirect)
                    {
                        return weapon.Id;
                    }
                }
            }

            return "";
        }

        public override void Step(SimWorld world)
        {
            long tick = world.Tick;
            foreach (Mission mission in Missions)
            {
                if (mission.Phase != Phase.DONE && mission.Phase != Phase.CANCELLED)
                {
                    Advance(mission, tick);
                }
            }

            if (_shells.Count != 0)
            {
                List<Shell> keep = new List<Shell>();
                foreach (Shell shell in _shells)
                {
                    if (tick >= shell.LandTick)
                    {
                        Land(shell, tick);
                    }
                    else
                    {
                        keep.Add(shell);
                    }
                }

                _shells.Clear();
                _shells.AddRange(keep);
            }

            if (tick % 600 == 0)
            {
                Prune(tick);
            }
        }

        private void Advance(Mission mission, long tick)
        {
            switch (mission.Phase)
            {
                case Phase.PROCESSING:
                    if (tick < mission.ReadyTick)
                    {
                        return;
                    }

                    if (mission.Asset == Asset.CAS)
                    {
                        CasPass(mission, tick);
                        return;
                    }

                    if (mission.ObserverQuality < 2)
                    {
                        mission.Phase = Phase.ADJUSTING;
                        if (FireRound(mission, tick, mission.Sigma * AdjustSigmaMult))
                        {
                            mission.NextRoundTick = tick + SecondsToTicks(CorrectionSec);
                        }

                        EmitPhase(mission, tick);
                    }
                    else
                    {
                        mission.Phase = Phase.EFFECT;
                        mission.NextRoundTick = tick;
                        EmitPhase(mission, tick);
                    }

                    break;
                case Phase.ADJUSTING:
                    if (tick < mission.NextRoundTick)
                    {
                        return;
                    }

                    Element? observer = _battle.GetElement(mission.ObserverId);
                    if (observer == null || !observer.IsActive() ||
                        !Los.Check(_battle.Terrain, _battle.Smokes, tick, observer.Pos, Los.EyeHeight(observer),
                            mission.Target, TargetPointH))
                    {
                        mission.Sigma = SigmaM[(int)mission.Asset] * ObserverSigma[2];
                    }

                    mission.Phase = Phase.EFFECT;
                    mission.NextRoundTick = tick;
                    EmitPhase(mission, tick);
                    break;
                case Phase.EFFECT:
                    if (tick < mission.NextRoundTick)
                    {
                        return;
                    }

                    if (mission.RoundsLeft <= 0)
                    {
                        Finish(mission, tick);
                        return;
                    }

                    if (mission.ShooterUnitId >= 0)
                    {
                        MortarVolley(mission, tick);
                    }
                    else if (mission.Asset == Asset.ARTILLERY)
                    {
                        TaskForce? taskForce = _battle.GetTaskForce(mission.Side);
                        int guns = taskForce?.Toe?.BatteryGuns ?? 6;
                        int count = Math.Min(guns, mission.RoundsLeft);
                        for (int i = 0; i < count; ++i)
                        {
                            FireRound(mission, tick, mission.Sigma);
                        }

                        mission.NextRoundTick = tick + SecondsToTicks(ArtilleryVolleySec);
                    }

                    break;
            }
        }

        private void MortarVolley(Mission mission, long tick)
        {
            Platoon? platoon = _battle.GetUnit(mission.ShooterUnitId) as Platoon;
            WeaponDef? weapon = _battle.Catalog.GetWeapon(mission.Weapon);
            if (platoon == null || platoon.IsDestroyed() || weapon == null)
            {
                Finish(mission, tick);
                return;
            }

            int fired = 0;
            bool anyAmmo = false;
            long interval = SecondsToTicks(60.0 / Math.Max(weapon.RofRpm, 0.1));
            foreach (Element element in platoon.AliveElements())
            {
                int ammo = element.Ammo.TryGetValue(mission.Weapon, out int current) ? current : 0;
                if (ammo <= 0)
                {
                    continue;
                }

                anyAmmo = true;
                if (element.Velocity.Length() > 0.5 || mission.RoundsLeft <= 0)
                {
                    continue;
                }

                element.Ammo[mission.Weapon] = ammo - 1;
                element.LastFiredTick = tick;
                FireRound(mission, tick, mission.Sigma, element);
                ++fired;
            }

            if (!anyAmmo)
            {
                Finish(mission, tick);
                return;
            }

            mission.NextRoundTick = tick + (fired > 0 ? interval : SimConst.TickRate);
        }

        private bool FireRound(Mission mission, long tick, double sigma, Element? source = null)
        {
            long timeOfFlightTicks = 0;
            if (mission.ShooterUnitId >= 0)
            {
                if (source == null)
                {
                    Platoon? platoon = _battle.GetUnit(mission.ShooterUnitId) as Platoon;
                    if (platoon == null)
                    {
                        return false;
                    }

                    foreach (Element element in platoon.AliveElements())
                    {
                        int ammo = element.Ammo.TryGetValue(mission.Weapon, out int current) ? current : 0;
                        if (ammo > 0)
                        {
                            source = element;
                            element.Ammo[mission.Weapon] = ammo - 1;
                            break;
                        }
                    }

                    if (source == null)
                    {
                        return false;
                    }
                }

                double shellSpeed = mission.Asset == Asset.MORTAR ? MortarShellSpeed : ArtilleryShellSpeed;
                timeOfFlightTicks = SecondsToTicks(source.Pos.DistanceTo(mission.Target) / shellSpeed);
            }

            Shell shell = new Shell
            {
                MissionId = mission.Id,
                Pos = _battle.Terrain.ClampToMap(mission.Target + _battle.World.Rng.NextScatter(sigma), 5.0),
                Weapon = mission.Weapon,
                Smoke = mission.Smoke,
                Asset = mission.Asset,
                Side = mission.Side,
                SourceId = source?.Id ?? -1
            };
            shell.LandTick = tick + Math.Max(1, timeOfFlightTicks);
            _shells.Add(shell);
            --mission.RoundsLeft;
            ++mission.RoundsFired;
            if (source != null)
            {
                _battle.World.Events.EmitEvent("indirect_fire", tick, new Dictionary<string, object>
                {
                    ["from"] = source.Id,
                    ["side"] = mission.Side,
                    ["pos"] = source.Pos
                });
            }

            return true;
        }

        private void Land(Shell shell, long tick)
        {
            Mission? mission = FindMission(shell.MissionId);
            if (mission != null && mission.FirstImpact == SimVec2.Inf)
            {
                mission.FirstImpact = shell.Pos;
            }

            _battle.World.Events.EmitEvent("shell", tick, new Dictionary<string, object>
            {
                ["pos"] = shell.Pos,
                ["smoke"] = shell.Smoke,
                ["asset"] = (int)shell.Asset,
                ["side"] = shell.Side,
                ["radius"] = shell.Smoke ? SmokeRadius[(int)shell.Asset] : 0.0
            });
            if (shell.Smoke)
            {
                _battle.Smokes.Add(new Los.Smoke(shell.Pos, SmokeRadius[(int)shell.Asset],
                    tick + SecondsToTicks(SmokeSec[(int)shell.Asset])));
                return;
            }

            WeaponDef? weapon = _battle.Catalog.GetWeapon(shell.Weapon);
            if (weapon != null)
            {
                _battle.IndirectImpact(shell.Pos, weapon, shell.Side, _battle.GetElement(shell.SourceId), tick);
            }
        }

        private void CasPass(Mission mission, long tick)
        {
            foreach (Element element in _battle.Elements)
            {
                if (element.Side == mission.Side || !element.IsActive() || element.IsMounted())
                {
                    continue;
                }

                foreach (string weaponId in element.Def.Weapons)
                {
                    WeaponDef? weapon = _battle.Catalog.GetWeapon(weaponId);
                    if (weapon == null || !weapon.CanTarget("air") ||
                        !element.Ammo.TryGetValue(weapon.Id, out int ammo) || ammo <= 0)
                    {
                        continue;
                    }

                    if (element.Pos.DistanceTo(mission.Target) > weapon.RangeMaxM)
                    {
                        continue;
                    }

                    element.Ammo[weapon.Id] = ammo - 1;
                    element.LastFiredTick = tick;
                    _battle.World.Events.EmitEvent("sam_launch", tick, new Dictionary<string, object>
                    {
                        ["from"] = element.Id,
                        ["side"] = element.Side,
                        ["pos"] = element.Pos,
                        ["to"] = mission.Target
                    });
                    if (_battle.World.Rng.Chance(weapon.AccuracyBase))
                    {
                        mission.Phase = Phase.CANCELLED;
                        _battle.World.Events.EmitEvent("cas_shot_down", tick, new Dictionary<string, object>
                        {
                            ["side"] = mission.Side,
                            ["mission"] = mission.Id,
                            ["pos"] = mission.Target,
                            ["by"] = element.Id
                        });
                        return;
                    }

                    break;
                }
            }

            mission.Phase = Phase.EFFECT;
            EmitPhase(mission, tick);
            while (mission.RoundsLeft > 0)
            {
                FireRound(mission, tick, mission.Sigma);
            }

            Finish(mission, tick);
        }

        private void Finish(Mission mission, long tick)
        {
            mission.Phase = Phase.DONE;
            EmitPhase(mission, tick);
        }

        private void EmitPhase(Mission mission, long tick)
        {
            _battle.World.Events.EmitEvent("fire_mission", tick, new Dictionary<string, object>
            {
                ["side"] = mission.Side,
                ["mission"] = mission.Id,
                ["asset"] = (int)mission.Asset,
                ["phase"] = (int)mission.Phase,
                ["smoke"] = mission.Smoke,
                ["pos"] = mission.Target
            });
        }

        private Mission? FindMission(int missionId)
        {
            foreach (Mission mission in Missions)
            {
                if (mission.Id == missionId)
                {
                    return mission;
                }
            }

            return null;
        }

        private void Prune(long tick)
        {
            List<Mission> keep = new List<Mission>();
            foreach (Mission mission in Missions)
            {
                if ((mission.Phase == Phase.DONE || mission.Phase == Phase.CANCELLED) &&
                    tick - mission.ReadyTick > 1200)
                {
                    continue;
                }

                keep.Add(mission);
            }

            Missions = keep;
            for (int i = _battle.Smokes.Count - 1; i >= 0; --i)
            {
                if (_battle.Smokes[i].UntilTick <= tick)
                {
                    _battle.Smokes.RemoveAt(i);
                }
            }
        }

        private double CommsToTaskForce(Platoon platoon, TaskForce taskForce, double hop)
        {
            double total = 0.0;
            if (platoon.Parent != null && platoon.Parent.Level == UnitLevel.Company)
            {
                double first = LinkDelay(platoon.Parent, platoon, hop);
                if (first < 0.0)
                {
                    return -1.0;
                }

                double second = LinkDelay(taskForce, platoon.Parent, hop);
                if (second < 0.0)
                {
                    return -1.0;
                }

                total += first + second;
            }
            else if (ReferenceEquals(platoon.Parent, taskForce))
            {
                double link = LinkDelay(taskForce, platoon, hop);
                if (link < 0.0)
                {
                    return -1.0;
                }

                total += link;
            }

            return total;
        }

        private double LinkDelay(Unit fromUnit, Unit toUnit, double hop)
        {
            CommsLink link = _battle.LinkBetween(fromUnit, toUnit);
            if (!link.Ok)
            {
                return -1.0;
            }

            return hop * CommsNet.DelayFactor(link.Quality) + link.Relays * CommsNet.RelayDelaySec;
        }

        private static Dictionary<string, object> Fail(string message)
        {
            return new Dictionary<string, object> { ["ok"] = false, ["error"] = message };
        }

        private static long SecondsToTicks(double seconds)
        {
            return (long)SimMath.Round(seconds * SimConst.TickRate);
        }
    }
}
