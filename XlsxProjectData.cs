// ==================== XlsxProjectData.cs ====================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

namespace Hdm
{
    /// <summary>从 .xlsx 读项目数据（0 依赖，ZIP + XML）</summary>
    public static class XlsxProjectData
    {
        public static ProjectData Load(string xlsxPath)
        {
            if (!File.Exists(xlsxPath))
                throw new FileNotFoundException($"项目文件不存在: {xlsxPath}");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            void Tick(string tag)
            {
                Console.WriteLine("[" + sw.ElapsedMilliseconds.ToString().PadLeft(6) + " ms] " + tag);
                sw.Restart();
            }

            Tick("开始");

            var xlsx = new XlsxReader(xlsxPath);
            Tick("打开 xlsx (zip + workbook + sharedStrings)");

            var config = ReadConfig(xlsx);
            Tick("配置");

            double[][] pqx = ReadMatrix(xlsx, "平曲线", 8);
            Tick("平曲线 (" + pqx.Length + " 行)");

            double[][] sqx = ReadMatrix(xlsx, "竖曲线", 3);
            Tick("竖曲线 (" + sqx.Length + " 行)");

            double[][] kzb = ReadMatrix(xlsx, "桩号", 1);
            Tick("桩号 (" + kzb.Length + " 行)");

            double[][] xyz = ReadMatrix(xlsx, "原地面", 3);
            Tick("原地面 (" + xyz.Length + " 行)");

            var leftWidths = ReadWidths(xlsx, "左板块");
            var rightWidths = ReadWidths(xlsx, "右板块");
            Tick("左右板块");

            var slopeData = ReadSlope(xlsx, "边坡");
            Tick("边坡");

            var leftStructures = ReadLeftStructures(xlsx, "左结构层");
            var rightStructures = ReadRightStructures(xlsx, "右结构层");
            Tick("左右结构层");

            double[][] leftCrossfalls = ReadMatrix(xlsx, "左结构层横坡", 2);
            double[][] rightCrossfalls = ReadMatrix(xlsx, "右结构层横坡", 2);
            Tick("左右结构层横坡");

            double[][] mesh = LL.hua_Fs_Batch(pqx, xyz);
            Tick("批量反算 mesh (" + mesh.Length + " 点)");

            var calc = new SectionCalculator(
                mesh, pqx, sqx,
                leftWidths, rightWidths, slopeData,
                leftCrossfalls, rightCrossfalls,
                leftStructures, rightStructures,
                config.clearDepth);
            Tick("构造 Calculator");

            return new ProjectData
            {
                config = config,
                pqx = pqx,
                sqx = sqx,
                kzb = kzb,
                mesh = mesh,
                leftWidths = leftWidths,
                rightWidths = rightWidths,
                slopeData = slopeData,
                leftCrossfalls = leftCrossfalls,
                rightCrossfalls = rightCrossfalls,
                leftStructures = leftStructures,
                rightStructures = rightStructures,
                calculator = calc
            };
        }

        // ============================================================
        // 读"配置"里的"投影方式 / 中央子午线 / 南北半球"
        // ============================================================
        private static void ReadProjectionConfig(XlsxReader xlsx, ProjectData data)
        {
            var rows = ReadRows(xlsx, "配置");
            foreach (var row in rows)
            {
                string key = GetString(row, 0).Trim();
                string val = GetString(row, 1).Trim();
                if (key.Length == 0) continue;

                if (key == "投影方式") data.projection = val.ToLower();
                else if (key == "中央子午线")
                {
                    double d;
                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                        data.lonCenter = d;
                }
                else if (key == "南北半球") data.isNorthern = val.Contains("北");
            }
        }

        // ============================================================
        // 读"控制点" sheet
        // ============================================================
        private static void ReadControlPoints(XlsxReader xlsx, ProjectData data)
        {
            List<Dictionary<int, string>> rows;
            try { rows = ReadRows(xlsx, "控制点"); }
            catch
            {
                data.controlLonLat = new double[0][];
                data.controlXy = new double[0][];
                return;
            }

            var llList = new List<double[]>();
            var xyList = new List<double[]>();

            foreach (var row in rows)
            {
                if (IsEmptyRow(row)) continue;
                double x = GetDouble(row, 1);
                double y = GetDouble(row, 2);
                double lon = GetDouble(row, 4);
                double lat = GetDouble(row, 5);
                if (x == 0 && y == 0) continue;
                xyList.Add(new[] { x, y });
                llList.Add(new[] { lon, lat });
            }

            data.controlXy = new double[xyList.Count][];
            data.controlLonLat = new double[llList.Count][];
            for (int i = 0; i < xyList.Count; i++)
            {
                data.controlXy[i] = new[] { xyList[i][0], xyList[i][1] };
                data.controlLonLat[i] = new[] { llList[i][0], llList[i][1] };
            }
        }

        // ============================================================
        // 通用读取
        // ============================================================
        private static List<Dictionary<int, string>> ReadRows(XlsxReader xlsx, string sheetName)
        {
            return xlsx.ReadSheet(sheetName);
        }

        private static double[][] ReadMatrix(XlsxReader xlsx, string sheetName, int cols)
        {
            var rows = ReadRows(xlsx, sheetName);
            var list = new List<double[]>();
            foreach (var row in rows)
            {
                if (IsEmptyRow(row)) continue;
                double[] d = new double[cols];
                for (int j = 0; j < cols; j++) d[j] = GetDouble(row, j);
                list.Add(d);
            }
            return list.ToArray();
        }

        private static List<DuanMianShuJu> ReadWidths(XlsxReader xlsx, string sheetName)
        {
            var rows = ReadRows(xlsx, sheetName);
            var list = new List<DuanMianShuJu>();
            foreach (var row in rows)
            {
                if (IsEmptyRow(row)) continue;
                var d = new DuanMianShuJu();
                d.zhuangHao = GetDouble(row, 0);

                int maxKey = 0;
                foreach (int k in row.Keys)
                    if (k > maxKey) maxKey = k;

                for (int j = 1; j + 1 <= maxKey; j += 2)
                {
                    if (IsCellEmpty(row, j) && IsCellEmpty(row, j + 1)) break;
                    d.banKuaiJiHe.Add(new[] { GetDouble(row, j), GetDouble(row, j + 1) });
                }
                list.Add(d);
            }
            return list;
        }

        private static List<BianPoDuanLuo> ReadSlope(XlsxReader xlsx, string sheetName)
        {
            var rows = ReadRows(xlsx, sheetName);
            var result = new List<BianPoDuanLuo>();
            BianPoDuanLuo cur = null;
            foreach (var row in rows)
            {
                if (IsEmptyRow(row)) continue;
                double? k1 = TryGetDouble(row, 0);
                double? k2 = TryGetDouble(row, 1);
                string type = GetString(row, 2).Trim().ToLower();
                string dispStr = GetString(row, 3);

                if (k1.HasValue && k2.HasValue)
                {
                    cur = new BianPoDuanLuo();
                    cur.qiShiZhuangHao = k1.Value;
                    cur.jieShuZhuangHao = k2.Value;
                    result.Add(cur);
                }
                if (cur == null) continue;

                double[][] disp = ParseDisp(dispStr);
                if (type == "leftfill") cur.zuoTian = disp;
                else if (type == "leftcut") cur.zuoWa = disp;
                else if (type == "rightfill") cur.youTian = disp;
                else if (type == "rightcut") cur.youWa = disp;
            }
            return result;
        }

        private static double[][] ParseDisp(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Trim().Length == 0)
                return new double[0][];

            string[] parts = s.Split(',');
            var nums = new List<double>();
            foreach (var p in parts)
            {
                string t = p.Trim();
                if (t.Length == 0) continue;
                double v;
                if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                    nums.Add(v);
            }
            int n = nums.Count / 2;
            double[][] m = new double[n][];
            for (int i = 0; i < n; i++)
                m[i] = new[] { nums[i * 2], nums[i * 2 + 1] };
            return m;
        }

        private static List<LeftJiegoucengConfig> ReadLeftStructures(XlsxReader xlsx, string sheetName)
        {
            var rows = ReadRows(xlsx, sheetName);
            var list = new List<LeftJiegoucengConfig>();
            foreach (var row in rows)
            {
                if (IsEmptyRow(row)) continue;
                var c = new LeftJiegoucengConfig();
                c.startStation = GetDouble(row, 0);
                c.endStation = GetDouble(row, 1);
                c.layerIndex = (int)GetDouble(row, 2);
                c.layerName = GetString(row, 3).Trim();
                c.thickness = GetDouble(row, 4);
                c.innerStepWidth = GetDouble(row, 5);
                c.innerSlope = GetDouble(row, 6);
                c.outerStepWidth = GetDouble(row, 7);
                c.outerSlope = GetDouble(row, 8);
                list.Add(c);
            }
            return list;
        }

        private static List<RightJiegoucengConfig> ReadRightStructures(XlsxReader xlsx, string sheetName)
        {
            var rows = ReadRows(xlsx, sheetName);
            var list = new List<RightJiegoucengConfig>();
            foreach (var row in rows)
            {
                if (IsEmptyRow(row)) continue;
                var c = new RightJiegoucengConfig();
                c.startStation = GetDouble(row, 0);
                c.endStation = GetDouble(row, 1);
                c.layerIndex = (int)GetDouble(row, 2);
                c.layerName = GetString(row, 3).Trim();
                c.thickness = GetDouble(row, 4);
                c.innerStepWidth = GetDouble(row, 5);
                c.innerSlope = GetDouble(row, 6);
                c.outerStepWidth = GetDouble(row, 7);
                c.outerSlope = GetDouble(row, 8);
                list.Add(c);
            }
            return list;
        }

        private static AppConfig ReadConfig(XlsxReader xlsx)
        {
            var cfg = new AppConfig();
            var rows = ReadRows(xlsx, "配置");
            foreach (var row in rows)
            {
                string key = GetString(row, 0).Trim();
                string val = GetString(row, 1).Trim();
                if (key.Length == 0) continue;

                if (key == "ClearDepth")
                {
                    double d;
                    if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                        cfg.clearDepth = d;
                }
                else if (key == "XyzRows")
                {
                    int n;
                    if (int.TryParse(val, out n)) cfg.xyzRows = n;
                }
                else if (key == "XyzCols")
                {
                    int n;
                    if (int.TryParse(val, out n)) cfg.xyzCols = n;
                }
            }
            return cfg;
        }

        // ============================================================
        private static bool IsEmptyRow(Dictionary<int, string> row)
        {
            if (row == null || row.Count == 0) return true;
            foreach (var v in row.Values)
                if (!string.IsNullOrEmpty(v) && v.Trim().Length > 0) return false;
            return true;
        }

        private static bool IsCellEmpty(Dictionary<int, string> row, int idx)
        {
            string v;
            if (!row.TryGetValue(idx, out v)) return true;
            return string.IsNullOrEmpty(v) || v.Trim().Length == 0;
        }

        private static double GetDouble(Dictionary<int, string> row, int idx)
        {
            string v;
            if (!row.TryGetValue(idx, out v)) return 0;
            if (string.IsNullOrEmpty(v) || v.Trim().Length == 0) return 0;
            double d;
            if (double.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                return d;
            return 0;
        }

        private static double? TryGetDouble(Dictionary<int, string> row, int idx)
        {
            string v;
            if (!row.TryGetValue(idx, out v)) return null;
            if (string.IsNullOrEmpty(v) || v.Trim().Length == 0) return null;
            double d;
            if (double.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                return d;
            return null;
        }

        private static string GetString(Dictionary<int, string> row, int idx)
        {
            string v;
            return row.TryGetValue(idx, out v) ? (v ?? "") : "";
        }
    }

    /// <summary>项目数据 + 计算器</summary>
    public class ProjectData
    {
        public AppConfig config;
        public double[][] pqx, sqx, kzb, mesh;
        public List<DuanMianShuJu> leftWidths, rightWidths;
        public List<BianPoDuanLuo> slopeData;
        public double[][] leftCrossfalls, rightCrossfalls;
        public List<LeftJiegoucengConfig> leftStructures;
        public List<RightJiegoucengConfig> rightStructures;
        public SectionCalculator calculator;

        public string projection = "gauss";
        public double lonCenter = 0;
        public bool isNorthern = false;
        public double[][] controlLonLat;
        public double[][] controlXy;

        public ComputeResult Query(double station)
        {
            return calculator.Compute(station);
        }

        public double[] Stations()
        {
            double[] arr = new double[kzb.Length];
            for (int i = 0; i < kzb.Length; i++) arr[i] = kzb[i][0];
            return arr;
        }
    }

    // ============================================================
    // .xlsx 读取器（ZIP + XML，使用 XDocument 流式解析）
    // ============================================================
    internal class XlsxReader : IDisposable
    {
        private const string NsMain = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string NsRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string NsPkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

        private readonly ZipArchive zip;
        private readonly Dictionary<string, string> sheetNameToPath = new Dictionary<string, string>();
        private readonly List<string> sharedStrings = new List<string>();

        public XlsxReader(string xlsxPath)
        {
            try
            {
                var fs = File.OpenRead(xlsxPath);
                zip = new ZipArchive(fs, ZipArchiveMode.Read);
                LoadWorkbook();
                LoadSharedStrings();
            }
            catch (Exception e)
            {
                throw new Exception("打开 xlsx 失败: " + xlsxPath, e);
            }
        }

        public void Dispose()
        {
            if (zip != null) zip.Dispose();
        }

        private void LoadWorkbook()
        {
            var rIdToName = new Dictionary<string, string>();

            XDocument doc;
            using (var s = zip.GetEntry("xl/workbook.xml").Open())
                doc = XDocument.Load(s);

            var sheets = doc.Root.Element(XName.Get("sheets", NsMain));
            if (sheets == null) return;

            foreach (var el in sheets.Elements(XName.Get("sheet", NsMain)))
            {
                string name = (string)el.Attribute("name");
                string rId = (string)el.Attribute(XName.Get("id", NsRel));
                if (!string.IsNullOrEmpty(rId)) rIdToName[rId] = name;
            }

            XDocument relDoc;
            using (var s = zip.GetEntry("xl/_rels/workbook.xml.rels").Open())
                relDoc = XDocument.Load(s);

            foreach (var rel in relDoc.Root.Elements(XName.Get("Relationship", NsPkgRel)))
            {
                string id = (string)rel.Attribute("Id");
                string target = (string)rel.Attribute("Target");
                if (id == null || target == null) continue;
                string name;
                if (!rIdToName.TryGetValue(id, out name)) continue;
                if (!target.StartsWith("xl/")) target = "xl/" + target;
                sheetNameToPath[name] = target;
            }
        }

        private void LoadSharedStrings()
        {
            var entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return;
            try
            {
                XDocument doc;
                using (var s = entry.Open())
                    doc = XDocument.Load(s);
                if (doc.Root == null) return;

                foreach (var si in doc.Root.Elements(XName.Get("si", NsMain)))
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var t in si.Descendants(XName.Get("t", NsMain)))
                        sb.Append(t.Value);
                    sharedStrings.Add(sb.ToString());
                }
            }
            catch { }
        }

        public List<Dictionary<int, string>> ReadSheet(string sheetName)
        {
            string path;
            if (!sheetNameToPath.TryGetValue(sheetName, out path))
                throw new Exception("找不到 sheet: " + sheetName);

            var rows = new List<Dictionary<int, string>>();
            try
            {
                XDocument doc;
                using (var s = zip.GetEntry(path).Open())
                    doc = XDocument.Load(s);

                var sheetData = doc.Root.Element(XName.Get("sheetData", NsMain));
                if (sheetData == null) return rows;

                foreach (var rowElem in sheetData.Elements(XName.Get("row", NsMain)))
                {
                    string rAttr = (string)rowElem.Attribute("r");
                    if (rAttr == "1") continue;

                    var map = new Dictionary<int, string>();
                    foreach (var c in rowElem.Elements(XName.Get("c", NsMain)))
                    {
                        string refStr = (string)c.Attribute("r");
                        string type = (string)c.Attribute("t");
                        int colIdx = ColRefToIndex(refStr);
                        string val = ReadCellValue(c, type);
                        if (!string.IsNullOrEmpty(val))
                            map[colIdx] = val;
                    }
                    rows.Add(map);
                }
            }
            catch (Exception e)
            {
                throw new Exception("读 sheet 失败: " + sheetName, e);
            }
            return rows;
        }

        private string ReadCellValue(XElement c, string type)
        {
            if (type == "inlineStr")
            {
                var isElem = c.Element(XName.Get("is", NsMain));
                if (isElem != null)
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var t in isElem.Descendants(XName.Get("t", NsMain)))
                        sb.Append(t.Value);
                    return sb.ToString();
                }
            }

            var vElem = c.Element(XName.Get("v", NsMain));
            if (vElem == null) return null;
            string v = vElem.Value;
            if (v == null) return null;

            if (type == "s")
            {
                int idx;
                if (!int.TryParse(v.Trim(), out idx)) return "";
                if (idx >= 0 && idx < sharedStrings.Count) return sharedStrings[idx];
                return "";
            }
            return v;
        }

        private static int ColRefToIndex(string refStr)
        {
            if (string.IsNullOrEmpty(refStr)) return 0;
            int col = 0;
            for (int i = 0; i < refStr.Length; i++)
            {
                char ch = refStr[i];
                if (ch >= 'A' && ch <= 'Z') col = col * 26 + (ch - 'A' + 1);
                else break;
            }
            return col - 1;
        }
    }
}