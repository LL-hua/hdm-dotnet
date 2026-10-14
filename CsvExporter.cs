using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Hdm
{
    public static class CsvExporter
    {
        /// <summary>
        /// 按桩号输出土方汇总 CSV。
        /// 列：桩号, 间距, 填方面积, 挖方面积, 清表面积,
        ///     平均填方, 平均挖方, 填方体积, 挖方体积, 累计填方, 累计挖方
        /// </summary>
        public static string SaveEarthwork(IList<SectionResult> sections, string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("桩号,间距(m),填方面积(㎡),挖方面积(㎡),清表面积(㎡)," +
                          "平均填方面积(㎡),平均挖方面积(㎡),填方体积(m³),挖方体积(m³)," +
                          "累计填方(m³),累计挖方(m³)");

            double cumFill = 0, cumCut = 0;
            double prevK = 0, prevFill = 0, prevCut = 0;
            bool first = true;

            var ci = CultureInfo.InvariantCulture;
            for (int i = 0; i < sections.Count; i++)
            {
                var s = sections[i];
                double k = s.station;
                double fill = s.fillArea;
                double cut = s.cutArea;
                double clear = s.clearArea;

                double span = 0, avgFill = 0, avgCut = 0, vFill = 0, vCut = 0;

                if (!first)
                {
                    span = k - prevK;
                    avgFill = (prevFill + fill) / 2.0;
                    avgCut = (prevCut + cut) / 2.0;
                    vFill = avgFill * span;
                    vCut = avgCut * span;
                    cumFill += vFill;
                    cumCut += vCut;
                }

                sb.Append(Escape(LL.hua_Num2K(k))).Append(',');
                sb.Append(span.ToString("F3", ci)).Append(',');
                sb.Append(fill.ToString("F3", ci)).Append(',');
                sb.Append(cut.ToString("F3", ci)).Append(',');
                sb.Append(clear.ToString("F3", ci)).Append(',');
                sb.Append(avgFill.ToString("F3", ci)).Append(',');
                sb.Append(avgCut.ToString("F3", ci)).Append(',');
                sb.Append(vFill.ToString("F3", ci)).Append(',');
                sb.Append(vCut.ToString("F3", ci)).Append(',');
                sb.Append(cumFill.ToString("F3", ci)).Append(',');
                sb.Append(cumCut.ToString("F3", ci));
                sb.AppendLine();

                prevK = k; prevFill = fill; prevCut = cut;
                first = false;
            }

            sb.AppendLine();
            sb.Append("合计,,,,,,,,,");
            sb.Append(cumFill.ToString("F3", ci)).Append(',');
            sb.Append(cumCut.ToString("F3", ci));
            sb.AppendLine();

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            return path;
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            if (s.IndexOfAny(new[] { ',', '"', '\n' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}