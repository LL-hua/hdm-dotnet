// ==================== Parsers.cs ====================
using System;
using System.Collections.Generic;

namespace Hdm
{
    /// <summary>路基宽度引擎</summary>
    public static class LuJiYaoSuYinQing
    {
        public static LuJiCheDaoJieGuoBao getcrosectonxy(
            double muBiaoZhuangHao,
            double zhongZhuangGaoCheng,
            List<DuanMianShuJu> suoYouZuoData,
            List<DuanMianShuJu> suoYouYouData)
        {
            var jieGuoBao = new LuJiCheDaoJieGuoBao();

            // ===== 左侧 =====
            if (suoYouZuoData != null && suoYouZuoData.Count > 0)
            {
                double stBefore = suoYouZuoData[0].zhuangHao;
                double stAfter = suoYouZuoData[suoYouZuoData.Count - 1].zhuangHao;
                DuanMianShuJu dmBefore = suoYouZuoData[0];
                DuanMianShuJu dmAfter = suoYouZuoData[suoYouZuoData.Count - 1];

                if (muBiaoZhuangHao <= stBefore)
                {
                    dmAfter = dmBefore;
                    stAfter = stBefore;
                }
                else if (muBiaoZhuangHao >= stAfter)
                {
                    dmBefore = dmAfter;
                    stBefore = stAfter;
                }
                else
                {
                    for (int i = 1; i < suoYouZuoData.Count; i++)
                    {
                        if (suoYouZuoData[i].zhuangHao >= muBiaoZhuangHao)
                        {
                            stAfter = suoYouZuoData[i].zhuangHao;
                            dmAfter = suoYouZuoData[i];
                            stBefore = suoYouZuoData[i - 1].zhuangHao;
                            dmBefore = suoYouZuoData[i - 1];
                            break;
                        }
                    }
                }

                double ratio = (stBefore == stAfter) ? 0
                    : (muBiaoZhuangHao - stBefore) / (stAfter - stBefore);
                double curZuoX = 0.0;
                double curZuoY = zhongZhuangGaoCheng;

                var dianList = new List<double[]>();
                var banKuaiList = new List<double[]>();

                for (int k = 0; k < dmBefore.banKuaiJiHe.Count; k++)
                {
                    if (k >= dmAfter.banKuaiJiHe.Count) break;
                    double wBefore = dmBefore.banKuaiJiHe[k][0];
                    double sBefore = dmBefore.banKuaiJiHe[k][1];
                    double wAfter = dmAfter.banKuaiJiHe[k][0];
                    double sAfter = dmAfter.banKuaiJiHe[k][1];

                    double curWidth = wBefore + ratio * (wAfter - wBefore);
                    double curSlopePercent = sBefore + ratio * (sAfter - sBefore);
                    double curSlopeVal = curSlopePercent / 100.0;

                    curZuoX -= curWidth;
                    curZuoY += (curWidth * curSlopeVal);

                    dianList.Add(new[] { curZuoX, curZuoY });
                    banKuaiList.Add(new[] { curWidth, curSlopePercent });
                }

                dianList.Reverse();
                banKuaiList.Reverse();

                jieGuoBao.zuoCeCheDaoJueDui = ListToMat(dianList);
                jieGuoBao.zuoBanKuai = ListToMat(banKuaiList);
            }
            else
            {
                jieGuoBao.zuoCeCheDaoJueDui = new double[0][];
                jieGuoBao.zuoBanKuai = new double[0][];
            }

            // ===== 右侧 =====
            if (suoYouYouData != null && suoYouYouData.Count > 0)
            {
                double stBefore = suoYouYouData[0].zhuangHao;
                double stAfter = suoYouYouData[suoYouYouData.Count - 1].zhuangHao;
                DuanMianShuJu dmBefore = suoYouYouData[0];
                DuanMianShuJu dmAfter = suoYouYouData[suoYouYouData.Count - 1];

                if (muBiaoZhuangHao <= stBefore)
                {
                    dmAfter = dmBefore;
                    stAfter = stBefore;
                }
                else if (muBiaoZhuangHao >= stAfter)
                {
                    dmBefore = dmAfter;
                    stBefore = stAfter;
                }
                else
                {
                    for (int i = 1; i < suoYouYouData.Count; i++)
                    {
                        if (suoYouYouData[i].zhuangHao >= muBiaoZhuangHao)
                        {
                            stAfter = suoYouYouData[i].zhuangHao;
                            dmAfter = suoYouYouData[i];
                            stBefore = suoYouYouData[i - 1].zhuangHao;
                            dmBefore = suoYouYouData[i - 1];
                            break;
                        }
                    }
                }

                double ratio = (stBefore == stAfter) ? 0
                    : (muBiaoZhuangHao - stBefore) / (stAfter - stBefore);
                double curYouX = 0.0;
                double curYouY = zhongZhuangGaoCheng;

                var dianList = new List<double[]>();
                var banKuaiList = new List<double[]>();

                for (int k = 0; k < dmBefore.banKuaiJiHe.Count; k++)
                {
                    if (k >= dmAfter.banKuaiJiHe.Count) break;
                    double wBefore = dmBefore.banKuaiJiHe[k][0];
                    double sBefore = dmBefore.banKuaiJiHe[k][1];
                    double wAfter = dmAfter.banKuaiJiHe[k][0];
                    double sAfter = dmAfter.banKuaiJiHe[k][1];

                    double curWidth = wBefore + ratio * (wAfter - wBefore);
                    double curSlopePercent = sBefore + ratio * (sAfter - sBefore);
                    double curSlopeVal = curSlopePercent / 100.0;

                    curYouX += curWidth;
                    curYouY += (curWidth * curSlopeVal);

                    dianList.Add(new[] { curYouX, curYouY });
                    banKuaiList.Add(new[] { curWidth, curSlopePercent });
                }

                jieGuoBao.youCeCheDaoJueDui = ListToMat(dianList);
                jieGuoBao.youBanKuai = ListToMat(banKuaiList);
            }
            else
            {
                jieGuoBao.youCeCheDaoJueDui = new double[0][];
                jieGuoBao.youBanKuai = new double[0][];
            }

            return jieGuoBao;
        }

        private static double[][] ListToMat(List<double[]> list)
        {
            double[][] m = new double[list.Count][];
            for (int i = 0; i < list.Count; i++) m[i] = list[i];
            return m;
        }
    }

    /// <summary>边坡引擎</summary>
    public static class BianPoYinQing
    {
        public static BianPoDuanLuo k2BianPo(List<BianPoDuanLuo> suoYouBianPo, double muBiaoZhuangHao)
        {
            foreach (var duanLuo in suoYouBianPo)
            {
                if (muBiaoZhuangHao >= duanLuo.qiShiZhuangHao
                        && muBiaoZhuangHao < duanLuo.jieShuZhuangHao)
                {
                    return duanLuo;
                }
            }
            return null;
        }

        public static BianPoHouXuanBao getAbsolute(
            BianPoDuanLuo dangQianBianPo,
            double zuoYuanX, double zuoYuanY,
            double youYuanX, double youYuanY)
        {
            var houxuan = new BianPoHouXuanBao();
            if (dangQianBianPo == null)
            {
                houxuan.zuoTianJueDui = new double[0][];
                houxuan.zuoWaJueDui = new double[0][];
                houxuan.youTianJueDui = new double[0][];
                houxuan.youWaJueDui = new double[0][];
                return houxuan;
            }

            houxuan.zuoTianJueDui = Accumulate(dangQianBianPo.zuoTian, zuoYuanX, zuoYuanY, true);
            houxuan.zuoWaJueDui = Accumulate(dangQianBianPo.zuoWa, zuoYuanX, zuoYuanY, true);
            houxuan.youTianJueDui = Accumulate(dangQianBianPo.youTian, youYuanX, youYuanY, false);
            houxuan.youWaJueDui = Accumulate(dangQianBianPo.youWa, youYuanX, youYuanY, false);

            return houxuan;
        }

        private static double[][] Accumulate(double[][] steps, double startX, double startY, bool reverse)
        {
            if (steps == null || steps.Length == 0)
                return new double[0][];

            int n = steps.Length;
            double[][] result = new double[n][];
            for (int i = 0; i < n; i++) result[i] = new double[2];

            double x = startX, y = startY;
            for (int i = 0; i < n; i++)
            {
                x += steps[i][0];
                y += steps[i][1];
                result[i][0] = x;
                result[i][1] = y;
            }

            if (reverse)
            {
                for (int i = 0, j = n - 1; i < j; i++, j--)
                {
                    double tx = result[i][0], ty = result[i][1];
                    result[i][0] = result[j][0];
                    result[i][1] = result[j][1];
                    result[j][0] = tx;
                    result[j][1] = ty;
                }
            }
            return result;
        }
    }

    /// <summary>横坡引擎</summary>
    public static class LumianSlopeManager
    {
        public static double InterpolateCrossfall(double[][] records, double currentStation)
        {
            if (records == null) return 0.0;
            int n = records.Length;
            if (n == 0) return 0.0;
            if (n == 1) return records[0][1];

            if (currentStation <= records[0][0]) return records[0][1];
            if (currentStation >= records[n - 1][0]) return records[n - 1][1];

            int low = 0, high = n - 1;
            while (low <= high)
            {
                int mid = low + ((high - low) >> 1);
                double midStation = records[mid][0];
                if (Math.Abs(midStation - currentStation) < 0.00001) return records[mid][1];
                if (midStation < currentStation) low = mid + 1;
                else high = mid - 1;
            }

            double s1 = records[high][0], v1 = records[high][1];
            double s2 = records[low][0], v2 = records[low][1];
            if (Math.Abs(s2 - s1) < 0.00001) return v1;
            return v1 + (v2 - v1) * (currentStation - s1) / (s2 - s1);
        }
    }

    /// <summary>左结构层引擎</summary>
    public static class LeftJiegoucengManager
    {
        public static List<double[][]> ComputeCoordinates(
            double station, double[] centerPoint, double crossfall,
            List<LeftJiegoucengConfig> configs,
            double[][][] subgradeLineOut)
        {
            var polygons = new List<double[][]>();

            var activeLayers = new List<LeftJiegoucengConfig>();
            foreach (var cfg in configs)
            {
                if (station >= cfg.startStation && station <= cfg.endStation)
                    activeLayers.Add(cfg);
            }
            activeLayers.Sort((a, b) => a.layerIndex.CompareTo(b.layerIndex));

            if (activeLayers.Count == 0)
            {
                subgradeLineOut[0] = new double[0][];
                return polygons;
            }

            int layerCount = activeLayers.Count;
            double k = -crossfall / 100.0;

            double[] topOutX = new double[layerCount];
            double[] topOutY = new double[layerCount];
            double[] botOutX = new double[layerCount];
            double[] botOutY = new double[layerCount];
            double[] topInX = new double[layerCount];
            double[] topInY = new double[layerCount];
            double[] botInX = new double[layerCount];
            double[] botInY = new double[layerCount];

            LeftJiegoucengConfig firstLayer = activeLayers[0];
            double curTopInX = centerPoint[0] + firstLayer.innerStepWidth;
            double curTopInY = centerPoint[1];

            double curTopOutX = curTopInX + firstLayer.outerStepWidth;
            double curTopOutY = curTopInY + (curTopOutX - curTopInX) * k;

            for (int i = 0; i < layerCount; i++)
            {
                LeftJiegoucengConfig cfg = activeLayers[i];

                if (i > 0)
                {
                    curTopOutX = botOutX[i - 1] + cfg.outerStepWidth;
                    curTopOutY = botOutY[i - 1] + (curTopOutX - botOutX[i - 1]) * k;
                    curTopInX = botInX[i - 1] + cfg.innerStepWidth;
                    curTopInY = botInY[i - 1] + (curTopInX - botInX[i - 1]) * k;
                }

                topOutX[i] = curTopOutX;
                topOutY[i] = curTopOutY;
                topInX[i] = curTopInX;
                topInY[i] = curTopInY;

                double outN = Math.Abs(cfg.outerSlope);
                double dx = -(outN * cfg.thickness) / (1.0 - outN * k);
                double botOutXv = curTopOutX + dx;
                double botOutYv = curTopOutY - cfg.thickness + dx * k;
                botOutX[i] = botOutXv;
                botOutY[i] = botOutYv;

                botInX[i] = curTopInX;
                botInY[i] = curTopInY - cfg.thickness;

                double[][] poly = new double[4][];
                for (int t = 0; t < 4; t++) poly[t] = new double[2];
                poly[0][0] = topOutX[i]; poly[0][1] = topOutY[i];
                poly[1][0] = botOutX[i]; poly[1][1] = botOutY[i];
                poly[2][0] = botInX[i];  poly[2][1] = botInY[i];
                poly[3][0] = topInX[i];  poly[3][1] = topInY[i];
                polygons.Add(poly);
            }

            var combined = new List<double[]>();

            for (int i = 0; i < layerCount; i++)
            {
                combined.Add(new[] { topOutX[i], topOutY[i] });
                combined.Add(new[] { botOutX[i], botOutY[i] });
            }

            var innerList = new List<double[]>();
            innerList.Add(new[] { topInX[0], centerPoint[1] });
            for (int i = 0; i < layerCount; i++)
            {
                innerList.Add(new[] { topInX[i], topInY[i] });
                innerList.Add(new[] { botInX[i], botInY[i] });
            }
            innerList.Reverse();
            combined.AddRange(innerList);

            double[][] subgradeLine = new double[combined.Count][];
            for (int i = 0; i < combined.Count; i++)
            {
                subgradeLine[i] = new double[2];
                subgradeLine[i][0] = combined[i][0];
                subgradeLine[i][1] = combined[i][1];
            }
            subgradeLineOut[0] = subgradeLine;

            return polygons;
        }
    }

    /// <summary>右结构层引擎</summary>
    public static class RightJiegoucengManager
    {
        public static List<double[][]> ComputeCoordinates(
            double station, double[] centerPoint, double crossfall,
            List<RightJiegoucengConfig> configs,
            double[][][] subgradeLineOut)
        {
            var polygons = new List<double[][]>();

            var activeLayers = new List<RightJiegoucengConfig>();
            foreach (var cfg in configs)
            {
                if (station >= cfg.startStation && station <= cfg.endStation)
                    activeLayers.Add(cfg);
            }
            activeLayers.Sort((a, b) => a.layerIndex.CompareTo(b.layerIndex));

            if (activeLayers.Count == 0)
            {
                subgradeLineOut[0] = new double[0][];
                return polygons;
            }

            int layerCount = activeLayers.Count;
            double k = crossfall / 100.0;

            double[] topOutX = new double[layerCount];
            double[] topOutY = new double[layerCount];
            double[] botOutX = new double[layerCount];
            double[] botOutY = new double[layerCount];
            double[] topInX = new double[layerCount];
            double[] topInY = new double[layerCount];
            double[] botInX = new double[layerCount];
            double[] botInY = new double[layerCount];

            RightJiegoucengConfig firstLayer = activeLayers[0];
            double curTopInX = centerPoint[0] + firstLayer.innerStepWidth;
            double curTopInY = centerPoint[1];

            double curTopOutX = curTopInX + firstLayer.outerStepWidth;
            double curTopOutY = curTopInY + (curTopOutX - curTopInX) * k;

            for (int i = 0; i < layerCount; i++)
            {
                RightJiegoucengConfig cfg = activeLayers[i];

                if (i > 0)
                {
                    curTopOutX = botOutX[i - 1] + cfg.outerStepWidth;
                    curTopOutY = botOutY[i - 1] + (curTopOutX - botOutX[i - 1]) * k;
                    curTopInX = botInX[i - 1] + cfg.innerStepWidth;
                    curTopInY = botInY[i - 1] + (curTopInX - botInX[i - 1]) * k;
                }

                topOutX[i] = curTopOutX;
                topOutY[i] = curTopOutY;
                topInX[i] = curTopInX;
                topInY[i] = curTopInY;

                double outN = Math.Abs(cfg.outerSlope);
                double dx = (outN * cfg.thickness) / (1.0 + outN * k);
                double botOutXv = curTopOutX + dx;
                double botOutYv = curTopOutY - cfg.thickness + (botOutXv - curTopOutX) * k;
                botOutX[i] = botOutXv;
                botOutY[i] = botOutYv;

                botInX[i] = curTopInX;
                botInY[i] = curTopInY - cfg.thickness;

                double[][] poly = new double[4][];
                for (int t = 0; t < 4; t++) poly[t] = new double[2];
                poly[0][0] = topOutX[i]; poly[0][1] = topOutY[i];
                poly[1][0] = botOutX[i]; poly[1][1] = botOutY[i];
                poly[2][0] = botInX[i];  poly[2][1] = botInY[i];
                poly[3][0] = topInX[i];  poly[3][1] = topInY[i];
                polygons.Add(poly);
            }

            var combined = new List<double[]>();

            combined.Add(new[] { topInX[0], centerPoint[1] });
            for (int i = 0; i < layerCount; i++)
            {
                combined.Add(new[] { topInX[i], topInY[i] });
                combined.Add(new[] { botInX[i], botInY[i] });
            }

            var outerList = new List<double[]>();
            for (int i = 0; i < layerCount; i++)
            {
                outerList.Add(new[] { topOutX[i], topOutY[i] });
                outerList.Add(new[] { botOutX[i], botOutY[i] });
            }
            outerList.Reverse();
            combined.AddRange(outerList);

            double[][] subgradeLine = new double[combined.Count][];
            for (int i = 0; i < combined.Count; i++)
            {
                subgradeLine[i] = new double[2];
                subgradeLine[i][0] = combined[i][0];
                subgradeLine[i][1] = combined[i][1];
            }
            subgradeLineOut[0] = subgradeLine;

            return polygons;
        }
    }
}