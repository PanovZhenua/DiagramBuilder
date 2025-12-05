using DiagramBuilder.Models.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DiagramBuilder.Services.Rendering
{
    public class DFDRenderer
    {
        private readonly Canvas canvas;
        private readonly Dictionary<string, FrameworkElement> blockVisuals = new Dictionary<string, FrameworkElement>();
        private readonly List<TextBlock> arrowLabels = new List<TextBlock>();

        private const double ARROW_SPACING = 18.0;
        private const double ARROW_HEAD_LENGTH = 12.0;
        private const double ARROW_HEAD_ANGLE = 0.45;

        public DFDRenderer(Canvas canvas)
        {
            this.canvas = canvas;
        }

        public void Render(DFDDiagram diagram)
        {
            canvas.Children.Clear();
            blockVisuals.Clear();
            arrowLabels.Clear();

            foreach (var proc in diagram.Processes)
            {
                var visual = DFDProcess.CreateVisual(proc);
                Canvas.SetLeft(visual, proc.X);
                Canvas.SetTop(visual, proc.Y);
                Panel.SetZIndex(visual, 30);
                canvas.Children.Add(visual);
                blockVisuals[proc.Id] = visual;
            }

            foreach (var entity in diagram.Entities)
            {
                var visual = DFDEntity.CreateVisual(entity);
                Canvas.SetLeft(visual, entity.X);
                Canvas.SetTop(visual, entity.Y);
                Panel.SetZIndex(visual, 25);
                canvas.Children.Add(visual);
                blockVisuals[entity.Id] = visual;
            }

            foreach (var store in diagram.Stores)
            {
                var visual = DFDStore.CreateVisual(store);
                Canvas.SetLeft(visual, store.X);
                Canvas.SetTop(visual, store.Y);
                Panel.SetZIndex(visual, 22);
                canvas.Children.Add(visual);
                blockVisuals[store.Id] = visual;
            }

            RenderArrows(diagram);
        }

        public void RenderArrows(DFDDiagram diagram)
        {
            var toRemove = canvas.Children.OfType<Line>().ToList();
            var pRemove = canvas.Children.OfType<Polygon>().ToList();
            foreach (var l in toRemove) canvas.Children.Remove(l);
            foreach (var p in pRemove) canvas.Children.Remove(p);
            foreach (var lbl in arrowLabels) canvas.Children.Remove(lbl);
            arrowLabels.Clear();

            var centers = new Dictionary<string, Point>();
            foreach (var kv in blockVisuals)
            {
                var fe = kv.Value;
                if (fe == null) continue;
                double left = Canvas.GetLeft(fe);
                double top = Canvas.GetTop(fe);
                centers[kv.Key] = new Point(left + fe.Width / 2, top + fe.Height / 2);
            }

            // Группируем стрелки по направленной паре (A→B это отдельно от B→A)
            var arrowsByDirection = new Dictionary<string, List<DFDArrow>>();

            foreach (var arrow in diagram.Arrows)
            {
                string directionKey = $"{arrow.FromId}→{arrow.ToId}";
                if (!arrowsByDirection.ContainsKey(directionKey))
                    arrowsByDirection[directionKey] = new List<DFDArrow>();
                arrowsByDirection[directionKey].Add(arrow);
            }

            // Рисуем каждую группу стрелок одного направления
            foreach (var directionGroup in arrowsByDirection)
            {
                var arrowsInDirection = directionGroup.Value;
                int totalInDirection = arrowsInDirection.Count;

                var firstArrow = arrowsInDirection[0];

                // Проверяем наличие ОБРАТНОГО направления
                string reverseDirection = $"{firstArrow.ToId}→{firstArrow.FromId}";
                bool hasReverseArrows = arrowsByDirection.ContainsKey(reverseDirection);

                for (int i = 0; i < totalInDirection; i++)
                {
                    var arrow = arrowsInDirection[i];

                    if (!centers.ContainsKey(arrow.FromId) || !centers.ContainsKey(arrow.ToId))
                        continue;

                    var fromCenter = centers[arrow.FromId];
                    var toCenter = centers[arrow.ToId];

                    // Рассчитываем смещение
                    // Передаём информацию: есть ли обратные стрелки И количество стрелок в этом направлении
                    Point offset = CalculateDirectionOffset(
                        fromCenter,
                        toCenter,
                        i,
                        totalInDirection,
                        hasReverseArrows
                    );

                    Point shiftedFrom = new Point(fromCenter.X + offset.X, fromCenter.Y + offset.Y);
                    Point shiftedTo = new Point(toCenter.X + offset.X, toCenter.Y + offset.Y);

                    var toBlock = blockVisuals[arrow.ToId];
                    var intersection = GetIntersectionWithBlock(toBlock, shiftedFrom, shiftedTo);

                    // Рисуем линию
                    var line = new Line
                    {
                        X1 = shiftedFrom.X,
                        Y1 = shiftedFrom.Y,
                        X2 = intersection.X,
                        Y2 = intersection.Y,
                        Stroke = Brushes.Black,
                        StrokeThickness = 2
                    };
                    canvas.Children.Add(line);
                    Panel.SetZIndex(line, 20);

                    // Рисуем стрелку (треугольник)
                    double angle = Math.Atan2(intersection.Y - shiftedFrom.Y, intersection.X - shiftedFrom.X);
                    Point p1 = new Point(
                        intersection.X - ARROW_HEAD_LENGTH * Math.Cos(angle - ARROW_HEAD_ANGLE),
                        intersection.Y - ARROW_HEAD_LENGTH * Math.Sin(angle - ARROW_HEAD_ANGLE));
                    Point p2 = new Point(
                        intersection.X - ARROW_HEAD_LENGTH * Math.Cos(angle + ARROW_HEAD_ANGLE),
                        intersection.Y - ARROW_HEAD_LENGTH * Math.Sin(angle + ARROW_HEAD_ANGLE));

                    var poly = new Polygon
                    {
                        Points = new PointCollection { intersection, p1, p2 },
                        Fill = Brushes.Black,
                        Stroke = Brushes.Black,
                        StrokeThickness = 0.5
                    };
                    canvas.Children.Add(poly);
                    Panel.SetZIndex(poly, 20);

                    // Подпись
                    if (!string.IsNullOrWhiteSpace(arrow.Label))
                    {
                        var tb = new TextBlock
                        {
                            Text = arrow.Label,
                            FontSize = 10,
                            FontWeight = FontWeights.Normal,
                            Foreground = Brushes.Black,
                            Background = Brushes.White,
                            Padding = new Thickness(4, 2, 4, 2),
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 120
                        };

                        double midX = (shiftedFrom.X + intersection.X) / 2;
                        double midY = (shiftedFrom.Y + intersection.Y) / 2;

                        Canvas.SetLeft(tb, midX - 50);
                        Canvas.SetTop(tb, midY - 12);
                        Panel.SetZIndex(tb, 90);
                        canvas.Children.Add(tb);
                        arrowLabels.Add(tb);
                    }
                }
            }
        }

        private string GetPairKey(string id1, string id2)
        {
            var ids = new[] { id1, id2 };
            Array.Sort(ids);
            return $"{ids[0]}|{ids[1]}";
        }

        /// <summary>
        /// Рассчитывает смещение для стрелок с учётом наличия обратных стрелок
        /// </summary>
        private Point CalculateDirectionOffset(Point from, Point to, int index, int totalCount, bool hasReverseArrows)
        {
            // Если только одна стрелка И нет обратной - смещение = 0 (прямая линия)
            if (totalCount == 1 && !hasReverseArrows)
                return new Point(0, 0);

            double dx = to.X - from.X;
            double dy = to.Y - from.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance < 0.01)
                return new Point(0, 0);

            // Нормализуем вектор направления
            dx /= distance;
            dy /= distance;

            // Вычисляем нормаль (перпендикулярно направлению)
            double nx = -dy;
            double ny = dx;

            double offset = 0;

            // ГЛАВНАЯ ЛОГИКА:
            // Если есть обратные стрелки ИЛИ несколько стрелок в этом направлении
            // -> разнесить симметрично на 9px от центра

            if (hasReverseArrows || totalCount > 1)
            {
                // Разнесение с шагом 9px
                if (totalCount == 1)
                {
                    // Одна стрелка, но есть обратная -> смещение +9
                    offset = 9.0;
                }
                else if (totalCount == 2)
                {
                    // Две стрелки в одном направлении
                    offset = (index == 0) ? -9.0 : 9.0;
                }
                else if (totalCount == 3)
                {
                    // Три стрелки в одном направлении
                    if (index == 0) offset = -9.0;
                    else if (index == 1) offset = 0.0;
                    else offset = 9.0;
                }
                else
                {
                    // 4+ стрелок: симметрично от центра с шагом 9px
                    offset = (index - (totalCount - 1) / 2.0) * 9.0;
                }
            }
            else
            {
                // Нет обратных стрелок и только одна в этом направлении
                offset = 0.0;
            }

            return new Point(nx * offset, ny * offset);
        }

        /// <summary>
        /// Получает точку пересечения луча с границей блока
        /// </summary>
        private Point GetIntersectionWithBlock(FrameworkElement block, Point from, Point to)
        {
            double left = Canvas.GetLeft(block);
            double top = Canvas.GetTop(block);
            double width = block.Width;
            double height = block.Height;

            if (block is Border border && border.CornerRadius.TopLeft > 0.1)
            {
                return GetIntersectionWithEllipse(left, top, width, height, from, to);
            }
            else
            {
                return GetIntersectionWithRect(left, top, width, height, from, to);
            }
        }

        private Point GetIntersectionWithEllipse(double left, double top, double width, double height, Point from, Point to)
        {
            double cx = left + width / 2;
            double cy = top + height / 2;
            double rx = width / 2;
            double ry = height / 2;

            double dx = to.X - from.X;
            double dy = to.Y - from.Y;

            double a = (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry);
            double b = 2 * ((from.X - cx) * dx) / (rx * rx) + 2 * ((from.Y - cy) * dy) / (ry * ry);
            double c = ((from.X - cx) * (from.X - cx)) / (rx * rx) +
                       ((from.Y - cy) * (from.Y - cy)) / (ry * ry) - 1;

            double discriminant = b * b - 4 * a * c;
            if (discriminant < 0)
                return to;

            double t1 = (-b + Math.Sqrt(discriminant)) / (2 * a);
            double t2 = (-b - Math.Sqrt(discriminant)) / (2 * a);
            double t = Math.Abs(t1) < Math.Abs(t2) ? t1 : t2;

            if (t < 0.01)
                t = Math.Max(t1, t2);

            return new Point(from.X + dx * t, from.Y + dy * t);
        }

        private Point GetIntersectionWithRect(double left, double top, double width, double height, Point from, Point to)
        {
            Rect rect = new Rect(left, top, width, height);
            double dx = to.X - from.X;
            double dy = to.Y - from.Y;

            var candidates = new List<Point>();
            double[] ts = new double[4];

            if (Math.Abs(dx) > 0.01)
            {
                ts[0] = (rect.Left - from.X) / dx;
                ts[1] = (rect.Right - from.X) / dx;
            }

            if (Math.Abs(dy) > 0.01)
            {
                ts[2] = (rect.Top - from.Y) / dy;
                ts[3] = (rect.Bottom - from.Y) / dy;
            }

            foreach (double t in ts)
            {
                if (t > 0.01 && t <= 1)
                {
                    double ix = from.X + dx * t;
                    double iy = from.Y + dy * t;
                    if (rect.Contains(ix, iy))
                        candidates.Add(new Point(ix, iy));
                }
            }

            if (candidates.Count > 0)
                return candidates.OrderBy(pt => Distance(pt, to)).First();

            return to;
        }

        private double Distance(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public Dictionary<string, FrameworkElement> GetBlockVisuals()
        {
            var result = new Dictionary<string, FrameworkElement>();
            foreach (var kv in blockVisuals)
                result[kv.Key] = kv.Value;
            return result;
        }

        public List<TextBlock> GetArrowLabels()
        {
            return arrowLabels;
        }
    }
}
