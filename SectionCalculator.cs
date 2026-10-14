// ==================== SectionCalculator.cs ====================
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hdm
{
    /// <summary>单桩号断面计算核心</summary>
    public class SectionCalculator
    {
        // 输入
        public readonly double[][] mesh;
        public readonly double[][] pqx;
        public readonly double[][] sqx;
        public readonly List<DuanMianShuJu> leftWidths;
        public readonly List<DuanMianShuJu> rightWidths;
        public readonly List<BianPoDuanLuo> slopeData;
        public readonly double[][] leftCrossfalls;
        public readonly double[][] rightCrossfalls;
        public readonly List<LeftJiegoucengConfig> leftStructures;
        public readonly List<RightJiegoucengConfig> rightStructures;
        public readonly double clearDepth;

        public SectionCalculator(
            double[][] mesh, double[][] pqx, double[][] sqx,
            List<DuanMianShuJu> leftWidths, List<DuanMianShuJu> rightWidths,
            List<BianPoDuanLuo> slopeData,
            double[][] leftCrossfalls, double[][] rightCrossfalls,
            List<LeftJiegoucengConfig> leftStructures,
            List<RightJiegoucengConfig> rightStructures,
            double clearDepth)
        {
            this.mesh = mesh;
            this.pqx = pqx;
            this.sqx = sqx;
            this.leftWidths = leftWidths;
            this.rightWidths = rightWidths;
            this.slopeData = slopeData;
            this.leftCrossfalls = leftCrossfalls;
            this.rightCrossfalls = rightCrossfalls;
            this.leftStructures = leftStructures;
            this.rightStructures = rightStructures;
            this.clearDepth = clearDepth;
        }

        public ComputeResult Compute(double station)
        {
            double centerY = LL.hua_H(sqx, station);

            LuJiCheDaoJieGuoBao dao = LuJiYaoSuYinQing.getcrosectonxy(
                station, centerY, leftWidths, rightWidths);

            double[][] zuoDian = dao.zuoCeCheDaoJueDui;
            double[][] youDian = dao.youCeCheDaoJueDui;

            if (zuoDian == null || zuoDian.Length == 0
                    || youDian == null || youDian.Length == 0)
            {
                return Err("路基宽度数据缺失");
            }

            double lOuterX = zuoDian[0][0];
            double lOuterY = zuoDian[0][1];
            int nYou = youDian.Length;
            double rOuterX = youDian[nYou - 1][0];
            double rOuterY = youDian[nYou - 1][1];

            double[][] ground = LL.getMatchedBZ(mesh, station, 3.0);
            if (ground == null || ground.Length == 0)
                return Err("地面线为空");

            double[][] cleared = LL.hua_OffsetPolyline(ground, clearDepth);

            BianPoDuanLuo bpCfg = BianPoYinQing.k2BianPo(slopeData, station);
            if (bpCfg == null) return Err("边坡段落为空");

            BianPoHouXuanBao candidates = BianPoYinQing.getAbsolute(
                bpCfg, lOuterX, lOuterY, rOuterX, rOuterY);
            double lGroundY = LL.FromXgetY(ground, lOuterX);
            double[][] finalLeft = (lOuterY > lGroundY)
                ? candidates.zuoTianJueDui : candidates.zuoWaJueDui;
            double rGroundY = LL.FromXgetY(ground, rOuterX);
            double[][] finalRight = (rOuterY > rGroundY)
                ? candidates.youTianJueDui : candidates.youWaJueDui;

            // 拼设计线（未裁剪）
            var rawDesign = new List<double[]>();
            if (finalLeft != null)
                foreach (var p in finalLeft) rawDesign.Add(new[] { p[0], p[1] });
            foreach (var p in zuoDian) rawDesign.Add(new[] { p[0], p[1] });
            rawDesign.Add(new[] { 0.0, centerY });
            foreach (var p in youDian) rawDesign.Add(new[] { p[0], p[1] });
            if (finalRight != null)
                foreach (var p in finalRight) rawDesign.Add(new[] { p[0], p[1] });

            double[][] designTop = ToMat(rawDesign);

            // 左结构层
            double leftCrossfall = LumianSlopeManager.InterpolateCrossfall(leftCrossfalls, station);
            double[] leftCenter = new[] { 0.0, centerY };
            double[][][] leftSubOut = new double[1][][];
            List<double[][]> leftPolygons = LeftJiegoucengManager.ComputeCoordinates(
                station, leftCenter, leftCrossfall, leftStructures, leftSubOut);
            double[][] leftSubgrade = leftSubOut[0];

            if (leftPolygons.Count == 0) return Err("左幅结构层段落为空");

            double leftInnerX = leftSubgrade[leftSubgrade.Length - 1][0];
            double newH = LL.FromXgetY(designTop, leftInnerX);
            leftCenter = new[] { 0.0, newH };
            leftPolygons = LeftJiegoucengManager.ComputeCoordinates(
                station, leftCenter, leftCrossfall, leftStructures, leftSubOut);
            leftSubgrade = leftSubOut[0];

            // 右结构层
            double rightCrossfall = LumianSlopeManager.InterpolateCrossfall(rightCrossfalls, station);
            double[] rightCenter = new[] { 0.0, centerY };
            double[][][] rightSubOut = new double[1][][];
            List<double[][]> rightPolygons = RightJiegoucengManager.ComputeCoordinates(
                station, rightCenter, rightCrossfall, rightStructures, rightSubOut);
            double[][] rightSubgrade = rightSubOut[0];

            if (rightPolygons.Count == 0) return Err("右幅结构层段落为空");

            double rightInnerX = rightSubgrade[0][0];
            newH = LL.FromXgetY(designTop, rightInnerX);
            rightCenter = new[] { 0.0, newH };
            rightPolygons = RightJiegoucengManager.ComputeCoordinates(
                station, rightCenter, rightCrossfall, rightStructures, rightSubOut);
            rightSubgrade = rightSubOut[0];

            // 拼最终设计线
            double leftOuterAnchor = leftSubgrade.Length > 0 ? leftSubgrade[0][0] : lOuterX;
            double rightOuterAnchor = rightSubgrade.Length > 0
                ? rightSubgrade[rightSubgrade.Length - 1][0] : rOuterX;

            var designList = new List<double[]>();
            AddOuterPart(rawDesign, designList, leftOuterAnchor, -1);
            foreach (var p in leftSubgrade) designList.Add(new[] { p[0], p[1] });

            double leftInnerAnchor = leftSubgrade[leftSubgrade.Length - 1][0];
            double rightInnerAnchor = rightSubgrade[0][0];
            foreach (var p in rawDesign)
            {
                if (p[0] >= leftInnerAnchor && p[0] <= rightInnerAnchor)
                    designList.Add(p);
            }

            foreach (var p in rightSubgrade) designList.Add(new[] { p[0], p[1] });
            AddOuterPart(rawDesign, designList, rightOuterAnchor, 1);

            double[][] designMat = ToMat(designList);

            // 算面积
            double[] res = LL.hua_CutAndFillArea(cleared, designMat, 8);
            double fill = Math.Abs(res[0]);
            double cut = Math.Abs(res[1]);
            double minX = res[2], maxX = res[3], minY = res[4];
            double[][] clearPoly = LL.BuildClearPolygon(ground, cleared, minX, maxX);
            double clearArea = LL.hua_PolygonArea(clearPoly);

            int count = (int)res[6];
            if (count == 0) return Err("设计线与地面线无有效交点");

            double[][] finalDesign = new double[count][];
            for (int i = 0; i < count; i++) finalDesign[i] = new double[2];
            int idx = 7;
            for (int i = 0; i < count; i++)
            {
                finalDesign[i][0] = res[idx++];
                finalDesign[i][1] = res[idx++];
            }

            // 结构层面积 + cleared1
            var layerAreaTexts = new List<string>();
            var layerPolygons = new List<double[][]>();

            var temp = new List<double[]>();
            foreach (var c in cleared)
            {
                if (c[0] >= minX && c[0] <= maxX)
                    temp.Add(new[] { c[0], c[1] });
            }
            int n = temp.Count;
            double[][] cleared1 = new double[n + 2][];
            for (int i = 0; i < n + 2; i++) cleared1[i] = new double[2];
            cleared1[0][0] = finalDesign[0][0];
            cleared1[0][1] = finalDesign[0][1];
            for (int i = 0; i < n; i++)
            {
                cleared1[i + 1][0] = temp[i][0];
                cleared1[i + 1][1] = temp[i][1];
            }
            cleared1[n + 1][0] = finalDesign[finalDesign.Length - 1][0];
            cleared1[n + 1][1] = finalDesign[finalDesign.Length - 1][1];

            for (int i = 0; i < leftPolygons.Count; i++)
            {
                double[][] poly = leftPolygons[i];
                double area = Math.Abs(LL.hua_PolygonArea(poly));
                string name = (i < leftStructures.Count
                        && !string.IsNullOrEmpty(leftStructures[i].layerName))
                        ? leftStructures[i].layerName
                        : "L_Lay" + (i + 1);
                layerAreaTexts.Add(string.Format(CultureInfo.InvariantCulture, "L:{0}:{1:F3}", name, area));
                layerPolygons.Add(poly);
            }

            for (int i = 0; i < rightPolygons.Count; i++)
            {
                double[][] poly = rightPolygons[i];
                double area = Math.Abs(LL.hua_PolygonArea(poly));
                string name = (i < rightStructures.Count
                        && !string.IsNullOrEmpty(rightStructures[i].layerName))
                        ? rightStructures[i].layerName
                        : "R_Lay" + (i + 1);
                layerAreaTexts.Add(string.Format(CultureInfo.InvariantCulture, "R:{0}:{1:F3}", name, area));
                layerPolygons.Add(poly);
            }

            // 边坡裁剪
            double lToeX = finalDesign[0][0];
            double lToeY = finalDesign[0][1];
            double rToeX = finalDesign[finalDesign.Length - 1][0];
            double rToeY = finalDesign[finalDesign.Length - 1][1];

            var leftTrimmed = new List<double[]>();
            leftTrimmed.Add(new[] { lToeX, lToeY });
            if (finalLeft != null)
            {
                foreach (var p in finalLeft)
                {
                    if (p[0] >= lToeX)
                        leftTrimmed.Add(new[] { p[0], p[1] });
                }
            }
            leftTrimmed.Add(new[] { lOuterX, lOuterY });

            var rightTrimmed = new List<double[]>();
            rightTrimmed.Add(new[] { rOuterX, rOuterY });
            if (finalRight != null)
            {
                foreach (var p in finalRight)
                {
                    if (p[0] <= rToeX)
                        rightTrimmed.Add(new[] { p[0], p[1] });
                }
            }
            rightTrimmed.Add(new[] { rToeX, rToeY });

            // 组装
            var sr = new SectionResult();
            sr.station = station;
            sr.centerY = centerY;
            sr.lOuter = new[] { lOuterX, lOuterY };
            sr.rOuter = new[] { rOuterX, rOuterY };
            sr.lToe = new[] { lToeX, lToeY };
            sr.rToe = new[] { rToeX, rToeY };
            sr.ground = ground;
            sr.cleared = cleared1;
            sr.design = finalDesign;
            sr.leftSubgrade = leftSubgrade;
            sr.rightSubgrade = rightSubgrade;
            sr.layers = layerPolygons;
            sr.leftSlabs = dao.zuoBanKuai;
            sr.rightSlabs = dao.youBanKuai;
            sr.leftSlopeRaw = finalLeft ?? new double[0][];
            sr.leftSlopeTrimmed = ToMat(leftTrimmed);
            sr.rightSlopeRaw = finalRight ?? new double[0][];
            sr.rightSlopeTrimmed = ToMat(rightTrimmed);
            sr.fillArea = fill;
            sr.cutArea = cut;
            sr.clearArea = clearArea;
            sr.layerAreas = layerAreaTexts;
            sr.bounds = new[] { minX, minY, maxX, res[5] };

            var cr = new ComputeResult();
            cr.success = true;
            cr.result = sr;
            return cr;
        }

        // ===== 第 3a 段结束，辅助方法见 3b =====
        // ============================================================

        private static double[][] ToMat(List<double[]> list)
        {
            double[][] m = new double[list.Count][];
            for (int i = 0; i < list.Count; i++)
            {
                m[i] = new double[2];
                m[i][0] = list[i][0];
                m[i][1] = list[i][1];
            }
            return m;
        }

        private static void AddOuterPart(List<double[]> raw, List<double[]> design,
                                          double anchor, int side)
        {
            for (int i = 0; i < raw.Count; i++)
            {
                double[] p = raw[i];
                bool condition = side < 0 ? p[0] <= anchor : p[0] >= anchor;
                if (condition) design.Add(p);

                if (i > 0)
                {
                    double[] prev = raw[i - 1];
                    if ((side < 0 && prev[0] < anchor && p[0] > anchor)
                            || (side > 0 && prev[0] > anchor && p[0] < anchor))
                    {
                        double x1 = prev[0], y1 = prev[1], x2 = p[0], y2 = p[1];
                        double y = y1 + (y2 - y1) * (anchor - x1) / (x2 - x1);
                        design.Add(new[] { anchor, y });
                    }
                }
            }
        }

        private static ComputeResult Err(string msg)
        {
            var cr = new ComputeResult();
            cr.success = false;
            cr.errorMessage = msg;
            return cr;
        }
    }
}
        