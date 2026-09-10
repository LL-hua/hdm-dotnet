using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using tianwaBianpo;
using topSlope;
using jiegouceng;
using LLutile;
using System.Diagnostics;
using System.Globalization;
public static class ProjectProcessor {
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
/*
        {
            List<double> ks = new List<double>();
            for (int k = 85525; k < 118700; k = k + 25) {
                ks.Add(k);
            }
            File.WriteAllLines("ks.txt", ks.Select(x => x.ToString()));
        }
*/


        checkData.SourceDataValidator.ValidateAllSourceFiles(lt, rt, bp, l_st, r_st, l_cf, r_cf, xyzPath); var stopwatch = Stopwatch.StartNew();
        // var mesh = TerrainMesh.FromTextFile(xyzPath, AppConfig.XyzRows, AppConfig.XyzCols);
        double[,] pqx = LL.ReadDataFromFile(pqxPath, 8);
        double[,] sqx = LL.ReadDataFromFile(sqxPath, 3);
        double[,] kzb = LL.ReadDataFromFile(kzbPath, 1);

        //  xyz
        double[,] xyz = LL.ReadDataFromFile(xyzPath, 3, null, true);
        xyz = LL.hua_Fs_Batch(pqx, xyz);
        LL.SortInPlace(xyz);
        // Console.WriteLine(kbz.GetLength(0));
        var leftWidths = LuJiYaoSuYinQing.parseKuandDuFile(lt);
        var rightWidths = LuJiYaoSuYinQing.parseKuandDuFile(rt);
        var slopeData = BianPoYinQing.parseBianPo(bp);
        var leftCrossfalls = LumianSlopeManager.ParseFile(l_cf);
        var rightCrossfalls = LumianSlopeManager.ParseFile(r_cf);
        var leftStructures = LeftJiegoucengManager.ParseFile(l_st);
        var rightStructures = RightJiegoucengManager.ParseFile(r_st);

        var calculator = new SectionCalculator(
            xyz, pqx, sqx, leftWidths, rightWidths, slopeData,
            leftCrossfalls, rightCrossfalls, leftStructures, rightStructures);

        string csvHeader = CsvBuilder.BuildHeader(leftStructures, rightStructures);
        var csvSb = new StringBuilder(csvHeader);
        var dxfEntities = new StringBuilder();

        double currentOffsetX = 0.0;
        var sectionResults = new List<SectionResult>();

        // 📊 统计计数器 + 桩号列表 + 失败原因
        int total = kzb.GetLength(0);
        int successCount = 0;
        int failedCount = 0;
        var successStations = new List<string>();
        var failedStations = new List<string>();
        var failedReasons = new List<string>();

        for (int i = 0; i < total; i++) {
            double st = kzb[i, 0];
            string stationStr = LL.hua_Num2K(st);

            // Console.Write($"\r进度: {i + 1}/{total}  ✅成功:{successCount}  ❌失败:{failedCount}");

            try {
                var computeResult = calculator.Compute(st);
                //  Console.WriteLine("computeResult.Success:" + computeResult.Success);
                if (!computeResult.Success) {
                    failedCount++;
                    failedStations.Add(stationStr);
                    failedReasons.Add(computeResult.ErrorMessage);
                    continue;
                }

                var result = computeResult.Result;

                AppendSectionToDxf(dxfEntities, result, ref currentOffsetX);

                string layerCsv = "";
                foreach (var areaText in result.LayerAreaTexts) {
                    string[] parts = areaText.Split(':');
                    double area = double.Parse(parts[2]);
                    layerCsv += $",{area:F3}";
                }
                csvSb.AppendLine(CsvBuilder.BuildDataRow(
                    stationStr, result.FillArea, result.CutArea, result.ClearArea,
                    result.MinX, result.MaxX, layerCsv));
                sectionResults.Add(result);
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
        Console.WriteLine();
        Console.WriteLine("=========================");
        Console.WriteLine($" 断面统计:");
        Console.WriteLine($" 成功: {successCount}");
        Console.WriteLine($" 失败: {failedCount}");
        Console.WriteLine($"总计: {total}");
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
                // 如果成功桩号太多（超过20个），只显示前5个 + 总数
                if (successStations.Count > 20) {
                    Console.WriteLine($"成功桩号: {string.Join(", ", successStations.Take(5))} ... 共 {successStations.Count} 个");
                } else {
                    Console.WriteLine($" 成功桩号: {string.Join(", ", successStations)}");
                }
            }
        }


        // --- 写入文件 ---
        File.WriteAllText(csvPath, csvSb.ToString(), Encoding.UTF8);
        string dxfContent = DxfBuilder.BuildDxf(dxfEntities);
        File.WriteAllText(dxfPath, dxfContent, new UTF8Encoding(false));

        string html = SvgBuilder.BuildHtml(projectName, sectionResults);
        File.WriteAllText(Path.Combine(resultDir, projectName + ".html"), html, Encoding.UTF8);

        Console.WriteLine($"{projectName}: 处理完成");
    }

    // ========== 以下辅助方法不变 ==========

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
            LL.AppendDxfLwPolyline(dxf, r.LayerPolygons[i], offX, offY, $"LAYER_{i}", color);
        }

        string station = LL.hua_Num2K(r.Station);
        var infoTexts = new List<string> {
            $"AT:{r.FillArea:F3}",
            $"AW:{r.CutArea:F3}",
            $"Topsoil:{r.ClearArea:F3}"
        };
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
        LL.AppendDxfTextCenter(dxf, $"{r.CenterY:F3}", offX, r.CenterY + textScale * 1.2, textScale, "POINT_TEXT_H", 90);
        LL.AppendDxfTextCenter(dxf, $"{r.LOuterY:F3}", offX + r.LOuterX, r.LOuterY + textScale * 1.2, textScale, "LEFT_TEXT_H", 90);
        LL.AppendDxfTextCenter(dxf, $"{r.ROuterY:F3}", offX + r.ROuterX, r.ROuterY + textScale * 1.2, textScale, "RIGHT_TEXT_H", 90);

        double lcf = Math.Abs(r.LeftCrossfall) < 0.2 ? r.LeftCrossfall * 100 : r.LeftCrossfall;
        double rcf = Math.Abs(r.RightCrossfall) < 0.2 ? r.RightCrossfall * 100 : r.RightCrossfall;
        LL.AppendDxfTextCenter(dxf, $"{lcf:F2}%", offX + r.LOuterX / 2, r.CenterY + Math.Abs(r.LOuterX / 2) * r.LeftCrossfall / 100 + textScale * 0.8, textScale, "SLOPE_TEXT");
        LL.AppendDxfTextCenter(dxf, $"{rcf:F2}%", offX + r.ROuterX / 2, r.CenterY + Math.Abs(r.ROuterX / 2) * r.RightCrossfall / 100 + textScale * 0.8, textScale, "SLOPE_TEXT");


        //边坡标注
        // Console.WriteLine();
        //  Console.WriteLine();
        AnnotateSlope(dxf, r.LeftSlopePoints, r.LeftToeX, r.LeftToeY, r.LOuterX, r.LOuterY, offX, textScale, true);
        //  Console.WriteLine();
        //  Console.WriteLine();
        AnnotateSlope(dxf, r.RightSlopePoints, r.RightToeX, r.RightToeY, r.ROuterX, r.ROuterY, offX, textScale, false);

        //AnnotateSlope(dxf, r.RightSlopePoints, r.ROuterX, r.ROuterY, r.RightToeX, r.RightToeY, offX, textScale, false);

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
        // var pts = new List<double[]>();
        pts.Add(new[] { startX, startY });
        foreach (var p in slopePoints) {
            if (isLeft && p[0] > startX) pts.Add(p);
            else if (!isLeft && p[0] < startX) pts.Add(p);
        }
        pts.Add(new[] { endX, endY });

        // 让 pts 始终按 startX → endX 排列
        if (endX < startX)
            pts.Sort((a, b) => b[0].CompareTo(a[0]));  // 降序
        else
            pts.Sort((a, b) => a[0].CompareTo(b[0]));  // 升序
        for (int i = 0; i < pts.Count - 1; i++) {
            double x1 = pts[i][0], y1 = pts[i][1], x2 = pts[i + 1][0], y2 = pts[i + 1][1];
            double dx = x2 - x1, dy = y2 - y1;
            //  Console.WriteLine($"{x1:f3},{y1:f3}  {x2:f3},{y2:f3}");
            if (Math.Abs(dy) > 0.01 && Math.Abs(dx) > 0.01) {
                double ratio = Math.Abs(dx) / Math.Abs(dy);
                double midX = offX + (x1 + x2) / 2.0;
                double midY = (y1 + y2) / 2.0;
                double angle = Math.Atan2(dy, dx) * (180.0 / Math.PI);

                if (isLeft) {
                    LL.AppendDxfTextSlope(dxf, $"1:{ratio:F3}", midX, midY, textScale, angle, "SLOPE_left");


                } else {
                    LL.AppendDxfTextSlope(dxf, $"1:{ratio:F3}", midX, midY, textScale, angle, "SLOPE_right");


                }

            }
        }

    }
}