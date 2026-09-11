using System;
using System.Collections.Generic;
using tianwaBianpo;
using topSlope;
using jiegouceng;
using LLutile;
public class ComputeResult {
    public bool Success { get; set; }
    public SectionResult Result { get; set; }
    public string ErrorMessage { get; set; }
}

public class SectionCalculator {
    private readonly double[][] _mesh;
    private readonly double[,] _pqx;
    private readonly double[,] _sqx;
    private readonly List<DuanMianShuJu> _leftWidths;
    private readonly List<DuanMianShuJu> _rightWidths;
    private readonly List<BianPoDuanLuo> _slopeData;
    private readonly List<CrossfallRecord> _leftCrossfalls;
    private readonly List<CrossfallRecord> _rightCrossfalls;
    private readonly List<LeftJiegoucengConfig> _leftStructures;
    private readonly List<RightJiegoucengConfig> _rightStructures;

    public SectionCalculator(
        double[][] mesh,
        double[,] pqx,
        double[,] sqx,
        List<DuanMianShuJu> leftWidths,
        List<DuanMianShuJu> rightWidths,
        List<BianPoDuanLuo> slopeData,
        List<CrossfallRecord> leftCrossfalls,
        List<CrossfallRecord> rightCrossfalls,
        List<LeftJiegoucengConfig> leftStructures,
        List<RightJiegoucengConfig> rightStructures) {
        _mesh = mesh;
        _pqx = pqx;
        _sqx = sqx;
        _leftWidths = leftWidths;
        _rightWidths = rightWidths;
        _slopeData = slopeData;
        _leftCrossfalls = leftCrossfalls;
        _rightCrossfalls = rightCrossfalls;
        _leftStructures = leftStructures;
        _rightStructures = rightStructures;
    }

    public ComputeResult Compute(double station) {
        double centerY = LL.hua_H(_sqx, station);

        var dao = LuJiYaoSuYinQing.getcrosectonxy(station, centerY, _leftWidths, _rightWidths);
        if (dao.ZuoCeCheDaoJueDui.Count == 0 || dao.YouCeCheDaoJueDui.Count == 0)
            return new ComputeResult { Success = false, ErrorMessage = "路基宽度数据缺失" };

        double lOuterX = dao.ZuoCeCheDaoJueDui[0].WidthX;
        double lOuterY = dao.ZuoCeCheDaoJueDui[0].GaoChengY;
        double rOuterX = dao.YouCeCheDaoJueDui[^1].WidthX;
        double rOuterY = dao.YouCeCheDaoJueDui[^1].GaoChengY;

        double[,] ground = LL.getMatchedBZ(_mesh, station);
        // Console.WriteLine("ground:" + ground.GetLength(0));


        if (ground == null || ground.GetLength(0) == 0)
            return new ComputeResult { Success = false, ErrorMessage = "地面线为空" };

        double[,] cleared = LL.hua_OffsetPolyline(ground, AppConfig.ClearDepth);
        // Console.WriteLine(cleared);
        var bpCfg = BianPoYinQing.k2BianPo(_slopeData, station);
        if (bpCfg == null)
            return new ComputeResult { Success = false, ErrorMessage = "边坡段落为空" };

        var candidates = BianPoYinQing.getAbsolute(bpCfg, lOuterX, lOuterY, rOuterX, rOuterY);
        double lGroundY = LL.FromXgetY(ground, lOuterX);
        var finalLeft = (lOuterY > lGroundY) ? candidates.ZuoTianJueDui : candidates.ZuoWaJueDui;
        double rGroundY = LL.FromXgetY(ground, rOuterX);
        var finalRight = (rOuterY > rGroundY) ? candidates.YouTianJueDui : candidates.YouWaJueDui;

        var rawDesign = new List<double[]>();
        if (finalLeft != null) rawDesign.AddRange(finalLeft);
        foreach (var p in dao.ZuoCeCheDaoJueDui) rawDesign.Add(new[] { p.WidthX, p.GaoChengY });
        rawDesign.Add(new[] { 0.0, centerY });
        foreach (var p in dao.YouCeCheDaoJueDui) rawDesign.Add(new[] { p.WidthX, p.GaoChengY });
        if (finalRight != null) rawDesign.AddRange(finalRight);
        double[,] designTop = ToMat(rawDesign);

        double leftCrossfall = LumianSlopeManager.InterpolateCrossfall(_leftCrossfalls, station);
        var leftCenter = new LeftPoint2D(0.0, centerY);
        LeftPoint2D[] leftSubgrade;
        var leftPolygons = LeftJiegoucengManager.ComputeCoordinates(station, leftCenter, leftCrossfall, _leftStructures, out leftSubgrade);



        if (leftPolygons.Count == 0)
            return new ComputeResult { Success = false, ErrorMessage = "左幅结构层段落为空" };



        double leftInnerX = leftSubgrade[leftSubgrade.Length - 1].X;
        double newH = LL.FromXgetY(designTop, leftInnerX);
        leftCenter = new LeftPoint2D(0.0, newH);
        leftPolygons = LeftJiegoucengManager.ComputeCoordinates(station, leftCenter, leftCrossfall, _leftStructures, out leftSubgrade);

        double rightCrossfall = LumianSlopeManager.InterpolateCrossfall(_rightCrossfalls, station);
        var rightCenter = new RightPoint2D(0.0, centerY);
        RightPoint2D[] rightSubgrade;
        var rightPolygons = RightJiegoucengManager.ComputeCoordinates(station, rightCenter, rightCrossfall, _rightStructures, out rightSubgrade);

        if (rightPolygons.Count == 0)
            return new ComputeResult { Success = false, ErrorMessage = "右幅结构层段落为空" };


        double rightInnerX = rightSubgrade[0].X;
        newH = LL.FromXgetY(designTop, rightInnerX);
        rightCenter = new RightPoint2D(0.0, newH);
        rightPolygons = RightJiegoucengManager.ComputeCoordinates(station, rightCenter, rightCrossfall, _rightStructures, out rightSubgrade);

        double leftOuterAnchor = leftSubgrade.Length > 0 ? leftSubgrade[0].X : lOuterX;
        double rightOuterAnchor = rightSubgrade.Length > 0 ? rightSubgrade[rightSubgrade.Length - 1].X : rOuterX;

        var designList = new List<double[]>();
        var finishedList = new List<double[]>();

        AddOuterPart(rawDesign, designList, finishedList, leftOuterAnchor, -1);
        if (leftSubgrade != null)
            foreach (var p in leftSubgrade) designList.Add(new[] { p.X, p.Y });
        foreach (var p in dao.ZuoCeCheDaoJueDui) finishedList.Add(new[] { p.WidthX, p.GaoChengY });

        double leftInnerAnchor = leftSubgrade[leftSubgrade.Length - 1].X;
        double rightInnerAnchor = rightSubgrade[0].X;
        foreach (var p in rawDesign)
            if (p[0] >= leftInnerAnchor && p[0] <= rightInnerAnchor) { designList.Add(p); finishedList.Add(p); }

        if (rightSubgrade != null)
            foreach (var p in rightSubgrade) designList.Add(new[] { p.X, p.Y });
        foreach (var p in dao.YouCeCheDaoJueDui) finishedList.Add(new[] { p.WidthX, p.GaoChengY });

        AddOuterPart(rawDesign, designList, finishedList, rightOuterAnchor, 1);

        double[,] designMat = ToMat(designList);

        var res = LL.hua_CutAndFillArea(cleared, designMat, 8);
        double fill = Math.Abs(res[0]), cut = Math.Abs(res[1]);
        double minX = res[2], maxX = res[3], minY = res[4];
        var clearPoly = LL.BuildClearPolygon(ground, cleared, minX, maxX);
        double clearArea = LL.hua_PolygonArea(clearPoly);

        int count = (int)res[6];
        if (count == 0)
            return new ComputeResult { Success = false, ErrorMessage = "设计线与地面线无有效交点（填挖方区域为空）" };

        double[,] finalDesign = new double[count, 2];
        int idx = 7;
        for (int i = 0; i < count; i++) { finalDesign[i, 0] = res[idx++]; finalDesign[i, 1] = res[idx++]; }

        double minTrim = finalDesign[0, 0], maxTrim = finalDesign[finalDesign.GetLength(0) - 1, 0];
        var trimmedFinished = new List<double[]>();
        foreach (var p in finishedList)
            if (p[0] >= minTrim && p[0] <= maxTrim) trimmedFinished.Add(p);
        double[,] finalFinished = ToMat(trimmedFinished);

        var layerAreaTexts = new List<string>();
        var layerPolygons = new List<double[,]>();

        // 清除表层土只保留坡脚范围内的
        var temp = new List<double[]>();
        for (int i = 0; i < cleared.GetLength(0); i++) {
            double x = cleared[i, 0];
            if (x >= minX && x <= maxX)
                temp.Add(new double[] { x, cleared[i, 1] });
        }

        int n = temp.Count;
        double[,] cleared1 = new double[n + 2, 2];
        cleared1[0, 0] = finalDesign[0, 0];
        cleared1[0, 1] = finalDesign[0, 1];
        for (int i = 0; i < n; i++) {
            cleared1[i + 1, 0] = temp[i][0];
            cleared1[i + 1, 1] = temp[i][1];
        }
        cleared1[n + 1, 0] = finalDesign[finalDesign.GetLength(0) - 1, 0];
        cleared1[n + 1, 1] = finalDesign[finalDesign.GetLength(0) - 1, 1];

        if (leftPolygons != null)
            for (int i = 0; i < leftPolygons.Count; i++) {
                var pts = leftPolygons[i];
                double[,] poly = new double[pts.Length, 2];
                for (int j = 0; j < pts.Length; j++) { poly[j, 0] = pts[j].X; poly[j, 1] = pts[j].Y; }
                double area = Math.Abs(LL.hua_PolygonArea(poly));
                string name = (i < _leftStructures.Count && !string.IsNullOrEmpty(_leftStructures[i].LayerName)) ?
                                _leftStructures[i].LayerName : $"L_Lay{i + 1}";
                layerAreaTexts.Add($"L:{name}:{area:F3}");
                layerPolygons.Add(poly);
            }

        if (rightPolygons != null)
            for (int i = 0; i < rightPolygons.Count; i++) {
                var pts = rightPolygons[i];
                double[,] poly = new double[pts.Length, 2];
                for (int j = 0; j < pts.Length; j++) { poly[j, 0] = pts[j].X; poly[j, 1] = pts[j].Y; }
                double area = Math.Abs(LL.hua_PolygonArea(poly));
                string name = (i < _rightStructures.Count && !string.IsNullOrEmpty(_rightStructures[i].LayerName)) ?
                                _rightStructures[i].LayerName : $"R_Lay{i + 1}";
                layerAreaTexts.Add($"R:{name}:{area:F3}");
                layerPolygons.Add(poly);
            }

        return new ComputeResult {
            Success = true,
            Result = new SectionResult {
                Station = station,
                CenterY = centerY,
                LOuterX = lOuterX,
                LOuterY = lOuterY,
                ROuterX = rOuterX,
                ROuterY = rOuterY,
                LeftCrossfall = leftCrossfall,
                RightCrossfall = rightCrossfall,
                FinalDesign = finalDesign,
                FinalFinished = finalFinished,
                Ground = ground,
                Cleared = cleared1,
                LeftSubgrade = SubToMat(leftSubgrade),
                RightSubgrade = SubToMat(rightSubgrade),
                LayerPolygons = layerPolygons,
                LayerAreaTexts = layerAreaTexts,
                FillArea = fill,
                CutArea = cut,
                ClearArea = clearArea,
                MinX = minX,
                MaxX = maxX,
                MinY = minY,
                LeftSlopePoints = finalLeft,
                RightSlopePoints = finalRight,
                LeftToeX = finalDesign[0, 0],
                LeftToeY = finalDesign[0, 1],
                RightToeX = finalDesign[finalDesign.GetLength(0) - 1, 0],
                RightToeY = finalDesign[finalDesign.GetLength(0) - 1, 1]
            }
        };
    }

    private double[,] ToMat(List<double[]> list) {
        var m = new double[list.Count, 2];
        for (int i = 0; i < list.Count; i++) { m[i, 0] = list[i][0]; m[i, 1] = list[i][1]; }
        return m;
    }

    private double[,] SubToMat(Array arr) {
        if (arr == null || arr.Length == 0) return new double[0, 0];
        int n = arr.Length;
        var m = new double[n, 2];
        dynamic d = arr;
        for (int i = 0; i < n; i++) { m[i, 0] = d[i].X; m[i, 1] = d[i].Y; }
        return m;
    }

    private void AddOuterPart(List<double[]> raw, List<double[]> design, List<double[]> finished, double anchor, int side) {
        for (int i = 0; i < raw.Count; i++) {
            var p = raw[i];
            bool condition = side < 0 ? p[0] <= anchor : p[0] >= anchor;
            if (condition) { design.Add(p); finished.Add(p); }
            if (i > 0) {
                var prev = raw[i - 1];
                if ((side < 0 && prev[0] < anchor && p[0] > anchor) ||
                    (side > 0 && prev[0] > anchor && p[0] < anchor)) {
                    double x1 = prev[0], y1 = prev[1], x2 = p[0], y2 = p[1];
                    double y = y1 + (y2 - y1) * (anchor - x1) / (x2 - x1);
                    var pt = new double[] { anchor, y };
                    design.Add(pt); finished.Add(pt);
                }
            }
        }
    }
}

public class SectionResult {
    public double Station;
    public double CenterY;
    public double LOuterX, LOuterY;
    public double ROuterX, ROuterY;
    public double LeftCrossfall, RightCrossfall;
    public double[,] FinalDesign;
    public double[,] FinalFinished;
    public double[,] Ground;
    public double[,] Cleared;
    public double[,] LeftSubgrade;
    public double[,] RightSubgrade;
    public List<double[,]> LayerPolygons;
    public List<string> LayerAreaTexts;
    public double FillArea, CutArea, ClearArea;
    public double MinX, MaxX, MinY;
    public List<double[]> LeftSlopePoints;
    public List<double[]> RightSlopePoints;
    public double LeftToeX, LeftToeY, RightToeX, RightToeY;
}