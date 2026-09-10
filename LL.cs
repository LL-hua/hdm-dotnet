using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
namespace LLutile {
    public class LL {
        private static readonly double[] GaussRr = { 0.1739274226, 0.3260725774, 0.3260725774, 0.1739274226 };
        private static readonly double[] GaussVv = { 0.0694318442, 0.3300094782, 0.6699905218, 0.9305681558 };

        public static double[,] BuildClearPolygon(double[,] ground, double[,] cleared, double minX, double maxX) {
            var list = new List<double[]>();
            list.Add(new double[] { minX, LL.FromXgetY(ground, minX) });
            for (int i = 0; i < ground.GetLength(0); i++)
                if (ground[i, 0] > minX && ground[i, 0] < maxX)
                    list.Add(new double[] { ground[i, 0], ground[i, 1] });
            list.Add(new double[] { maxX, LL.FromXgetY(ground, maxX) });
            list.Add(new double[] { maxX, LL.FromXgetY(cleared, maxX) });
            for (int i = cleared.GetLength(0) - 1; i >= 0; i--)
                if (cleared[i, 0] > minX && cleared[i, 0] < maxX)
                    list.Add(new double[] { cleared[i, 0], cleared[i, 1] });
            list.Add(new double[] { minX, LL.FromXgetY(cleared, minX) });

            double[,] mat = new double[list.Count, 2];
            for (int i = 0; i < list.Count; i++) {
                mat[i, 0] = list[i][0];
                mat[i, 1] = list[i][1];
            }
            return mat;
        }
        public static void SortInPlace(double[,] array) {
            int rows = array.GetLength(0);
            int cols = array.GetLength(1);
            if (rows <= 1 || cols < 2) return;

            int[] indices = Enumerable.Range(0, rows).ToArray();
            // 排序索引，比较器只用到第1列和第2列
            Array.Sort(indices, (i, j) => {
                int cmp = array[i, 0].CompareTo(array[j, 0]);
                if (cmp != 0) return cmp;
                return array[i, 1].CompareTo(array[j, 1]);
                // 第3列（array[i,2]）完全不参与比较
            });

            // 原地重排（整行交换）
            double[] temp = new double[cols];
            for (int i = 0; i < rows; i++) {
                if (indices[i] == i) continue;
                for (int c = 0; c < cols; c++) temp[c] = array[i, c];
                int j = i;
                while (true) {
                    int k = indices[j];
                    indices[j] = j;
                    if (k == i) break;
                    for (int c = 0; c < cols; c++) array[j, c] = array[k, c];
                    j = k;
                }
                for (int c = 0; c < cols; c++) array[j, c] = temp[c];
            }
        }

        public static void AppendDxfLwPolyline(StringBuilder sb, double[,] points, double offsetX, double offsetY, string layer = "0", int colorIndex = 7) {
            int vertexCount = points.GetLength(0);
            if (vertexCount < 2) return;

            // 1) 声明一条经典折线的开始
            sb.AppendLine("  0");
            sb.AppendLine("POLYLINE");
            sb.AppendLine("  8");
            sb.AppendLine(layer);
            sb.AppendLine(" 62");          // 注入颜色组码
            sb.AppendLine(colorIndex.ToString());
            sb.AppendLine(" 66");          // 核心：通知 CAD 后面有一串子顶点
            sb.AppendLine("  1");

            // 2) 循环展开、高精度安全写入每个子顶点
            for (int i = 0; i < vertexCount; i++) {
                double absoluteX = points[i, 0] + offsetX;
                double absoluteY = points[i, 1] + offsetY;

                sb.AppendLine("  0");
                sb.AppendLine("VERTEX");
                sb.AppendLine("  8");
                sb.AppendLine(layer);
                sb.AppendLine(" 62");      // 子顶点跟随主线颜色
                sb.AppendLine(colorIndex.ToString());
                sb.AppendLine(" 10");      // X 坐标
                sb.AppendLine(absoluteX.ToString("F3"));
                sb.AppendLine(" 20");      // Y 坐标
                sb.AppendLine(absoluteY.ToString("F3"));
            }

            // 3) 核心闭合：声明整个多段线图元正式结束
            sb.AppendLine("  0");
            sb.AppendLine("SEQEND");
            sb.AppendLine("  8");
            sb.AppendLine(layer);
        }


        public static void AppendDxfText(StringBuilder sb, string content, double x, double y, double height = 2.5, string layer = "TEXT_INFO") {

            sb.AppendLine("  0");
            sb.AppendLine("TEXT");
            sb.AppendLine("  8");
            sb.AppendLine(layer);
            sb.AppendLine(" 10");
            sb.AppendLine(x.ToString("F3"));
            sb.AppendLine(" 20");
            sb.AppendLine(y.ToString("F3"));
            sb.AppendLine(" 40");
            sb.AppendLine(height.ToString("F2"));
            sb.AppendLine("  1");
            sb.AppendLine(content);


        }

        public static void AppendDxfTextCenter(StringBuilder sb, string content, double x, double y, double height = 2.5, string layer = "TEXT_INFO", double rotation = 0.0) {
            sb.AppendLine("  0");
            sb.AppendLine("TEXT");
            sb.AppendLine("  8");
            sb.AppendLine(layer);

            // 10/20 永远是你的绝对插入点
            sb.AppendLine(" 10");
            sb.AppendLine(x.ToString("F3"));
            sb.AppendLine(" 20");
            sb.AppendLine(y.ToString("F3"));

            sb.AppendLine(" 40");
            sb.AppendLine(height.ToString("F3"));

            sb.AppendLine(" 50");
            sb.AppendLine(rotation.ToString("F3"));

            sb.AppendLine("  1");
            sb.AppendLine(content);

            // 🎯 完美的对齐与旋转切换控制
            if (Math.Abs(rotation) < 0.001) {
                // 1️⃣ 0度水平文字：使用居中对齐（72=1）
                // 此时 11/21 作为中心点，它与 10/20 重合，在 0 度下显示完美
                sb.AppendLine(" 72");
                sb.AppendLine("  1");
                sb.AppendLine(" 11");
                sb.AppendLine(x.ToString("F3"));
                sb.AppendLine(" 21");
                sb.AppendLine(y.ToString("F3"));
            } else {

            }
        }



        public static void AppendDxfTextSlope(StringBuilder sb, string content, double x, double y, double height, double rotationDegrees, string layer = "SLOPE_TEXT_SIDE") {

            double cleanAngle = rotationDegrees;
            while (cleanAngle > 90.0) cleanAngle -= 180.0;
            while (cleanAngle <= -90.0) cleanAngle += 180.0;


            double angleRad = cleanAngle * (Math.PI / 180.0);
            double offsetDistance = height * 0.6;


            double offsetX = -Math.Sin(angleRad) * offsetDistance;
            double offsetY = Math.Cos(angleRad) * offsetDistance;

            double finalX = x + offsetX;
            double finalY = y + offsetY;

            sb.AppendLine("  0");
            sb.AppendLine("TEXT");
            sb.AppendLine("  8");
            sb.AppendLine(layer);

            // Primary insertion coordinates
            sb.AppendLine(" 10");
            sb.AppendLine(finalX.ToString("F3"));
            sb.AppendLine(" 20");
            sb.AppendLine(finalY.ToString("F3"));

            sb.AppendLine(" 40");
            sb.AppendLine(height.ToString("F3"));
            sb.AppendLine(" 50");
            sb.AppendLine(cleanAngle.ToString("F3"));
            sb.AppendLine("  1");
            sb.AppendLine(content);

            // 🌟 Perfect Alignment Settings: 72=1 (Center), 73=1 (Vertical Middle)
            // Changing 73 from 0 to 1 allows the calculation to anchor smoothly to the text body center
            sb.AppendLine(" 72");
            sb.AppendLine("  1");
            sb.AppendLine(" 73");
            sb.AppendLine("  1");

            // Second alignment coordinates (mandatory for center alignment)
            sb.AppendLine(" 11");
            sb.AppendLine(finalX.ToString("F3"));
            sb.AppendLine(" 21");
            sb.AppendLine(finalY.ToString("F3"));
        }

        public static double[,] hua_getKBZ(double x1, double y1, double x2, double y2, double[,] points) {
            if (points == null)
                throw new ArgumentNullException(nameof(points));
            int rows = points.GetLength(0);
            int cols = points.GetLength(1);
            if (cols < 3)
                throw new ArgumentException("输入点集必须至少包含 X, Y, Z 三列。");
            double dx = x2 - x1;
            double dy = y2 - y1;
            double len2 = dx * dx + dy * dy;
            if (len2 == 0)
                throw new ArgumentException("直线两点重合，无法定义直线。");
            double len = Math.Sqrt(len2);
            double a = y1 - y2;
            double b = x2 - x1;
            double c = x1 * y2 - x2 * y1;
            double[,] result = new double[rows, 3];
            for (int i = 0; i < rows; i++) {
                double x0 = points[i, 0];
                double y0 = points[i, 1];
                double z = points[i, 2];
                double dot = (x0 - x1) * dx + (y0 - y1) * dy;
                double signedDistToFoot = dot / len;
                double signedVerticalDist = (a * x0 + b * y0 + c) / len;
                result[i, 0] = signedDistToFoot;
                result[i, 1] = signedVerticalDist;
                result[i, 2] = z;
            }
            return result;
        }


        public static double[,] getMatchedBZ(double[,] data, double target, double tolerance = 3.0) {
            int rows = data.GetLength(0);
            if (rows == 0) return new double[0, 2];

            // 1. 二分查找左边界（第一个 k >= target - tolerance）
            int left = 0, right = rows;
            double lower = target - tolerance;
            while (left < right) {
                int mid = (left + right) / 2;
                if (data[mid, 0] < lower)
                    left = mid + 1;
                else
                    right = mid;
            }

            // 2. 从左边界开始，收集所有满足 k <= target + tolerance 的点
            var matched = new List<(double b, double z)>();
            double upper = target + tolerance;
            for (int i = left; i < rows && data[i, 0] <= upper; i++) {
                matched.Add((data[i, 1], data[i, 2]));
            }

            // 3. 按宽度 b 升序排序
            matched.Sort((a, b) => a.b.CompareTo(b.b));

            // 4. 转换为二维数组
            int count = matched.Count;
            double[,] result = new double[count, 2];
            for (int i = 0; i < count; i++) {
                result[i, 0] = matched[i].b;
                result[i, 1] = matched[i].z;
            }
            return result;
        }
        public static string hua_Num2K(double meters) {
            int km = (int)Math.Floor(meters / 1000);
            double m = meters - km * 1000;
            m = Math.Round(m, 2);
            if (m == (int)m)
                return $"K{km}+{(int)m:D3}";
            else
                return $"K{km}+{m:000.00}";
        }
        public static double hua_DmsToRadians(double dms) {
            int degrees = (int)dms;
            double fractional = dms - degrees;
            int minutes = (int)(fractional * 100);
            double seconds = (fractional * 100 - minutes) * 100;
            double totalDegrees = degrees + minutes / 60.0 + seconds / 3600.0;
            return totalDegrees * Math.PI / 180;
        }
        public static double hua_radiansToDMS(double radians) {
            double degrees = radians * (180 / Math.PI);
            int d = (int)Math.Floor(degrees);
            double remaining = (degrees - d) * 60;
            int m = (int)Math.Floor(remaining);
            double s = (remaining - m) * 60;
            string mm = m < 10 ? $"0{m}" : m.ToString();
            string ss = s < 10 ? $"0{s:F1}" : $"{s:F1}";
            string dfm = $"{d}{mm}{ss}";
            return Math.Round(double.Parse(dfm) / 10000.0, 5);
        }
        public static string hua_radiansToDMS_度分秒(double radians) {
            double degrees = radians * (180 / Math.PI);
            int d = (int)Math.Floor(degrees);
            double remaining = (degrees - d) * 60;
            int m = (int)Math.Floor(remaining);
            double s = (remaining - m) * 60;
            string mm = m < 10 ? $"0{m}" : m.ToString();
            string ss = s < 10 ? $"0{s:F1}" : $"{s:F1}";
            return $"{d}°{mm}′{ss}″";
        }
        public static double[] hua_Fwj(double x0, double y0, double x1, double y1) {
            double x = x1 - x0;
            double y = y1 - y0;
            double cd = Math.Sqrt(x * x + y * y);
            double hd = Math.Atan2(y, x);
            if (hd < 0) hd += 2 * Math.PI;
            return new double[] { cd, hd };
        }
        /// <summary>
        /// 计算线路任意点坐标及方位角（无分配版）
        /// </summary>
        public static void hua_Zs(
            double xyk, double xyx, double xyy, double xyhd, double xycd,
            double xyqdr, double xyzdr, double xyzy, double jsk,
            double jsb, double jd,
            out double x, out double y, out double angle) {
            jd = hua_DmsToRadians(jd);
            double w = jsk - xyk;

            if (Math.Abs(xyqdr - xyzdr) < 0.01 && xyqdr > 0) {
                double a = xyhd + xyzy * Math.PI / 2;
                double da = w / xyqdr * xyzy;
                double ta = xyhd + da;
                if (ta < 0) ta += 2 * Math.PI;
                x = xyx + xyqdr * Math.Cos(a) - xyqdr * Math.Cos(a + da) + jsb * Math.Cos(a + da - xyzy * Math.PI / 2 + jd);
                y = xyy + xyqdr * Math.Sin(a) - xyqdr * Math.Sin(a + da) + jsb * Math.Sin(a + da - xyzy * Math.PI / 2 + jd);
                angle = ta;
                return;
            }

            if (xyqdr < 0.01 && xyzdr < 0.01 && xyzy < 0.01) {
                x = xyx + w * Math.Cos(xyhd) + jsb * Math.Cos(xyhd + jd);
                y = xyy + w * Math.Sin(xyhd) + jsb * Math.Sin(xyhd + jd);
                angle = xyhd;
                return;
            }

            double qr = xyqdr < 0.001 ? 99999999 : xyqdr;
            double zr = xyzdr < 0.001 ? 99999999 : xyzdr;
            double c = 1 / qr;
            double d = (qr - zr) / (2 * xycd * qr * zr);
            double xs = 0, ys = 0;
            for (int i = 0; i < 4; i++) {
                double v = GaussVv[i];
                double r = GaussRr[i];
                double f = xyhd + xyzy * v * w * (c + v * w * d);
                xs += r * Math.Cos(f);
                ys += r * Math.Sin(f);
            }
            double fhz3 = xyhd + xyzy * w * (c + w * d);
            fhz3 = (fhz3 % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
            x = xyx + w * xs + jsb * Math.Cos(fhz3 + jd);
            y = xyy + w * ys + jsb * Math.Sin(fhz3 + jd);
            angle = fhz3;
        }
        /// <summary>
        /// 根据里程查找所在段落并计算坐标（无分配版）
        /// </summary>
        public static void hua_Dantiaoxianludange(
            double[,] pqx, double k, double b, double z,
            out double x, out double y, out double angle) {
            int segCount = pqx.GetLength(0);
            for (int i = 0; i < segCount; i++) {
                if (k >= pqx[i, 0] && k <= pqx[i, 0] + pqx[i, 4]) {
                    double rad = hua_DmsToRadians(pqx[i, 3]);
                    hua_Zs(
                        pqx[i, 0], pqx[i, 1], pqx[i, 2], rad,
                        pqx[i, 4], pqx[i, 5], pqx[i, 6], pqx[i, 7],
                        k, b, z,
                        out double rx, out double ry, out double ra);
                    x = Math.Round(rx, 3);
                    y = Math.Round(ry, 3);
                    angle = ra;
                    return;
                }
            }
            x = 0; y = 0; angle = 0;
        }


        public static double[] hua_Dantiaoxianludange(
    double[,] pqx, double k, double b, double z) {
            hua_Dantiaoxianludange(pqx, k, b, z, out double x, out double y, out double angle);
            return new double[] { x, y, angle };
        }

        public static double[] hua_Fs(double[,] pqx, double fsx, double fsy) {
            // 先计算初始点
            hua_Dantiaoxianludange(pqx, pqx[0, 0], 0, 0, out double cx, out double cy, out double cAngle);
            double[] jljd = hua_Fwj(cx, cy, fsx, fsy);
            double k = pqx[0, 0];
            double cz = jljd[0] * Math.Cos(jljd[1] - cAngle);
            double pj = jljd[0] * Math.Sin(jljd[1] - cAngle);

            int hang = pqx.GetLength(0) - 1;
            double qdlc = pqx[0, 0];
            double zdlc = pqx[hang, 0] + pqx[hang, 4];
            int iter = 0;

            while (Math.Abs(cz) > 0.01 && iter < 15) {
                k += cz;
                iter++;
                if (k < qdlc) return new double[] { -1, -1 };
                if (k > zdlc) return new double[] { -2, -2 };

                hua_Dantiaoxianludange(pqx, k, 0, 0, out cx, out cy, out cAngle);
                jljd = hua_Fwj(cx, cy, fsx, fsy);
                cz = jljd[0] * Math.Cos(jljd[1] - cAngle);
                pj = jljd[0] * Math.Sin(jljd[1] - cAngle);
            }

            return new double[] { Math.Round(k, 3), Math.Round(pj, 3) };
        }
        /// <summary>
        /// 批量计算（无数组分配，线性查找，并行加速）
        /// </summary>
        public static double[,] hua_Fs_Batch(double[,] pqx, double[,] xy) {
            if (pqx == null || pqx.GetLength(0) == 0 || xy == null || xy.GetLength(0) == 0)
                return new double[0, 0];

            // ---------- 预提取线路参数 ----------
            int segCount = pqx.GetLength(0);
            double[] startK = new double[segCount];
            double[] endK = new double[segCount];
            double[] startX = new double[segCount];
            double[] startY = new double[segCount];
            double[] angleRad = new double[segCount];
            double[] len = new double[segCount];
            double[] A = new double[segCount], B = new double[segCount], C = new double[segCount];

            for (int i = 0; i < segCount; i++) {
                startK[i] = pqx[i, 0];
                len[i] = pqx[i, 4];
                endK[i] = startK[i] + len[i];
                startX[i] = pqx[i, 1];
                startY[i] = pqx[i, 2];
                angleRad[i] = hua_DmsToRadians(pqx[i, 3]);
                A[i] = pqx[i, 5];
                B[i] = pqx[i, 6];
                C[i] = pqx[i, 7];
            }

            int pointCount = xy.GetLength(0);
            double[,] results = new double[pointCount, 3];

            // ---------- 同步循环（无并行，更稳定） ----------
            for (int i = 0; i < pointCount; i++) {
                double fsx = xy[i, 0];
                double fsy = xy[i, 1];
                double fsz = xy[i, 2];

                // 初始点：取第一段起点
                double cx, cy, cAngle;
                hua_Zs(startK[0], startX[0], startY[0], angleRad[0], len[0],
                       A[0], B[0], C[0], startK[0], 0, 0,
                       out cx, out cy, out cAngle);

                hua_Fwj(cx, cy, fsx, fsy, out double dist, out double az);
                double k = startK[0];
                double cz = dist * Math.Cos(az - cAngle);
                double pj = dist * Math.Sin(az - cAngle);

                double qdlc = startK[0];
                double zdlc = endK[segCount - 1];
                int iter = 0;
                const int maxIter = 15;

                while (Math.Abs(cz) > 0.01 && iter < maxIter) {
                    k += cz;
                    iter++;

                    if (k < qdlc) { results[i, 0] = -1; results[i, 1] = -1; goto NextPoint; }
                    if (k > zdlc) { results[i, 0] = -2; results[i, 1] = -2; goto NextPoint; }

                    // 线性查找所在段
                    int segIdx = 0;
                    for (int j = 0; j < segCount; j++) {
                        if (k >= startK[j] && k <= endK[j]) {
                            segIdx = j;
                            break;
                        }
                    }

                    hua_Zs(startK[segIdx], startX[segIdx], startY[segIdx],
                           angleRad[segIdx], len[segIdx],
                           A[segIdx], B[segIdx], C[segIdx],
                           k, 0, 0,
                           out cx, out cy, out cAngle);

                    hua_Fwj(cx, cy, fsx, fsy, out dist, out az);
                    cz = dist * Math.Cos(az - cAngle);
                    pj = dist * Math.Sin(az - cAngle);
                }

                results[i, 0] = Math.Round(k, 3);
                results[i, 1] = Math.Round(pj, 3);
                results[i, 2] = fsz;

            NextPoint:;
            }

            return results;
        }
        /// <summary>
        /// 无数组分配的 Fwj (out 版)
        /// </summary>
        public static void hua_Fwj(double x1, double y1, double x2, double y2,
                                   out double dist, out double azimuth) {
            double dx = x2 - x1;
            double dy = y2 - y1;
            dist = Math.Sqrt(dx * dx + dy * dy);
            azimuth = Math.Atan2(dy, dx);
            if (azimuth < 0) azimuth += 2 * Math.PI;
        }


        public static double hua_H(double[,] sqx, double k) {
            int n = sqx.GetLength(0);
            if (n == 0) throw new ArgumentException("设计竖曲线wrong");
            double firstZ = sqx[0, 0], lastZ = sqx[n - 1, 0];
            if (k < firstZ || k > lastZ) return -1;
            if (n == 1) return Math.Abs(k - firstZ) < 1e-9 ? sqx[0, 1] : -1;
            int idx = -1;
            for (int i = 0; i < n - 1; i++)
                if (k >= sqx[i, 0] && k <= sqx[i + 1, 0]) { idx = i; break; }
            if (idx == -1) return -1;
            double z1 = sqx[idx, 0], h1 = sqx[idx, 1];
            double z2 = sqx[idx + 1, 0], h2 = sqx[idx + 1, 1];
            double slope = (h2 - h1) / (z2 - z1);
            double result = h1 + slope * (k - z1);
            int[] points = { idx, idx + 1 };
            foreach (int p in points) {
                if (p <= 0 || p >= n - 1) continue;
                double r = sqx[p, 2];
                if (r <= 0) continue;
                double zv = sqx[p, 0], hv = sqx[p, 1];
                double zPrev = sqx[p - 1, 0], hPrev = sqx[p - 1, 1];
                double i1 = (hv - hPrev) / (zv - zPrev);
                double zNext = sqx[p + 1, 0], hNext = sqx[p + 1, 1];
                double i2 = (hNext - hv) / (zNext - zv);
                double omega = i2 - i1;
                double T = r * Math.Abs(omega) / 2.0;
                double start = zv - T, end = zv + T;
                if (k >= start && k <= end) {
                    double tanElev = (k <= zv) ? hv + i1 * (k - zv) : hv + i2 * (k - zv);
                    double x = (k <= zv) ? (k - start) : (end - k);
                    double y = x * x / (2.0 * r) * Math.Sign(omega);
                    result = tanElev + y;
                }
            }
            return Math.Round(result, 3);
        }
        public static double[] hua_Gauss_proj(double L, double B, double lonCenter = 360.0) {
            double pi = 3.141592653589793238463;
            double p0 = 206264.8062470963551564;
            double e = 0.00669438002290;
            double e1 = 0.00673949677548;
            double b = 6356752.3141;
            double a = 6378137.0;
            B = B * pi / 180;
            L = L * pi / 180;
            double L_num;
            double L_center;
            if (lonCenter >= 359) {
                L_num = Math.Floor(L * 180 / pi / 3.0 + 0.5);
                L_center = 3 * L_num;
            } else {
                L_center = lonCenter;
            }
            double l = (L / pi * 180 - L_center) * 3600;
            double M0 = a * (1 - e);
            double M2 = 3.0 / 2.0 * e * M0;
            double M4 = 5.0 / 4.0 * e * M2;
            double M6 = 7.0 / 6.0 * e * M4;
            double M8 = 9.0 / 8.0 * e * M6;
            double a0 = M0 + M2 / 2.0 + 3.0 / 8.0 * M4 + 5.0 / 16.0 * M6 + 35.0 / 128.0 * M8;
            double a2 = M2 / 2.0 + M4 / 2 + 15.0 / 32.0 * M6 + 7.0 / 16.0 * M8;
            double a4 = M4 / 8.0 + 3.0 / 16.0 * M6 + 7.0 / 32.0 * M8;
            double a6 = M6 / 32.0 + M8 / 16.0;
            double a8 = M8 / 128.0;
            double Xz = a0 * B - a2 / 2.0 * Math.Sin(2 * B) + a4 / 4.0 * Math.Sin(4 * B) - a6 / 6.0 * Math.Sin(6 * B) + a8 / 8.0 * Math.Sin(8 * B);
            double c = a * a / b;
            double V = Math.Sqrt(1 + e1 * Math.Cos(B) * Math.Cos(B));
            double N = c / V;
            double t = Math.Tan(B);
            double n = e1 * Math.Cos(B) * Math.Cos(B);
            double m1 = N * Math.Cos(B);
            double m2 = N / 2.0 * Math.Sin(B) * Math.Cos(B);
            double m3 = N / 6.0 * Math.Pow(Math.Cos(B), 3) * (1 - t * t + n);
            double m4 = N / 24.0 * Math.Sin(B) * Math.Pow(Math.Cos(B), 3) * (5 - t * t + 9 * n);
            double m5 = N / 120.0 * Math.Pow(Math.Cos(B), 5) * (5 - 18 * t * t + Math.Pow(t, 4) + 14 * n - 58 * n * t * t);
            double m6 = N / 720.0 * Math.Sin(B) * Math.Pow(Math.Cos(B), 5) * (61 - 58 * t * t + Math.Pow(t, 4));
            double x = Xz + m2 * l * l / Math.Pow(p0, 2) + m4 * Math.Pow(l, 4) / Math.Pow(p0, 4) + m6 * Math.Pow(l, 6) / Math.Pow(p0, 6);
            double y0 = m1 * l / p0 + m3 * Math.Pow(l, 3) / Math.Pow(p0, 3) + m5 * Math.Pow(l, 5) / Math.Pow(p0, 5);
            double y = y0 + 500000;
            return new double[] { x, y, L_center };
        }
        public static double[] hua_Gauss_unproj(double x, double y, double l0) {
            double pi = 3.141592653589793238463;
            double e = 0.00669438002290;
            double e1 = 0.00673949677548;
            double b = 6356752.3141;
            double a = 6378137.0;
            double y1 = y - 500000;
            double M0 = a * (1 - e);
            double M2 = 3.0 / 2.0 * e * M0;
            double M4 = 5.0 / 4.0 * e * M2;
            double M6 = 7.0 / 6.0 * e * M4;
            double M8 = 9.0 / 8.0 * e * M6;
            double a0 = M0 + M2 / 2.0 + 3.0 / 8.0 * M4 + 5.0 / 16.0 * M6 + 35.0 / 128.0 * M8;
            double a2 = M2 / 2.0 + M4 / 2 + 15.0 / 32.0 * M6 + 7.0 / 16.0 * M8;
            double a4 = M4 / 8.0 + 3.0 / 16.0 * M6 + 7.0 / 32.0 * M8;
            double a6 = M6 / 32.0 + M8 / 16.0;
            double Bf = x / a0;
            double B0 = Bf;
            while (Math.Abs(Bf - B0) > 0.0000001 || B0 == Bf) {
                B0 = Bf;
                double FBf = -a2 / 2.0 * Math.Sin(2 * B0) + a4 / 4.0 * Math.Sin(4 * B0) - a6 / 6.0 * Math.Sin(6 * B0);
                Bf = (x - FBf) / a0;
            }
            double t = Math.Tan(Bf);
            double c = a * a / b;
            double V = Math.Sqrt(1 + e1 * Math.Cos(Bf) * Math.Cos(Bf));
            double N = c / V;
            double M = c / Math.Pow(V, 3);
            double n = e1 * Math.Cos(Bf) * Math.Cos(Bf);
            double n1 = 1 / (N * Math.Cos(Bf));
            double n2 = -t / (2.0 * M * N);
            double n3 = -(1 + 2 * t * t + n) / (6.0 * Math.Pow(N, 3) * Math.Cos(Bf));
            double n4 = t * (5 + 3 * t * t + n - 9 * n * t * t) / (24.0 * M * Math.Pow(N, 3));
            double n5 = (5 + 28 * t * t + 24 * Math.Pow(t, 4) + 6 * n + 8 * n * t * t) / (120.0 * Math.Pow(N, 5) * Math.Cos(Bf));
            double n6 = -t * (61 + 90 * t * t + 45 * Math.Pow(t, 4)) / (720.0 * M * Math.Pow(N, 5));
            double B = (Bf + n2 * y1 * y1 + n4 * Math.Pow(y1, 4) + n6 * Math.Pow(y1, 6)) / pi * 180;
            double l = n1 * y1 + n3 * Math.Pow(y1, 3) + n5 * Math.Pow(y1, 5);
            double L = l0 + l / pi * 180;
            return new double[] { L, B };
        }
        public static double[] hua_Utm_proj(double longitude, double latitude) {
            double EQUATORIAL_RADIUS = 6378137.0;
            double FLATTENING = 1 / 298.257223563;
            double ECC_SQUARED = 2 * FLATTENING - Math.Pow(FLATTENING, 2);
            double ECC_PRIME_SQUARED = ECC_SQUARED / (1 - ECC_SQUARED);
            double SCALE_FACTOR = 0.9996;
            double FALSE_EASTING = 500000.0;
            double FALSE_NORTHING_S = 10000000.0;
            int zoneNumber = (int)Math.Floor((longitude + 180) / 6) + 1;
            double centralMeridian = (zoneNumber - 1) * 6 - 180 + 3;
            double latRad = latitude * Math.PI / 180.0;
            double lonRad = longitude * Math.PI / 180.0;
            double lonCenterRad = centralMeridian * Math.PI / 180.0;
            double N = EQUATORIAL_RADIUS / Math.Sqrt(1 - ECC_SQUARED * Math.Pow(Math.Sin(latRad), 2));
            double T = Math.Pow(Math.Tan(latRad), 2);
            double C = ECC_PRIME_SQUARED * Math.Pow(Math.Cos(latRad), 2);
            double A = (lonRad - lonCenterRad) * Math.Cos(latRad);
            double M = EQUATORIAL_RADIUS * ((1 - ECC_SQUARED / 4 - 3 * Math.Pow(ECC_SQUARED, 2) / 64 - 5 * Math.Pow(ECC_SQUARED, 3) / 256) * latRad - (3 * ECC_SQUARED / 8 + 3 * Math.Pow(ECC_SQUARED, 2) / 32 + 45 * Math.Pow(ECC_SQUARED, 3) / 1024) * Math.Sin(2 * latRad) + (15 * Math.Pow(ECC_SQUARED, 2) / 256 + 45 * Math.Pow(ECC_SQUARED, 3) / 1024) * Math.Sin(4 * latRad) - (35 * Math.Pow(ECC_SQUARED, 3) / 3072) * Math.Sin(6 * latRad));
            double easting = SCALE_FACTOR * N * (A + (1 - T + C) * Math.Pow(A, 3) / 6 + (5 - 18 * T + Math.Pow(T, 2) + 72 * C - 58 * ECC_PRIME_SQUARED) * Math.Pow(A, 5) / 120) + FALSE_EASTING;
            double northing = SCALE_FACTOR * (M + N * Math.Tan(latRad) * (Math.Pow(A, 2) / 2 + (5 - T + 9 * C + 4 * Math.Pow(C, 2)) * Math.Pow(A, 4) / 24 + (61 - 58 * T + Math.Pow(T, 2) + 600 * C - 330 * ECC_PRIME_SQUARED) * Math.Pow(A, 6) / 720));
            if (latitude < 0) northing += FALSE_NORTHING_S;
            return new double[] { northing, easting, zoneNumber };
        }
        public static double[] hua_Utm_unproj(double northing, double easting, bool isNorthern, int zoneNumber) {
            double EQUATORIAL_RADIUS = 6378137.0;
            double FLATTENING = 1 / 298.257223563;
            double ECC_SQUARED = 2 * FLATTENING - Math.Pow(FLATTENING, 2);
            double ECC_PRIME_SQUARED = ECC_SQUARED / (1 - ECC_SQUARED);
            double SCALE_FACTOR = 0.9996;
            double FALSE_EASTING = 500000.0;
            double FALSE_NORTHING_S = 10000000.0;
            double x = easting - FALSE_EASTING;
            double y = isNorthern ? northing : northing - FALSE_NORTHING_S;
            double centralMeridian = (zoneNumber - 1) * 6 - 180 + 3;
            double lonCenterRad = centralMeridian * Math.PI / 180.0;
            double M = y / SCALE_FACTOR;
            double mu = M / (EQUATORIAL_RADIUS * (1 - ECC_SQUARED / 4 - 3 * Math.Pow(ECC_SQUARED, 2) / 64.0 - 5 * Math.Pow(ECC_SQUARED, 3) / 256.0));
            double e1 = (1 - Math.Sqrt(1 - ECC_SQUARED)) / (1 + Math.Sqrt(1 - ECC_SQUARED));
            double phi1Rad = mu + (3 * e1 / 2 - 27 * Math.Pow(e1, 3) / 32) * Math.Sin(2 * mu) + (21 * Math.Pow(e1, 2) / 16 - 55 * Math.Pow(e1, 4) / 32) * Math.Sin(4 * mu) + (151 * Math.Pow(e1, 3) / 96) * Math.Sin(6 * mu);
            double N1 = EQUATORIAL_RADIUS / Math.Sqrt(1 - ECC_SQUARED * Math.Pow(Math.Sin(phi1Rad), 2));
            double T1 = Math.Pow(Math.Tan(phi1Rad), 2);
            double C1 = ECC_PRIME_SQUARED * Math.Pow(Math.Cos(phi1Rad), 2);
            double R1 = EQUATORIAL_RADIUS * (1 - ECC_SQUARED) / Math.Pow(1 - ECC_SQUARED * Math.Pow(Math.Sin(phi1Rad), 2), 1.5);
            double D = x / (N1 * SCALE_FACTOR);
            double latRad = phi1Rad - (N1 * Math.Tan(phi1Rad) / R1) * (Math.Pow(D, 2) / 2 - (5 + 3 * T1 + 10 * C1 - 4 * Math.Pow(C1, 2) - 9 * ECC_PRIME_SQUARED) * Math.Pow(D, 4) / 24 + (61 + 90 * T1 + 298 * C1 + 45 * Math.Pow(T1, 2) - 252 * ECC_PRIME_SQUARED - 3 * Math.Pow(C1, 2)) * Math.Pow(D, 6) / 720);
            double lonRad = lonCenterRad + (D - (1 + 2 * T1 + C1) * Math.Pow(D, 3) / 6 + (5 - 2 * C1 + 28 * T1 - 3 * Math.Pow(C1, 2) + 8 * ECC_PRIME_SQUARED + 24 * Math.Pow(T1, 2)) * Math.Pow(D, 5) / 120) / Math.Cos(phi1Rad);
            return new double[] { lonRad * 180 / Math.PI, latRad * 180 / Math.PI };
        }
        public static int utm_zone(double longitude) {
            return (int)Math.Floor((longitude + 180) / 6) + 1;
        }
        public static double[] hua_Cs4(double[] source, double[] target) {
            if (source == null || target == null || source.Length != target.Length) {
                Console.WriteLine("坐标数组长度必须相等");
                return new double[] { 0, 0, 0, 1 };
            }
            if (source.Length < 4 || source.Length % 2 != 0) {
                Console.WriteLine("至少需要2个点且坐标为偶数");
                return new double[] { 0, 0, 0, 1 };
            }
            int pointCount = source.Length / 2;
            double sumX1 = 0, sumY1 = 0, sumX2 = 0, sumY2 = 0;
            for (int i = 0; i < pointCount; i++) {
                sumX1 += source[2 * i];
                sumY1 += source[2 * i + 1];
                sumX2 += target[2 * i];
                sumY2 += target[2 * i + 1];
            }
            double meanX1 = sumX1 / pointCount;
            double meanY1 = sumY1 / pointCount;
            double meanX2 = sumX2 / pointCount;
            double meanY2 = sumY2 / pointCount;
            double[] centeredSource = new double[source.Length];
            double[] centeredTarget = new double[target.Length];
            for (int i = 0; i < pointCount; i++) {
                centeredSource[2 * i] = source[2 * i] - meanX1;
                centeredSource[2 * i + 1] = source[2 * i + 1] - meanY1;
                centeredTarget[2 * i] = target[2 * i] - meanX2;
                centeredTarget[2 * i + 1] = target[2 * i + 1] - meanY2;
            }
            double H11 = 0, H12 = 0, H21 = 0, H22 = 0;
            double B1 = 0, B2 = 0;
            for (int i = 0; i < pointCount; i++) {
                double x1 = centeredSource[2 * i];
                double y1 = centeredSource[2 * i + 1];
                double x2 = centeredTarget[2 * i];
                double y2 = centeredTarget[2 * i + 1];
                H11 += x1 * x1 + y1 * y1;
                H22 += x1 * x1 + y1 * y1;
                B1 += x1 * x2 + y1 * y2;
                B2 += x1 * y2 - y1 * x2;
            }
            double det = H11 * H22 - H12 * H21;
            if (Math.Abs(det) < 1e-15) {
                Console.WriteLine("矩阵奇异，无法求解参数");
            }
            double a = (H22 * B1 - H12 * B2) / det;
            double b = (-H21 * B1 + H11 * B2) / det;
            double scale = Math.Sqrt(a * a + b * b);
            double rotation = Math.Atan2(b, a);
            double deltaX = meanX2 - (a * meanX1 - b * meanY1);
            double deltaY = meanY2 - (b * meanX1 + a * meanY1);
            return new double[] { deltaX, deltaY, rotation, scale };
        }
        public static double[] hua_FourParameterTransform(double x, double y, double deltaX, double deltaY, double rotation, double scale) {
            double convertedX = scale * (x * Math.Cos(rotation) - y * Math.Sin(rotation)) + deltaX;
            double convertedY = scale * (x * Math.Sin(rotation) + y * Math.Cos(rotation)) + deltaY;
            return new double[] { convertedX, convertedY };
        }


        public static double[,] mapGps2XyBatch(double[,] lonlatPoints, string proj, double[,] controlLonLat, double[,] controlXy, double center) {
            if (lonlatPoints == null || controlLonLat == null || controlXy == null)
                throw new ArgumentNullException();
            int pointCount = controlLonLat.GetLength(0);
            if (pointCount < 2 || controlXy.GetLength(0) < pointCount)
                throw new ArgumentException("控制点数量不足（至少需要2个）或控制点坐标数组不匹配");

            // 先计算四参数（基于控制点）
            double[] sourceXy = new double[pointCount * 2];
            double[] targetXy = new double[pointCount * 2];

            bool isGauss = proj.Contains("gao");
            bool isUtm = proj.Contains("utm");
            if (!isGauss && !isUtm)
                throw new ArgumentException("投影类型必须包含 'gao' 或 'utm'");

            double[] cs4 = null;
            if (isGauss) {
                for (int i = 0; i < pointCount; i++) {
                    double[] ls = hua_Gauss_proj(controlLonLat[i, 0], controlLonLat[i, 1], center);
                    sourceXy[i * 2] = ls[0];
                    sourceXy[i * 2 + 1] = ls[1];
                    targetXy[i * 2] = controlXy[i, 0];
                    targetXy[i * 2 + 1] = controlXy[i, 1];
                }
                cs4 = hua_Cs4(sourceXy, targetXy);
            } else if (isUtm) {
                for (int i = 0; i < pointCount; i++) {
                    double[] ls = hua_Utm_proj(controlLonLat[i, 0], controlLonLat[i, 1]);
                    sourceXy[i * 2] = ls[1];     // easting -> X
                    sourceXy[i * 2 + 1] = ls[0]; // northing -> Y
                    targetXy[i * 2] = controlXy[i, 0];
                    targetXy[i * 2 + 1] = controlXy[i, 1];
                }
                cs4 = hua_Cs4(sourceXy, targetXy);
            }

            // 批量处理每个待转换点
            int rows = lonlatPoints.GetLength(0);
            double[,] result = new double[rows, 2];
            for (int i = 0; i < rows; i++) {
                double lon = lonlatPoints[i, 0];
                double lat = lonlatPoints[i, 1];
                double[] projCoord;
                if (isGauss) {
                    double[] ls = hua_Gauss_proj(lon, lat, center);
                    projCoord = new double[] { ls[0], ls[1] };
                } else // UTM
                  {
                    double[] ls = hua_Utm_proj(lon, lat);
                    projCoord = new double[] { ls[1], ls[0] }; // [easting, northing]
                }
                double[] transformed = hua_FourParameterTransform(projCoord[0], projCoord[1], cs4[0], cs4[1], cs4[2], cs4[3]);
                result[i, 0] = transformed[0];
                result[i, 1] = transformed[1];
            }
            return result;
        }


        public static double[,] mapXy2GpsBatch(double[,] xyPoints, string proj, double[,] controlLonLat, double[,] controlXy) {
            if (xyPoints == null || controlLonLat == null || controlXy == null)
                throw new ArgumentNullException();
            int pointCount = controlLonLat.GetLength(0);
            if (pointCount < 2 || controlXy.GetLength(0) < pointCount)
                throw new ArgumentException("控制点数量不足（至少需要2个）或控制点坐标数组不匹配");

            // 计算平均经度作为中心经线（用于高斯投影和 UTM 带号）
            double center = 0.0;
            for (int i = 0; i < pointCount; i++)
                center += controlLonLat[i, 0];
            center /= pointCount;

            double[] sourceXY = new double[pointCount * 2];
            double[] targetXY = new double[pointCount * 2];

            bool isGauss = proj.ToLower().Contains("gao");
            bool isUtm = proj.ToLower().Contains("utm");
            if (!isGauss && !isUtm)
                throw new ArgumentException("投影类型必须包含 'gao' 或 'utm'");

            double[] cs4 = null;
            if (isGauss) {
                for (int i = 0; i < pointCount; i++) {
                    double[] projected = hua_Gauss_proj(controlLonLat[i, 0], controlLonLat[i, 1], center);
                    sourceXY[i * 2] = projected[0];
                    sourceXY[i * 2 + 1] = projected[1];
                    targetXY[i * 2] = controlXy[i, 0];
                    targetXY[i * 2 + 1] = controlXy[i, 1];
                }
                cs4 = hua_Cs4(targetXY, sourceXY); // 逆变换
            } else // UTM
              {
                bool north = controlLonLat[0, 1] >= 0;
                for (int i = 0; i < pointCount; i++) {
                    double[] projected = hua_Utm_proj(controlLonLat[i, 0], controlLonLat[i, 1]);
                    sourceXY[i * 2] = projected[1];     // easting -> X
                    sourceXY[i * 2 + 1] = projected[0]; // northing -> Y
                    targetXY[i * 2] = controlXy[i, 0];
                    targetXY[i * 2 + 1] = controlXy[i, 1];
                }
                cs4 = hua_Cs4(targetXY, sourceXY);
            }

            int rows = xyPoints.GetLength(0);
            double[,] result = new double[rows, 2];
            for (int i = 0; i < rows; i++) {
                double x = xyPoints[i, 0];
                double y = xyPoints[i, 1];
                double[] lsxy = hua_FourParameterTransform(x, y, cs4[0], cs4[1], cs4[2], cs4[3]);

                double[] gps;
                if (isGauss) {
                    gps = hua_Gauss_unproj(lsxy[0], lsxy[1], center);
                } else // UTM
                  {
                    bool north = controlLonLat[0, 1] >= 0;
                    int zone = utm_zone(center);
                    double[] unproj = hua_Utm_unproj(lsxy[1], lsxy[0], north, zone); // northing, easting
                    gps = (unproj != null && unproj.Length >= 2) ? new double[] { unproj[0], unproj[1] } : new double[] { 0, 0 };
                }
                result[i, 0] = gps[0];
                result[i, 1] = gps[1];
            }
            return result;
        }






        public static double FromXgetY(double[,] points, double targetX) {
            const double tolerance = 1e-6;
            int n = points.GetLength(0);
            if (n == 0) return 0.0;

            // 1. 直接对二维矩阵的 [i, 0]（即 X 通道）执行原生二分查找
            int low = 0;
            int high = n - 1;
            int index = -1;
            int rightIndex = 0;

            while (low <= high) {
                int mid = (low + high) >> 1; // 位移运算代替除以2，速度极快
                double midX = points[mid, 0];

                if (midX == targetX) {
                    index = mid;
                    break;
                }
                if (midX < targetX) {
                    low = mid + 1;
                } else {
                    high = mid - 1;
                }
            }

            // 2. 模拟 Array.BinarySearch 的位反转机制来确定边界索引
            if (index >= 0) {
                return points[index, 1]; // 恰好完美命中，直接返回对应的 Y
            } else {
                rightIndex = low; // 没命中时，low 刚好就是大于 targetX 的第一个索引
            }

            int leftIndex = rightIndex - 1;

            // 3. 越界保护（安全 Clamp 延展）
            if (rightIndex >= n) return points[n - 1, 1];
            if (leftIndex < 0) return points[0, 1];

            // 4. 提取相邻控制点坐标（直接读，无损零拷贝）
            double xLeft = points[leftIndex, 0];
            double xRight = points[rightIndex, 0];

            if (Math.Abs(xRight - xLeft) < tolerance) {
                return Math.Max(points[leftIndex, 1], points[rightIndex, 1]);
            }

            double yLeft = points[leftIndex, 1];
            double yRight = points[rightIndex, 1];

            // 5. 线性内插公式完美闭环
            return yLeft + (yRight - yLeft) * (targetX - xLeft) / (xRight - xLeft);
        }

        public static double[] FromYgetX(double[,] points, double targetY) {
            const double tolerance = 1e-12;
            var result = new List<double>();
            int n = points.GetLength(0);
            if (n == 0) return Array.Empty<double>();
            for (int i = 0; i < n - 1; i++) {
                double x1 = points[i, 0];
                double y1 = points[i, 1];
                double x2 = points[i + 1, 0];
                double y2 = points[i + 1, 1];
                bool y1Below = y1 <= targetY + tolerance;
                bool y1Above = y1 >= targetY - tolerance;
                bool y2Below = y2 <= targetY + tolerance;
                bool y2Above = y2 >= targetY - tolerance;
                bool cross = (y1Below && y2Above) || (y1Above && y2Below);
                if (!cross) continue;
                if (Math.Abs(y2 - y1) < tolerance) {
                    if (result.Count == 0 || Math.Abs(result[result.Count - 1] - x1) > tolerance)
                        result.Add(x1);
                    if (i == n - 2 && (result.Count == 0 || Math.Abs(result[result.Count - 1] - x2) > tolerance))
                        result.Add(x2);
                    continue;
                }
                double t = (targetY - y1) / (y2 - y1);
                double xIntersect = x1 + t * (x2 - x1);
                if (result.Count == 0 || Math.Abs(result[result.Count - 1] - xIntersect) > tolerance)
                    result.Add(xIntersect);
            }
            return result.ToArray();
        }
        public static double[] hua_CutAndFillArea(double[,] dmx, double[,] sjx, double extendDist) {
            if (extendDist > 0) {
                int n = dmx.GetLength(0);
                if (n >= 2) {
                    double x1 = dmx[0, 0], y1 = dmx[0, 1];
                    double x2 = dmx[1, 0], y2 = dmx[1, 1];
                    double dx = x1 - x2, dy = y1 - y2;
                    double len = Math.Sqrt(dx * dx + dy * dy);
                    if (len > 1e-12) {
                        dx /= len; dy /= len;
                        dmx[0, 0] = x1 + dx * extendDist;
                        dmx[0, 1] = y1 + dy * extendDist;
                    }
                    x1 = dmx[n - 2, 0]; y1 = dmx[n - 2, 1];
                    x2 = dmx[n - 1, 0]; y2 = dmx[n - 1, 1];
                    dx = x2 - x1; dy = y2 - y1;
                    len = Math.Sqrt(dx * dx + dy * dy);
                    if (len > 1e-12) {
                        dx /= len; dy /= len;
                        dmx[n - 1, 0] = x2 + dx * extendDist;
                        dmx[n - 1, 1] = y2 + dy * extendDist;
                    }
                }
            }

            List<double[]> xys = new List<double[]>();
            double fill = 0;
            double cut = 0;
            int lenA = sjx.GetLength(0);
            int lenB = dmx.GetLength(0);

            for (int i = 0; i < lenA - 1; i++) {
                double x1 = sjx[i, 0];
                double y1 = sjx[i, 1];
                double x2 = sjx[i + 1, 0];
                double y2 = sjx[i + 1, 1];
                for (int j = 0; j < lenB - 1; j++) {
                    double x3 = dmx[j, 0];
                    double y3 = dmx[j, 1];
                    double x4 = dmx[j + 1, 0];
                    double y4 = dmx[j + 1, 1];
                    double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
                    if (denom != 0) {
                        double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
                        double u = ((x1 - x3) * (y1 - y2) - (y1 - y3) * (x1 - x2)) / denom;
                        if (t >= 0 && t <= 1 && u >= 0 && u <= 1) {
                            double px = x1 + t * (x2 - x1);
                            double py = y1 + t * (y2 - y1);
                            xys.Add(new double[] { px, py, (double)i, (double)j });
                        }
                    }
                }
            }

            // 💡 初始化包围框变量并追踪产生最左/最右交点的 xys 索引
            double minX = 0.0, maxX = 0.0, minY = 0.0, maxY = 0.0;
            int leftXysIdx = 0;   // 最左侧交点在 xys 集合里的位置
            int rightXysIdx = 0;  // 最右侧交点在 xys 集合里的位置

            if (xys.Count > 0) {
                minX = xys[0][0]; maxX = xys[0][0];
                minY = xys[0][1]; maxY = xys[0][1];

                for (int i = 1; i < xys.Count; i++) {
                    double x = xys[i][0], y = xys[i][1];
                    if (x < minX) {
                        minX = x;
                        leftXysIdx = i;  // 记录最左交点索引
                    } else if (x > maxX) {
                        maxX = x;
                        rightXysIdx = i; // 记录最右交点索引
                    }
                    if (y < minY) minY = y;
                    else if (y > maxY) maxY = y;
                }
            }

            // 原有计算填挖面积的循环（完全一致，保持不变）
            for (int idx = 0; idx < xys.Count - 1; idx++) {
                double[] xy0 = xys[idx];
                double[] xy1 = xys[idx + 1];
                double x0 = xy0[0], y0 = xy0[1];
                double x1p = xy1[0], y1p = xy1[1];
                int i0 = (int)xy0[2];
                int i1 = (int)xy1[2];
                int j0 = (int)xy0[3];
                int j1 = (int)xy1[3];
                List<double[]> pts = new List<double[]>();
                pts.Add(new double[] { x0, y0 });
                for (int k = i0 + 1; k <= i1; k++)
                    pts.Add(new double[] { sjx[k, 0], sjx[k, 1] });
                pts.Add(new double[] { x1p, y1p });
                for (int k = j1; k > j0; k--)
                    pts.Add(new double[] { dmx[k, 0], dmx[k, 1] });
                pts.Add(new double[] { x0, y0 });
                double signedArea = 0;
                for (int k = 0; k < pts.Count - 1; k++) {
                    double[] p1 = pts[k];
                    double[] p2 = pts[k + 1];
                    signedArea += p1[0] * p2[1] - p1[1] * p2[0];
                }
                double area = signedArea / 2.0;
                if (signedArea > 0)
                    cut += area;
                else
                    fill += area;
            }


            List<double[]> finalSjxList = new List<double[]>();

            if (xys.Count >= 2) {
                double[] leftIntersection = xys[leftXysIdx];
                double[] rightIntersection = xys[rightXysIdx];

                int leftSjxSegIdx = (int)leftIntersection[2];
                int rightSjxSegIdx = (int)rightIntersection[2];


                finalSjxList.Add(new double[] { leftIntersection[0], leftIntersection[1] });


                for (int k = leftSjxSegIdx + 1; k <= rightSjxSegIdx; k++) {
                    finalSjxList.Add(new double[] { sjx[k, 0], sjx[k, 1] });
                }


                finalSjxList.Add(new double[] { rightIntersection[0], rightIntersection[1] });
            } else {

                for (int k = 0; k < lenA; k++) finalSjxList.Add(new double[] { sjx[k, 0], sjx[k, 1] });
            }

            List<double> finalResults = new List<double>
    {
        Math.Round(fill, 4),
        Math.Round(cut, 4),
        minX,
        maxX,
        minY,
        maxY,
        (double)finalSjxList.Count // index 6: 截断后的顶点总数
    };

            foreach (var pt in finalSjxList) {
                finalResults.Add(pt[0]); // 存入 X
                finalResults.Add(pt[1]); // 存入 Y
            }

            return finalResults.ToArray();
        }



        public static double[,] hua_OffsetPolyline(double[,] points, double offset) {
            if (points == null) return null;
            int ptCount = points.GetLength(0);
            if (ptCount < 2) return (double[,])points.Clone();

            int segmentCount = ptCount - 1;
            double[,] segLines = new double[segmentCount, 4];
            bool[] validSeg = new bool[segmentCount];

            // 1. 全量生成所有线段的偏移平行线（没有任何人工距离过滤，保持高精）
            for (int i = 0; i < segmentCount; i++) {
                double dx = points[i + 1, 0] - points[i, 0];
                double dy = points[i + 1, 1] - points[i, 1];
                double len = Math.Sqrt(dx * dx + dy * dy);

                if (len < 1e-8) continue; // 仅过滤重合点

                validSeg[i] = true;
                double nx = -dy / len;
                double ny = dx / len;

                segLines[i, 0] = points[i, 0] + nx * offset;
                segLines[i, 1] = points[i, 1] + ny * offset;
                segLines[i, 2] = points[i + 1, 0] + nx * offset;
                segLines[i, 3] = points[i + 1, 1] + ny * offset;
            }

            // 2. 直线高精连续求交（生成全量无截断的原始偏移链）
            List<double[]> rawVertices = new List<double[]>();
            int firstIdx = -1;
            for (int i = 0; i < segmentCount; i++) if (validSeg[i]) { firstIdx = i; break; }
            if (firstIdx == -1) return new double[0, 2];

            rawVertices.Add(new double[] { segLines[firstIdx, 0], segLines[firstIdx, 1] });

            int prev = firstIdx;
            for (int i = firstIdx + 1; i < segmentCount; i++) {
                if (!validSeg[i]) continue;

                double x1 = segLines[prev, 0], y1 = segLines[prev, 1];
                double x2 = segLines[prev, 2], y2 = segLines[prev, 3];
                double x3 = segLines[i, 0], y3 = segLines[i, 1];
                double x4 = segLines[i, 2], y4 = segLines[i, 3];

                double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);

                if (Math.Abs(denom) > 1e-8) {
                    double t1 = x1 * y2 - y1 * x2;
                    double t2 = x3 * y4 - y3 * x4;
                    rawVertices.Add(new double[] { (t1 * (x3 - x4) - (x1 - x2) * t2) / denom, (t1 * (y3 - y4) - (y1 - y2) * t2) / denom });
                } else {
                    rawVertices.Add(new double[] { (x2 + x3) / 2.0, (y2 + y3) / 2.0 });
                }
                prev = i;
            }
            rawVertices.Add(new double[] { segLines[prev, 2], segLines[prev, 3] });

            // 3. 严格前向拓扑解结（回溯栈消解微小倒退与死环）
            List<double[]> cleanVertices = new List<double[]>();
            if (rawVertices.Count > 0) cleanVertices.Add(rawVertices[0]);

            for (int i = 1; i < rawVertices.Count; i++) {
                double[] curr = rawVertices[i];

                // 循环检查：如果当前点让拓扑序列发生回退，则吞噬历史死结
                while (cleanVertices.Count > 1) {
                    double[] lastValid = cleanVertices[cleanVertices.Count - 1];

                    // 100% 修复：对齐 C# 一维普通 double[] 数组的索引分量引用 [0]
                    if (curr[0] < lastValid[0] - 1e-5) {
                        // 弹出被挤死的无效历史畸变点
                        cleanVertices.RemoveAt(cleanVertices.Count - 1);
                    } else {
                        break;
                    }
                }

                // 最后一层物理防御：防止极近距离的重复交点存留
                double[] top = cleanVertices[cleanVertices.Count - 1];
                double dist = Math.Sqrt(Math.Pow(curr[0] - top[0], 2) + Math.Pow(curr[1] - top[1], 2));

                if (dist > 1e-4) {
                    // 100% 修复：对齐一维普通 double[] 数组的索引分量引用 [0]
                    if (curr[0] < top[0] + 1e-4 && cleanVertices.Count > 1) {
                        cleanVertices[cleanVertices.Count - 1] = new double[] { (top[0] + curr[0]) / 2.0, (top[1] + curr[1]) / 2.0 };
                    } else {
                        cleanVertices.Add(curr);
                    }
                }
            }
            // 4. 标准二维数组输出映射
            double[,] output = new double[cleanVertices.Count, 2]; for (int i = 0; i < cleanVertices.Count; i++) { output[i, 0] = cleanVertices[i][0]; output[i, 1] = cleanVertices[i][1]; }
            return output;
        }
        public static double[] hua_CircleFrom3Points(double x1, double y1, double x2, double y2, double x3, double y3) {
            double A = x1 * (y2 - y3) - y1 * (x2 - x3) + x2 * y3 - x3 * y2;
            double B = (x1 * x1 + y1 * y1) * (y3 - y2) + (x2 * x2 + y2 * y2) * (y1 - y3) + (x3 * x3 + y3 * y3) * (y2 - y1);
            double C = (x1 * x1 + y1 * y1) * (x2 - x3) + (x2 * x2 + y2 * y2) * (x3 - x1) + (x3 * x3 + y3 * y3) * (x1 - x2);
            double D = (x1 * x1 + y1 * y1) * (x3 * y2 - x2 * y3) + (x2 * x2 + y2 * y2) * (x1 * y3 - x3 * y1) + (x3 * x3 + y3 * y3) * (x2 * y1 - x1 * y2);
            double denom = 2 * A;
            if (Math.Abs(denom) < 1e-12) return new double[] { 0, 0, -1 };
            double centerX = B / denom;
            double centerY = C / denom;
            double radius = Math.Sqrt((x1 - centerX) * (x1 - centerX) + (y1 - centerY) * (y1 - centerY));
            return new double[] { centerX, centerY, radius };
        }
        public static double hua_PolygonArea(double[,] poly) {
            if (poly.GetLength(0) < 3) return 0;
            double area = 0;
            int n = poly.GetLength(0);
            for (int i = 0; i < n; i++) {
                int j = (i + 1) % n;
                area += poly[i, 0] * poly[j, 1];
                area -= poly[i, 1] * poly[j, 0];
            }
            return Math.Abs(area) / 2.0;
        }
        public static double[] hua_FootPoint(double px, double py, double x1, double y1, double x2, double y2) {
            double dx = x2 - x1, dy = y2 - y1;
            double len2 = dx * dx + dy * dy;
            if (len2 < 1e-12) return new double[] { x1, y1, 0 };
            double t = ((px - x1) * dx + (py - y1) * dy) / len2;
            double footX = x1 + t * dx, footY = y1 + t * dy;
            int flag = (t >= 0 && t <= 1) ? 0 : (t > 1 ? 1 : 2);
            return new double[] { footX, footY, flag };
        }
        public static double[] hua_SegmentIntersection(double x1, double y1, double x2, double y2, double x3, double y3, double x4, double y4) {
            double dx1 = x2 - x1, dy1 = y2 - y1;
            double dx2 = x4 - x3, dy2 = y4 - y3;
            double denom = dx1 * dy2 - dy1 * dx2;
            if (Math.Abs(denom) < 1e-12) return new double[] { 0, 0, -1, -1 };
            double t = ((x3 - x1) * dy2 - (y3 - y1) * dx2) / denom;
            double u = ((x3 - x1) * dy1 - (y3 - y1) * dx1) / denom;
            double ix = x1 + t * dx1, iy = y1 + t * dy1;
            int flag1 = (t >= 0 && t <= 1) ? 0 : (t > 1 ? 1 : 2);
            int flag2 = (u >= 0 && u <= 1) ? 0 : (u > 1 ? 1 : 2);
            return new double[] { ix, iy, flag1, flag2 };
        }
        public static double hua_slope(double mileage, double[,] points, int interpType = 0) {
            if (points == null) throw new ArgumentNullException(nameof(points));
            int n = points.GetLength(0);
            if (n == 0) throw new ArgumentException("点表不能为空。");
            if (points.GetLength(1) < 2) throw new ArgumentException("点表必须包含至少两列：里程和横坡。");
            for (int i = 1; i < n; i++)
                if (points[i, 0] <= points[i - 1, 0])
                    throw new ArgumentException($"里程必须严格递增，第{i}行里程 {points[i, 0]} <= 上一行 {points[i - 1, 0]}");
            if (mileage <= points[0, 0]) return points[0, 1];
            if (mileage >= points[n - 1, 0]) return points[n - 1, 1];
            if (interpType == 0) {
                for (int i = 0; i < n - 1; i++) {
                    double k1 = points[i, 0], k2 = points[i + 1, 0];
                    if (mileage >= k1 && mileage <= k2) {
                        double s1 = points[i, 1], s2 = points[i + 1, 1];
                        if (Math.Abs(k2 - k1) < 1e-9) return s1;
                        double t = (mileage - k1) / (k2 - k1);
                        return s1 + t * (s2 - s1);
                    }
                }
            } else if (interpType == 1) {
                int i = 0;
                for (; i < n - 1; i++)
                    if (mileage <= points[i + 1, 0]) break;
                int left, mid, right;
                if (i + 2 < n) {
                    left = i;
                    mid = i + 1;
                    right = i + 2;
                } else if (i - 2 >= 0) {
                    left = i - 2;
                    mid = i - 1;
                    right = i;
                } else {
                    return hua_slope(mileage, points, 0);
                }
                double x0 = points[left, 0], y0 = points[left, 1];
                double x1 = points[mid, 0], y1 = points[mid, 1];
                double x2 = points[right, 0], y2 = points[right, 1];
                double x = mileage;
                double result = y0 * (x - x1) * (x - x2) / ((x0 - x1) * (x0 - x2))
                              + y1 * (x - x0) * (x - x2) / ((x1 - x0) * (x1 - x2))
                              + y2 * (x - x0) * (x - x1) / ((x2 - x0) * (x2 - x1));
                return result;
            } else {
                throw new ArgumentException("不支持的插值类型，当前仅支持 0=线性，1=二次抛物线。");
            }
            return points[0, 1];
        }
        public static object[,] hua_area(double x1, double y1, double x2, double y2, double[,] points, double[,] queryKeys, double tolerance) {
            double[,] data = hua_getKBZ(x1, y1, x2, y2, points);
            int dataLen = data.GetLength(0);
            bool[] used = new bool[dataLen];
            int queryCount = queryKeys.GetLength(0);
            object[,] result = new object[queryCount + 2 + dataLen, 3];
            int resultRow = 0;
            double totalVolume = 0;
            double prevK = 0, prevArea = 0;
            result[resultRow, 0] = "断面编号";
            result[resultRow, 1] = "断面面积(m²)";
            result[resultRow, 2] = "体积(m³)";
            resultRow++;
            for (int idx = 0; idx < queryCount; idx++) {
                double qk = queryKeys[idx, 0];
                var matched = new List<double[]>();
                for (int i = 0; i < dataLen; i++) {
                    if (Math.Abs(data[i, 0] - qk) <= tolerance) {
                        matched.Add(new double[] { data[i, 1], data[i, 2] });
                        used[i] = true;
                    }
                }
                matched.Sort((a, b) => a[0].CompareTo(b[0]));
                double area = 0;
                if (matched.Count >= 3) {
                    double[,] polygon = new double[matched.Count, 2];
                    for (int i = 0; i < matched.Count; i++) {
                        polygon[i, 0] = matched[i][0];
                        polygon[i, 1] = matched[i][1];
                    }
                    area = hua_PolygonArea(polygon);
                }
                double volume = 0;
                if (idx > 0) {
                    double deltaK = qk - prevK;
                    double avgArea = (area + prevArea) / 2.0;
                    volume = avgArea * deltaK;
                    totalVolume += volume;
                }
                result[resultRow, 0] = hua_Num2K(qk);
                result[resultRow, 1] = Math.Round(area, 3);
                result[resultRow, 2] = Math.Round(volume, 3);
                prevK = qk;
                prevArea = area;
                resultRow++;
            }
            double minK = queryKeys[0, 0];
            double maxK = queryKeys[queryCount - 1, 0];
            string rangeStr = $"{hua_Num2K(minK)}~{hua_Num2K(maxK)}";
            result[resultRow, 0] = rangeStr;
            result[resultRow, 1] = "合计";
            result[resultRow, 2] = Math.Round(totalVolume, 3);
            resultRow++;
            result[resultRow, 0] = "无效点";
            result[resultRow, 1] = "桩号";
            result[resultRow, 2] = "行号";
            resultRow++;
            for (int i = 0; i < dataLen; i++) {
                if (!used[i]) {
                    result[resultRow, 0] = -1;
                    result[resultRow, 1] = hua_Num2K(data[i, 0]);
                    result[resultRow, 2] = i;
                    resultRow++;
                }
            }
            object[,] final = new object[resultRow, 3];
            for (int i = 0; i < resultRow; i++) {
                final[i, 0] = result[i, 0];
                final[i, 1] = result[i, 1];
                final[i, 2] = result[i, 2];
            }
            return final;
        }
        public static double[,] ReadDataFromFile(string filePath, int columnCount, Encoding encoding = null, bool skipFirstRow = false) {
            string fileName = Path.GetFileName(filePath);
            Encoding actualEncoding = encoding ?? Encoding.UTF8;

            string[] allLines = File.ReadAllLines(filePath, actualEncoding);
            List<double[]> validRows = new List<double[]>();

            char[] separators = { ' ', ',', '，', '\t' };

            int startIndex = skipFirstRow ? 1 : 0;
            for (int lineIndex = startIndex; lineIndex < allLines.Length; lineIndex++) {
                string line = allLines[lineIndex];
                string trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine))
                    continue;

                string[] tokens = trimmedLine.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < columnCount) {
                    throw new FormatException(
                        $"文件 {fileName} 第 {lineIndex + 1} 行不是 {columnCount} 列数据，实际列数: {tokens.Length}");
                }

                double[] doubleRow = new double[columnCount];
                for (int tokenIndex = 0; tokenIndex < columnCount; tokenIndex++) {
                    if (!double.TryParse(tokens[tokenIndex], out double value)) {
                        throw new FormatException(
                            $"解析失败 → 文件：{fileName} → 行号：{lineIndex + 1} → 列号：{tokenIndex + 1} → 内容：{tokens[tokenIndex]}");
                    }
                    doubleRow[tokenIndex] = value;
                }
                validRows.Add(doubleRow);
            }

            double[,] result = new double[validRows.Count, columnCount];
            for (int i = 0; i < validRows.Count; i++) {
                for (int j = 0; j < columnCount; j++) {
                    result[i, j] = validRows[i][j];
                }
            }
            return result;
        }
    }
}
