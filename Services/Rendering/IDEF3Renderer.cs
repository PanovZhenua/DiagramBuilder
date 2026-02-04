// =================== Services/Rendering/IDEF3Renderer.cs ===================
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DiagramBuilder.Models;
using DiagramBuilder.Services.Layout;
using DiagramBuilder.Services.Core;

namespace DiagramBuilder.Services.Rendering
{
    public class IDEF3Renderer
    {
        private readonly Canvas canvas;
        private readonly Dictionary<string, DiagramBlock> blocks;
        private readonly Dictionary<string, JunctionVisual> junctions = new Dictionary<string, JunctionVisual>();
        private readonly List<IDEF3Connection> connections = new List<IDEF3Connection>();
        private readonly IDEF3LayoutEngine layoutEngine;
        private DiagramStyle style;

        // ✅ Добавляем поддержку магнита
        private bool snapEnabled = false;

        public IDEF3Renderer(Canvas canvas, Dictionary<string, DiagramBlock> blocks, DiagramStyle style = null)
        {
            this.canvas = canvas;
            this.blocks = blocks;
            this.layoutEngine = new IDEF3LayoutEngine();
            this.style = style ?? DiagramStyle.GetStyle(DiagramStyleType.ClassicBlackWhite);
        }

        // ✅ Метод для управления магнитом
        public void SetSnapEnabled(bool enabled)
        {
            snapEnabled = enabled;
        }

        public void SetStyle(DiagramStyle style)
        {
            this.style = style ?? DiagramStyle.GetStyle(DiagramStyleType.ClassicBlackWhite);
        }

        public void RenderIDEF3(List<IDEF3UOW> uows, List<IDEF3Junction> junctionData, List<IDEF3Link> links)
        {
            canvas.Children.Clear();
            blocks.Clear();
            junctions.Clear();
            connections.Clear();

            var layout = layoutEngine.CalculateLayout(uows, junctionData, links);

            // Рисуем UOW (блоки)
            foreach (var uow in uows)
            {
                if (!layout.BlockPositions.ContainsKey(uow.Id))
                    continue;

                var pos = layout.BlockPositions[uow.Id];
                var (height, lines) = TextFormatterHelper.CalculateTextSize(uow.Name, 180 - 16, 80);

                var border = new Border
                {
                    Width = 180,
                    Height = height,
                    Background = style.BlockFill,
                    BorderBrush = style.BlockBorder,
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(8),
                    Child = TextFormatterHelper.CreateAutoWrapTextBlock($"{uow.Name}\n{uow.Id}", style, 164)
                };

                Canvas.SetLeft(border, pos.X);
                Canvas.SetTop(border, pos.Y);
                Panel.SetZIndex(border, 10);
                canvas.Children.Add(border);

                blocks[uow.Id] = new DiagramBlock
                {
                    Code = uow.Id,
                    Text = uow.Name,
                    Visual = border,
                    Lines = lines
                };
            }

            // Рисуем Junction (узлы)
            foreach (var junc in junctionData)
            {
                if (!layout.JunctionPositions.ContainsKey(junc.Id))
                    continue;

                var pos = layout.JunctionPositions[junc.Id];

                var ellipse = new Ellipse
                {
                    Width = 40,
                    Height = 40,
                    Fill = style.JunctionFill,
                    Stroke = style.JunctionBorder,
                    StrokeThickness = 2
                };

                Canvas.SetLeft(ellipse, pos.X - 20);
                Canvas.SetTop(ellipse, pos.Y - 20);
                Panel.SetZIndex(ellipse, 20);
                canvas.Children.Add(ellipse);

                var label = new TextBlock
                {
                    Text = junc.Type.ToUpper(),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = style.Text,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                Canvas.SetLeft(label, pos.X - 13);
                Canvas.SetTop(label, pos.Y - 11);
                Panel.SetZIndex(label, 21);
                canvas.Children.Add(label);

                junctions[junc.Id] = new JunctionVisual
                {
                    Id = junc.Id,
                    Visual = ellipse,
                    Label = label,
                    Type = junc.Type,
                    X = pos.X,
                    Y = pos.Y
                };
            }

            RenderConnections(links, layout);
        }

        private void RenderConnections(List<IDEF3Link> links, IDEF3LayoutEngine.LayoutResult layout)
        {
            foreach (var link in links)
            {
                var fromPos = GetNodeCenter(link.From, layout);
                var toPos = GetNodeCenter(link.To, layout);

                if (!fromPos.HasValue || !toPos.HasValue)
                    continue;

                var line = new Line
                {
                    X1 = fromPos.Value.X,
                    Y1 = fromPos.Value.Y,
                    X2 = toPos.Value.X,
                    Y2 = toPos.Value.Y,
                    Stroke = style.Line,
                    StrokeThickness = 2
                };
                Panel.SetZIndex(line, 5);
                canvas.Children.Add(line);

                var arrowHead = CreateArrowHead(new Point(line.X2, line.Y2), new Point(line.X1, line.Y1));
                Panel.SetZIndex(arrowHead, 6);
                canvas.Children.Add(arrowHead);

                connections.Add(new IDEF3Connection
                {
                    Line = line,
                    ArrowHead = arrowHead,
                    FromId = link.From,
                    ToId = link.To
                });

                if (!string.IsNullOrEmpty(link.Label))
                {
                    var tb = TextFormatterHelper.CreateAutoWrapTextBlock(link.Label, style, 150, 11, TextAlignment.Center);
                    double mx = (line.X1 + line.X2) / 2, my = (line.Y1 + line.Y2) / 2;
                    Canvas.SetLeft(tb, mx - 40);
                    Canvas.SetTop(tb, my - 14);
                    Panel.SetZIndex(tb, 7);
                    canvas.Children.Add(tb);
                }
            }
        }

        private Point? GetNodeCenter(string nodeId, IDEF3LayoutEngine.LayoutResult layout)
        {
            if (layout.BlockPositions.ContainsKey(nodeId))
            {
                var pos = layout.BlockPositions[nodeId];
                return new Point(pos.X + 90, pos.Y + 40);
            }
            if (layout.JunctionPositions.ContainsKey(nodeId))
            {
                var pos = layout.JunctionPositions[nodeId];
                return new Point(pos.X, pos.Y);
            }
            return null;
        }

        public void UpdateConnections()
        {
            foreach (var conn in connections)
            {
                Point start = GetConnectionPoint(conn.FromId);
                Point end = GetConnectionPoint(conn.ToId);

                conn.Line.X1 = start.X;
                conn.Line.Y1 = start.Y;
                conn.Line.X2 = end.X;
                conn.Line.Y2 = end.Y;
                conn.Line.Stroke = style.Line;

                UpdateArrowHead(conn.ArrowHead, end, start);
            }
        }

        private Point GetConnectionPoint(string id)
        {
            if (blocks.ContainsKey(id) && blocks[id]?.Visual != null)
            {
                var visual = blocks[id].Visual;
                return new Point(Canvas.GetLeft(visual) + visual.Width / 2, Canvas.GetTop(visual) + visual.Height / 2);
            }
            else if (junctions.ContainsKey(id))
            {
                var j = junctions[id];
                return new Point(j.X, j.Y);
            }
            return new Point(0, 0);
        }

        private Polygon CreateArrowHead(Point tip, Point from)
        {
            double angle = Math.Atan2(tip.Y - from.Y, tip.X - from.X);
            double arrowSize = 8;

            Point p1 = new Point(
                tip.X - arrowSize * Math.Cos(angle - Math.PI / 6),
                tip.Y - arrowSize * Math.Sin(angle - Math.PI / 6));

            Point p2 = new Point(
                tip.X - arrowSize * Math.Cos(angle + Math.PI / 6),
                tip.Y - arrowSize * Math.Sin(angle + Math.PI / 6));

            return new Polygon
            {
                Fill = style.Line,
                Points = new PointCollection { tip, p1, p2 }
            };
        }

        private void UpdateArrowHead(Polygon arrowHead, Point tip, Point from)
        {
            double angle = Math.Atan2(tip.Y - from.Y, tip.X - from.X);
            double arrowSize = 8;

            Point p1 = new Point(
                tip.X - arrowSize * Math.Cos(angle - Math.PI / 6),
                tip.Y - arrowSize * Math.Sin(angle - Math.PI / 6));

            Point p2 = new Point(
                tip.X - arrowSize * Math.Cos(angle + Math.PI / 6),
                tip.Y - arrowSize * Math.Sin(angle + Math.PI / 6));

            arrowHead.Points = new PointCollection { tip, p1, p2 };
            arrowHead.Fill = style.Line;
        }

        // ✅ ИСПРАВЛЕННЫЙ AttachJunctionDragEvents - магнит убран из drag
        public void AttachJunctionDragEvents(string junctionId, Action<string> onMoved)
        {
            if (!junctions.ContainsKey(junctionId))
                return;

            var junction = junctions[junctionId];
            var ellipse = junction.Visual;
            var label = junction.Label;

            bool isDragging = false;
            Point dragStart = new Point();

            ellipse.MouseLeftButtonDown += (s, e) =>
            {
                isDragging = true;
                dragStart = e.GetPosition(canvas);
                ellipse.CaptureMouse();
                ((MainWindow)Application.Current.MainWindow).SelectedElement = ellipse;
                e.Handled = true;
            };

            ellipse.MouseMove += (s, e) =>
            {
                if (!isDragging) return;

                Point currentPos = e.GetPosition(canvas);
                Vector offset = currentPos - dragStart;

                double newX = junction.X + offset.X;
                double newY = junction.Y + offset.Y;

                // ✅ УБРАЛИ МАГНИТ ИЗ DRAG - магнит будет работать только при движении стрелками
                junction.X = newX;
                junction.Y = newY;

                Canvas.SetLeft(ellipse, junction.X - ellipse.Width / 2);
                Canvas.SetTop(ellipse, junction.Y - ellipse.Height / 2);

                if (label != null)
                {
                    Canvas.SetLeft(label, junction.X - 13);
                    Canvas.SetTop(label, junction.Y - 11);
                }

                dragStart = currentPos;
                onMoved?.Invoke(junctionId);
                e.Handled = true;
            };

            ellipse.MouseLeftButtonUp += (s, e) =>
            {
                isDragging = false;
                ellipse.ReleaseMouseCapture();
                e.Handled = true;
            };

            ellipse.MouseLeave += (s, e) =>
            {
                if (isDragging)
                {
                    isDragging = false;
                    ellipse.ReleaseMouseCapture();
                }
            };
        }

        public bool SetJunctionPositionFromEllipse(Ellipse ellipse, double left, double top)
        {
            foreach (var kvp in junctions)
            {
                if (kvp.Value.Visual == ellipse)
                {
                    kvp.Value.X = left + 20; // X центр
                    kvp.Value.Y = top + 20; // Y центр

                    if (kvp.Value.Label != null)
                    {
                        Canvas.SetLeft(kvp.Value.Label, kvp.Value.X - 13);
                        Canvas.SetTop(kvp.Value.Label, kvp.Value.Y - 11);
                    }
                    return true;
                }
            }
            return false;
        }

        private class JunctionVisual
        {
            public string Id { get; set; }
            public Ellipse Visual { get; set; }
            public TextBlock Label { get; set; }
            public string Type { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
        }

        private class IDEF3Connection
        {
            public Line Line { get; set; }
            public Polygon ArrowHead { get; set; }
            public string FromId { get; set; }
            public string ToId { get; set; }
        }
    }
}
