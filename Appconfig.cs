// ==================== AppConfig.cs ====================
using System;

namespace Hdm
{
    /// <summary>项目配置</summary>
    public class AppConfig
    {
        public const double DEFAULT_CLEAR_DEPTH = -0.3;
        public const int DEFAULT_XYZ_ROWS = 35;
        public const int DEFAULT_XYZ_COLS = 60;

        public double clearDepth = DEFAULT_CLEAR_DEPTH;
        public int xyzRows = DEFAULT_XYZ_ROWS;
        public int xyzCols = DEFAULT_XYZ_COLS;
    }
}

