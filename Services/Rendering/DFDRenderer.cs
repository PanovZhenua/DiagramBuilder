// =================== Services/Rendering/DFDRenderer.cs ===================
using DiagramBuilder.Models;
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
        private readonly Dictionary<string, (FrameworkElement visual, string type)> blockVisuals =
            new Dictionary<string, (FrameworkElement, string)>();
        private readonly List<TextBlock> arrowLabels = new List<TextBlock>();

        private const double ARROW_SPACING = 18.0;
        private const double ARROWHEAD_LENGTH = 12.0;
        private const double ARROWHEAD_ANGLE = 0.45;

        public DFDRenderer(Canvas canvas)
        {
            this.canvas = canvas;
        }

        // ✅ УНИВЕРСАЛЬНЫЙ МЕТОД - работает для DFD и DocumentFlow
        public void Render(DFDDiagram diagram)
        {
            canvas.Children.Clear();
            blockVisuals.Clear();
            arrowLabels.Clear();

            // Рендерим все типы блоков
            foreach (var proc in diagram.Processes)
            {
                var visual = CreateProcessVisual(proc.Name);
                Canvas.SetLeft(visual, proc.X);
                Canvas.SetTop(visual, proc.Y);
                Panel.SetZIndex(visual, 30);
                canvas.Children.Add(visual);
                blockVisuals[proc.Id] = (visual, "PROCESS");
            }

            foreach (var entity in diagram.Entities)
            {
                var visual = CreateEntityVisual(entity.Name);
                Canvas.SetLeft(visual, entity.X);
                Canvas.SetTop(visual, entity.Y);
                Panel.SetZIndex(visual, 25);
                canvas.Children.Add(visual);
                blockVisuals[entity.Id] = (visual, "ENTITY");
            }

            foreach (var store in diagram.Stores)
            {
                var visual = CreateStoreVisual(store.Name);
                Canvas.SetLeft(visual, store.X);
                Canvas.SetTop(visual, store.Y);
                Panel.SetZIndex(visual, 22);
                canvas.Children.Add(visual);
                blockVisuals[store.Id] = (visual, "STORE");
            }

            if (diagram.DocFlows != null)
            {
                foreach (var docFlow in diagram.DocFlows)
                {
                    var visual = CreateDocFlowVisual(docFlow.Name);
                    Canvas.SetLeft(visual, docFlow.X);
                    Canvas.SetTop(visual, docFlow.Y);
                    Panel.SetZIndex(visual, 24);
                    canvas.Children.Add(visual);
                    blockVisuals[docFlow.Id] = (visual, "DOCFLOW");
                }
            }

            RenderArrows(diagram);
        }

        // ====== СОЗДАНИЕ ВИЗУАЛЬНЫХ ЭЛЕМЕНТОВ ======

        // ✅ PROCESS - УВЕЛИЧЕННЫЕ ОТСТУПЫ
        private FrameworkElement CreateProcessVisual(string text)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = FontWeights.Normal,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.Black,
                Padding = new Thickness(12, 8, 12, 8),  // ✅ БЫЛО: (4, 2, 4, 2)
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 160,  // ✅ БЫЛО: 128
                IsHitTestVisible = false
            };

            textBlock.Measure(new Size(textBlock.MaxWidth, double.PositiveInfinity));

            double width = Math.Max(textBlock.DesiredSize.Width + 24, 110);  // ✅ БЫЛО: +12, 92
            double height = Math.Max(textBlock.DesiredSize.Height + 20, 48); // ✅ БЫЛО: +12, 34

            var ellipse = new Ellipse
            {
                Width = width,
                Height = height,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };

            var grid = new Grid { Width = width, Height = height };
            grid.Children.Add(ellipse);
            grid.Children.Add(textBlock);
            return grid;
        }

        // ✅ ENTITY - прямоугольник с жирной рамкой, шрифт 14
        private FrameworkElement CreateEntityVisual(string text)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = FontWeights.Normal,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.Black,
                Padding = new Thickness(3, 1, 3, 1),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 120,
                IsHitTestVisible = false
            };

            textBlock.Measure(new Size(textBlock.MaxWidth, double.PositiveInfinity));
            double width = Math.Max(textBlock.DesiredSize.Width + 16, 92);
            double height = Math.Max(textBlock.DesiredSize.Height + 8, 34);

            var canvasEntity = new Canvas { Width = width, Height = height, Background = Brushes.Transparent };

            // Основная рамка
            var border = new Rectangle
            {
                Width = width,
                Height = height,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            canvasEntity.Children.Add(border);

            // Жирная левая граница
            var shadowLeft = new Line
            {
                X1 = 0.9,
                Y1 = 0.9,
                X2 = 0.9,
                Y2 = height - 0.9,
                Stroke = Brushes.Black,
                StrokeThickness = 3.2
            };
            canvasEntity.Children.Add(shadowLeft);

            // Жирная верхняя граница
            var shadowTop = new Line
            {
                X1 = 0.9,
                Y1 = 0.9,
                X2 = width - 0.9,
                Y2 = 0.9,
                Stroke = Brushes.Black,
                StrokeThickness = 3.2
            };
            canvasEntity.Children.Add(shadowTop);

            Canvas.SetLeft(textBlock, 6);
            Canvas.SetTop(textBlock, (height - textBlock.DesiredSize.Height) / 2);
            canvasEntity.Children.Add(textBlock);

            return canvasEntity;
        }

        // ✅ STORE - хранилище с двойной левой линией, шрифт 14
        private FrameworkElement CreateStoreVisual(string text)
        {
            double barX = 6.0;
            double barThickness = 1.8;
            double sidePad = 6.0;

            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = FontWeights.Normal,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.Black,
                Padding = new Thickness(0, 2, 0, 2),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 140,
                IsHitTestVisible = false
            };

            textBlock.Measure(new Size(textBlock.MaxWidth, double.PositiveInfinity));

            double textWidth = textBlock.DesiredSize.Width;
            double textHeight = textBlock.DesiredSize.Height;

            double width = barX + sidePad + textWidth + sidePad;
            double height = Math.Max(textHeight + 8, 36);

            var canvasStore = new Canvas { Width = width, Height = height, Background = Brushes.Transparent };

            // Внешняя рамка
            var border = new Rectangle
            {
                Width = width,
                Height = height,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = barThickness
            };
            canvasStore.Children.Add(border);

            // Вертикальная разделительная линия
            var leftLine = new Line
            {
                X1 = barX,
                Y1 = barThickness,
                X2 = barX,
                Y2 = height - barThickness,
                Stroke = Brushes.Black,
                StrokeThickness = barThickness
            };
            canvasStore.Children.Add(leftLine);

            Canvas.SetLeft(textBlock, barX + sidePad);
            Canvas.SetTop(textBlock, (height - textHeight) / 2);
            canvasStore.Children.Add(textBlock);

            return canvasStore;
        }

        // ✅ DOCFLOW - документ с волнистой нижней линией, шрифт 14
        private FrameworkElement CreateDocFlowVisual(string text)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = FontWeights.Normal,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.Black,
                Padding = new Thickness(3, 1, 3, 1),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 140,
                IsHitTestVisible = false
            };

            textBlock.Measure(new Size(textBlock.MaxWidth, double.PositiveInfinity));

            double width = Math.Max(textBlock.DesiredSize.Width + 16, 100);
            double height = Math.Max(textBlock.DesiredSize.Height + 12, 50);

            var canvasElement = new Canvas { Width = width, Height = height, Background = Brushes.White };

            // Верхняя линия
            var topLine = new Line
            {
                X1 = 0,
                Y1 = 0,
                X2 = width,
                Y2 = 0,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            canvasElement.Children.Add(topLine);

            // Левая линия
            var leftLine = new Line
            {
                X1 = 0,
                Y1 = 0,
                X2 = 0,
                Y2 = height - 8,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            canvasElement.Children.Add(leftLine);

            // Правая линия
            var rightLine = new Line
            {
                X1 = width,
                Y1 = 0,
                X2 = width,
                Y2 = height - 8,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            canvasElement.Children.Add(rightLine);

            // Волнистая нижняя линия
            var wavyPath = CreateTwoSegmentWavyLine(width, height - 8);
            canvasElement.Children.Add(wavyPath);

            Canvas.SetLeft(textBlock, 8);
            Canvas.SetTop(textBlock, (height - textBlock.DesiredSize.Height - 8) / 2);
            canvasElement.Children.Add(textBlock);

            return canvasElement;
        }

        // ✅ Волнистая линия из 2 сегментов Безье
        private Path CreateTwoSegmentWavyLine(double width, double yPos)
        {
            var path = new Path
            {
                Stroke = Brushes.Black,
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };

            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = new Point(0, yPos) };

            double segmentWidth = width / 2.0;
            double amplitude = 8;

            // Первый изгиб (вверх)
            figure.Segments.Add(new BezierSegment(
                new Point(segmentWidth * 0.25, yPos),
                new Point(segmentWidth * 0.5, yPos + amplitude),
                new Point(segmentWidth, yPos),
                true
            ));

            // Второй изгиб (вниз)
            figure.Segments.Add(new BezierSegment(
                new Point(segmentWidth + segmentWidth * 0.25, yPos),
                new Point(segmentWidth + segmentWidth * 0.5, yPos - amplitude),
                new Point(width, yPos),
                true
            ));

            geometry.Figures.Add(figure);
            path.Data = geometry;
            return path;
        }

        // ====== РЕНДЕРИНГ СТРЕЛОК ======

        private void RenderArrows(DFDDiagram diagram)
        {
            var centers = GetBlockCenters();
            var arrowsByDirection = GroupArrowsByDirection(diagram.Arrows);

            foreach (var directionGroup in arrowsByDirection)
            {
                var arrowsInDirection = directionGroup.Value;
                int totalInDirection = arrowsInDirection.Count;

                var firstArrow = arrowsInDirection[0];
                string reverseDirection = $"{firstArrow.ToId}→{firstArrow.FromId}";
                bool hasReverseArrows = arrowsByDirection.ContainsKey(reverseDirection);

                for (int i = 0; i < totalInDirection; i++)
                {
                    var arrow = arrowsInDirection[i];
                    if (!centers.ContainsKey(arrow.FromId) || !centers.ContainsKey(arrow.ToId))
                        continue;

                    var fromCenter = centers[arrow.FromId];
                    var toCenter = centers[arrow.ToId];

                    Point offset = CalculateDirectionOffset(fromCenter, toCenter, i, totalInDirection, hasReverseArrows);

                    Point shiftedFrom = new Point(fromCenter.X + offset.X, fromCenter.Y + offset.Y);
                    Point shiftedTo = new Point(toCenter.X + offset.X, toCenter.Y + offset.Y);

                    var toBlock = blockVisuals[arrow.ToId].visual;
                    var intersection = GetIntersectionWithBlock(toBlock, shiftedFrom, shiftedTo);

                    DrawArrow(shiftedFrom, intersection, arrow.Label);
                }
            }
        }

        // ✅ Расчёт смещения для параллельных стрелок (18px между ними)
        private Point CalculateDirectionOffset(Point from, Point to, int index, int totalCount, bool hasReverseArrows)
        {
            if (totalCount == 1 && !hasReverseArrows)
                return new Point(0, 0);

            double dx = to.X - from.X;
            double dy = to.Y - from.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance < 0.01)
                return new Point(0, 0);

            dx /= distance;
            dy /= distance;

            double nx = -dy;
            double ny = dx;

            double offset = 0;

            if (hasReverseArrows && totalCount == 1)
            {
                offset = 9.0;
            }
            else if (totalCount == 1)
            {
                offset = 0.0;
            }
            else if (totalCount == 2)
            {
                offset = (index == 0) ? -9.0 : 9.0;
            }
            else if (totalCount == 3)
            {
                if (index == 0) offset = -9.0;
                else if (index == 1) offset = 0.0;
                else offset = 9.0;
            }
            else
            {
                offset = (index - (totalCount - 1) / 2.0) * 9.0;
            }

            return new Point(nx * offset, ny * offset);
        }

        // ✅ Группировка стрелок по направлению
        private Dictionary<string, List<DFDArrow>> GroupArrowsByDirection(List<DFDArrow> arrows)
        {
            var result = new Dictionary<string, List<DFDArrow>>();
            foreach (var arrow in arrows)
            {
                string key = $"{arrow.FromId}→{arrow.ToId}";
                if (!result.ContainsKey(key))
                    result[key] = new List<DFDArrow>();
                result[key].Add(arrow);
            }
            return result;
        }

        // ✅ Получение центров всех блоков
        private Dictionary<string, Point> GetBlockCenters()
        {
            var centers = new Dictionary<string, Point>();
            foreach (var kv in blockVisuals)
            {
                var fe = kv.Value.visual;
                if (fe == null) continue;

                double left = Canvas.GetLeft(fe);
                double top = Canvas.GetTop(fe);
                centers[kv.Key] = new Point(left + fe.Width / 2, top + fe.Height / 2);
            }
            return centers;
        }

        // ✅ Рисование стрелки с наконечником и подписью
        private void DrawArrow(Point start, Point end, string label)
        {
            // Линия стрелки
            var line = new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            Panel.SetZIndex(line, 20);
            canvas.Children.Add(line);

            // ✅ НАКОНЕЧНИК СТРЕЛКИ
            double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            Point p1 = new Point(
                end.X - ARROWHEAD_LENGTH * Math.Cos(angle - ARROWHEAD_ANGLE),
                end.Y - ARROWHEAD_LENGTH * Math.Sin(angle - ARROWHEAD_ANGLE));
            Point p2 = new Point(
                end.X - ARROWHEAD_LENGTH * Math.Cos(angle + ARROWHEAD_ANGLE),
                end.Y - ARROWHEAD_LENGTH * Math.Sin(angle + ARROWHEAD_ANGLE));

            var poly = new Polygon
            {
                Points = new PointCollection { new Point(end.X, end.Y), p1, p2 },
                Fill = Brushes.Black,
                Stroke = Brushes.Black,
                StrokeThickness = 0.5
            };
            Panel.SetZIndex(poly, 20);
            canvas.Children.Add(poly);

            // ✅ ПОДПИСЬ СТРЕЛКИ (перетаскиваемая)
            if (!string.IsNullOrWhiteSpace(label))
            {
                var tb = new TextBlock
                {
                    Text = label,
                    FontSize = 11,
                    FontWeight = FontWeights.Normal,
                    Foreground = Brushes.Black,
                    Background = Brushes.White,
                    Padding = new Thickness(3, 1, 3, 1),
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 120
                };

                double midX = (start.X + end.X) / 2;
                double midY = (start.Y + end.Y) / 2;

                Canvas.SetLeft(tb, midX - 50);
                Canvas.SetTop(tb, midY - 12);
                Panel.SetZIndex(tb, 90);
                canvas.Children.Add(tb);
                arrowLabels.Add(tb);  // ✅ Добавляем в список для AttachLabelEvents
            }
        }

        // ====== ПЕРЕСЕЧЕНИЯ С БЛОКАМИ ======

        private Point GetIntersectionWithBlock(FrameworkElement block, Point from, Point to)
        {
            double left = Canvas.GetLeft(block);
            double top = Canvas.GetTop(block);
            double width = block.Width;
            double height = block.Height;

            // Проверяем, овал это или прямоугольник
            if (block is Grid grid && grid.Children.Count > 0 && grid.Children[0] is Ellipse)
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
            double b = 2 * ((from.X - cx) * dx / (rx * rx) + (from.Y - cy) * dy / (ry * ry));
            double c = ((from.X - cx) * (from.X - cx)) / (rx * rx) + ((from.Y - cy) * (from.Y - cy)) / (ry * ry) - 1;

            double discriminant = b * b - 4 * a * c;
            if (discriminant < 0)
                return to;

            double t1 = (-b + Math.Sqrt(discriminant)) / (2 * a);
            double t2 = (-b - Math.Sqrt(discriminant)) / (2 * a);
            double t = (Math.Abs(t1) < Math.Abs(t2)) ? t1 : t2;

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

        // ====== ОБНОВЛЕНИЕ СТРЕЛОК ======

        public void UpdateArrows(DFDDiagram diagram)
        {
            // Удаляем старые линии и наконечники
            var toRemove = canvas.Children.OfType<Line>().ToList();
            var pRemove = canvas.Children.OfType<Polygon>().ToList();

            foreach (var l in toRemove)
                canvas.Children.Remove(l);
            foreach (var p in pRemove)
                canvas.Children.Remove(p);
            foreach (var lbl in arrowLabels)
                canvas.Children.Remove(lbl);

            arrowLabels.Clear();

            // Перерисовываем стрелки
            RenderArrows(diagram);
        }

        // ====== ПУБЛИЧНЫЕ МЕТОДЫ ======

        public Dictionary<string, FrameworkElement> GetBlockVisuals()
        {
            return blockVisuals.ToDictionary(k => k.Key, v => v.Value.visual);
        }

        public Dictionary<FrameworkElement, string> GetBlockTypes()
        {
            return blockVisuals.ToDictionary(k => k.Value.visual, v => v.Value.type);
        }

        // ✅ ВАЖНО! Для подключения перетаскивания подписей
        public List<TextBlock> GetArrowLabels()
        {
            return arrowLabels;
        }
    }
}
