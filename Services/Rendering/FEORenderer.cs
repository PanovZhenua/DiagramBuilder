using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DiagramBuilder.Models;

// Нужно: один публичный метод — SetDiagram(FEODiagram) и Render(), всё остальное — через модель
namespace DiagramBuilder.Services.Rendering
{
    public class FEORenderer
    {
        private readonly Canvas canvas;
        private FEODiagram diagram;
        // Для drag'n'drop — тэги!
        public Action<Border> BlockVisualCallback { get; set; }
        public Action<TextBlock> LabelVisualCallback { get; set; }

        private const double ORTHO_OFFSET = 24;

        public FEORenderer(Canvas canvas)
        {
            this.canvas = canvas;
        }

        public void SetDiagram(FEODiagram diagram)
        {
            this.diagram = diagram;
        }

        public void Render()
        {
            if (diagram == null) return;
            canvas.Children.Clear();

            // 1. Блоки
            foreach (var comp in diagram.Components)
            {
                var border = new Border
                {
                    Width = comp.Width,
                    Height = comp.Height,
                    CornerRadius = new CornerRadius(8),
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(2),
                    Background = Brushes.LightBlue,
                    Tag = comp
                };

                var textBlock = new TextBlock
                {
                    Text = comp.Name,
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(5)
                };
                border.Child = textBlock;

                Canvas.SetLeft(border, comp.X);
                Canvas.SetTop(border, comp.Y);
                canvas.Children.Add(border);

                // Пробросим наружу для назначения drag/drop
                BlockVisualCallback?.Invoke(border);
            }

            // 2. Стрелки
            foreach (var arrow in diagram.Arrows)
            {
                DrawArrow(arrow);
            }
        }

        // --- Ортогональная стрелка от блока к блоку ---
        private void DrawArrow(FEOArrow arrow)
        {
            var from = arrow.From;
            var to = arrow.To;

            if (from == null || to == null) return;

            Point start = GetComponentSidePoint(from, arrow.SideFrom);
            Point end = GetComponentSidePoint(to, arrow.SideTo);

            Vector vFrom = GetDirectionOffset(arrow.SideFrom);
            Vector vTo = GetDirectionOffset(arrow.SideTo);

            Point p1 = new Point(start.X + vFrom.X * ORTHO_OFFSET, start.Y + vFrom.Y * ORTHO_OFFSET);
            Point p4 = new Point(end.X + vTo.X * ORTHO_OFFSET, end.Y + vTo.Y * ORTHO_OFFSET);

            List<Point> pts = new List<Point> { start, p1 };
            // Классическая ортолиния — можно добавить огибание!
            if (arrow.SideFrom == "left" || arrow.SideFrom == "right")
                pts.Add(new Point(p4.X, p1.Y));
            else
                pts.Add(new Point(p1.X, p4.Y));
            pts.Add(p4);
            pts.Add(end);

            var polyline = new Polyline
            {
                Points = new PointCollection(pts),
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            canvas.Children.Add(polyline);

            var arrowHead = CreateArrowHead(end, pts[pts.Count - 2]);
            canvas.Children.Add(arrowHead);

            // Подпись
            if (!string.IsNullOrEmpty(arrow.Label))
            {
                var lp = pts[pts.Count / 2];
                var labelBlock = new TextBlock
                {
                    Text = arrow.Label,
                    FontSize = 10,
                    Background = Brushes.White,
                    Padding = new Thickness(2),
                    Tag = arrow // для drag/drop подписи
                };
                Canvas.SetLeft(labelBlock, lp.X + 2);
                Canvas.SetTop(labelBlock, lp.Y - 10);
                canvas.Children.Add(labelBlock);
                LabelVisualCallback?.Invoke(labelBlock);
            }
        }

        // --- Позиция выхода на стороне блока ---
        private Point GetComponentSidePoint(FEOComponent comp, string side)
        {
            side = (side ?? "right").ToLower();
            if (side == "left")
                return new Point(comp.X, comp.Y + comp.Height / 2);
            if (side == "right")
                return new Point(comp.X + comp.Width, comp.Y + comp.Height / 2);
            if (side == "top")
                return new Point(comp.X + comp.Width / 2, comp.Y);
            // "bottom"
            return new Point(comp.X + comp.Width / 2, comp.Y + comp.Height);
        }

        private Vector GetDirectionOffset(string side)
        {
            switch ((side ?? "right").ToLower())
            {
                case "left": return new Vector(-1, 0);
                case "right": return new Vector(1, 0);
                case "top": return new Vector(0, -1);
                case "bottom": return new Vector(0, 1);
                default: return new Vector(1, 0);
            }
        }

        private Polygon CreateArrowHead(Point tip, Point lineStart)
        {
            double angle = Math.Atan2(tip.Y - lineStart.Y, tip.X - lineStart.X);
            double arrowLength = 10;
            double arrowAngle = Math.PI / 6;

            Point p1 = new Point(
                tip.X - arrowLength * Math.Cos(angle - arrowAngle),
                tip.Y - arrowLength * Math.Sin(angle - arrowAngle));
            Point p2 = new Point(
                tip.X - arrowLength * Math.Cos(angle + arrowAngle),
                tip.Y - arrowLength * Math.Sin(angle + arrowAngle));
            var arrowHead = new Polygon
            {
                Points = new PointCollection { tip, p1, p2 },
                Fill = Brushes.Black
            };
            return arrowHead;
        }
    }
}
