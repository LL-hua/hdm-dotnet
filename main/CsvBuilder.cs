using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using jiegouceng;
using topSlope;

public static class CsvBuilder
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string BuildHeader(
        List<LeftJiegoucengConfig> leftStructures,
        List<RightJiegoucengConfig> rightStructures)
    {
        var sb = new StringBuilder(256);
        sb.Append("桩号,填方面积(㎡),挖方面积(㎡),清表面积(㎡),清表左边界X,清表右边界X");

        if (leftStructures != null)
        {
            for (int i = 0; i < leftStructures.Count; i++)
            {
                string name = !string.IsNullOrEmpty(leftStructures[i].LayerName)
                    ? leftStructures[i].LayerName
                    : "left" + (i + 1).ToString(Inv);
                sb.Append(',').Append(name);
            }
        }

        if (rightStructures != null)
        {
            for (int i = 0; i < rightStructures.Count; i++)
            {
                string name = !string.IsNullOrEmpty(rightStructures[i].LayerName)
                    ? rightStructures[i].LayerName
                    : "right" + (i + 1).ToString(Inv);
                sb.Append(',').Append(name);
            }
        }

        sb.AppendLine();
        return sb.ToString();
    }

    public static string BuildDataRow(string station, double fill, double cut, double clearArea, double minX, double maxX, string layerAreasCsv)
    {
        var sb = new StringBuilder(128 + layerAreasCsv.Length);
        sb.Append(station).Append(',')
          .Append(fill.ToString("F3", Inv)).Append(',')
          .Append(cut.ToString("F3", Inv)).Append(',')
          .Append(clearArea.ToString("F3", Inv)).Append(',')
          .Append(minX.ToString("F3", Inv)).Append(',')
          .Append(maxX.ToString("F3", Inv))
          .Append(layerAreasCsv);
        return sb.ToString();
    }
}