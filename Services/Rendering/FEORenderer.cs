using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DiagramBuilder.Models;
using DiagramBuilder.Services.Management;
using DiagramBuilder.Services.Core;

namespace DiagramBuilder.Services.Rendering
{
    public class FEORenderer
    {
        private readonly Canvas canvas;
        private readonly DragDropManager dragDropManager;
        private readonly DiagramStyle style;

        public FEORenderer(Canvas canvas, DragDropManager dragDropManager, DiagramStyle style = null)
        {
            this.canvas = canvas;
            this.dragDropManager = dragDropManager;
            this.style = style ?? DiagramStyle.GetStyle(DiagramStyleType.ClassicBlackWhite);
        }

        public void Render(FEODiagram diagram, Dictionary<string, DiagramBlock> blocks, List<DiagramArrow> arrows)
        {
            canvas.Children.Clear();
            AutoLayoutFEO(diagram, 350, 120, 160, 340);
            blocks.Clear();
            arrows.Clear();

            // Создаём блоки
            foreach (var cmp in diagram.Components)
            {
                var block = new DiagramBlock
                {
                    Code = cmp.Code,
                    Text = cmp.Name,
                    Visual = CreateBlockVisual(cmp.Name, cmp.Code, cmp.X, cmp.Y, cmp.Width, cmp.Height)
                };
                blocks[cmp.Code] = block;
                Canvas.SetLeft(block.Visual, cmp.X);
                Canvas.SetTop(block.Visual, cmp.Y);
                canvas.Children.Add(block.Visual);
                dragDropManager.AttachBlockEvents(block.Visual);
            }

            var externalBlocks = new Dictionary<string, DiagramBlock>();

            // Создаём стрелки
            foreach (var ad in diagram.Arrows)
            {
                DiagramBlock fromBlock = GetBlock(ad.From, blocks, externalBlocks);
                DiagramBlock toBlock = GetBlock(ad.To, blocks, externalBlocks);

                if (fromBlock != null && toBlock != null)
                {
                    // Проверяем циклическую стрелку (из нижнего уровня в верхний)
                    bool isCyclic = IsCyclicArrow(fromBlock, toBlock, blocks);

                    var arrow = isCyclic
                        ? CreateCyclicArrow(fromBlock, toBlock, ad.Label)
                        : CreateOrthogonalArrow(fromBlock, toBlock, ad.Label);

                    if (arrow != null)
                    {
                        arrows.Add(arrow);
                        if (arrow.Label != null)
                            dragDropManager.AttachLabelEvents(arrow.Label);
                    }
                }
            }
        }

        public void UpdateArrows(FEODiagram diagram, Dictionary<string, DiagramBlock> blocks, List<DiagramArrow> arrows)
        {
            for (int i = canvas.Children.Count - 1; i >= 0; i--)
            {
                var el = canvas.Children[i];
                if (el is Line || el is Polygon || (el is TextBlock tb && tb.FontStyle == FontStyles.Italic))
                    canvas.Children.RemoveAt(i);
            }

            arrows.Clear();
            var externalBlocks = new Dictionary<string, DiagramBlock>();

            foreach (var ad in diagram.Arrows)
            {
                DiagramBlock fromBlock = GetBlock(ad.From, blocks, externalBlocks);
                DiagramBlock toBlock = GetBlock(ad.To, blocks, externalBlocks);

                if (fromBlock != null && toBlock != null)
                {
                    bool isCyclic = IsCyclicArrow(fromBlock, toBlock, blocks);

                    var arrow = isCyclic
                        ? CreateCyclicArrow(fromBlock, toBlock, ad.Label)
                        : CreateOrthogonalArrow(fromBlock, toBlock, ad.Label);

                    if (arrow != null)
                    {
                        arrows.Add(arrow);
                        if (arrow.Label != null)
                            dragDropManager.AttachLabelEvents(arrow.Label);
                    }
                }
            }
        }

        private DiagramBlock GetBlock(string code, Dictionary<string, DiagramBlock> blocks,
            Dictionary<string, DiagramBlock> externalBlocks)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            if (blocks.ContainsKey(code)) return blocks[code];
            if (code.StartsWith("external", StringComparison.OrdinalIgnoreCase))
                return ResolveExternalBlock(code, blocks, externalBlocks);
            return null;
        }

        private bool IsCyclicArrow(DiagramBlock from, DiagramBlock to, Dictionary<string, DiagramBlock> blocks)
        {
            if (from.Code.StartsWith("external") || to.Code.StartsWith("external"))
                return false;

            double yFrom = Canvas.GetTop(from.Visual);
            double yTo = Canvas.GetTop(to.Visual);

            // Если стрелка идёт снизу вверх (возврат)
            return yFrom > yTo + 50;
        }

        // ========== УГЛОВАТЫЕ СТРЕЛКИ (ПРЯМЫЕ С 90°) ==========
        private DiagramArrow CreateOrthogonalArrow(DiagramBlock from, DiagramBlock to, string label)
        {
            double x1 = Canvas.GetLeft(from.Visual);
            double y1 = Canvas.GetTop(from.Visual);
            double w1 = from.Visual.Width;
            double h1 = from.Visual.Height;

            double x2 = Canvas.GetLeft(to.Visual);
            double y2 = Canvas.GetTop(to.Visual);
            double w2 = to.Visual.Width;
            double h2 = to.Visual.Height;

            List<Point> points = new List<Point>();

            // Определяем откуда и куда идёт стрелка
            double centerX1 = x1 + w1 / 2;
            double centerY1 = y1 + h1 / 2;
            double centerX2 = x2 + w2 / 2;
            double centerY2 = y2 + h2 / 2;

            // ВНИЗ (external_bottom или блок снизу)
            if (y2 > y1 + h1 + 20 && Math.Abs(centerX1 - centerX2) < w1 / 2)
            {
                points.Add(new Point(centerX1, y1 + h1));
                points.Add(new Point(centerX1, y2));
            }
            // ВВЕРХ (external сверху)
            else if (y2 + h2 < y1 - 20 && Math.Abs(centerX1 - centerX2) < w1 / 2)
            {
                points.Add(new Point(centerX1, y1));
                points.Add(new Point(centerX1, y2 + h2));
            }
            // ВПРАВО
            else if (x2 > x1 + w1)
            {
                points.Add(new Point(x1 + w1, centerY1));

                if (Math.Abs(centerY2 - centerY1) > 10)
                {
                    double midX = (x1 + w1 + x2) / 2;
                    points.Add(new Point(midX, centerY1));
                    points.Add(new Point(midX, centerY2));
                }

                points.Add(new Point(x2, centerY2));
            }
            // ВЛЕВО
            else if (x2 + w2 < x1)
            {
                points.Add(new Point(x1, centerY1));

                if (Math.Abs(centerY2 - centerY1) > 10)
                {
                    double midX = (x1 + x2 + w2) / 2;
                    points.Add(new Point(midX, centerY1));
                    points.Add(new Point(midX, centerY2));
                }

                points.Add(new Point(x2 + w2, centerY2));
            }
            // ВНУТРИ (перекрываются по X) - вниз-вбок-вверх
            else
            {
                points.Add(new Point(centerX1, y1 + h1));
                double bottomY = Math.Max(y1 + h1, y2 + h2) + 30;
                points.Add(new Point(centerX1, bottomY));
                points.Add(new Point(centerX2, bottomY));
                points.Add(new Point(centerX2, y2 + h2));
            }

            return DrawPolyline(from, to, points, label);
        }

        // ========== КРУГОВЫЕ СТРЕЛКИ (U-ОБРАЗНЫЕ ДЛЯ ВОЗВРАТА) ==========
        private DiagramArrow CreateCyclicArrow(DiagramBlock from, DiagramBlock to, string label)
        {
            double x1 = Canvas.GetLeft(from.Visual);
            double y1 = Canvas.GetTop(from.Visual);
            double w1 = from.Visual.Width;
            double h1 = from.Visual.Height;

            double x2 = Canvas.GetLeft(to.Visual);
            double y2 = Canvas.GetTop(to.Visual);
            double w2 = to.Visual.Width;
            double h2 = to.Visual.Height;

            double centerX1 = x1 + w1 / 2;
            double centerX2 = x2 + w2 / 2;

            // U-образная стрелка: вниз → вбок → вверх
            double bottomY = y1 + h1 + 50;

            List<Point> points = new List<Point>
            {
                new Point(centerX1, y1 + h1),        // От нижней грани from
                new Point(centerX1, bottomY),        // Вниз
                new Point(centerX2, bottomY),        // Горизонтально
                new Point(centerX2, y2)              // Вверх к верхней грани to
            };

            return DrawPolyline(from, to, points, label);
        }

        // ========== ОТРИСОВКА ЛОМАНОЙ ЛИНИИ ==========
        private DiagramArrow DrawPolyline(DiagramBlock from, DiagramBlock to, List<Point> points, string label)
        {
            var lines = new List<Line>();

            // Рисуем сегменты
            for (int i = 0; i < points.Count - 1; i++)
            {
                var line = new Line
                {
                    X1 = points[i].X,
                    Y1 = points[i].Y,
                    X2 = points[i + 1].X,
                    Y2 = points[i + 1].Y,
                    Stroke = style.Line,
                    StrokeThickness = 2
                };
                Panel.SetZIndex(line, 50);
                canvas.Children.Add(line);
                lines.Add(line);
            }

            // Стрелка в конце
            Point lastPoint = points[points.Count - 1];
            Point prevPoint = points[points.Count - 2];

            double dx = lastPoint.X - prevPoint.X;
            double dy = lastPoint.Y - prevPoint.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);

            if (len > 0.001)
            {
                dx /= len;
                dy /= len;
            }

            double arrowSize = 10;
            double ox = -dy * 5;
            double oy = dx * 5;

            var arrowHead = new Polygon
            {
                Fill = style.Line,
                Points = new PointCollection
                {
                    lastPoint,
                    new Point(lastPoint.X - dx * arrowSize + ox, lastPoint.Y - dy * arrowSize + oy),
                    new Point(lastPoint.X - dx * arrowSize - ox, lastPoint.Y - dy * arrowSize - oy)
                }
            };
            Panel.SetZIndex(arrowHead, 51);
            canvas.Children.Add(arrowHead);

            // Подпись
            TextBlock labelBlock = null;
            if (!string.IsNullOrEmpty(label))
            {
                int midIndex = points.Count / 2;
                Point midPoint = points[midIndex];

                labelBlock = new TextBlock
                {
                    Text = label,
                    FontSize = 10,
                    FontWeight = FontWeights.Medium,
                    FontStyle = FontStyles.Italic,
                    Foreground = style.Text,
                    Background = Brushes.White,
                    Padding = new Thickness(3, 1, 3, 1)
                };

                Canvas.SetLeft(labelBlock, midPoint.X - 25);
                Canvas.SetTop(labelBlock, midPoint.Y - 20);
                Panel.SetZIndex(labelBlock, 52);
                canvas.Children.Add(labelBlock);
            }

            return new DiagramArrow
            {
                FromBlock = from,
                ToBlock = to,
                Lines = lines,
                ArrowHead = arrowHead,
                Label = labelBlock,
                FromId = from.Code,
                ToId = to.Code,
                ArrowType = "connect"
            };
        }

        private Border CreateBlockVisual(string name, string code, double x, double y, double w, double h)
        {
            var (finalHeight, lines) = TextFormatterHelper.CalculateTextSize(name, w - 16, h);

            var border = new Border
            {
                Width = w,
                Height = finalHeight,
                Background = style.BlockFill,
                BorderBrush = style.BlockBorder,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(6),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = (style.BlockShadow as SolidColorBrush)?.Color ?? Colors.Gray,
                    BlurRadius = 7,
                    Opacity = 0.35,
                    Direction = 320,
                    ShadowDepth = 3
                },
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var grid = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                    new RowDefinition { Height = GridLength.Auto }
                }
            };

            var textBlock = TextFormatterHelper.CreateAutoWrapTextBlock(name, style, w - 16);
            var codeBlock = new TextBlock
            {
                Text = code,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = style.CodeText,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };

            grid.Children.Add(textBlock);
            grid.Children.Add(codeBlock);
            Grid.SetRow(textBlock, 0);
            Grid.SetRow(codeBlock, 1);

            border.Child = grid;
            Panel.SetZIndex(border, 100);

            return border;
        }

        private DiagramBlock ResolveExternalBlock(string code, Dictionary<string, DiagramBlock> blocks,
            Dictionary<string, DiagramBlock> externalBlocks)
        {
            if (externalBlocks.ContainsKey(code))
                return externalBlocks[code];

            double x = 0, y = 0;

            if (code.ToLower().EndsWith("left"))
            {
                x = -100;
                y = 200;
            }
            else if (code.ToLower().EndsWith("bottom"))
            {
                x = 400;
                y = 600;
            }

            var border = new Border
            {
                Width = 1,
                Height = 1,
                Background = Brushes.Transparent
            };

            Canvas.SetLeft(border, x);
            Canvas.SetTop(border, y);

            var block = new DiagramBlock
            {
                Code = code,
                Text = "",
                Visual = border
            };

            externalBlocks[code] = block;
            return block;
        }

        public void AutoLayoutFEO(FEODiagram diagram, double startX = 350, double baseLayerY = 120,
            double layerStepY = 160, double blockStepX = 340)
        {
            var ids = diagram.Components.Select(c => c.Code).ToHashSet();
            var edges = diagram.Arrows
                .Where(a => ids.Contains(a.From) && ids.Contains(a.To) && a.From != a.To)
                .ToList();

            var incoming = ids.ToDictionary(code => code, code => new List<string>());
            foreach (var arrow in edges)
                incoming[arrow.To].Add(arrow.From);

            var layer = ids.ToDictionary(code => code, code => 0);
            var queue = new Queue<string>(ids.Where(code => incoming[code].Count == 0));

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                int l = layer[cur];
                foreach (var a in edges.Where(e => e.From == cur))
                {
                    if (layer[a.To] < l + 1)
                    {
                        layer[a.To] = l + 1;
                        queue.Enqueue(a.To);
                    }
                }
            }

            int maxLayer = (layer.Count > 0) ? layer.Values.Max() : 0;

            var byLayer = new List<List<FEOComponent>>();
            for (int i = 0; i <= maxLayer; i++)
                byLayer.Add(new List<FEOComponent>());

            foreach (var cmp in diagram.Components)
                byLayer[layer[cmp.Code]].Add(cmp);

            for (int l = 0; l < byLayer.Count; l++)
            {
                var group = byLayer[l];
                int count = group.Count;
                double totalWidth = (count - 1) * blockStepX;
                double baseX = startX - totalWidth / 2.0;

                for (int i = 0; i < count; i++)
                {
                    group[i].X = baseX + i * blockStepX;
                    group[i].Y = baseLayerY + (maxLayer - l) * layerStepY;
                }
            }

            double minX = diagram.Components.Min(c => c.X);
            double shift = 50 - minX;
            if (shift > 0)
            {
                foreach (var cmp in diagram.Components)
                    cmp.X += shift;
            }
        }
    }
}
