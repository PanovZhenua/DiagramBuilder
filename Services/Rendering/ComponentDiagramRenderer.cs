using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DiagramBuilder.Models;

namespace DiagramBuilder.Services.Rendering
{
    public class ComponentDiagramRenderer
    {
        private readonly Canvas canvas;

        private readonly Dictionary<string, (FrameworkElement visual, string type)> blockVisuals =
            new Dictionary<string, (FrameworkElement visual, string type)>();

        private readonly List<TextBlock> arrowLabels = new List<TextBlock>();

        private ComponentDiagram currentDiagram;

        private ResizeAdorner currentResizeAdorner;
        private FrameworkElement currentSelected;

        private const double ARROWHEADLENGTH = 12.0;
        private const double ARROWHEADANGLE = 0.45;

        private static readonly Brush ComponentFill = new SolidColorBrush(Color.FromRgb(255, 250, 210));
        private static readonly Brush StrokeBrush = Brushes.Black;

        public ComponentDiagramRenderer(Canvas canvas)
        {
            this.canvas = canvas;
        }

        // ===========================
        // Public API
        // ===========================

        public void Render(ComponentDiagram diagram)
        {
            currentDiagram = diagram;

            canvas.Children.Clear();
            blockVisuals.Clear();
            arrowLabels.Clear();

            canvas.Background = Brushes.White;

            if (diagram == null) return;

            if (diagram.Components != null)
            {
                foreach (var c in diagram.Components)
                {
                    if (c == null) continue;

                    FrameworkElement visual = CreateComponentVisual(c);
                    Canvas.SetLeft(visual, c.X);
                    Canvas.SetTop(visual, c.Y);
                    Panel.SetZIndex(visual, 30);
                    canvas.Children.Add(visual);

                    if (!string.IsNullOrWhiteSpace(c.Id))
                        blockVisuals[c.Id] = (visual, "COMPONENT");

                    AttachSelection(visual);
                }
            }

            if (diagram.Databases != null)
            {
                foreach (var db in diagram.Databases)
                {
                    if (db == null) continue;

                    FrameworkElement visual = CreateDatabaseVisual(db.Width, db.Height, db.MinWidth, db.MinHeight, db.Name);
                    Canvas.SetLeft(visual, db.X);
                    Canvas.SetTop(visual, db.Y);
                    Panel.SetZIndex(visual, 24);
                    canvas.Children.Add(visual);

                    if (!string.IsNullOrWhiteSpace(db.Id))
                        blockVisuals[db.Id] = (visual, "DATABASE");

                    AttachSelection(visual);
                }
            }

            if (diagram.WorkflowNodes != null)
            {
                foreach (var n in diagram.WorkflowNodes)
                {
                    if (n == null) continue;

                    FrameworkElement visual = CreateWorkflowVisual(n);
                    Canvas.SetLeft(visual, n.X);
                    Canvas.SetTop(visual, n.Y);
                    Panel.SetZIndex(visual, 28);
                    canvas.Children.Add(visual);

                    if (!string.IsNullOrWhiteSpace(n.Id))
                        blockVisuals[n.Id] = (visual, "WORKFLOW_" + n.Type.ToString().ToUpperInvariant());

                    AttachSelection(visual);
                }
            }

            RenderArrows(diagram);

            canvas.MouseLeftButtonDown -= Canvas_MouseLeftButtonDown;
            canvas.MouseLeftButtonDown += Canvas_MouseLeftButtonDown;
        }

        public void UpdateArrows(ComponentDiagram diagram)
        {
            var lines = canvas.Children.OfType<Line>().ToList();
            var polys = canvas.Children.OfType<Polygon>().ToList();

            foreach (var l in lines) canvas.Children.Remove(l);
            foreach (var p in polys) canvas.Children.Remove(p);

            foreach (var lbl in arrowLabels) canvas.Children.Remove(lbl);
            arrowLabels.Clear();

            RenderArrows(diagram);
        }

        public Dictionary<string, FrameworkElement> GetBlockVisuals()
        {
            return blockVisuals.ToDictionary(k => k.Key, v => v.Value.visual);
        }

        public Dictionary<FrameworkElement, string> GetBlockTypes()
        {
            return blockVisuals.ToDictionary(k => k.Value.visual, v => v.Value.type);
        }

        public List<TextBlock> GetArrowLabels()
        {
            return arrowLabels;
        }

        // ===========================
        // Selection + Resize
        // ===========================

        private void AttachSelection(FrameworkElement visual)
        {
            visual.MouseLeftButtonDown -= Visual_MouseLeftButtonDown;
            visual.MouseLeftButtonDown += Visual_MouseLeftButtonDown;
        }

        private void Visual_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            FrameworkElement fe = sender as FrameworkElement;
            if (fe == null) return;

            e.Handled = false;
            SelectElement(fe);
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == canvas)
                ClearSelection();
        }

        private void SelectElement(FrameworkElement fe)
        {
            if (currentSelected == fe) return;

            ClearSelection();
            currentSelected = fe;

            AdornerLayer layer = AdornerLayer.GetAdornerLayer(fe);
            if (layer == null) return;

            currentResizeAdorner = new ResizeAdorner(fe, OnResizeChanged);
            layer.Add(currentResizeAdorner);
        }

        private void ClearSelection()
        {
            if (currentSelected != null)
            {
                AdornerLayer layer = AdornerLayer.GetAdornerLayer(currentSelected);
                if (layer != null && currentResizeAdorner != null)
                    layer.Remove(currentResizeAdorner);
            }

            currentResizeAdorner = null;
            currentSelected = null;
        }

        private void OnResizeChanged(FrameworkElement fe)
        {
            if (fe == null) return;

            Canvas host = fe as Canvas;
            if (host != null)
            {
                HostTag tag = host.Tag as HostTag;
                if (tag != null)
                {
                    if (tag.Kind == HostKind.Component)
                        UpdateComponentHostLayout(host);
                    else if (tag.Kind == HostKind.Database)
                        UpdateDatabaseHostLayout(host);
                    else if (tag.Kind == HostKind.Workflow)
                        UpdateWorkflowHostLayout(host);
                }
            }

            if (currentDiagram != null)
                UpdateArrows(currentDiagram);
        }

        // ===========================
        // Visuals: Component
        // ===========================

        private FrameworkElement CreateComponentVisual(ComponentNode c)
        {
            TextBlock tb = new TextBlock
            {
                Text = c != null ? (c.Name ?? "") : "",
                FontSize = 30,
                FontWeight = FontWeights.Normal,
                TextAlignment = TextAlignment.Center,
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
                // MaxWidth задаётся в UpdateComponentHostLayout
                IsHitTestVisible = false
            };

            Border outer = new Border
            {
                Background = ComponentFill,
                BorderBrush = StrokeBrush,
                BorderThickness = new Thickness(3),
                CornerRadius = new CornerRadius(10)
            };

            Rectangle tabTop = new Rectangle
            {
                Fill = ComponentFill,
                Stroke = StrokeBrush,
                StrokeThickness = 3,
                IsHitTestVisible = false
            };

            Rectangle tabBottom = new Rectangle
            {
                Fill = ComponentFill,
                Stroke = StrokeBrush,
                StrokeThickness = 3,
                IsHitTestVisible = false
            };

            double width = c != null ? c.Width : 170;
            double height = c != null ? c.Height : 46;

            Canvas host = new Canvas
            {
                Width = width,
                Height = height,
                MinWidth = c != null ? c.MinWidth : 120,
                MinHeight = c != null ? c.MinHeight : 36,
                Background = Brushes.Transparent,
                Tag = new HostTag
                {
                    Kind = HostKind.Component,
                    Outer = outer,
                    TabTop = tabTop,
                    TabBottom = tabBottom,
                    Text = tb
                }
            };

            host.Children.Add(outer);
            host.Children.Add(tabTop);
            host.Children.Add(tabBottom);
            host.Children.Add(tb);

            UpdateComponentHostLayout(host);
            return host;
        }

        private void UpdateComponentHostLayout(Canvas host)
        {
            HostTag tag = host.Tag as HostTag;
            if (tag == null) return;

            double w = host.Width;
            double h = host.Height;

            if (tag.Outer != null)
            {
                tag.Outer.Width = w;
                tag.Outer.Height = h;
                Canvas.SetLeft(tag.Outer, 0);
                Canvas.SetTop(tag.Outer, 0);
            }

            double tabW = Clamp(w * 0.16, 26, 44);
            double tabH = Clamp(h * 0.18, 16, 30);
            double gap = Clamp(h * 0.08, 10, 22);

            double midY = h / 2.0;
            double topY = midY - gap / 2.0 - tabH;
            double bottomY = midY + gap / 2.0;

            topY = Clamp(topY, 8, Math.Max(8, h - tabH - 8));
            bottomY = Clamp(bottomY, 8, Math.Max(8, h - tabH - 8));

            if (tag.TabTop != null)
            {
                tag.TabTop.Width = tabW;
                tag.TabTop.Height = tabH;
                Canvas.SetLeft(tag.TabTop, -tabW / 2.0);
                Canvas.SetTop(tag.TabTop, topY);
            }

            if (tag.TabBottom != null)
            {
                tag.TabBottom.Width = tabW;
                tag.TabBottom.Height = tabH;
                Canvas.SetLeft(tag.TabBottom, -tabW / 2.0);
                Canvas.SetTop(tag.TabBottom, bottomY);
            }

            if (tag.Text != null)
            {
                // ФИкс: MaxWidth ограничен реальной шириной блока минус отступ под вкладки
                tag.Text.MaxWidth = Math.Max(40, w - 60);
                tag.Text.Measure(new Size(tag.Text.MaxWidth, double.PositiveInfinity));

                double tx = (w - tag.Text.DesiredSize.Width) / 2.0;
                double ty = (h - tag.Text.DesiredSize.Height) / 2.0;

                Canvas.SetLeft(tag.Text, Math.Max(10, tx));
                Canvas.SetTop(tag.Text, Math.Max(8, ty));
            }
        }

        // ===========================
        // Visuals: Database (cylinder)
        // ===========================

        private FrameworkElement CreateDatabaseVisual(double width, double height, double minW, double minH, string text)
        {
            // ИСПРАВЛЕНО: TextAlignment.Center, без Padding, MaxWidth задаётся в Layout
            TextBlock tb = new TextBlock
            {
                Text = text ?? "",
                FontSize = 26,
                FontWeight = FontWeights.Normal,
                TextAlignment = TextAlignment.Center,
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
                IsHitTestVisible = false
            };

            Path cylinder = new Path
            {
                Fill = Brushes.White,
                Stroke = StrokeBrush,
                StrokeThickness = 3,
                SnapsToDevicePixels = true,
                IsHitTestVisible = false
            };

            Ellipse topArc = new Ellipse
            {
                Stroke = StrokeBrush,
                StrokeThickness = 3,
                Fill = Brushes.Transparent,
                SnapsToDevicePixels = true,
                IsHitTestVisible = false
            };

            Canvas host = new Canvas
            {
                Width = width,
                Height = height,
                MinWidth = minW,
                MinHeight = minH,
                Background = Brushes.Transparent,
                Tag = new HostTag
                {
                    Kind = HostKind.Database,
                    Cylinder = cylinder,
                    TopArc = topArc,
                    Text = tb
                }
            };

            host.Children.Add(cylinder);
            host.Children.Add(topArc);
            host.Children.Add(tb);

            UpdateDatabaseHostLayout(host);
            return host;
        }

        private void UpdateDatabaseHostLayout(Canvas host)
        {
            HostTag tag = host.Tag as HostTag;
            if (tag == null) return;

            double w = host.Width;
            double h = host.Height;

            double capHeight = Math.Max(16, Math.Min(26, h * 0.26));
            double capRadiusY = capHeight / 2.0;

            if (tag.Cylinder != null)
            {
                PathFigure fig = new PathFigure();
                fig.StartPoint = new Point(0, capRadiusY);
                fig.IsClosed = true;
                fig.IsFilled = true;

                fig.Segments.Add(new ArcSegment(
                    new Point(w, capRadiusY),
                    new Size(w / 2.0, capRadiusY),
                    0, false, SweepDirection.Clockwise, true));

                fig.Segments.Add(new LineSegment(new Point(w, h - capRadiusY), true));

                fig.Segments.Add(new ArcSegment(
                    new Point(0, h - capRadiusY),
                    new Size(w / 2.0, capRadiusY),
                    0, false, SweepDirection.Clockwise, true));

                fig.Segments.Add(new LineSegment(new Point(0, capRadiusY), true));

                PathGeometry geom = new PathGeometry();
                geom.Figures.Add(fig);

                tag.Cylinder.Data = geom;
                Canvas.SetLeft(tag.Cylinder, 0);
                Canvas.SetTop(tag.Cylinder, 0);
            }

            if (tag.TopArc != null)
            {
                tag.TopArc.Width = w;
                tag.TopArc.Height = capHeight;
                Canvas.SetLeft(tag.TopArc, 0);
                Canvas.SetTop(tag.TopArc, 0);
            }

            if (tag.Text != null)
            {
                // ИСПРАВЛЕНО: MaxWidth = реальная ширина минус боковые поля
                tag.Text.MaxWidth = Math.Max(40, w - 24);
                tag.Text.Measure(new Size(tag.Text.MaxWidth, double.PositiveInfinity));

                double bodyTop = capRadiusY;
                double bodyHeight = Math.Max(10, h - capRadiusY);

                // ИСПРАВЛЕНО: горизонтальное центрирование
                double tx = (w - tag.Text.DesiredSize.Width) / 2.0;
                double ty = bodyTop + (bodyHeight - tag.Text.DesiredSize.Height) / 2.0;

                Canvas.SetLeft(tag.Text, Math.Max(4, tx));
                Canvas.SetTop(tag.Text, Math.Max(0, ty));
            }
        }

        // ===========================
        // Visuals: Workflow
        // ===========================

        private FrameworkElement CreateWorkflowVisual(WorkflowNode n)
        {
            if (n == null) return new Canvas { Width = 120, Height = 40 };

            if (n.Type == WorkflowNodeType.StartEnd)
                return CreateWorkflowStartEnd(n);
            if (n.Type == WorkflowNodeType.Process)
                return CreateWorkflowProcess(n);
            if (n.Type == WorkflowNodeType.Data)
                return CreateWorkflowDataTrapezoid(n);
            if (n.Type == WorkflowNodeType.Form)
                return CreateWorkflowFormOval(n);
            if (n.Type == WorkflowNodeType.Database)
                return CreateWorkflowDatabase(n);

            return CreateWorkflowProcess(n);
        }

        private FrameworkElement CreateWorkflowStartEnd(WorkflowNode n)
        {
            Ellipse ellipse = new Ellipse
            {
                Fill = Brushes.White,
                Stroke = StrokeBrush,
                StrokeThickness = 2.5,
                IsHitTestVisible = false
            };

            TextBlock tb = CreateWorkflowText(n.Text, n.Width);

            Canvas host = NewWorkflowHost(n);
            host.Tag = new HostTag
            {
                Kind = HostKind.Workflow,
                WorkflowType = n.Type,
                Shape1 = ellipse,
                Text = tb
            };

            host.Children.Add(ellipse);
            host.Children.Add(tb);

            UpdateWorkflowHostLayout(host);
            return host;
        }

        private FrameworkElement CreateWorkflowProcess(WorkflowNode n)
        {
            Rectangle rect = new Rectangle
            {
                Fill = Brushes.White,
                Stroke = StrokeBrush,
                StrokeThickness = 2.5,
                RadiusX = 0,
                RadiusY = 0,
                IsHitTestVisible = false
            };

            TextBlock tb = CreateWorkflowText(n.Text, n.Width);

            Canvas host = NewWorkflowHost(n);
            host.Tag = new HostTag
            {
                Kind = HostKind.Workflow,
                WorkflowType = n.Type,
                Shape1 = rect,
                Text = tb
            };

            host.Children.Add(rect);
            host.Children.Add(tb);

            UpdateWorkflowHostLayout(host);
            return host;
        }

        private FrameworkElement CreateWorkflowFormOval(WorkflowNode n)
        {
            Ellipse ellipse = new Ellipse
            {
                Fill = Brushes.White,
                Stroke = StrokeBrush,
                StrokeThickness = 2.5,
                IsHitTestVisible = false
            };

            TextBlock tb = CreateWorkflowText(n.Text, n.Width);

            Canvas host = NewWorkflowHost(n);
            host.Tag = new HostTag
            {
                Kind = HostKind.Workflow,
                WorkflowType = n.Type,
                Shape1 = ellipse,
                Text = tb
            };

            host.Children.Add(ellipse);
            host.Children.Add(tb);

            UpdateWorkflowHostLayout(host);
            return host;
        }

        private FrameworkElement CreateWorkflowDataTrapezoid(WorkflowNode n)
        {
            Polygon trap = new Polygon
            {
                Fill = Brushes.White,
                Stroke = StrokeBrush,
                StrokeThickness = 2.5,
                IsHitTestVisible = false
            };

            TextBlock tb = CreateWorkflowText(n.Text, n.Width);

            Canvas host = NewWorkflowHost(n);
            host.Tag = new HostTag
            {
                Kind = HostKind.Workflow,
                WorkflowType = n.Type,
                Shape1 = trap,
                Text = tb
            };

            host.Children.Add(trap);
            host.Children.Add(tb);

            UpdateWorkflowHostLayout(host);
            return host;
        }

        private FrameworkElement CreateWorkflowDatabase(WorkflowNode n)
        {
            // ИСПРАВЛЕНО: TextAlignment.Center, MaxWidth задаётся в Layout
            TextBlock tb = new TextBlock
            {
                Text = n.Text ?? "",
                FontSize = 18,
                FontWeight = FontWeights.Normal,
                TextAlignment = TextAlignment.Center,
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
                IsHitTestVisible = false
            };

            Path cylinder = new Path
            {
                Fill = Brushes.White,
                Stroke = StrokeBrush,
                StrokeThickness = 2.5,
                SnapsToDevicePixels = true,
                IsHitTestVisible = false
            };

            Ellipse topArc = new Ellipse
            {
                Stroke = StrokeBrush,
                StrokeThickness = 2.5,
                Fill = Brushes.Transparent,
                SnapsToDevicePixels = true,
                IsHitTestVisible = false
            };

            Canvas host = new Canvas
            {
                Width = n.Width,
                Height = n.Height,
                MinWidth = n.MinWidth,
                MinHeight = n.MinHeight,
                Background = Brushes.Transparent,
                Tag = new HostTag
                {
                    Kind = HostKind.Workflow,
                    WorkflowType = n.Type,
                    Cylinder = cylinder,
                    TopArc = topArc,
                    Text = tb
                }
            };

            host.Children.Add(cylinder);
            host.Children.Add(topArc);
            host.Children.Add(tb);

            // Workflow Database использует тот же UpdateDatabaseHostLayout
            UpdateDatabaseHostLayout(host);
            return host;
        }

        private Canvas NewWorkflowHost(WorkflowNode n)
        {
            return new Canvas
            {
                Width = n.Width,
                Height = n.Height,
                MinWidth = n.MinWidth,
                MinHeight = n.MinHeight,
                Background = Brushes.Transparent
            };
        }

        // ИСПРАВЛЕНО: принимаем реальную ширину блока, MaxWidth = width - отступы
        private TextBlock CreateWorkflowText(string text, double hostWidth)
        {
            return new TextBlock
            {
                Text = text ?? "",
                FontSize = 18,
                FontWeight = FontWeights.Normal,
                TextAlignment = TextAlignment.Center,
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = Math.Max(40, hostWidth - 20),
                IsHitTestVisible = false
            };
        }

        private void UpdateWorkflowHostLayout(Canvas host)
        {
            HostTag tag = host.Tag as HostTag;
            if (tag == null) return;

            double w = host.Width;
            double h = host.Height;

            // Shape layout
            if (tag.WorkflowType == WorkflowNodeType.Data)
            {
                Polygon trap = tag.Shape1 as Polygon;
                if (trap != null)
                {
                    double inset = Clamp(w * 0.12, 16, 40);
                    trap.Points = new PointCollection
                    {
                        new Point(inset, 0),
                        new Point(w,     0),
                        new Point(w - inset, h),
                        new Point(0, h)
                    };
                    Canvas.SetLeft(trap, 0);
                    Canvas.SetTop(trap, 0);
                }
            }
            else if (tag.Shape1 is Rectangle)
            {
                Rectangle r = (Rectangle)tag.Shape1;
                r.Width = w;
                r.Height = h;
                Canvas.SetLeft(r, 0);
                Canvas.SetTop(r, 0);
            }
            else if (tag.Shape1 is Ellipse)
            {
                Ellipse e = (Ellipse)tag.Shape1;
                e.Width = w;
                e.Height = h;
                Canvas.SetLeft(e, 0);
                Canvas.SetTop(e, 0);
            }

            // Text layout
            if (tag.Text != null)
            {
                // ИСПРАВЛЕНО: для трапеции учитываем inset с обеих сторон
                double insetForText = (tag.WorkflowType == WorkflowNodeType.Data)
                    ? Clamp(w * 0.12, 16, 40)
                    : 0;

                // ИСПРАВЛЕНО: MaxWidth пересчитывается по актуальной ширине хоста
                tag.Text.MaxWidth = Math.Max(40, w - 20 - insetForText);
                tag.Text.Measure(new Size(tag.Text.MaxWidth, double.PositiveInfinity));

                double tx = (w - tag.Text.DesiredSize.Width) / 2.0;
                double ty = (h - tag.Text.DesiredSize.Height) / 2.0;

                Canvas.SetLeft(tag.Text, Math.Max(6, tx));
                Canvas.SetTop(tag.Text, Math.Max(4, ty));
            }
        }

        // ===========================
        // HostTag
        // ===========================

        private enum HostKind
        {
            Component,
            Database,
            Workflow
        }

        private class HostTag
        {
            public HostKind Kind;

            public Border Outer;
            public Rectangle TabTop;
            public Rectangle TabBottom;

            public Path Cylinder;
            public Ellipse TopArc;

            public WorkflowNodeType WorkflowType;
            public Shape Shape1;

            public TextBlock Text;
        }

        private static double Clamp(double v, double min, double max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        // ===========================
        // Arrows
        // ===========================

        private void RenderArrows(ComponentDiagram diagram)
        {
            if (diagram == null || diagram.Links == null) return;

            foreach (var link in diagram.Links)
            {
                if (link == null) continue;
                if (!blockVisuals.ContainsKey(link.FromId) || !blockVisuals.ContainsKey(link.ToId))
                    continue;

                FrameworkElement fromEl = blockVisuals[link.FromId].visual;
                FrameworkElement toEl = blockVisuals[link.ToId].visual;

                Point fromCenter = GetCenter(fromEl);
                Point toCenter = GetCenter(toEl);
                Point intersection = GetIntersectionWithBlock(toEl, fromCenter, toCenter);

                DrawArrow(fromCenter, intersection);
            }
        }

        private void DrawArrow(Point start, Point end)
        {
            Line line = new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = StrokeBrush,
                StrokeThickness = 3,
                IsHitTestVisible = false
            };
            Panel.SetZIndex(line, 20);
            canvas.Children.Add(line);

            double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);

            Point p1 = new Point(
                end.X - ARROWHEADLENGTH * Math.Cos(angle - ARROWHEADANGLE),
                end.Y - ARROWHEADLENGTH * Math.Sin(angle - ARROWHEADANGLE));

            Point p2 = new Point(
                end.X - ARROWHEADLENGTH * Math.Cos(angle + ARROWHEADANGLE),
                end.Y - ARROWHEADLENGTH * Math.Sin(angle + ARROWHEADANGLE));

            Polygon poly = new Polygon
            {
                Points = new PointCollection { new Point(end.X, end.Y), p1, p2 },
                Fill = StrokeBrush,
                Stroke = StrokeBrush,
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
            Panel.SetZIndex(poly, 20);
            canvas.Children.Add(poly);
        }

        // ===========================
        // Geometry helpers
        // ===========================

        private Point GetCenter(FrameworkElement fe)
        {
            double left = Canvas.GetLeft(fe);
            double top = Canvas.GetTop(fe);

            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            return new Point(left + fe.Width / 2.0, top + fe.Height / 2.0);
        }

        private Point GetIntersectionWithBlock(FrameworkElement block, Point from, Point to)
        {
            double left = Canvas.GetLeft(block);
            double top = Canvas.GetTop(block);

            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            return GetIntersectionWithRect(left, top, block.Width, block.Height, from, to);
        }

        private Point GetIntersectionWithRect(double left, double top, double width, double height, Point from, Point to)
        {
            Rect rect = new Rect(left, top, width, height);

            double dx = to.X - from.X;
            double dy = to.Y - from.Y;

            List<Point> candidates = new List<Point>();
            double[] ts = new double[4];

            if (Math.Abs(dx) > 0.0001)
            {
                ts[0] = (rect.Left - from.X) / dx;
                ts[1] = (rect.Right - from.X) / dx;
            }
            else
            {
                ts[0] = double.NaN;
                ts[1] = double.NaN;
            }

            if (Math.Abs(dy) > 0.0001)
            {
                ts[2] = (rect.Top - from.Y) / dy;
                ts[3] = (rect.Bottom - from.Y) / dy;
            }
            else
            {
                ts[2] = double.NaN;
                ts[3] = double.NaN;
            }

            for (int i = 0; i < ts.Length; i++)
            {
                double t = ts[i];
                if (double.IsNaN(t)) continue;
                if (t < 0.01 || t > 1) continue;

                double ix = from.X + dx * t;
                double iy = from.Y + dy * t;

                if (rect.Contains(ix, iy))
                    candidates.Add(new Point(ix, iy));
            }

            if (candidates.Count == 0) return to;

            candidates.Sort((a, b) => Distance(a, to).CompareTo(Distance(b, to)));
            return candidates[0];
        }

        private double Distance(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // ===========================
        // Resize Adorner
        // ===========================

        private class ResizeAdorner : Adorner
        {
            private readonly VisualCollection visuals;
            private readonly Action<FrameworkElement> onChanged;

            private readonly Thumb topLeft, top, topRight, right, bottomRight, bottom, bottomLeft, left;

            private const double HandleSize = 8.0;

            public ResizeAdorner(UIElement adornedElement, Action<FrameworkElement> onChanged)
                : base(adornedElement)
            {
                this.onChanged = onChanged;
                visuals = new VisualCollection(this);

                topLeft = BuildHandle(Cursors.SizeNWSE);
                top = BuildHandle(Cursors.SizeNS);
                topRight = BuildHandle(Cursors.SizeNESW);
                right = BuildHandle(Cursors.SizeWE);
                bottomRight = BuildHandle(Cursors.SizeNWSE);
                bottom = BuildHandle(Cursors.SizeNS);
                bottomLeft = BuildHandle(Cursors.SizeNESW);
                left = BuildHandle(Cursors.SizeWE);

                topLeft.DragDelta += delegate (object s, DragDeltaEventArgs e) { HandleTopLeft(e); };
                top.DragDelta += delegate (object s, DragDeltaEventArgs e) { HandleTop(e); };
                topRight.DragDelta += delegate (object s, DragDeltaEventArgs e) { HandleTopRight(e); };
                right.DragDelta += delegate (object s, DragDeltaEventArgs e) { HandleRight(e); };
                bottomRight.DragDelta += delegate (object s, DragDeltaEventArgs e) { HandleBottomRight(e); };
                bottom.DragDelta += delegate (object s, DragDeltaEventArgs e) { HandleBottom(e); };
                bottomLeft.DragDelta += delegate (object s, DragDeltaEventArgs e) { HandleBottomLeft(e); };
                left.DragDelta += delegate (object s, DragDeltaEventArgs e) { HandleLeft(e); };

                visuals.Add(topLeft);
                visuals.Add(top);
                visuals.Add(topRight);
                visuals.Add(right);
                visuals.Add(bottomRight);
                visuals.Add(bottom);
                visuals.Add(bottomLeft);
                visuals.Add(left);

                IsHitTestVisible = true;
            }

            private Thumb BuildHandle(Cursor cursor)
            {
                return new Thumb
                {
                    Width = HandleSize,
                    Height = HandleSize,
                    Cursor = cursor,
                    Background = Brushes.White,
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1),
                    Opacity = 0.95
                };
            }

            protected override int VisualChildrenCount { get { return visuals.Count; } }
            protected override Visual GetVisualChild(int index) { return visuals[index]; }

            protected override Size ArrangeOverride(Size finalSize)
            {
                FrameworkElement fe = AdornedElement as FrameworkElement;
                if (fe == null) return finalSize;

                double w = fe.ActualWidth;
                double h = fe.ActualHeight;

                ArrangeThumb(topLeft, 0, 0);
                ArrangeThumb(top, w / 2, 0);
                ArrangeThumb(topRight, w, 0);
                ArrangeThumb(right, w, h / 2);
                ArrangeThumb(bottomRight, w, h);
                ArrangeThumb(bottom, w / 2, h);
                ArrangeThumb(bottomLeft, 0, h);
                ArrangeThumb(left, 0, h / 2);

                return finalSize;
            }

            private void ArrangeThumb(Thumb t, double x, double y)
            {
                t.Arrange(new Rect(x - HandleSize / 2, y - HandleSize / 2, HandleSize, HandleSize));
            }

            private void EnsureSize(FrameworkElement fe)
            {
                if (double.IsNaN(fe.Width)) fe.Width = fe.ActualWidth;
                if (double.IsNaN(fe.Height)) fe.Height = fe.ActualHeight;
            }

            private void NotifyChanged()
            {
                FrameworkElement fe = AdornedElement as FrameworkElement;
                if (fe != null && onChanged != null) onChanged(fe);
                InvalidateArrange();
            }

            private void HandleRight(DragDeltaEventArgs e)
            {
                FrameworkElement fe = AdornedElement as FrameworkElement;
                if (fe == null) return;
                EnsureSize(fe);
                fe.Width = Math.Max(fe.MinWidth, fe.Width + e.HorizontalChange);
                NotifyChanged();
            }

            private void HandleBottom(DragDeltaEventArgs e)
            {
                FrameworkElement fe = AdornedElement as FrameworkElement;
                if (fe == null) return;
                EnsureSize(fe);
                fe.Height = Math.Max(fe.MinHeight, fe.Height + e.VerticalChange);
                NotifyChanged();
            }

            private void HandleBottomRight(DragDeltaEventArgs e)
            {
                HandleRight(e);
                HandleBottom(e);
            }

            private void HandleLeft(DragDeltaEventArgs e)
            {
                FrameworkElement fe = AdornedElement as FrameworkElement;
                if (fe == null) return;
                EnsureSize(fe);

                double oldWidth = fe.Width;
                double newWidth = Math.Max(fe.MinWidth, fe.Width - e.HorizontalChange);

                double leftOld = Canvas.GetLeft(fe);
                if (double.IsNaN(leftOld)) leftOld = 0;

                fe.Width = newWidth;
                Canvas.SetLeft(fe, leftOld - (newWidth - oldWidth));
                NotifyChanged();
            }

            private void HandleTop(DragDeltaEventArgs e)
            {
                FrameworkElement fe = AdornedElement as FrameworkElement;
                if (fe == null) return;
                EnsureSize(fe);

                double oldHeight = fe.Height;
                double newHeight = Math.Max(fe.MinHeight, fe.Height - e.VerticalChange);

                double topOld = Canvas.GetTop(fe);
                if (double.IsNaN(topOld)) topOld = 0;

                fe.Height = newHeight;
                Canvas.SetTop(fe, topOld - (newHeight - oldHeight));
                NotifyChanged();
            }

            private void HandleTopLeft(DragDeltaEventArgs e)
            {
                HandleLeft(e);
                HandleTop(e);
            }

            private void HandleTopRight(DragDeltaEventArgs e)
            {
                HandleRight(e);
                HandleTop(e);
            }

            private void HandleBottomLeft(DragDeltaEventArgs e)
            {
                HandleLeft(e);
                HandleBottom(e);
            }
        }
    }
}
