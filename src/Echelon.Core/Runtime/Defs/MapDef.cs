using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Echelon.Core
{
    public sealed class MapDef
    {
        public sealed class Feature
        {
            public string Name { get; set; } = "";
            public List<SimVec2> Points { get; } = new List<SimVec2>();
            public double WidthM { get; set; }
            public double HeightM { get; set; }
        }

        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public SimVec2 SizeM { get; set; }
        public double CellM { get; set; }
        public int HeightSeed { get; set; }
        public double BaseM { get; set; }
        public double AmplitudeM { get; set; }
        public double Frequency { get; set; }
        public int Octaves { get; set; } = 4;
        public List<Feature> Ridges { get; } = new List<Feature>();
        public List<Feature> Rivers { get; } = new List<Feature>();
        public List<Feature> Roads { get; } = new List<Feature>();
        public List<Feature> Forests { get; } = new List<Feature>();
        public List<Feature> Towns { get; } = new List<Feature>();

        public static MapDef FromJObject(JObject data, DefReader reader)
        {
            MapDef map = new MapDef();
            map.Id = reader.ReqString(data, "id");
            map.DisplayName = reader.ReqString(data, "name");
            map.SizeM = reader.ReqVec2(data, "size_m");
            map.CellM = reader.ReqNumber(data, "cell_m");
            reader.CheckRange(map.CellM, 2.0, 100.0, "cell_m");
            if (map.SizeM.X < map.CellM || map.SizeM.Y < map.CellM)
            {
                reader.Error("size_m 이 cell_m 보다 작음");
            }

            JObject height = reader.ReqObject(data, "height");
            map.HeightSeed = reader.ReqInt(height, "seed");
            map.BaseM = reader.ReqNumber(height, "base_m");
            map.AmplitudeM = reader.ReqNumber(height, "amplitude_m");
            map.Frequency = reader.ReqNumber(height, "frequency");
            map.Octaves = reader.OptInt(height, "octaves", 4);
            ReadFeatures(data, "ridges", reader, true, true, 2, map.Ridges);
            ReadFeatures(data, "rivers", reader, true, false, 2, map.Rivers);
            ReadFeatures(data, "roads", reader, true, false, 2, map.Roads);
            ReadFeatures(data, "forests", reader, false, false, 3, map.Forests);
            ReadFeatures(data, "towns", reader, false, false, 3, map.Towns);
            return map;
        }

        private static void ReadFeatures(JObject data, string key, DefReader reader, bool linear,
            bool withHeight, int minimumPoints, List<Feature> output)
        {
            JArray raw = reader.OptArray(data, key);
            for (int i = 0; i < raw.Count; ++i)
            {
                JObject featureData = reader.AsObject(raw[i], key + "[" + i + "]");
                Feature feature = new Feature();
                feature.Name = reader.OptString(featureData, "name", key + "_" + i);
                JArray points = reader.ReqArray(featureData, "points");
                for (int j = 0; j < points.Count; ++j)
                {
                    feature.Points.Add(reader.AsVec2(points[j],
                        key + "[" + i + "].points[" + j + "]"));
                }

                if (feature.Points.Count < minimumPoints)
                {
                    reader.Error(key + "[" + i + "] 의 점이 " + minimumPoints + "개 미만");
                }

                if (linear)
                {
                    feature.WidthM = reader.ReqNumber(featureData, "width_m");
                }

                if (withHeight)
                {
                    feature.HeightM = reader.ReqNumber(featureData, "height_m");
                }

                output.Add(feature);
            }
        }
    }
}
