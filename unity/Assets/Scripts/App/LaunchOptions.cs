using System;
using System.Globalization;
using Echelon.Core;
using UnityEngine;

namespace Echelon.Unity.App
{
    /// <summary>
    /// 명령줄 옵션. 자동 검증(스모크·스크린샷·AAR 저장)과 디버그 시작 상태에 쓴다.
    /// 예: -- --scene=skirmish --start --ai-vs-ai --aar-out=build/aar.json
    /// </summary>
    public sealed class LaunchOptions
    {
        public const int DefaultScreenshotFrames = 30;

        public string Scene = "";
        public bool AutoStart;
        public bool AiVsAi;
        public bool NoFog;
        public string AarOut = "";
        public float RunSeconds = -1f;
        public int SmokeTicks = -1;
        public string ScreenshotPath = "";
        public int ScreenshotFrames = DefaultScreenshotFrames;
        public int SeriesCount = 1;
        public int SeriesInterval = 1;
        public bool HasCamera;
        public Vector2 CameraFocus;
        public float CameraDistance;
        public float CameraPitch = 55f;
        public float CameraYaw;
        public bool RedAttack;
        public Vector2 RedAttackPosition;
        public bool FireDebug;
        public int FireAsset;
        public SimVec2 FirePosition;
        public bool FireSmoke;
        public int SelectCompany = -1;
        public bool OpenGuide;
        public string[] SupportCompanies = Array.Empty<string>();
        public bool AirLaunch;
        public Vector2 AirTarget;

        /// <summary>자동 검증 실행이면 오류 로그가 나오는 즉시 실패 코드로 끝낸다.</summary>
        public bool IsAutomated => SmokeTicks >= 0 || ScreenshotPath != "";

        public static LaunchOptions Parse(string[] args)
        {
            var options = new LaunchOptions();
            foreach (string arg in args ?? Array.Empty<string>())
            {
                int split = arg.IndexOf('=');
                string value = split >= 0 ? arg.Substring(split + 1) : "";
                string[] parts = value.Split(',');
                if (arg.StartsWith("--smoke-ticks="))
                    options.SmokeTicks = Int(value);
                else if (arg.StartsWith("--screenshot-frames="))
                    options.ScreenshotFrames = Int(value);
                else if (arg.StartsWith("--screenshot-series="))
                {
                    options.SeriesCount = Math.Max(1, Int(parts[0]));
                    options.SeriesInterval = Math.Max(1, parts.Length > 1 ? Int(parts[1]) : 1);
                }
                else if (arg.StartsWith("--screenshot="))
                    options.ScreenshotPath = value;
                else if (arg.StartsWith("--scene="))
                    options.Scene = value;
                else if (arg == "--sandbox")
                    options.Scene = "sandbox";
                else if (arg == "--start")
                    options.AutoStart = true;
                else if (arg == "--ai-vs-ai")
                    options.AiVsAi = true;
                else if (arg == "--nofog")
                    options.NoFog = true;
                else if (arg.StartsWith("--aar-out="))
                    options.AarOut = value;
                else if (arg == "--guide")
                    options.OpenGuide = true;
                else if (arg.StartsWith("--support="))
                    options.SupportCompanies = parts;
                else if (arg.StartsWith("--air=") && parts.Length >= 2)
                {
                    options.AirLaunch = true;
                    options.AirTarget = new Vector2(Float(parts[0]), Float(parts[1]));
                }
                else if (arg.StartsWith("--select="))
                    options.SelectCompany = Int(value);
                else if (arg.StartsWith("--run-sec="))
                    options.RunSeconds = Float(value);
                else if (arg.StartsWith("--camera=") && parts.Length >= 3)
                {
                    options.HasCamera = true;
                    options.CameraFocus = new Vector2(Float(parts[0]), Float(parts[1]));
                    options.CameraDistance = Float(parts[2]);
                    options.CameraPitch = parts.Length > 3 ? Float(parts[3]) : 55f;
                    options.CameraYaw = parts.Length > 4 ? Float(parts[4]) : 0f;
                }
                else if (arg.StartsWith("--red-attack=") && parts.Length >= 2)
                {
                    options.RedAttack = true;
                    options.RedAttackPosition = new Vector2(Float(parts[0]), Float(parts[1]));
                }
                else if (arg.StartsWith("--fire=") && parts.Length >= 3)
                {
                    options.FireDebug = true;
                    options.FireAsset = Int(parts[0]);
                    options.FirePosition = new SimVec2(Float(parts[1]), Float(parts[2]));
                    options.FireSmoke = parts.Length > 3;
                }
            }
            return options;
        }

        private static int Int(string text)
        {
            return int.Parse(text, CultureInfo.InvariantCulture);
        }

        private static float Float(string text)
        {
            return float.Parse(text, CultureInfo.InvariantCulture);
        }
    }
}
