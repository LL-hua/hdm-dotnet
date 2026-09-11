using System;
using System.IO;
using System.Text;

public static class AppConfig
{
    // 默认值（与 Go 版本一致）
    private const double DefaultClearDepth = -0.3;
    private const double DefaultA4WidthMM = 297.0;
    private const double DefaultA4HeightMM = 210.0;
    private const double DefaultA4Gap = 30.0;
    private const int DefaultXyzRows = 35;
    private const int DefaultXyzCols = 60;
    private const double DefaultTextScaleFactor = 3.0;
    private const double DefaultLineSpacingFactor = 1.5;
    private const bool DefaultOutputDxf = true;
    private const bool DefaultOutputHtml = true;

    // 运行时变量（可由配置文件覆盖）
    public static double ClearDepth { get; private set; } = DefaultClearDepth;
    public static double A4WidthMM { get; private set; } = DefaultA4WidthMM;
    public static double A4HeightMM { get; private set; } = DefaultA4HeightMM;
    public static double A4Gap { get; private set; } = DefaultA4Gap;
    public static int XyzRows { get; private set; } = DefaultXyzRows;
    public static int XyzCols { get; private set; } = DefaultXyzCols;
    public static double TextScaleFactor { get; private set; } = DefaultTextScaleFactor;
    public static double LineSpacingFactor { get; private set; } = DefaultLineSpacingFactor;
    public static bool OutputDxf { get; private set; } = DefaultOutputDxf;
    public static bool OutputHtml { get; private set; } = DefaultOutputHtml;

    /// <summary>
    /// 从项目文件夹加载配置文件（项目名.config）
    /// 格式：每行一个配置项，等号分隔，分号后为注释
    /// 例如：ClearDepth = -0.3    ; 清表深度
    /// </summary>
    public static void LoadConfig(string projectName, string projectDir)
    {
        string configPath = Path.Combine(projectDir, projectName + ".config");
        if (!File.Exists(configPath))
            return;

        try
        {
            string[] lines = File.ReadAllLines(configPath, Encoding.UTF8);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";"))
                    continue;

                int eqIdx = trimmed.IndexOf('=');
                if (eqIdx < 0)
                    continue;

                string key = trimmed.Substring(0, eqIdx).Trim();
                string value = trimmed.Substring(eqIdx + 1).Trim();

                // 去除行内注释（分号后的内容）
                int commentIdx = value.IndexOf(';');
                if (commentIdx >= 0)
                    value = value.Substring(0, commentIdx).Trim();

                // 解析并赋值
                switch (key)
                {
                    case "ClearDepth":
                        if (double.TryParse(value, out double cd)) ClearDepth = cd;
                        break;
                    case "A4WidthMM":
                        if (double.TryParse(value, out double w)) A4WidthMM = w;
                        break;
                    case "A4HeightMM":
                        if (double.TryParse(value, out double h)) A4HeightMM = h;
                        break;
                    case "A4Gap":
                        if (double.TryParse(value, out double gap)) A4Gap = gap;
                        break;
                    case "XyzRows":
                        if (int.TryParse(value, out int rows)) XyzRows = rows;
                        break;
                    case "XyzCols":
                        if (int.TryParse(value, out int cols)) XyzCols = cols;
                        break;
                    case "TextScaleFactor":
                        if (double.TryParse(value, out double tf)) TextScaleFactor = tf;
                        break;
                    case "LineSpacingFactor":
                        if (double.TryParse(value, out double lsf)) LineSpacingFactor = lsf;
                        break;
                    case "OutputDxf":
                        if (bool.TryParse(value, out bool od)) OutputDxf = od;
                        break;
                    case "OutputHtml":
                        if (bool.TryParse(value, out bool oh)) OutputHtml = oh;
                        break;
                }
            }
        }
        catch
        {
            // 配置文件解析失败则忽略，使用默认值
        }
    }
}