
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
namespace jiegouceng {

    public class Point2D {
        public double X { get; set; }
        public double Y { get; set; }
        public Point2D(double x, double y) { X = x; Y = y; }
    }

    public struct CrossfallRecord {
        public double Station;
        public double Slope;   // 绝对横坡(%)，例如 -2.5 表示 -2.5%
    }

    public static class LumianSlopeManager {
        /// <summary>
        /// 严格跳过首行说明头，无脑硬转并强制执行站号升序排列
        /// </summary>
        public static List<CrossfallRecord> ParseFile(string filePath) {
            var records = new List<CrossfallRecord>();

            using (var reader = new StreamReader(filePath, Encoding.UTF8)) {
                reader.ReadLine(); // 跳过格式说明头

                string line;
                while ((line = reader.ReadLine()) != null) {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("//"))
                        continue;

                    string[] parts = line.Split(',');

                    records.Add(new CrossfallRecord {
                        Station = double.Parse(parts[0].Trim()),
                        Slope = double.Parse(parts[1].Trim())
                    });
                }
            }
            records.Sort((a, b) => a.Station.CompareTo(b.Station));
            return records;
        }


        public static double InterpolateCrossfall(List<CrossfallRecord> records, double currentStation) {
            if (records == null || records.Count == 0) return 0.0;
            if (records.Count == 1) return records[0].Slope;

            if (currentStation <= records[0].Station) return records[0].Slope;
            if (currentStation >= records[records.Count - 1].Station) return records[records.Count - 1].Slope;

            int low = 0;
            int high = records.Count - 1;

            while (low <= high) {
                int mid = low + ((high - low) >> 1);
                double midStation = records[mid].Station;

                if (Math.Abs(midStation - currentStation) < 0.00001) return records[mid].Slope;
                if (midStation < currentStation) low = mid + 1;
                else high = mid - 1;
            }

            var left = records[high];
            var right = records[low];

            double stationDelta = right.Station - left.Station;
            if (Math.Abs(stationDelta) < 0.00001) {
                return left.Slope;
            }

            double ratio = (currentStation - left.Station) / stationDelta;
            return left.Slope + ratio * (right.Slope - left.Slope);
        }
    }
}
