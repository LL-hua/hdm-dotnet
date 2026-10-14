using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Hdm;

class Program
{
    static void Main(string[] args)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; }
        catch { }

        string baseDir = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(baseDir))
            baseDir = Directory.GetCurrentDirectory();

        Console.WriteLine("程序目录: " + baseDir);

        string[] files;
        try
        {
            files = Directory.GetFiles(baseDir, "*.xlsx")
                             .Where(f => !f.EndsWith("test.xlsx", StringComparison.OrdinalIgnoreCase))
                             .ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine("读取目录失败: " + ex.Message);
            Console.ReadLine();
            return;
        }

        if (files.Length == 0)
        {
            Console.WriteLine("在 " + baseDir + " 下找不到 .xlsx（除 test.xlsx）");
            Console.WriteLine("请把 xlsx 复制到该目录后重试。");
            Console.ReadLine();
            return;
        }

        Console.WriteLine("找到 " + files.Length + " 个 xlsx：");
        for (int i = 0; i < files.Length; i++)
            Console.WriteLine("  [" + i + "] " + Path.GetFileName(files[i]));

        Console.Write("选择序号（默认 0）: ");
        string pick = Console.ReadLine();
        int pickIdx = 0;
        if (!string.IsNullOrEmpty(pick)) int.TryParse(pick.Trim(), out pickIdx);
        if (pickIdx < 0 || pickIdx >= files.Length) pickIdx = 0;

        string xlsxPath = files[pickIdx];
        string baseName = Path.GetFileNameWithoutExtension(xlsxPath);
        Console.WriteLine();
        Console.WriteLine("项目文件: " + xlsxPath);
        Console.WriteLine("项目名称: " + baseName);
        Console.WriteLine();

        ProjectData data;
        var sw = Stopwatch.StartNew();
        try
        {
            data = XlsxProjectData.Load(xlsxPath);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("加载失败: " + ex.Message);
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            Console.WriteLine("按回车退出...");
            Console.ReadLine();
            return;
        }
        sw.Stop();
        Console.WriteLine("加载完成，耗时 " + sw.ElapsedMilliseconds + " ms");
        Console.WriteLine("桩号总数: " + data.Stations().Length);
        Console.WriteLine();

        while (true)
        {
            Console.Write("请输入桩号（米，例如 103200），或输入 q 退出，或输入 all 批量导出: ");
            string input = Console.ReadLine();
            if (input == null) break;
            input = input.Trim();
            if (input.Length == 0 || input.Equals("q", StringComparison.OrdinalIgnoreCase))
                break;

            if (input.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                DoBatchExport(data, baseDir, baseName);
                continue;
            }

            double station;
            if (!double.TryParse(input, out station))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("桩号格式错误");
                Console.ResetColor();
                continue;
            }

            var t = Stopwatch.StartNew();
            ComputeResult r;
            try
            {
                r = data.Query(station);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("计算异常: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
                continue;
            }
            t.Stop();

            Console.WriteLine();
            Console.WriteLine("=========================");
            Console.WriteLine(" 桩号: " + LL.hua_Num2K(station) + "  (" + station + " m)");

            if (!r.success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(" 失败: " + r.errorMessage);
                Console.ResetColor();
            }
            else
            {
                var s = r.result;
                Console.WriteLine(" 填方面积: " + s.fillArea.ToString("F3") + " ㎡");
                Console.WriteLine(" 挖方面积: " + s.cutArea.ToString("F3") + " ㎡");
                Console.WriteLine(" 清表面积: " + s.clearArea.ToString("F3") + " ㎡");
                Console.WriteLine(" 中桩高程: " + s.centerY.ToString("F3"));
                Console.WriteLine(" 清表范围: [" + s.bounds[0].ToString("F3")
                                  + ", " + s.bounds[2].ToString("F3") + "]");
                Console.WriteLine(" 结构层:");
                if (s.layerAreas != null)
                {
                    foreach (var txt in s.layerAreas)
                        Console.WriteLine("   " + txt);
                }
            }
            Console.WriteLine(" 耗时: " + t.Elapsed.TotalMilliseconds.ToString("F2") + " ms");
            Console.WriteLine("=========================");
            Console.WriteLine();
        }

        Console.WriteLine("退出。");
    }

    // ============================================================
    // 批量导出：出图 HTML + 土方数据 CSV + 出错位置 TXT
    // ============================================================
    static void DoBatchExport(ProjectData data, string baseDir, string baseName)
    {
        var stations = data.Stations();
        Console.WriteLine("共 " + stations.Length + " 个桩号，开始计算...");

        var results = new List<SectionResult>();
        var errors = new List<string>();     // 每行: "K85+600\t边坡段落为空"
        var batchSw = Stopwatch.StartNew();
        int okCount = 0, failCount = 0;

        for (int i = 0; i < stations.Length; i++)
        {
            double k = stations[i];
            ComputeResult rr;
            try
            {
                rr = data.Query(k);
            }
            catch (Exception ex)
            {
                failCount++;
                errors.Add(LL.hua_Num2K(k) + "\t计算异常: " + ex.Message);
                continue;
            }

            if (rr.success && rr.result != null)
            {
                results.Add(rr.result);
                okCount++;
            }
            else
            {
                failCount++;
                string msg = string.IsNullOrEmpty(rr.errorMessage) ? "未知错误" : rr.errorMessage;
                errors.Add(LL.hua_Num2K(k) + "\t" + msg);
            }

            if ((i + 1) % 100 == 0)
                Console.WriteLine("  进度 " + (i + 1) + "/" + stations.Length);
        }
        batchSw.Stop();

        Console.WriteLine("计算完成：成功 " + okCount + "，失败 " + failCount
                          + "，耗时 " + batchSw.ElapsedMilliseconds + " ms");

        if (results.Count == 0)
        {
            Console.WriteLine("没有可导出的断面。");
            return;
        }

        // ---------- 出图 HTML ----------
        string htmlPath = Path.Combine(baseDir, baseName + "_出图.html");
        try
        {
            HtmlSectionExporter.SaveBatch(results, baseName + " 横断面", htmlPath);
            var fi = new FileInfo(htmlPath);
            Console.WriteLine("已导出出图: " + htmlPath);
            Console.WriteLine("  文件大小: " + (fi.Length / 1024) + " KB");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("导出出图失败: " + ex.Message);
            Console.ResetColor();
        }

        // ---------- 土方数据 CSV ----------
        string csvPath = Path.Combine(baseDir, baseName + "_土方数据.csv");
        try
        {
            CsvExporter.SaveEarthwork(results, csvPath);
            var fi = new FileInfo(csvPath);
            Console.WriteLine("已导出土方数据: " + csvPath);
            Console.WriteLine("  文件大小: " + (fi.Length / 1024) + " KB");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("导出土方数据失败: " + ex.Message);
            Console.ResetColor();
        }

        // ---------- 出错位置 TXT ----------
        if (errors.Count > 0)
        {
            string errPath = Path.Combine(baseDir, baseName + "_出错位置.txt");
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine(baseName + " 出错位置清单");
                sb.AppendLine("生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("失败总数: " + errors.Count);
                sb.AppendLine();
                sb.AppendLine("桩号\t原因");
                sb.AppendLine("----------------------------------------");
                foreach (var line in errors) sb.AppendLine(line);
                File.WriteAllText(errPath, sb.ToString(), new System.Text.UTF8Encoding(true));
                Console.WriteLine("已导出出错位置: " + errPath);
                Console.WriteLine("  失败数: " + errors.Count);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("导出出错位置失败: " + ex.Message);
                Console.ResetColor();
            }
        }
        else
        {
            Console.WriteLine("没有失败桩号，未生成出错位置文件。");
        }

        Console.WriteLine();
    }
}