using System;
using System.IO;
using System.Collections.Generic;

namespace topSlope {

    public class DuanMianShuJu {
        public double ZhuangHao { get; set; }        // 每一行的第一个数字：绝对桩号
        public List<double[]> BanKuaiJiHe { get; set; } // 后面连续两两配对的物理板块：[[宽度, 横坡%], ...]

        public DuanMianShuJu() {
            BanKuaiJiHe = new List<double[]>();
        }
    }

    public class JueDuiBanKuaiDian {
        public double WidthX { get; set; }   // 绝对横坐标（中桩为0，左负右正）
        public double GaoChengY { get; set; } // 绝对高程

        public JueDuiBanKuaiDian(double x, double y) {
            WidthX = x;
            GaoChengY = y;
        }
    }

    public class LuJiCheDaoJieGuoBao {
        public List<JueDuiBanKuaiDian> ZuoCeCheDaoJueDui { get; set; } // 左侧所有拐点（已自动反转，从外侧向内指向中桩）
        public List<JueDuiBanKuaiDian> YouCeCheDaoJueDui { get; set; } // 右侧所有拐点（原序，从中桩向外指向外侧）

        public LuJiCheDaoJieGuoBao() {
            ZuoCeCheDaoJueDui = new List<JueDuiBanKuaiDian>();
            YouCeCheDaoJueDui = new List<JueDuiBanKuaiDian>();
        }
    }

    public static class LuJiYaoSuYinQing {

        public static List<DuanMianShuJu> parseKuandDuFile(string filePath) {
            var jieGuoList = new List<DuanMianShuJu>();
            if (!File.Exists(filePath)) return jieGuoList;
            bool isFirstLine = true;
            foreach (var line in File.ReadLines(filePath)) {
                string cleanLine = line.Trim();
                if (string.IsNullOrEmpty(cleanLine) || cleanLine.StartsWith(";")) continue;

                if (isFirstLine) {
                    isFirstLine = false;
                    continue; // 完美跳过第一行的简洁中文说明
                }

                string[] tokens = cleanLine.Split(new char[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 3) continue;

                var dmData = new DuanMianShuJu();
                dmData.ZhuangHao = Convert.ToDouble(tokens[0]);

                for (int j = 1; j < tokens.Length; j += 2) {
                    if (j + 1 >= tokens.Length) break;
                    double kuanDu = Convert.ToDouble(tokens[j]);
                    double hengPo = Convert.ToDouble(tokens[j + 1]);
                    dmData.BanKuaiJiHe.Add(new double[] { kuanDu, hengPo });
                }

                jieGuoList.Add(dmData);
            }
            return jieGuoList;
        }

        public static LuJiCheDaoJieGuoBao getcrosectonxy(
            double muBiaoZhuangHao,
            double zhongZhuangGaoCheng,
            List<DuanMianShuJu> suoYouZuoData,
            List<DuanMianShuJu> suoYouYouData) {
            var jieGuoBao = new LuJiCheDaoJieGuoBao();
            if (suoYouZuoData != null && suoYouZuoData.Count > 0) {
                double stBefore = suoYouZuoData[0].ZhuangHao;
                double stAfter = suoYouZuoData[suoYouZuoData.Count - 1].ZhuangHao;
                DuanMianShuJu dmBefore = suoYouZuoData[0];
                DuanMianShuJu dmAfter = suoYouZuoData[suoYouZuoData.Count - 1];

                if (muBiaoZhuangHao <= stBefore) { dmAfter = dmBefore; stAfter = stBefore; } else if (muBiaoZhuangHao >= stAfter) { dmBefore = dmAfter; stBefore = stAfter; } else {
                    for (int i = 1; i < suoYouZuoData.Count; i++) {
                        if (suoYouZuoData[i].ZhuangHao >= muBiaoZhuangHao) {
                            stAfter = suoYouZuoData[i].ZhuangHao;
                            dmAfter = suoYouZuoData[i];
                            stBefore = suoYouZuoData[i - 1].ZhuangHao;
                            dmBefore = suoYouZuoData[i - 1];
                            break;
                        }
                    }
                }

                double ratio = (stBefore == stAfter) ? 0.0 : (muBiaoZhuangHao - stBefore) / (stAfter - stBefore);
                double curZuoX = 0.0;
                double curZuoY = zhongZhuangGaoCheng;

                for (int k = 0; k < dmBefore.BanKuaiJiHe.Count; k++) {
                    if (k >= dmAfter.BanKuaiJiHe.Count) break;
                    double wBefore = dmBefore.BanKuaiJiHe[k][0];
                    double sBefore = dmBefore.BanKuaiJiHe[k][1];
                    double wAfter = dmAfter.BanKuaiJiHe[k][0];
                    double sAfter = dmAfter.BanKuaiJiHe[k][1];

                    double curWidth = wBefore + ratio * (wAfter - wBefore);
                    double curSlopeVal = (sBefore + ratio * (sAfter - sBefore)) / 100.0;

                    curZuoX -= curWidth;
                    curZuoY += (curWidth * curSlopeVal);
                    jieGuoBao.ZuoCeCheDaoJueDui.Add(new JueDuiBanKuaiDian(curZuoX, curZuoY));
                }

                jieGuoBao.ZuoCeCheDaoJueDui.Reverse();
            }
            if (suoYouYouData != null && suoYouYouData.Count > 0) {
                double stBefore = suoYouYouData[0].ZhuangHao;
                double stAfter = suoYouYouData[suoYouYouData.Count - 1].ZhuangHao;
                DuanMianShuJu dmBefore = suoYouYouData[0];
                DuanMianShuJu dmAfter = suoYouYouData[suoYouYouData.Count - 1];

                if (muBiaoZhuangHao <= stBefore) { dmAfter = dmBefore; stAfter = stBefore; } else if (muBiaoZhuangHao >= stAfter) { dmBefore = dmAfter; stBefore = stAfter; } else {
                    for (int i = 1; i < suoYouYouData.Count; i++) {
                        if (suoYouYouData[i].ZhuangHao >= muBiaoZhuangHao) {
                            stAfter = suoYouYouData[i].ZhuangHao;
                            dmAfter = suoYouYouData[i];
                            stBefore = suoYouYouData[i - 1].ZhuangHao;
                            dmBefore = suoYouYouData[i - 1];
                            break;
                        }
                    }
                }

                double ratio = (stBefore == stAfter) ? 0.0 : (muBiaoZhuangHao - stBefore) / (stAfter - stBefore);
                double curYouX = 0.0;
                double curYouY = zhongZhuangGaoCheng;

                for (int k = 0; k < dmBefore.BanKuaiJiHe.Count; k++) {
                    if (k >= dmAfter.BanKuaiJiHe.Count) break;
                    double wBefore = dmBefore.BanKuaiJiHe[k][0];
                    double sBefore = dmBefore.BanKuaiJiHe[k][1];
                    double wAfter = dmAfter.BanKuaiJiHe[k][0];
                    double sAfter = dmAfter.BanKuaiJiHe[k][1];
                    double curWidth = wBefore + ratio * (wAfter - wBefore);
                    double curSlopeVal = (sBefore + ratio * (sAfter - sBefore)) / 100.0;

                    curYouX += curWidth;
                    curYouY += (curWidth * curSlopeVal);

                    jieGuoBao.YouCeCheDaoJueDui.Add(new JueDuiBanKuaiDian(curYouX, curYouY));
                }
            }

            return jieGuoBao;
        }
    }
}
