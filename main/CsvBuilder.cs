using System;
using System.Collections.Generic;
using System.Text;
using jiegouceng;          // 包含 LeftStructureLayerConfig
using topSlope; // 包含 RightStructureLayerConfig（根据实际命名空间调整）

public static class CsvBuilder
{
    public static string BuildHeader(
        List<LeftJiegoucengConfig> leftStructures,
        List<RightJiegoucengConfig> rightStructures)
    {
        var sb = new StringBuilder();
        sb.Append("桩号,填方面积(㎡),挖方面积(㎡),清表面积(㎡),清表左边界X,清表右边界X");

        if (leftStructures != null)
        {
            for (int i = 0; i < leftStructures.Count; i++)
            {
                string name = !string.IsNullOrEmpty(leftStructures[i].LayerName)
                    ? leftStructures[i].LayerName
                    : $"left{i + 1}";
                sb.Append($",{name}");
            }
        }

        if (rightStructures != null)
        {
            for (int i = 0; i < rightStructures.Count; i++)
            {
                string name = !string.IsNullOrEmpty(rightStructures[i].LayerName)
                    ? rightStructures[i].LayerName
                    : $"right{i + 1}";
                sb.Append($",{name}");
            }
        }

        sb.AppendLine();
        return sb.ToString();
    }

    public static string BuildDataRow(string station, double fill, double cut, double clearArea, double minX, double maxX, string layerAreasCsv)
    {
        return $"{station},{fill:F3},{cut:F3},{clearArea:F3},{minX:F3},{maxX:F3}{layerAreasCsv}";
    }
}