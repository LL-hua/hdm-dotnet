using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LLutile {
    /// <summary>
    /// SVG 横断面图生成器（支持字体大小调节）
    /// </summary>
    public static class SvgBuilder {
        /// <summary>
        /// 生成单个断面的 SVG
        /// </summary>
        public static string BuildSectionSvg(SectionResult result, int width = 900, int height = 500, int margin = 60) {
            var points = new List<double[]>();

            AddPoints(points, result.Ground);
            AddPoints(points, result.FinalDesign);
            AddPoints(points, result.Cleared);
            AddPoints(points, result.LeftSubgrade);
            AddPoints(points, result.RightSubgrade);
            if (result.LeftSlopePoints != null) AddPoints(points, result.LeftSlopePoints);
            if (result.RightSlopePoints != null) AddPoints(points, result.RightSlopePoints);
            if (result.LayerPolygons != null) {
                foreach (var poly in result.LayerPolygons)
                    AddPoints(points, poly);
            }

            if (points.Count == 0)
                return "<svg viewBox=\"0 0 800 400\"><text x=\"400\" y=\"200\" text-anchor=\"middle\" fill=\"#999\">无数据</text></svg>";

            // 计算边界
            double minX = points.Min(p => p[0]);
            double maxX = points.Max(p => p[0]);
            double minY = points.Min(p => p[1]);
            double maxY = points.Max(p => p[1]);

            if (maxX - minX < 0.1) maxX = minX + 1;
            if (maxY - minY < 0.1) maxY = minY + 1;

            // 等比例缩放
            double dataWidth = maxX - minX;
            double dataHeight = maxY - minY;
            double scaleX = (width - 2 * margin) / dataWidth;
            double scaleY = (height - 2 * margin) / dataHeight;
            double scale = Math.Min(scaleX, scaleY);

            double drawWidth = dataWidth * scale;
            double drawHeight = dataHeight * scale;
            double offsetX = (width - drawWidth) / 2;
            double offsetY = (height - drawHeight) / 2;

            Func<double, double> toX = x => offsetX + (x - minX) * scale;
            Func<double, double> toY = y => height - offsetY - (y - minY) * scale;

            var sb = new StringBuilder();
            sb.AppendLine($@"<svg viewBox=""0 0 {width} {height}"" xmlns=""http://www.w3.org/2000/svg"" style=""font-family:'Microsoft YaHei',sans-serif;"">");
            sb.AppendLine($@"<rect width=""{width}"" height=""{height}"" fill=""#fafcfe""/>");

            // 网格
            double gridStepX = Math.Pow(10, Math.Ceiling(Math.Log10(dataWidth / 10)));
            double gridStepY = Math.Pow(10, Math.Ceiling(Math.Log10(dataHeight / 10)));
            if (gridStepX < 1) gridStepX = 1;
            if (gridStepY < 1) gridStepY = 1;

            for (double x = Math.Ceiling(minX / gridStepX) * gridStepX; x <= maxX; x += gridStepX) {
                double sx = toX(x);
                sb.AppendLine($@"<line x1=""{sx:F1}"" y1=""{margin}"" x2=""{sx:F1}"" y2=""{height - margin}"" stroke=""#e8ecf1"" stroke-width=""0.5""/>");
            }
            for (double y = Math.Ceiling(minY / gridStepY) * gridStepY; y <= maxY; y += gridStepY) {
                double sy = toY(y);
                sb.AppendLine($@"<line x1=""{margin}"" y1=""{sy:F1}"" x2=""{width - margin}"" y2=""{sy:F1}"" stroke=""#e8ecf1"" stroke-width=""0.5""/>");
            }

            // 结构层填充
            if (result.LayerPolygons != null) {
                foreach (var poly in result.LayerPolygons) {
                    if (poly.GetLength(0) < 3) continue;
                    sb.Append(@"<polygon points=""");
                    for (int i = 0; i < poly.GetLength(0); i++)
                        sb.Append($"{toX(poly[i, 0]):F2},{toY(poly[i, 1]):F2} ");
                    sb.AppendLine(@""" fill=""#f3e5f5"" stroke=""#7b1fa2"" stroke-width=""1"" stroke-opacity=""0.5"" fill-opacity=""0.25""/>");
                }
            }

            // 绘制线条（添加 CSS class 以便控制字号）
            AppendPolyline(sb, result.Ground, toX, toY, "#d32f2f", 2);
            AppendPolyline(sb, result.Cleared, toX, toY, "#388e3c", 2, true);
            AppendPolyline(sb, result.LeftSubgrade, toX, toY, "#f57c00", 2);
            AppendPolyline(sb, result.RightSubgrade, toX, toY, "#f57c00", 2);
            AppendPolyline(sb, result.FinalDesign, toX, toY, "#1976d2", 2);

            // === 标注（添加 CSS class） ===

            // 中桩高程
            double cx = toX(0), cy = toY(result.CenterY);
            sb.AppendLine($@"<circle cx=""{cx:F2}"" cy=""{cy:F2}"" r=""1"" fill=""#1976d2""/>");
            sb.AppendLine($@"<text class=""text-elevation"" x=""{cx:F2}"" y=""{cy}""   text-anchor=""start""
        dominant-baseline=""middle"" font-size=""11"" fill=""#1976d2"" transform=""rotate(-90,{cx:F2},{cy:F2})"">{result.CenterY:F3}</text>");

            // 边桩高程
            if (result.LOuterX < -0.01) {
                double lx = toX(result.LOuterX), ly = toY(result.LOuterY);
                sb.AppendLine($@"<text class=""text-elevation"" x=""{lx:F2}"" y=""{ly}"" text-anchor=""start""
        dominant-baseline=""middle"" font-size=""10"" fill=""#1976d2"" transform=""rotate(-90,{lx:F2},{ly:F2})"">{result.LOuterY:F3}</text>");
            }
            if (result.ROuterX > 0.01) {
                double rx = toX(result.ROuterX), ry = toY(result.ROuterY);
                sb.AppendLine($@"<text class=""text-elevation"" x=""{rx:F2}"" y=""{ry}"" text-anchor=""start""
        dominant-baseline=""middle"" font-size=""10"" fill=""#1976d2"" transform=""rotate(-90,{rx:F2},{ry:F2})"">{result.ROuterY:F3}</text>");
            }

            // 横坡标注
            if (result.LOuterX < -0.01) {
                double midX = result.LOuterX / 2;
                double midY = result.CenterY + Math.Abs(result.LOuterX / 2) * (result.LeftCrossfall / 100);
                double mx = toX(midX), my = toY(midY);
                sb.AppendLine($@"<text class=""text-crossfall"" x=""{mx:F2}"" y=""{my - 10:F2}"" text-anchor=""middle"" font-size=""10"" fill=""#e65100"" font-weight=""bold"">{result.LeftCrossfall:F2}%</text>");
            }
            if (result.ROuterX > 0.01) {
                double midX = result.ROuterX / 2;
                double midY = result.CenterY + Math.Abs(result.ROuterX / 2) * (result.RightCrossfall / 100);
                double mx = toX(midX), my = toY(midY);
                sb.AppendLine($@"<text class=""text-crossfall"" x=""{mx:F2}"" y=""{my - 10:F2}"" text-anchor=""middle"" font-size=""10"" fill=""#e65100"" font-weight=""bold"">{result.RightCrossfall:F2}%</text>");
            }

            // 边坡坡率标注
            if (result.LeftSlopePoints != null && result.LeftSlopePoints.Count >= 1) {
                var pts = new List<double[]>();
                pts.Add(new[] { result.LOuterX, result.LOuterY });
                foreach (var p in result.LeftSlopePoints) {
                    // Console.WriteLine(result.LOuterX);
                    if (p[0] < result.LOuterX && p[0] > result.LeftToeX) {
                        pts.Add(p);
                        // Console.WriteLine($"{p[0]:f3},{p[1]:f3}");
                    }
                }
                pts.Add(new[] { result.LeftToeX, result.LeftToeY });
                // 按 x 降序：x 大的排前面
                pts.Sort((a, b) => b[0].CompareTo(a[0]));
                foreach (var p in pts) {
                    //Console.WriteLine($"{p[0]:F3}, {p[1]:F3}");
                }
                //Console.WriteLine("============");
                AnnotateSlope(sb, pts, toX, toY);
            }

            if (result.RightSlopePoints != null && result.RightSlopePoints.Count >= 1) {
                var pts = new List<double[]>();
                pts.Add(new[] { result.ROuterX, result.ROuterY });

                foreach (var p in result.RightSlopePoints) {
                    if (p[0] > result.ROuterX && p[0] < result.RightToeX) {
                        pts.Add(p);
                    }

                }
                pts.Add(new[] { result.RightToeX, result.RightToeY });
                pts.Sort((a, b) => b[0].CompareTo(a[0]));
                AnnotateSlope(sb, pts, toX, toY);
            }

            // 桩号标注
            sb.AppendLine($@"<text class=""text-station"" x=""{toX(0):F2}"" y=""{height - 8:F2}"" text-anchor=""middle"" font-size=""13"" fill=""#1a2a3a"" font-weight=""bold"">{LL.hua_Num2K(result.Station)}</text>");

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        /// <summary>
        /// 生成包含所有断面的完整 HTML 页面（带字体大小调节面板）
        /// </summary>
        public static string BuildHtml(string projectName, List<SectionResult> results) {
            var sb = new StringBuilder();
            sb.AppendLine($@"<!DOCTYPE html><html><head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""><title>{projectName} 横断面成果图</title>
<style>
*{{box-sizing:border-box;margin:0;padding:0}}body{{font-family:'Microsoft YaHei',sans-serif;background:#e8ecf1;padding:20px}}
.header{{background:white;padding:20px 30px;border-radius:12px;box-shadow:0 2px 8px rgba(0,0,0,0.1);margin-bottom:20px;display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap}}
.header h1{{color:#1a2a3a;font-size:24px}}.header .info{{color:#6a7a8a;font-size:14px}}
.controls{{display:flex;gap:12px;align-items:center;flex-wrap:wrap}}
.controls input{{padding:6px 14px;border:1px solid #c0d0e0;border-radius:6px;font-size:14px;width:160px}}
.controls button{{padding:6px 18px;background:#3b7cff;color:white;border:none;border-radius:6px;cursor:pointer;font-size:14px}}
.controls button:hover{{background:#2a6ae0}}
.section-container{{background:white;margin:16px 0;padding:20px 25px;border-radius:12px;box-shadow:0 2px 8px rgba(0,0,0,0.08)}}
.section-title{{font-size:18px;font-weight:600;color:#1a2a3a;margin-bottom:8px}}

.section-stats{{display:flex;flex-wrap:nowrap;gap:12px;font-size:14px;color:#4a5a6a;margin-bottom:12px;padding-bottom:10px;border-bottom:1px solid #eef2f7}}
.section-stats .fill{{color:#2e7d32}}.section-stats .cut{{color:#c62828}}.section-stats .clear{{color:#f9a825}}


svg{{display:block;width:100%;height:auto;background:#fafcfe;border-radius:8px;border:1px solid #e8ecf1}}
.legend{{display:flex;flex-wrap:wrap;gap:18px;padding:10px 0 4px 0;font-size:13px;color:#3a4a5a}}
.legend-item{{display:flex;align-items:center;gap:6px}}
.legend-color{{width:28px;height:3px;border-radius:2px}}
.footer{{text-align:center;color:#8a9aaa;font-size:12px;padding:20px 0 10px 0}}

/* 文字大小控制面板 */
.text-control{{position:fixed;top:20px;right:20px;z-index:999;background:white;border-radius:12px;box-shadow:0 4px 20px rgba(0,0,0,0.15);padding:12px 14px;border:1px solid #e0e6ed}}
.text-control .toggle-btn{{background:none;border:none;font-size:24px;cursor:pointer;padding:4px 8px;border-radius:50%;transition:background 0.2s;width:40px;height:40px;display:flex;align-items:center;justify-content:center;color:#3a4a5a}}
.text-control .toggle-btn:hover{{background:#eef2f7}}
.text-control .panel{{display:none;margin-top:10px;padding-top:10px;border-top:1px solid #eef2f7}}
.text-control .panel.open{{display:block}}
.text-control .panel-title{{font-weight:600;font-size:14px;color:#1a2a3a;text-align:center;margin-bottom:12px}}
.text-control .control-group{{margin-bottom:10px}}
.text-control .control-group:last-child{{margin-bottom:0}}
.text-control .control-group label{{display:flex;justify-content:space-between;font-size:13px;color:#3a4a5a;font-weight:500}}
.text-control .control-group input[type=""range""]{{width:100%;margin:2px 0;accent-color:#3b7cff}}
.text-control .control-group .value{{color:#3b7cff;font-weight:600}}
.text-control .reset-btn{{background:#eef2f7;border:none;padding:4px 14px;border-radius:4px;font-size:12px;cursor:pointer;color:#3a4a5a;margin-top:4px;width:100%}}
.text-control .reset-btn:hover{{background:#dce0e8}}
@media(max-width:600px){{.text-control{{top:10px;right:10px;padding:8px 10px}}.text-control .toggle-btn{{font-size:20px;width:32px;height:32px}}.header{{flex-direction:column;align-items:flex-start;gap:10px}}.controls{{width:100%}}.controls input{{flex:1}}}}
</style>
</head><body>
<!-- 文字大小控制面板 -->
<div class=""text-control"" id=""textControl"">
<button class=""toggle-btn"" onclick=""togglePanel()"" title=""调整文字大小"">⚙️</button>
<div class=""panel"" id=""controlPanel"">
<div class=""panel-title"">🔤 调整文字大小</div>
<div class=""control-group"">
<label>📌 桩号 <span class=""value"" id=""valStation"">13</span></label>
<input type=""range"" id=""sizeStation"" min=""6"" max=""30"" value=""13"" step=""0.5"" oninput=""updateSize('station', this.value)"">
</div>
<div class=""control-group"">
<label>📐 设计高程 <span class=""value"" id=""valElevation"">11</span></label>
<input type=""range"" id=""sizeElevation"" min=""6"" max=""30"" value=""11"" step=""0.5"" oninput=""updateSize('elevation', this.value)"">
</div>
<div class=""control-group"">
<label>📏 坡比 <span class=""value"" id=""valSlope"">9</span></label>
<input type=""range"" id=""sizeSlope"" min=""5"" max=""24"" value=""9"" step=""0.5"" oninput=""updateSize('slope', this.value)"">
</div>
<div class=""control-group"">
<label>📊 横坡 <span class=""value"" id=""valCrossfall"">10</span></label>
<input type=""range"" id=""sizeCrossfall"" min=""6"" max=""24"" value=""10"" step=""0.5"" oninput=""updateSize('crossfall', this.value)"">
</div>
<button class=""reset-btn"" onclick=""resetSizes()"">恢复默认</button>
</div>
</div>

<div class=""header""><div><h1>📐 {projectName} 横断面成果图</h1>
<div class=""info"">共 {results.Count} 个断面 | 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</div></div>
<div class=""controls""><input type=""text"" id=""searchInput"" placeholder=""🔍 搜索桩号..."" oninput=""filterSections()""><button onclick=""scrollToTop()"">⬆ 回到顶部</button></div></div>
<div id=""sectionsContainer"">");

            foreach (var r in results) {
                string station = LL.hua_Num2K(r.Station);
                sb.AppendLine($@"<div class=""section-container"" data-station=""{station}"">");
                sb.AppendLine($@"<div class=""section-title"">桩号: {station}</div>");
                sb.AppendLine($@"<div class=""section-stats"">");
                sb.AppendLine($@"<span class=""fill"">AT: {r.FillArea:F3} m²</span>");
                sb.AppendLine($@"<span class=""cut"">AW: {r.CutArea:F3} m²</span>");
                sb.AppendLine($@"<span class=""clear"">清表: {r.ClearArea:F3} m²</span>");
                /**
                    if (r.LayerAreaTexts != null) {
                      foreach (var txt in r.LayerAreaTexts)
                          sb.AppendLine($@"<span style=""color:#6a4a8a;"">📐 {txt}</span>");
                  }
                  **/
                sb.AppendLine("</div>");
                sb.AppendLine(BuildSectionSvg(r));
                sb.AppendLine("</div>");
            }

            sb.AppendLine(@"</div>
<div class=""legend"" style=""background:white;padding:12px 25px;border-radius:12px;margin-top:16px;box-shadow:0 2px 8px rgba(0,0,0,0.08);"">
<div class=""legend-item""><span class=""legend-color"" style=""background:#d32f2f;""></span> 地面线</div>
<div class=""legend-item""><span class=""legend-color"" style=""background:#1976d2;""></span> 设计线</div>
<div class=""legend-item""><span class=""legend-color"" style=""background:#388e3c;""></span> 清表线</div>
<div class=""legend-item""><span class=""legend-color"" style=""background:#f57c00;""></span> 结构层</div>
<div class=""legend-item""><span class=""legend-color"" style=""background:#7b1fa2;""></span> 结构层填充</div>
</div>
<div class=""footer"">生成于测绘自动化系统 v1.0</div>
<script>
// 文字大小控制
const SIZE_KEY = 'llhdm_text_sizes_dotnet';

function getDefaultSizes() {
    return { station: 13, elevation: 11, slope: 9, crossfall: 10 };
}

function loadSizes() {
    try {
        const saved = localStorage.getItem(SIZE_KEY);
        if (saved) {
            const parsed = JSON.parse(saved);
            const def = getDefaultSizes();
            return { ...def, ...parsed };
        }
    } catch (e) {}
    return getDefaultSizes();
}

function saveSizes(sizes) {
    localStorage.setItem(SIZE_KEY, JSON.stringify(sizes));
}

function applySizes(sizes) {
    document.getElementById('valStation').textContent = sizes.station;
    document.getElementById('valElevation').textContent = sizes.elevation;
    document.getElementById('valSlope').textContent = sizes.slope;
    document.getElementById('valCrossfall').textContent = sizes.crossfall;
    document.getElementById('sizeStation').value = sizes.station;
    document.getElementById('sizeElevation').value = sizes.elevation;
    document.getElementById('sizeSlope').value = sizes.slope;
    document.getElementById('sizeCrossfall').value = sizes.crossfall;

    document.querySelectorAll('.text-station').forEach(el => el.style.fontSize = sizes.station + 'px');
    document.querySelectorAll('.text-elevation').forEach(el => el.style.fontSize = sizes.elevation + 'px');
    document.querySelectorAll('.text-slope').forEach(el => el.style.fontSize = sizes.slope + 'px');
    document.querySelectorAll('.text-crossfall').forEach(el => el.style.fontSize = sizes.crossfall + 'px');
}

function updateSize(type, value) {
    const sizes = loadSizes();
    sizes[type] = parseFloat(value);
    saveSizes(sizes);
    applySizes(sizes);
}

function resetSizes() {
    const def = getDefaultSizes();
    saveSizes(def);
    applySizes(def);
}

function togglePanel() {
    const panel = document.getElementById('controlPanel');
    panel.classList.toggle('open');
}

document.addEventListener('DOMContentLoaded', function() {
    const sizes = loadSizes();
    applySizes(sizes);
});

// 搜索过滤
function filterSections() {
    var k = document.getElementById('searchInput').value.trim().toLowerCase();
    document.querySelectorAll('.section-container').forEach(function(el) {
        var t = el.querySelector('.section-title').textContent.toLowerCase();
        el.style.display = (!k || t.indexOf(k) > -1) ? 'block' : 'none';
    });
}
function scrollToTop(){window.scrollTo({top:0,behavior:'smooth'});}
</script>
</body></html>");

            return sb.ToString();
        }

        // ========== 辅助方法 ==========

        private static void AddPoints(List<double[]> list, double[,] arr) {
            if (arr == null) return;
            int rows = arr.GetLength(0);
            for (int i = 0; i < rows; i++)
                list.Add(new[] { arr[i, 0], arr[i, 1] });
        }

        private static void AddPoints(List<double[]> list, List<double[]> arr) {
            if (arr == null) return;
            foreach (var p in arr)
                list.Add(p);
        }

        private static void AddPoints(List<double[]> list, double[][] arr) {
            if (arr == null) return;
            foreach (var p in arr)
                list.Add(p);
        }

        private static void AppendPolyline(StringBuilder sb, double[,] points, Func<double, double> toX, Func<double, double> toY, string color, double strokeWidth, bool dashed = false) {
            if (points == null || points.GetLength(0) < 2) return;
            sb.Append(@"<polyline points=""");
            for (int i = 0; i < points.GetLength(0); i++)
                sb.Append($"{toX(points[i, 0]):F2},{toY(points[i, 1]):F2} ");
            sb.Append($@""" fill=""none"" stroke=""{color}"" stroke-width=""1""");
            if (dashed) sb.Append(@" stroke-dasharray=""6,4""");
            sb.AppendLine("/>");
        }

        private static void AppendPolyline(StringBuilder sb, List<double[]> points, Func<double, double> toX, Func<double, double> toY, string color, double strokeWidth, bool dashed = false) {
            if (points == null || points.Count < 2) return;
            sb.Append(@"<polyline points=""");
            foreach (var p in points)
                sb.Append($"{toX(p[0]):F2},{toY(p[1]):F2} ");
            sb.Append($@""" fill=""none"" stroke=""{color}"" stroke-width=""1""");
            if (dashed) sb.Append(@" stroke-dasharray=""6,4""");
            sb.AppendLine("/>");
        }

        private static void AnnotateSlope(StringBuilder sb, List<double[]> pts, Func<double, double> toX, Func<double, double> toY) {
            for (int i = 0; i < pts.Count - 1; i++) {
                double x1 = pts[i][0], y1 = pts[i][1];
                double x2 = pts[i + 1][0], y2 = pts[i + 1][1];
                double dx = x2 - x1, dy = y2 - y1;
                if (Math.Abs(dx) > 0.01 && Math.Abs(dy) > 0.01) {
                    double ratio = Math.Abs(dx) / Math.Abs(dy);
                    double midX = (x1 + x2) / 2;
                    double midY = (y1 + y2) / 2;
                    double mx = toX(midX), my = toY(midY);
                    double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
                    if (angle > 90) angle -= 180;
                    else if (angle < -90) angle += 180;
                    angle = -angle; // SVG Y轴向下
                    sb.AppendLine($@"<text class=""text-slope"" x=""{mx:F2}"" y=""{my - 4:F2}"" text-anchor=""middle"" font-size=""9"" fill=""#1565c0"" transform=""rotate({angle:F1},{mx:F2},{my:F2})"">1:{ratio:F2}</text>");
                }
            }
        }
    }
}