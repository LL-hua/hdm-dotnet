using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        // 获取当前工作目录
        string cwd = Directory.GetCurrentDirectory();

        // 需要排除的目录
        var exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", "result", ".git", ".idea", "vendor", "packages", "node_modules"
        };

        // 收集有效项目目录（排除隐藏目录、系统目录和结果目录）
        var projects = new List<string>();
        foreach (var dir in Directory.GetDirectories(cwd))
        {
            string name = Path.GetFileName(dir);

            // 跳过隐藏目录（以 "." 开头）
            if (name.StartsWith(".")) continue;
            if (exclude.Contains(name)) continue;

            projects.Add(name);
        }

        projects.Sort(StringComparer.OrdinalIgnoreCase);

        if (projects.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("当前目录下没有找到任何项目文件夹。");
            Console.ResetColor();
            return;
        }

        // 显示项目列表
        Console.WriteLine();
        Console.WriteLine("📁 可用的项目：");
        for (int i = 0; i < projects.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {projects[i]}");
        }

        // 用户选择
        Console.Write($"\n请输入项目序号 (1~{projects.Count})，或输入 0 退出: ");
        string input = Console.ReadLine()?.Trim();

        if (!int.TryParse(input, out int idx) || idx < 0 || idx > projects.Count)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"无效的序号，请输入 0~{projects.Count}");
            Console.ResetColor();
            return;
        }

        if (idx == 0)
        {
            Console.WriteLine("退出程序。");
            return;
        }

        // 处理选中的项目
        string selected = projects[idx - 1];
        Console.WriteLine($"开始处理项目: {selected}");

        var sw = Stopwatch.StartNew();
        try
        {
            ProjectProcessor.Process(selected);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"项目 {selected} 处理失败: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            return;
        }
        sw.Stop();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"项目 {selected} 处理完成，总耗时: {sw.ElapsedMilliseconds} ms");
        Console.ResetColor();
    }
}