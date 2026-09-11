using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using tianwaBianpo;
using topSlope;
using jiegouceng;
using LLutile;
using System.Diagnostics;

public static class ProjectProcessor {
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Process(string projectName) {
        string workingDir = Path.Combine(Directory.GetCurrentDirectory(), projectName);
        string resultDir = Path.Combine(workingDir, "result");
        Directory.CreateDirectory(resultDir);
        AppConfig.LoadConfig(projectName, workingDir);

        string xyzPath = Path.Combine(workingDir, projectName + ".原地面");
        string lt = Path.Combine(workingDir, projectName + ".左板块");
        string rt = Path.Combine(workingDir, projectName + ".右板块");
        string bp = Path.Combine(workingDir, projectName + ".边坡");
        string l_st = Path.Combine(workingDir, projectName + ".左结构层");
        string r_st = Path.Combine(workingDir, projectName + ".右结构层");
        string l_cf = Path.Combine(workingDir, projectName + ".左结构层横坡");
        string r_cf = Path.Combine(workingDir, projectName + ".右结构层横坡");
        string pqxPath = Path.Combine(workingDir, projectName + ".pqx");
        string sqxPath = Path.Combine(workingDir, projectName + ".sqx");
        string kzbPath = Path.Combine(workingDir, projectName + ".k");
        string csvPath = Path.Combine(resultDir, projectName + ".csv");
        string dxfPath = Path.Combine(resultDir, projectName + ".dxf");

        checkData.SourceDataValidator.ValidateAllSourceFiles(lt, rt, bp, l_st, r_st, l_cf, r_cf, xyzPath);

        var stopwatch = Stopwatch.StartNew();
        long freq = Stopwatch.Frequency;

        // ================= 读文件 =================
        double[,] pqx = LL.ReadDataFromFile(pqxPath, 8);
        double[,] sqx = LL.ReadDataFromFile(sqxPath, 3);
        double[,] kzb = LL.ReadDataFromFile(kzbPath, 1);
        double[,] xyz1 = LL.ReadDataFromFile(xyzPath, 3, null, true);
        // Console.WriteLine($"[T] 读文件: {stopwatch.ElapsedMilliseconds} ms  (xyz1点数={xyz1.GetLength(0)})");
        // stopwatch.Restart();

        // ================= hua_Fs_Batch =================
        var xyz = LL.hua_Fs_Batch(pqx, xyz1);
        // Console.WriteLine($"[T] hua_Fs_Batch: {stopwatch.ElapsedMilliseconds} ms");
        //  stopwatch.Restart();

        // ================= parse 数据 =================
        var leftWidths = LuJiYaoSuYinQing.parseKuandDuFile(lt);
        var rightWidths = LuJiYaoSuYinQing.parseKuandDuFile(rt);
        var slopeData = BianPoYinQing.parseBianPo(bp);
        var leftCrossfalls = LumianSlopeManager.ParseFile(l_cf);
        var rightCrossfalls = LumianSlopeManager.ParseFile(r_cf);
        var leftStructures = LeftJiegoucengManager.ParseFile(l_st);
        var rightStructures = RightJiegoucengManager.ParseFile(r_st);
        //Console.WriteLine($"[T] parse 各数据文件: {stopwatch.ElapsedMilliseconds} ms");
        // stopwatch.Restart();

        // ================= 构造 calculator =================
        var calculator = new SectionCalculator(
            xyz, pqx, sqx, leftWidths, rightWidths, slopeData,
            leftCrossfalls, rightCrossfalls, leftStructures, rightStructures);
        //  Console.WriteLine($"[T] 构造 calculator: {stopwatch.ElapsedMilliseconds} ms");
        //  stopwatch.Restart();

        string csvHeader = CsvBuilder.BuildHeader(leftStructures, rightStructures);
        var csvSb = new StringBuilder(csvHeader, 4 * 1024 * 1024);

        // DXF 容器：只有在需要时才创建
        StringBuilder dxfEntities = AppConfig.OutputDxf ? new StringBuilder(64 * 1024 * 1024) : null;

        double currentOffsetX = 0.0;
        List<SectionResult> sectionResults = AppConfig.OutputHtml ? new List<SectionResult>() : null;

        int total = kzb.GetLength(0);
        int successCount = 0;
        int failedCount = 0;
        var successStations = new List<string>();
        var failedStations = new List<string>();
        var failedReasons = new List<string>();
        /**
                // ================= 预热循环 =================
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < total; i++) {
                    double st = kzb[i, 0];
                    double[,] ground = LL.getMatchedBZ(xyz, st);
                }
               // Console.WriteLine($"[T] 预热循环(1310 次 getMatchedBZ): {sw.ElapsedMilliseconds} ms");
        **/
        // ================= 主循环 =================
        long tCompute = 0, tDxf = 0, tCsv = 0;
        stopwatch.Restart();

        for (int i = 0; i < total; i++) {
            double st = kzb[i, 0];
            string stationStr = LL.hua_Num2K(st);

            try {
                long t1 = Stopwatch.GetTimestamp();
                var computeResult = calculator.Compute(st);
                long t2 = Stopwatch.GetTimestamp();
                tCompute += t2 - t1;

                if (!computeResult.Success) {
                    failedCount++;
                    failedStations.Add(stationStr);
                    failedReasons.Add(computeResult.ErrorMessage);
                    continue;
                }

                var result = computeResult.Result;

                long t3 = Stopwatch.GetTimestamp();
                if (AppConfig.OutputDxf) {
                    AppendSectionToDxf(dxfEntities, result, ref currentOffsetX);
                }
                long t4 = Stopwatch.GetTimestamp();
                tDxf += t4 - t3;

                string layerCsv = "";
                foreach (var areaText in result.LayerAreaTexts) {
                    string[] parts = areaText.Split(':');
                    double area = double.Parse(parts[2]);
                    layerCsv += "," + area.ToString("F3", Inv);
                }
                csvSb.AppendLine(CsvBuilder.BuildDataRow(
                    stationStr, result.FillArea, result.CutArea, result.ClearArea,
                    result.MinX, result.MaxX, layerCsv));
                long t5 = Stopwatch.GetTimestamp();
                tCsv += t5 - t4;

                if (AppConfig.OutputHtml) {
                    sectionResults.Add(result);
                }
                successCount++;
                successStations.Add(stationStr);
            } catch (Exception ex) {
                failedCount++;
                failedStations.Add(stationStr);
                failedReasons.Add($"计算异常: {ex.Message}");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n⚠️ 断面 {stationStr} 计算异常: {ex.Message}");
                Console.ResetColor();
            }
        }
        long mainLoopMs = stopwatch.ElapsedMilliseconds;
        /*
        Console.WriteLine($"[T] 主循环 compute 累计: {tCompute * 1000 / freq} ms");
        Console.WriteLine($"[T] 主循环 DXF 累计:     {tDxf * 1000 / freq} ms");
        Console.WriteLine($"[T] 主循环 CSV 累计:     {tCsv * 1000 / freq} ms");
        Console.WriteLine($"[T] 主循环 总计(墙钟):   {mainLoopMs} ms");
        stopwatch.Restart();
*/
        // ================= 统计输出 =================
        Console.WriteLine();
        Console.WriteLine("=========================");
        Console.WriteLine($" =======断面统计:======");
        Console.WriteLine($" 成功✓✓: {successCount}");
        Console.WriteLine($" 失败××: {failedCount}");
        Console.WriteLine($"总计^^: {total}");
        Console.WriteLine("=========================");
        if (failedCount > 0) {
            Console.WriteLine(" 失败详情:");
            for (int j = 0; j < failedStations.Count; j++)
                Console.WriteLine($"   {failedStations[j]}: {failedReasons[j]}");
        } else {
            Console.WriteLine(" 失败桩号: 无");
        }
        if (successCount > 0) {
            if (failedCount == 0) {
                Console.WriteLine($" 全部 {successCount} 个断面处理成功");
            } else {
                if (successStations.Count > 10) {
                    Console.WriteLine($"成功桩号: {string.Join(", ", successStations.Take(5))} ... 共 {successStations.Count} 个");
                } else {
                    Console.WriteLine($" 成功桩号: {string.Join(", ", successStations)}");
                }
            }
        }

        // ================= 写 CSV =================
        File.WriteAllText(csvPath, csvSb.ToString(), Encoding.UTF8);
        //  Console.WriteLine($"[T] 写 CSV: {stopwatch.ElapsedMilliseconds} ms");
        //  stopwatch.Restart();

        // ================= 写 DXF（按开关） =================
        if (AppConfig.OutputDxf) {
            string dxfContent = DxfBuilder.BuildDxf(dxfEntities);
            File.WriteAllText(dxfPath, dxfContent, new UTF8Encoding(false));
            // Console.WriteLine($"[T] 写 DXF: {stopwatch.ElapsedMilliseconds} ms");
            // stopwatch.Restart();
        }

        // ================= 生成 + 写 HTML（按开关） =================
        if (AppConfig.OutputHtml) {
            string html = SvgBuilder.BuildHtml(projectName, sectionResults);
            File.WriteAllText(Path.Combine(resultDir, projectName + ".html"), html, Encoding.UTF8);
            //Console.WriteLine($"[T] 生成 + 写 HTML: {stopwatch.ElapsedMilliseconds} ms");
        }

        Console.WriteLine($"{projectName}: 处理完成");
    }

    // ========== 辅助方法 ==========

    private static void AppendSectionToDxf(StringBuilder dxf, SectionResult r, ref double currentOffsetX) {
        double width = r.MaxX - r.MinX > 0 ? r.MaxX - r.MinX : 40.0;
        double offX = currentOffsetX - r.MinX;
        double offY = 0;

        LL.AppendDxfLwPolyline(dxf, r.FinalDesign, offX, offY, "DESIGN_ROAD_TRIMMED", 1);
        LL.AppendDxfLwPolyline(dxf, r.Ground, offX, offY, "NATURAL_GROUND", 3);
        LL.AppendDxfLwPolyline(dxf, r.Cleared, offX, offY, "CLEAR_GROUND", 8);

        if (r.FinalFinished.GetLength(0) > 0)
            LL.AppendDxfLwPolyline(dxf, r.FinalFinished, offX, offY, "FINISHED_ROAD", 2);

        if (r.LeftSubgrade.GetLength(0) > 0)
            LL.AppendDxfLwPolyline(dxf, r.LeftSubgrade, offX, offY, "STRUCTURE_LEFT", 4);
        if (r.RightSubgrade.GetLength(0) > 0)
            LL.AppendDxfLwPolyline(dxf, r.RightSubgrade, offX, offY, "STRUCTURE_RIGHT", 6);

        for (int i = 0; i < r.LayerPolygons.Count; i++) {
            int color = i < r.LayerPolygons.Count / 2 ? 4 : 6;
            LL.AppendDxfLwPolyline(dxf, r.LayerPolygons[i], offX, offY, "LAYER_" + i.ToString(Inv), color);
        }

        string station = LL.hua_Num2K(r.Station);

        var infoTexts = new List<string>(3 + r.LayerAreaTexts.Count);
        infoTexts.Add("AT:" + r.FillArea.ToString("F3", Inv));
        infoTexts.Add("AW:" + r.CutArea.ToString("F3", Inv));
        infoTexts.Add("Topsoil:" + r.ClearArea.ToString("F3", Inv));
        infoTexts.AddRange(r.LayerAreaTexts);

        double rawW = width * 1.25;
        double estH = infoTexts.Count * (rawW * 0.015 * 1.5) + rawW * 0.015 * 5.0;
        double rawH = (r.CenterY - r.MinY + estH) * 1.35;
        double scale = Math.Max(rawW / AppConfig.A4WidthMM, rawH / AppConfig.A4HeightMM);
        if (scale < 0.1) scale = 0.1;
        double textScale = scale * AppConfig.TextScaleFactor;
        double lineSpacing = textScale * AppConfig.LineSpacingFactor;

        double boxW = AppConfig.A4WidthMM * scale;
        double boxH = AppConfig.A4HeightMM * scale;
        double boxMinX = offX - boxW / 2;
        double boxMinY = r.MinY - textScale * 4 - infoTexts.Count * lineSpacing - textScale * 3;

        double[,] frame = {
            { boxMinX, boxMinY }, { boxMinX + boxW, boxMinY },
            { boxMinX + boxW, boxMinY + boxH }, { boxMinX, boxMinY + boxH }, { boxMinX, boxMinY }
        };
        LL.AppendDxfLwPolyline(dxf, frame, 0, 0, "A4_BORDER", 7);

        LL.AppendDxfTextCenter(dxf, station, offX, boxMinY + textScale * 25, textScale * 1.2, "STATION_TITLE");
        LL.AppendDxfTextCenter(dxf, r.CenterY.ToString("F3", Inv), offX, r.CenterY + textScale * 1.2, textScale, "POINT_TEXT_H", 90);
        LL.AppendDxfTextCenter(dxf, r.LOuterY.ToString("F3", Inv), offX + r.LOuterX, r.LOuterY + textScale * 1.2, textScale, "LEFT_TEXT_H", 90);
        LL.AppendDxfTextCenter(dxf, r.ROuterY.ToString("F3", Inv), offX + r.ROuterX, r.ROuterY + textScale * 1.2, textScale, "RIGHT_TEXT_H", 90);

        double lcf = Math.Abs(r.LeftCrossfall) < 0.2 ? r.LeftCrossfall * 100 : r.LeftCrossfall;
        double rcf = Math.Abs(r.RightCrossfall) < 0.2 ? r.RightCrossfall * 100 : r.RightCrossfall;
        LL.AppendDxfTextCenter(dxf, lcf.ToString("F2", Inv) + "%",
            offX + r.LOuterX / 2,
            r.CenterY + Math.Abs(r.LOuterX / 2) * r.LeftCrossfall / 100 + textScale * 0.8,
            textScale, "SLOPE_TEXT");
        LL.AppendDxfTextCenter(dxf, rcf.ToString("F2", Inv) + "%",
            offX + r.ROuterX / 2,
            r.CenterY + Math.Abs(r.ROuterX / 2) * r.RightCrossfall / 100 + textScale * 0.8,
            textScale, "SLOPE_TEXT");

        AnnotateSlope(dxf, r.LeftSlopePoints, r.LeftToeX, r.LeftToeY, r.LOuterX, r.LOuterY, offX, textScale, true);
        AnnotateSlope(dxf, r.RightSlopePoints, r.RightToeX, r.RightToeY, r.ROuterX, r.ROuterY, offX, textScale, false);

        double tx = offX - textScale * 4.5;
        double baseY = boxMinY + textScale * 25 - textScale * 2;
        for (int i = 0; i < infoTexts.Count; i++)
            LL.AppendDxfText(dxf, infoTexts[i], tx, baseY - i * lineSpacing, textScale, "INFO_TEXT");

        currentOffsetX += boxW + AppConfig.A4Gap;
    }

    private static void AnnotateSlope(StringBuilder dxf, List<double[]> slopePoints,
        double startX, double startY, double endX, double endY, double offX, double textScale, bool isLeft) {
        if (slopePoints == null || slopePoints.Count < 2) return;
        var pts = new List<double[]>();
        pts.Add(new[] { startX, startY });
        foreach (var p in slopePoints) {
            if (isLeft && p[0] > startX) pts.Add(p);
            else if (!isLeft && p[0] < startX) pts.Add(p);
        }
        pts.Add(new[] { endX, endY });

        if (endX < startX)
            pts.Sort((a, b) => b[0].CompareTo(a[0]));
        else
            pts.Sort((a, b) => a[0].CompareTo(b[0]));

        for (int i = 0; i < pts.Count - 1; i++) {
            double x1 = pts[i][0], y1 = pts[i][1], x2 = pts[i + 1][0], y2 = pts[i + 1][1];
            double dx = x2 - x1, dy = y2 - y1;
            if (Math.Abs(dy) > 0.01 && Math.Abs(dx) > 0.01) {
                double ratio = Math.Abs(dx) / Math.Abs(dy);
                double midX = offX + (x1 + x2) / 2.0;
                double midY = (y1 + y2) / 2.0;
                double angle = Math.Atan2(dy, dx) * (180.0 / Math.PI);

                string label = "1:" + ratio.ToString("F3", Inv);

                if (isLeft) {
                    LL.AppendDxfTextSlope(dxf, label, midX, midY, textScale, angle, "SLOPE_left");
                } else {
                    LL.AppendDxfTextSlope(dxf, label, midX, midY, textScale, angle, "SLOPE_right");
                }
            }
        }
    }
}