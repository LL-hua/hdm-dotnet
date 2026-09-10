using System;
using System.IO;
using System.Collections.Generic;

namespace tianwaBianpo {
    /// <summary>
    /// 边坡段落数据模
    /// </summary>
    public class BianPoDuanLuo {
        public double QiShiZhuangHao { get; set; }     // 起始桩号
        public double JieShuZhuangHao { get; set; }    // 结束桩号
        public List<double[]> ZuoTian { get; set; }    // 左填相对位移矩阵 [[dX, dY], ...]
        public List<double[]> ZuoWa { get; set; }     // 左挖相对位移矩阵
        public List<double[]> YouTian { get; set; }    // 右填相对位移矩阵
        public List<double[]> YouWa { get; set; }     // 右挖相对位移矩阵
    }

    public class BianPoHouXuanBao {

        public List<double[]> ZuoTianJueDui { get; set; }
        public List<double[]> ZuoWaJueDui { get; set; }

        public List<double[]> YouTianJueDui { get; set; }
        public List<double[]> YouWaJueDui { get; set; }

        public BianPoHouXuanBao() {
            ZuoTianJueDui = new List<double[]>();
            ZuoWaJueDui = new List<double[]>();
            YouTianJueDui = new List<double[]>();
            YouWaJueDui = new List<double[]>();
        }
    }

    public static class BianPoYinQing {

        public static List<BianPoDuanLuo> parseBianPo(string filePath) {
            var jieGuoJi = new List<BianPoDuanLuo>();
            string[] lines = File.ReadAllLines(filePath);

            for (int i = 1; i < lines.Length; i += 5) {
                if (i + 4 >= lines.Length) break;

                var duanLuo = new BianPoDuanLuo();

                string[] zhuangHaoParts = lines[i].Split(',');
                duanLuo.QiShiZhuangHao = Convert.ToDouble(zhuangHaoParts[0]);
                duanLuo.JieShuZhuangHao = Convert.ToDouble(zhuangHaoParts[1]);

                // ---- 第2行：左填 (ZuoTian) ----
                duanLuo.ZuoTian = new List<double[]>();
                string[] tokensZT = lines[i + 1].Split(',');
                for (int j = 1; j < tokensZT.Length; j += 2) {
                    if (j + 1 >= tokensZT.Length) break;
                    duanLuo.ZuoTian.Add(new double[] { Convert.ToDouble(tokensZT[j]), Convert.ToDouble(tokensZT[j + 1]) });
                }

                // ---- 第3行：左挖 (ZuoWa) ----
                duanLuo.ZuoWa = new List<double[]>();
                string[] tokensZW = lines[i + 2].Split(',');
                for (int j = 1; j < tokensZW.Length; j += 2) {
                    if (j + 1 >= tokensZW.Length) break;
                    duanLuo.ZuoWa.Add(new double[] { Convert.ToDouble(tokensZW[j]), Convert.ToDouble(tokensZW[j + 1]) });
                }

                // ---- 第4行：右填 (YouTian) ----
                duanLuo.YouTian = new List<double[]>();
                string[] tokensYT = lines[i + 3].Split(',');
                for (int j = 1; j < tokensYT.Length; j += 2) {
                    if (j + 1 >= tokensYT.Length) break;
                    duanLuo.YouTian.Add(new double[] { Convert.ToDouble(tokensYT[j]), Convert.ToDouble(tokensYT[j + 1]) });
                }

                // ---- 第5行：右挖 (YouWa) ----
                duanLuo.YouWa = new List<double[]>();
                string[] tokensYW = lines[i + 4].Split(',');
                for (int j = 1; j < tokensYW.Length; j += 2) {
                    if (j + 1 >= tokensYW.Length) break; // 已修正 tokensRC 未定义变量的手误
                    duanLuo.YouWa.Add(new double[] { Convert.ToDouble(tokensYW[j]), Convert.ToDouble(tokensYW[j + 1]) });
                }

                jieGuoJi.Add(duanLuo);
            }

            return jieGuoJi;
        }


        public static BianPoDuanLuo k2BianPo(List<BianPoDuanLuo> suoYouBianPo, double muBiaoZhuangHao) {
            foreach (var duanLuo in suoYouBianPo) {
                if (muBiaoZhuangHao >= duanLuo.QiShiZhuangHao && muBiaoZhuangHao < duanLuo.JieShuZhuangHao) {
                    return duanLuo;
                }
            }
            return null;
        }

        public static BianPoHouXuanBao getAbsolute(
            BianPoDuanLuo dangQianBianPo,
            double zuoYuanX, double zuoYuanY,
            double youYuanX, double youYuanY) {
            var houxuan = new BianPoHouXuanBao();
            if (dangQianBianPo == null) return houxuan;
            double ztX = zuoYuanX; double ztY = zuoYuanY;
            foreach (var step in dangQianBianPo.ZuoTian) {
                ztX += step[0]; // step[0] 是 dX
                ztY += step[1]; // step[1] 是 dY
                houxuan.ZuoTianJueDui.Add(new double[] { ztX, ztY });
            }
            houxuan.ZuoTianJueDui.Reverse(); // 一次性反转，使其满足从最左侧坡脚向右排列的物理顺序
            double zwX = zuoYuanX; double zwY = zuoYuanY;
            foreach (var step in dangQianBianPo.ZuoWa) {
                zwX += step[0];
                zwY += step[1];
                houxuan.ZuoWaJueDui.Add(new double[] { zwX, zwY });
            }
            houxuan.ZuoWaJueDui.Reverse(); // 一次性反转
            double ytX = youYuanX; double ytY = youYuanY;
            foreach (var step in dangQianBianPo.YouTian) {
                ytX += step[0];
                ytY += step[1];
                houxuan.YouTianJueDui.Add(new double[] { ytX, ytY });
            }
            double ywX = youYuanX; double ywY = youYuanY;
            foreach (var step in dangQianBianPo.YouWa) {
                ywX += step[0];
                ywY += step[1];
                houxuan.YouWaJueDui.Add(new double[] { ywX, ywY });
            }

            return houxuan;
        }


    }
}