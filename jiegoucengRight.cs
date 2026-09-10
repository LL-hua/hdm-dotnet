using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace jiegouceng {
    public class RightPoint2D {
        public double X { get; set; }
        public double Y { get; set; }
        public RightPoint2D(double x, double y) { X = x; Y = y; }
    }

    public class RightJiegoucengConfig {
        public double StartStation { get; set; }
        public double EndStation { get; set; }
        public int LayerIndex { get; set; }
        public string LayerName { get; set; }
        public double Thickness { get; set; }
        public double InnerStepWidth { get; set; }
        public double InnerSlope { get; set; }
        public double OuterStepWidth { get; set; }
        public double OuterSlope { get; set; }
    }

    public static class RightJiegoucengManager {
        public static List<RightJiegoucengConfig> ParseFile(string filePath) {
            var list = new List<RightJiegoucengConfig>();
            using (var reader = new StreamReader(filePath, Encoding.UTF8)) {
                reader.ReadLine(); // 跳过表头
                string line;
                while ((line = reader.ReadLine()) != null) {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] parts = line.Split(',');
                    list.Add(new RightJiegoucengConfig {
                        StartStation = double.Parse(parts[0].Trim()),
                        EndStation = double.Parse(parts[1].Trim()),
                        LayerIndex = int.Parse(parts[2].Trim()),
                        LayerName = parts[3].Trim(),
                        Thickness = double.Parse(parts[4].Trim()),
                        InnerStepWidth = double.Parse(parts[5].Trim()),
                        InnerSlope = double.Parse(parts[6].Trim()),
                        OuterStepWidth = double.Parse(parts[7].Trim()),
                        OuterSlope = double.Parse(parts[8].Trim())
                    });
                }
            }
            return list;
        }

        public static List<RightPoint2D[]> ComputeCoordinates(double station, RightPoint2D centerPoint, double crossfall, List<RightJiegoucengConfig> configs, out RightPoint2D[] subgradeLine) {
            var polygons = new List<RightPoint2D[]>();

            var activeLayers = configs.Where(cfg => station >= cfg.StartStation && station <= cfg.EndStation).OrderBy(cfg => cfg.LayerIndex).ToList();
            if (activeLayers.Count == 0) { subgradeLine = Array.Empty<RightPoint2D>(); return polygons; }

            int layerCount = activeLayers.Count;
            double k = crossfall / 100.0; // 右幅横坡斜率 -0.025

            var topOutList = new RightPoint2D[layerCount];
            var botOutList = new RightPoint2D[layerCount];
            var topInList = new RightPoint2D[layerCount];
            var botInList = new RightPoint2D[layerCount];

            var firstLayer = activeLayers[0];
            double curTopInX = centerPoint.X + firstLayer.InnerStepWidth;
            double curTopInY = centerPoint.Y;

            double curTopOutX = curTopInX + firstLayer.OuterStepWidth;
            double curTopOutY = curTopInY + (curTopOutX - curTopInX) * k;

            for (int i = 0; i < layerCount; i++) {
                var cfg = activeLayers[i];

                if (i > 0) {
                    curTopOutX = botOutList[i - 1].X + cfg.OuterStepWidth;
                    curTopOutY = botOutList[i - 1].Y + (curTopOutX - botOutList[i - 1].X) * k;

                    curTopInX = botInList[i - 1].X + cfg.InnerStepWidth;
                    curTopInY = botInList[i - 1].Y + (curTopInX - botInList[i - 1].X) * k;
                }

                topOutList[i] = new RightPoint2D(curTopOutX, curTopOutY);
                topInList[i] = new RightPoint2D(curTopInX, curTopInY);

                double outN = Math.Abs(cfg.OuterSlope);
                double dx = (outN * cfg.Thickness) / (1.0 + outN * k);
                double botOutX = curTopOutX + dx;
                double botOutY = curTopOutY - cfg.Thickness + (botOutX - curTopOutX) * k;
                botOutList[i] = new RightPoint2D(botOutX, botOutY);

                botInList[i] = new RightPoint2D(curTopInX, curTopInY - cfg.Thickness);

                polygons.Add(new RightPoint2D[] {
                    new RightPoint2D(topOutList[i].X, topOutList[i].Y),
                    new RightPoint2D(botOutList[i].X, botOutList[i].Y),
                    new RightPoint2D(botInList[i].X, botInList[i].Y),
                    new RightPoint2D(topInList[i].X, topInList[i].Y)
                });
            }

            var innerList = new List<RightPoint2D>();
            var outerList = new List<RightPoint2D>();


            innerList.Add(new RightPoint2D(topInList[0].X, centerPoint.Y));
            for (int i = 0; i < layerCount; i++) {
                innerList.Add(topInList[i]);
                innerList.Add(botInList[i]);
            }


            for (int i = 0; i < layerCount; i++) {
                outerList.Add(topOutList[i]);
                outerList.Add(botOutList[i]);
            }

            outerList.Reverse();

            var combined = new List<RightPoint2D>();
            combined.AddRange(innerList);
            combined.AddRange(outerList);

            subgradeLine = combined.ToArray();
            return polygons;
        }
    }
}
