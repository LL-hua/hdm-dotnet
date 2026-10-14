using System;
using System.Collections.Generic;

namespace Hdm
{
    // ============================================================
    // 1. 路基宽度相关
    // ============================================================

    /// <summary>断面数据：一个桩号 + 若干板块（宽度, 横坡%）</summary>
    public class DuanMianShuJu
    {
        public double zhuangHao;
        public List<double[]> banKuaiJiHe = new List<double[]>();
    }

    /// <summary>路基车道结果包</summary>
    public class LuJiCheDaoJieGuoBao
    {
        public double[][] zuoCeCheDaoJueDui;   // N×2
        public double[][] youCeCheDaoJueDui;   // N×2
        public double[][] zuoBanKuai;          // N×2 [宽, 坡]
        public double[][] youBanKuai;          // N×2
    }

    // ============================================================
    // 2. 边坡
    // ============================================================

    /// <summary>边坡段落</summary>
    public class BianPoDuanLuo
    {
        public double qiShiZhuangHao;
        public double jieShuZhuangHao;
        public double[][] zuoTian;
        public double[][] zuoWa;
        public double[][] youTian;
        public double[][] youWa;
    }

    /// <summary>边坡候选包</summary>
    public class BianPoHouXuanBao
    {
        public double[][] zuoTianJueDui;
        public double[][] zuoWaJueDui;
        public double[][] youTianJueDui;
        public double[][] youWaJueDui;
    }

    // ============================================================
    // 3. 结构层配置
    // ============================================================

    public class LeftJiegoucengConfig
    {
        public double startStation;
        public double endStation;
        public int layerIndex;
        public string layerName = "";
        public double thickness;
        public double innerStepWidth;
        public double innerSlope;
        public double outerStepWidth;
        public double outerSlope;
    }

    public class RightJiegoucengConfig
    {
        public double startStation;
        public double endStation;
        public int layerIndex;
        public string layerName = "";
        public double thickness;
        public double innerStepWidth;
        public double innerSlope;
        public double outerStepWidth;
        public double outerSlope;
    }

    // ============================================================
    // 4. 计算结果
    // ============================================================

    /// <summary>单桩号计算结果</summary>
    public class SectionResult
    {
        // 位置
        public double station;
        public double centerY;

        // 关键点
        public double[] lOuter;
        public double[] rOuter;
        public double[] lToe;
        public double[] rToe;

        // 折线
        public double[][] ground;
        public double[][] cleared;
        public double[][] design;
        public double[][] leftSubgrade;
        public double[][] rightSubgrade;
        public List<double[][]> layers;

        // 顶面板块
        public double[][] leftSlabs;
        public double[][] rightSlabs;

        // 边坡
        public double[][] leftSlopeRaw;
        public double[][] leftSlopeTrimmed;
        public double[][] rightSlopeRaw;
        public double[][] rightSlopeTrimmed;

        // 面积
        public double fillArea, cutArea, clearArea;
        public List<string> layerAreas;

        // 包围盒 [minX, minY, maxX, maxY]
        public double[] bounds;
    }

    /// <summary>计算结果包装</summary>
    public class ComputeResult
    {
        public bool success;
        public SectionResult result;
        public string errorMessage;
    }
}