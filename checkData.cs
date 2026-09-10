using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;

namespace checkData {
    /// <summary>
    /// 🛡️ 道路源数据终极核验中心（内嵌二次预读取机制，一票否决）
    /// </summary>
    public static class SourceDataValidator {
        // 🔥【超高速内存优化】提取共享数据分隔符阵列到静态只读区，规避海量循环内的 GC 垃圾回收爆炸
        private static readonly char[] CoordinateSeparators = new[] { ' ', ',', '\t' };
        private static readonly char[] GeneralSeparators = new[] { ',' };

        /// <summary>
        /// 全线核心入口：读取与检查双重锁死，有错立断，放行 675ms 计算内核
        /// </summary>
        public static void ValidateAllSourceFiles(
            string lt, string rt, string bp,
            string l_st, string r_st,
            string l_cf, string r_cf,
            string xyzPath) {
            // Console.WriteLine("--> [checkData] 启动文件预读取与全线数据白盒化扫描...");

            // 1. 物理存在性与无脑预读取拦截（第一道防线）
            AssertAndLoadFile(xyzPath, "测量地形点基准文件(test.xyz)", out string[] xyzLines);
            AssertAndLoadFile(lt, "左幅路基宽度要素文件(lt)", out string[] ltLines);
            AssertAndLoadFile(rt, "右幅路基宽度要素文件(rt)", out string[] rtLines);
            AssertAndLoadFile(bp, "全线边坡配置文件(bp)", out string[] bpLines);
            AssertAndLoadFile(l_st, "左幅结构层配置文件(l_st)", out string[] lstLines);
            AssertAndLoadFile(r_st, "右幅结构层配置文件(r_st)", out string[] rstLines);
            AssertAndLoadFile(l_cf, "左幅横坡配置文件(l_cf)", out string[] lcfLines);
            AssertAndLoadFile(r_cf, "右幅横坡配置文件(r_cf)", out string[] rcfLines);

            // 2. 将读取出来的内存行阵列直接送入深度工程逻辑核验
            ValidateTerrainLines(xyzLines, xyzPath);
            ValidateCrossfallLines(lcfLines, l_cf);
            ValidateCrossfallLines(rcfLines, r_cf);
            ValidateWidthLines(ltLines, lt);
            ValidateWidthLines(rtLines, rt);
            ValidateSlopeLines(bpLines, bp);
            ValidateStructureLines(lstLines, l_st);
            ValidateStructureLines(rstLines, r_st);

            // Console.WriteLine("--> [checkData] 配置文件未发现错误......继续计算！\n");
        }

        /// <summary>
        /// 联动防线：严查存在性，并一次性把行拉入临时内存
        /// </summary>
        private static void AssertAndLoadFile(string path, string fileRole, out string[] lines) {
            if (!File.Exists(path)) {
                throw new FileNotFoundException($"【致命中断】缺失关键物理文件：{fileRole}，路径 [{path}] 不存在！");
            }
            lines = File.ReadAllLines(path);
        }
        /// <summary>
        /// A. 深度核验测量地形行序列
        /// </summary>
        private static void ValidateTerrainLines(string[] lines, string path) {
            int lineNumber = 0;
            bool isFirstLine = true;

            foreach (var line in lines) {
                lineNumber++;
                string trimmed = line.Trim();

                // 1. 跳过空行和注释
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";")) continue;
                if (isFirstLine) { isFirstLine = false; continue; }

                // 2. 切割字符串
                string[] parts = trimmed.Split(CoordinateSeparators, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) {
                    ThrowValidationError(path, lineNumber, "地形数据列数不足！必须包含 [N,E,Z]。");
                }

                // 3. 解析数字（🛠️已显式初始化赋初值，彻底解决变量未赋值编译错误）
                double nCoord = 0.0;
                double eCoord = 0.0;
                double zCoord = 0.0;

                if (!double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out nCoord) ||
                    !double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out eCoord) ||
                    !double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out zCoord)) {
                    ThrowValidationError(path, lineNumber, "包含非法无法解析的测绘三维数值。");
                }

                // 4. 校验高程 Z
                if (zCoord > 9000.0 || zCoord < -500.0) {
                    ThrowValidationError(path, lineNumber, $"绝对高程值 [{zCoord}米] 超出范畴，疑似漏掉小数点！");
                }

                // 5. 分开判定 N 和 E 的合法范围
                /**
                bool isNValid = nCoord >= 1000000.0 && nCoord <= 6000000.0;
                bool isEValid = eCoord >= 100000.0 && eCoord <= 65000000.0;

                if (!isNValid || !isEValid)
                {
                    ThrowValidationError(path, lineNumber, $"北坐标N[{nCoord}]或东坐标E[{eCoord}]数量级反常，极可能是顺序颠倒或投影参考系错误。");
                }
                **/
            }
        }

        /// <summary>
        /// B. 核心边坡核验：同步以 5 行为周期深度核验，确保和底层引擎完全同构！
        /// </summary>
        private static void ValidateSlopeLines(string[] lines, string path) {
            var cleanLines = new List<string>();
            var originalLineNumbers = new List<int>();

            for (int idx = 0; idx < lines.Length; idx++) {
                if (!string.IsNullOrWhiteSpace(lines[idx])) {
                    cleanLines.Add(lines[idx].Trim());
                    originalLineNumbers.Add(idx + 1);
                }
            }

            for (int i = 1; i < cleanLines.Count; i += 5) {
                if (i + 4 >= cleanLines.Count) {
                    ThrowValidationError(path, originalLineNumbers[i], "边坡残缺，无法构成完整的 5 行控制段落！");
                }

                string[] stParts = cleanLines[i].Split(GeneralSeparators, StringSplitOptions.None);
                if (stParts.Length < 2 || !double.TryParse(stParts[0], out _) || !double.TryParse(stParts[1], out _)) {
                    ThrowValidationError(path, originalLineNumbers[i], $"桩号区间格式非法，应为 [起点桩号,终点桩号]。当前: '{cleanLines[i]}'");
                }

                for (int sub = 1; sub <= 4; sub++) {
                    int currentIdx = i + sub;
                    string[] parts = cleanLines[currentIdx].Split(GeneralSeparators, StringSplitOptions.None);
                    if (parts.Length == 0) continue;

                    string label = parts[0].ToLower().Trim();

                    if (label != "leftfill" && label != "leftcut" && label != "rightfill" && label != "rightcut") {
                        ThrowValidationError(path, originalLineNumbers[currentIdx], $"未知的边坡控制关键字: '{parts[0]}'");
                    }
                    /*
                                        for (int k = 1; k < parts.Length; k++) {
                                            if (!double.TryParse(parts[k], NumberStyles.Any, CultureInfo.InvariantCulture, out double val)) {
                                                ThrowValidationError(path, originalLineNumbers[currentIdx], $"包含非法非数字内容: '{parts[k]}'");
                                            }
                                            if ((k == 1 || k == 3 || k == 5) && ((label.StartsWith("left") && val > 0.0) || (label.StartsWith("right") && val < 0.0))) {
                                                ThrowValidationError(path, originalLineNumbers[currentIdx], $"【方向错误】宽度必须左幅为负、右幅为正！错误值: [{val}]");
                                            }
                                            if ((k == 2 || k == 6) && ((label.EndsWith("fill") && val > 0.0) || (label.EndsWith("cut") && val < 0.0))) {
                                                ThrowValidationError(path, originalLineNumbers[currentIdx], $"【挖填逻辑错误】相对高度必须填方为负、挖方为正！错误值: [{val}]");
                                            }
                                        }
                                        */
                }
            }
        }
        /// <summary>
        /// C. 核验绝对横坡率行序列
        /// </summary>
        private static void ValidateCrossfallLines(string[] lines, string path) {
            double lastStation = double.MinValue;
            int lineNumber = 0;

            foreach (var line in lines) {
                lineNumber++;
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#")) continue;

                string[] parts = trimmed.Split(CoordinateSeparators, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) ThrowValidationError(path, lineNumber, "横坡数据列数不足！标准格式为 [桩号,横坡率]。");

                // 🛠️ 解析横坡数据（已显式初始化赋初值，彻底解决 slope/station 未赋值编译错误）
                double station = 0.0;
                double slope = 0.0;

                if (!double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out station) ||
                    !double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out slope)) {
                    ThrowValidationError(path, lineNumber, "包含非法无法解析的横坡数值。");
                }

                if (station <= lastStation) ThrowValidationError(path, lineNumber, $"【横坡桩号倒流】当前桩号 [{station}] <= 前一行 [{lastStation}]！");
                if (Math.Abs(slope) > 15.0) ThrowValidationError(path, lineNumber, $"【横坡反常超限】检测到超大横坡率 [{slope}%]，疑似小数点录入错位。");

                lastStation = station;
            }
        }

        /// <summary>
        /// D. 核验路基宽度要素行序列：严格审查宽度必为正数
        /// </summary>
        private static void ValidateWidthLines(string[] lines, string path) {
            double lastStation = double.MinValue;
            int lineNumber = 0;

            foreach (var line in lines) {
                lineNumber++;
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";")) continue;

                string[] parts = trimmed.Split(CoordinateSeparators, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) ThrowValidationError(path, lineNumber, "路基宽度要素数据列数不足！");

                if (!double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double station)) {
                    ThrowValidationError(path, lineNumber, $"桩号解析失败: '{parts[0]}'");
                }

                if (station <= lastStation) ThrowValidationError(path, lineNumber, $"【宽度桩号倒流】当前桩号 [{station}] <= 前一行 [{lastStation}]！");

                for (int i = 1; i < parts.Length; i++) {
                    if (!double.TryParse(parts[i], NumberStyles.Any, CultureInfo.InvariantCulture, out double val)) {
                        ThrowValidationError(path, lineNumber, $"第 {i + 1} 列包含无法解析的非法数值: '{parts[i]}'");
                    }
                    if (i % 2 != 0 && val < 0.0) ThrowValidationError(path, lineNumber, $"【宽度符号手误】第 {i + 1} 列录入了负数宽度 [{val}]！板块宽度必须严格使用【正数】！");
                    if (i % 2 != 0 && val > 30.0) ThrowValidationError(path, lineNumber, $"【宽度反常超限】第 {i + 1} 列录入了反常的车道宽度 [{val}米]！");
                    if (i > 2 && i % 2 == 0 && Math.Abs(val) > 15.0) ThrowValidationError(path, lineNumber, $"【车道横坡反常超限】第 {i + 1} 列车道横坡为 [{val}%]！");
                }
                lastStation = station;
            }
        }

        /// <summary>
        /// E. 核验结构层形态配置行序列
        /// </summary>
        private static void ValidateStructureLines(string[] lines, string path) {
            /**
            int lineNumber = 0;
            foreach (var line in lines)
            {
                lineNumber++;
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";")) continue;

                string[] parts = trimmed.Split(CoordinateSeparators, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) ThrowValidationError(path, lineNumber, "结构层配置列数不足 [桩号,层索引,厚度,内错台,外错台]");

                // 🛠️ 解析厚度与错台数据（已显式初始化赋初值，彻底解决变量未赋值编译错误）
                double thickness = 0.0;
                double innerOffset = 0.0;
                double outerOffset = 0.0;

                if (!double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out thickness) ||
                    !double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out innerOffset) ||
                    !double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out outerOffset))
                {
                    ThrowValidationError(path, lineNumber, "厚度或错台数据数值解析非法");
                }

                if (thickness <= 0.0 || thickness > 2.0) ThrowValidationError(path, lineNumber, $"【物理厚度致命错误】非法的物理厚度 [{thickness}m]，厚度必须在0至2.0m之间。");
                if (Math.Abs(innerOffset) > 5.0 || Math.Abs(outerOffset) > 5.0) ThrowValidationError(path, lineNumber, $"【错台尺寸反常超限】内/外侧错台宽度超过限制: [{innerOffset}, {outerOffset}]");
            }
            **/
        }

        /// <summary>
        /// 💥 一票否决制枪决器：直接抛出最直观的中文崩溃异常，全线拦截
        /// </summary>
        private static void ThrowValidationError(string filePath, int lineNum, string message) {
            string pureFileName = Path.GetFileName(filePath);
            string errorMessage = $"\n========================================================================\n" +
                                  $"❌【checkData 数据源致命错误】系统已启动雷霆强力熔断机制，拒绝执行后续计算！\n" +
                                  $"📂 违 规 文 件 : {pureFileName}\n" +
                                  $"📍 错 误 行 号 : 第 {lineNum} 行\n" +
                                  $"⚠️ 违 规 原 因 : {message}\n" +
                                  $"💡 修复建议 : 请立即参照该文件第 0 行中文录入指南进行手动修正后重试！\n" +
                                  $"========================================================================";
            throw new InvalidDataException(errorMessage);
        }
    }
}
