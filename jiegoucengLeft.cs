using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
namespace jiegouceng {
    public class LeftPoint2D {
        public double X { get; set; }
        public double Y { get; set; }
        public LeftPoint2D(double x, double y) { X = x; Y = y; }
    }

    public class LeftJiegoucengConfig {
        public double StartStation { get; set; }
        public double EndStation { get; set; }
        public int LayerIndex { get; set; }
        public string LayerName { get; set; }
        public double Thickness { get; set; }
        public double InnerStepWidth { get; set; } // 内台阶宽：负代表向左，正代表向右收缩
        public double InnerSlope { get; set; }
        public double OuterStepWidth { get; set; } // 外台阶宽：负代表向左延伸展宽
        public double OuterSlope { get; set; }
    }

    public static class LeftJiegoucengManager {
        public static List<LeftJiegoucengConfig> ParseFile(string filePath) {
            var list = new List<LeftJiegoucengConfig>();
            using (var reader = new StreamReader(filePath, Encoding.UTF8)) {
                reader.ReadLine(); // 跳过表头
                string line;
                while ((line = reader.ReadLine()) != null) {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] parts = line.Split(',');
                    list.Add(new LeftJiegoucengConfig {
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

        public static List<LeftPoint2D[]> ComputeCoordinates(double station, LeftPoint2D centerPoint, double crossfall, List<LeftJiegoucengConfig> configs, out LeftPoint2D[] subgradeLine) {
            var polygons = new List<LeftPoint2D[]>();

            var activeLayers = configs.Where(cfg => station >= cfg.StartStation && station <= cfg.EndStation).OrderBy(cfg => cfg.LayerIndex).ToList();
            if (activeLayers.Count == 0) { subgradeLine = Array.Empty<LeftPoint2D>(); return polygons; }

            int layerCount = activeLayers.Count;
            double k = -crossfall / 100.0; // 左幅横坡斜率 -2.5% 对应 0.025

            var topOutList = new LeftPoint2D[layerCount];
            var botOutList = new LeftPoint2D[layerCount];
            var topInList = new LeftPoint2D[layerCount];
            var botInList = new LeftPoint2D[layerCount];

            var firstLayer = activeLayers[0];
            double curTopInX = centerPoint.X + firstLayer.InnerStepWidth; // -1.5m
            double curTopInY = centerPoint.Y;

            double curTopOutX = curTopInX + firstLayer.OuterStepWidth;   // -5.5m
            double curTopOutY = curTopInY + (curTopOutX - curTopInX) * k;

            for (int i = 0; i < layerCount; i++) {
                var cfg = activeLayers[i];

                if (i > 0) {
                    curTopOutX = botOutList[i - 1].X + cfg.OuterStepWidth;
                    curTopOutY = botOutList[i - 1].Y + (curTopOutX - botOutList[i - 1].X) * k;

                    curTopInX = botInList[i - 1].X + cfg.InnerStepWidth;
                    curTopInY = botInList[i - 1].Y + (curTopInX - botInList[i - 1].X) * k;
                }

                topOutList[i] = new LeftPoint2D(curTopOutX, curTopOutY);
                topInList[i] = new LeftPoint2D(curTopInX, curTopInY);

                double outN = Math.Abs(cfg.OuterSlope);
                double dx = -(outN * cfg.Thickness) / (1.0 - outN * k);
                double botOutX = curTopOutX + dx;
                double botOutY = curTopOutY - cfg.Thickness + dx * k;
                botOutList[i] = new LeftPoint2D(botOutX, botOutY);

                botInList[i] = new LeftPoint2D(curTopInX, curTopInY - cfg.Thickness);

                polygons.Add(new LeftPoint2D[] {
                    new LeftPoint2D(topOutList[i].X, topOutList[i].Y),
                    new LeftPoint2D(botOutList[i].X, botOutList[i].Y),
                    new LeftPoint2D(botInList[i].X, botInList[i].Y),
                    new LeftPoint2D(topInList[i].X, topInList[i].Y)
                });
            }

            var innerList = new List<LeftPoint2D>();
            var outerList = new List<LeftPoint2D>();

            for (int i = 0; i < layerCount; i++) {
                outerList.Add(topOutList[i]);
                outerList.Add(botOutList[i]);
            }

            for (int i = 0; i < layerCount; i++) {
                innerList.Add(topInList[i]);
                innerList.Add(botInList[i]);
            }
            innerList.Insert(0, new LeftPoint2D(topInList[0].X, centerPoint.Y)); // 设计线挂接点，作为反转后的终点闭合

            innerList.Reverse();

            var combined = new List<LeftPoint2D>();
            combined.AddRange(outerList);
            combined.AddRange(innerList);

            subgradeLine = combined.ToArray();
            return polygons;
        }
    }

}
