using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DiagramBuilder.Models;
using DiagramBuilder.Services.Core;
using DiagramBuilder.Services.Management;

namespace DiagramBuilder.Services.Rendering
{
    /// <summary>
    /// FEO диаграмма рендерер (финальная исправленная версия)
    /// Использует ArrowCalculator для расчёта стрелок, как в IDEF0
    /// Все стрелки динамичные, external порты пересчитываются при движении блоков
    /// ИСПРАВЛЕНИЯ:
    /// 1. External стрелки получают наконечник правильно
    /// 2. Циклические (обратные) стрелки идут СНИЗУ под всеми диаграммами
    /// </summary>
    public class FEORenderer
    {
        private readonly Canvas canvas;
        private readonly DragDropManager dragDropManager;
        private readonly DiagramStyle style;
        private readonly Dictionary<string, DiagramBlock> externalBlocks = new Dictionary<string, DiagramBlock>();

        private const double ARROW_OFFSET = 70;
        private const double EXTERNAL_ARROW_LENGTH = 80;

        public FEORenderer(Canvas canvas, DragDropManager dragDropManager, DiagramStyle style)
        {
            this.canvas = canvas;
            this.dragDropManager = dragDropManager;
            this.style = style ?? DiagramStyle.GetStyle(DiagramStyleType.ClassicBlackWhite);
        }

        /// <summary>
        /// Главный метод рендеринга FEO диаграммы
        /// </summary>
        public void Render(FEODiagram diagram, Dictionary<string, DiagramBlock> blocks, List<DiagramArrow> arrows)
        {
            if (diagram?.Components == null)
                return;

            // 1. Автолейаут
            AutoLayoutFEO(diagram, 350, 120, 160, 340);

            // 2. Очищаем
            canvas.Children.Clear();
            blocks.Clear();
            arrows.Clear();
            externalBlocks.Clear();

            // 3. Рендерим основные блоки (компоненты)
            foreach (var cmp in diagram.Components)
            {
                var block = new DiagramBlock
                {
                    Code = cmp.Code,
                    Text = cmp.Name,
                    Visual = CreateBlockVisual(cmp.Name, cmp.Code, cmp.Width, cmp.Height)
                };

                blocks[cmp.Code] = block;
                Canvas.SetLeft(block.Visual, cmp.X);
                Canvas.SetTop(block.Visual, cmp.Y);
                canvas.Children.Add(block.Visual);

                if (dragDropManager != null)
                    dragDropManager.AttachBlockEvents(block.Visual);
            }

            // 4. Устанавливаем блоки для ArrowCalculator
            ArrowCalculator.SetAllBlocks(blocks);

            // 5. Предварительно обрабатываем стрелки
            var processedArrows = ArrowCalculator.PreprocessArrows(diagram.Arrows, blocks);

            // 6. Создаём external блоки при необходимости
            PrepareExternalBlocks(processedArrows, blocks);

            // 7. Рендерим стрелки
            foreach (var arrowData in processedArrows)
            {
                DiagramBlock fromBlock = GetOrCreateBlock(arrowData.From, blocks);
                DiagramBlock toBlock = GetOrCreateBlock(arrowData.To, blocks);

                if (fromBlock == null || toBlock == null)
                    continue;

                // Проверяем циклическая ли стрелка (обратная связь)
                bool isCyclic = IsCyclicArrow(fromBlock, toBlock);

                DiagramArrow arrow;
                if (isCyclic)
                {
                    // Циклические стрелки идут СНИЗУ под всеми диаграммами
                    arrow = CreateCyclicArrowUnderDiagrams(fromBlock, toBlock, arrowData.Label);
                }
                else
                {
                    // Обычные стрелки через ArrowCalculator
                    var segments = ArrowCalculator.CalculateArrowPath(
                        fromBlock, toBlock,
                        arrowData.Type ?? "connect",
                        arrowData.IndexOnSide, arrowData.TotalOnSide
                    );

                    if (segments == null || segments.Count == 0)
                        continue;

                    arrow = CreateArrowFromSegments(fromBlock, toBlock, segments, arrowData.Label);
                }

                if (arrow != null)
                {
                    arrows.Add(arrow);

                    if (arrow.Label != null && dragDropManager != null)
                        dragDropManager.AttachLabelEvents(arrow.Label);
                }
            }
        }

        /// <summary>
        /// Подготавливает external блоки для стрелок
        /// </summary>
        private void PrepareExternalBlocks(List<ArrowData> processedArrows, Dictionary<string, DiagramBlock> blocks)
        {
            foreach (var arrowData in processedArrows)
            {
                if (arrowData.From.StartsWith("external", StringComparison.OrdinalIgnoreCase))
                    GetOrCreateExternalBlock(arrowData.From, blocks);

                if (arrowData.To.StartsWith("external", StringComparison.OrdinalIgnoreCase))
                    GetOrCreateExternalBlock(arrowData.To, blocks);
            }
        }

        /// <summary>
        /// Получает или создаёт блок (обычный или external)
        /// </summary>
        private DiagramBlock GetOrCreateBlock(string blockId, Dictionary<string, DiagramBlock> blocks)
        {
            if (blocks.ContainsKey(blockId))
                return blocks[blockId];

            if (blockId.StartsWith("external", StringComparison.OrdinalIgnoreCase))
                return GetOrCreateExternalBlock(blockId, blocks);

            return null;
        }

        /// <summary>
        /// Создаёт или получает external блок
        /// </summary>
        private DiagramBlock GetOrCreateExternalBlock(string code, Dictionary<string, DiagramBlock> blocks)
        {
            if (externalBlocks.ContainsKey(code))
                return externalBlocks[code];

            // Создаём невидимый блок для external порта
            var border = new Border
            {
                Width = 1,
                Height = 1,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent
            };

            Canvas.SetLeft(border, 0);
            Canvas.SetTop(border, 0);
            Panel.SetZIndex(border, 1);
            canvas.Children.Add(border);

            var block = new DiagramBlock
            {
                Code = code,
                Text = code,
                Visual = border
            };

            externalBlocks[code] = block;
            blocks[code] = block;

            return block;
        }

        /// <summary>
        /// Проверяет, является ли стрелка циклической (A5→A1 = обратная связь)
        /// </summary>
        private bool IsCyclicArrow(DiagramBlock from, DiagramBlock to)
        {
            if (from.Code.StartsWith("external", StringComparison.OrdinalIgnoreCase) ||
                to.Code.StartsWith("external", StringComparison.OrdinalIgnoreCase))
                return false;

            double yFrom = Canvas.GetTop(from.Visual);
            double yTo = Canvas.GetTop(to.Visual);

            // Если FROM находится НИЖЕ TO — это обратная связь
            return yFrom > yTo + 50;
        }

        /// <summary>
        /// Создаёт циклическую (обратную) стрелку, которая идёт СНИЗУ под всеми диаграммами
        /// </summary>
        private DiagramArrow CreateCyclicArrowUnderDiagrams(DiagramBlock from, DiagramBlock to, string label)
        {
            double xFrom = Canvas.GetLeft(from.Visual);
            double yFrom = Canvas.GetTop(from.Visual);
            double wFrom = from.Visual.Width;
            double hFrom = from.Visual.Height;

            double xTo = Canvas.GetLeft(to.Visual);
            double yTo = Canvas.GetTop(to.Visual);
            double wTo = to.Visual.Width;
            double hTo = to.Visual.Height;

            double centerFromX = xFrom + wFrom / 2;
            double centerToX = xTo + wTo / 2;

            // Находим максимальную Y координату (самый низкий блок)
            double maxY = canvas.Children.OfType<Border>()
                .Where(b => b != from.Visual && b != to.Visual)
                .Select(b => Canvas.GetTop(b) + b.Height)
                .DefaultIfEmpty(Math.Max(yFrom + hFrom, yTo + hTo))
                .Max();

            // Стрелка идёт СНИЗУ на ARROW_OFFSET ниже самого низкого блока
            double bottomY = maxY + ARROW_OFFSET;

            var points = new List<Point>
            {
                // Выход из FROM блока снизу
                new Point(centerFromX, yFrom + hFrom),
                // Вниз к нижней линии
                new Point(centerFromX, bottomY),
                // Горизонтально к TO блоку
                new Point(centerToX, bottomY),
                // Вверх к входу в TO блок
                new Point(centerToX, yTo)
            };

            return DrawPolylineWithArrowHead(from, to, points, label, "up");
        }

        /// <summary>
        /// Создаёт стрелку из сегментов, полученных от ArrowCalculator
        /// </summary>
        private DiagramArrow CreateArrowFromSegments(DiagramBlock fromBlock, DiagramBlock toBlock, List<ArrowSegment> segments, string label)
        {
            var lines = new List<Line>();

            // Рисуем линии для каждого сегмента
            foreach (var segment in segments)
            {
                var line = new Line
                {
                    X1 = segment.Start.X,
                    Y1 = segment.Start.Y,
                    X2 = segment.End.X,
                    Y2 = segment.End.Y,
                    Stroke = style.Line,
                    StrokeThickness = 2
                };

                Panel.SetZIndex(line, 50);
                canvas.Children.Add(line);
                lines.Add(line);
            }

            // Создаём наконечник стрелки
            Polygon arrowHead = null;
            if (segments.Count > 0)
            {
                var lastSegment = segments[segments.Count - 1];

                double dx = lastSegment.End.X - lastSegment.Start.X;
                double dy = lastSegment.End.Y - lastSegment.Start.Y;

                // защита от нулевого вектора
                if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
                {
                    // попробуем взять предпоследний сегмент
                    if (segments.Count > 1)
                    {
                        lastSegment = segments[segments.Count - 2];
                        dx = lastSegment.End.X - lastSegment.Start.X;
                        dy = lastSegment.End.Y - lastSegment.Start.Y;
                    }
                }

                // если всё ещё ноль — ставим направление по умолчанию
                string dir;
                if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
                {
                    dir = "right";
                }
                else if (Math.Abs(dx) >= Math.Abs(dy))
                {
                    dir = dx >= 0 ? "right" : "left";
                }
                else
                {
                    dir = dy >= 0 ? "down" : "up";
                }

                arrowHead = CreateArrowHead(lastSegment.End, dir);

                Panel.SetZIndex(arrowHead, 51);
                canvas.Children.Add(arrowHead);
            }

            // Создаём подпись стрелки
            TextBlock labelBlock = null;
            if (!string.IsNullOrEmpty(label) && segments.Count > 0)
            {
                int midIndex = segments.Count / 2;
                var midSegment = segments[midIndex];
                double midX = (midSegment.Start.X + midSegment.End.X) / 2;
                double midY = (midSegment.Start.Y + midSegment.End.Y) / 2;

                labelBlock = new TextBlock
                {
                    Text = label?.Replace("\\n", "\n"),
                    FontSize = 10,
                    FontWeight = FontWeights.Medium,
                    FontStyle = FontStyles.Italic,
                    Foreground = style.Text,
                    Background = Brushes.White,
                    Padding = new Thickness(3, 1, 3, 1),
                    TextWrapping = TextWrapping.Wrap
                };

                Canvas.SetLeft(labelBlock, midX - 25);
                Canvas.SetTop(labelBlock, midY - 20);
                Panel.SetZIndex(labelBlock, 52);
                canvas.Children.Add(labelBlock);
            }

            return new DiagramArrow
            {
                FromBlock = fromBlock,
                ToBlock = toBlock,
                Lines = lines,
                ArrowHead = arrowHead,
                Label = labelBlock,
                FromId = fromBlock.Code,
                ToId = toBlock.Code,
                ArrowType = "connect"
            };
        }

        /// <summary>
        /// Рисует стрелку из списка точек с наконечником в конце
        /// Используется для циклических стрелок
        /// </summary>
        private DiagramArrow DrawPolylineWithArrowHead(DiagramBlock fromBlock, DiagramBlock toBlock, List<Point> points, string label, string endDirection)
        {
            var lines = new List<Line>();

            // Рисуем линии между точками
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

            // Создаём наконечник в конце (при входе в TO блок)
            Polygon arrowHead = null;
            if (points.Count > 0)
            {
                Point tipPoint = points[points.Count - 1];
                arrowHead = CreateArrowHead(tipPoint, endDirection);
                Panel.SetZIndex(arrowHead, 51);
                canvas.Children.Add(arrowHead);
            }

            // Создаём подпись
            TextBlock labelBlock = null;
            if (!string.IsNullOrEmpty(label) && points.Count > 0)
            {
                int midIndex = points.Count / 2;
                Point midPoint = points[midIndex];

                labelBlock = new TextBlock
                {
                    Text = label?.Replace("\\n", "\n"),
                    FontSize = 10,
                    FontWeight = FontWeights.Medium,
                    FontStyle = FontStyles.Italic,
                    Foreground = style.Text,
                    Background = Brushes.White,
                    Padding = new Thickness(3, 1, 3, 1),
                    TextWrapping = TextWrapping.Wrap
                };

                Canvas.SetLeft(labelBlock, midPoint.X - 25);
                Canvas.SetTop(labelBlock, midPoint.Y - 20);
                Panel.SetZIndex(labelBlock, 52);
                canvas.Children.Add(labelBlock);
            }

            return new DiagramArrow
            {
                FromBlock = fromBlock,
                ToBlock = toBlock,
                Lines = lines,
                ArrowHead = arrowHead,
                Label = labelBlock,
                FromId = fromBlock.Code,
                ToId = toBlock.Code,
                ArrowType = "cyclic"
            };
        }

        /// <summary>
        /// Создаёт наконечник стрелки в зависимости от направления
        /// </summary>
        private Polygon CreateArrowHead(Point tip, string direction)
        {
            var points = new PointCollection();

            switch (direction?.ToLower())
            {
                case "right":
                    points.Add(tip);
                    points.Add(new Point(tip.X - 10, tip.Y - 5));
                    points.Add(new Point(tip.X - 10, tip.Y + 5));
                    break;

                case "left":
                    points.Add(tip);
                    points.Add(new Point(tip.X + 10, tip.Y - 5));
                    points.Add(new Point(tip.X + 10, tip.Y + 5));
                    break;

                case "down":
                    points.Add(tip);
                    points.Add(new Point(tip.X - 5, tip.Y - 10));
                    points.Add(new Point(tip.X + 5, tip.Y - 10));
                    break;

                case "up":
                    points.Add(tip);
                    points.Add(new Point(tip.X - 5, tip.Y + 10));
                    points.Add(new Point(tip.X + 5, tip.Y + 10));
                    break;

                default:
                    points.Add(tip);
                    points.Add(new Point(tip.X - 10, tip.Y - 5));
                    points.Add(new Point(tip.X - 10, tip.Y + 5));
                    break;
            }

            return new Polygon
            {
                Fill = style.Line,
                Points = points
            };
        }

        /// <summary>
        /// Обновляет стрелки при перемещении блоков
        /// </summary>
        public void UpdateArrows(FEODiagram diagram, Dictionary<string, DiagramBlock> blocks, List<DiagramArrow> arrows)
        {
            // Удаляем все стрелки с канваса
            for (int i = canvas.Children.Count - 1; i >= 0; i--)
            {
                var el = canvas.Children[i];

                if (el is Line || el is Polygon || (el is TextBlock tb && tb.FontStyle == FontStyles.Italic))
                {
                    canvas.Children.RemoveAt(i);
                }
            }

            arrows.Clear();

            // Устанавливаем обновлённые блоки
            ArrowCalculator.SetAllBlocks(blocks);

            // Пересчитываем координаты external блоков на основе основных блоков
            UpdateExternalBlockPositions(diagram, blocks);

            // Обработка стрелок
            var processedArrows = ArrowCalculator.PreprocessArrows(diagram.Arrows, blocks);

            // Перерисовываем стрелки
            foreach (var arrowData in processedArrows)
            {
                DiagramBlock fromBlock = GetOrCreateBlock(arrowData.From, blocks);
                DiagramBlock toBlock = GetOrCreateBlock(arrowData.To, blocks);

                if (fromBlock == null || toBlock == null)
                    continue;

                bool isCyclic = IsCyclicArrow(fromBlock, toBlock);

                DiagramArrow arrow;
                if (isCyclic)
                {
                    arrow = CreateCyclicArrowUnderDiagrams(fromBlock, toBlock, arrowData.Label);
                }
                else
                {
                    var segments = ArrowCalculator.CalculateArrowPath(
                        fromBlock, toBlock,
                        arrowData.Type ?? "connect",
                        arrowData.IndexOnSide, arrowData.TotalOnSide
                    );

                    if (segments == null || segments.Count == 0)
                        continue;

                    arrow = CreateArrowFromSegments(fromBlock, toBlock, segments, arrowData.Label);
                }

                if (arrow != null)
                {
                    arrows.Add(arrow);

                    if (arrow.Label != null && dragDropManager != null)
                        dragDropManager.AttachLabelEvents(arrow.Label);
                }
            }
        }

        /// <summary>
        /// Пересчитывает координаты external блоков на основе основных блоков
        /// </summary>
        private void UpdateExternalBlockPositions(FEODiagram diagram, Dictionary<string, DiagramBlock> blocks)
        {
            foreach (var arrow in diagram.Arrows)
            {
                // Обработка external FROM
                if (arrow.From.StartsWith("external", StringComparison.OrdinalIgnoreCase))
                {
                    var extBlock = GetOrCreateBlock(arrow.From, blocks);
                    if (extBlock != null && blocks.ContainsKey(arrow.To))
                    {
                        var toBlock = blocks[arrow.To];
                        UpdateExternalBlockPosition(arrow.From, toBlock, extBlock);
                    }
                }

                // Обработка external TO
                if (arrow.To.StartsWith("external", StringComparison.OrdinalIgnoreCase))
                {
                    var extBlock = GetOrCreateBlock(arrow.To, blocks);
                    if (extBlock != null && blocks.ContainsKey(arrow.From))
                    {
                        var fromBlock = blocks[arrow.From];
                        UpdateExternalBlockPosition(arrow.To, fromBlock, extBlock);
                    }
                }
            }
        }

        /// <summary>
        /// Обновляет позицию external блока на основе основного блока и направления
        /// </summary>
        private void UpdateExternalBlockPosition(string externalCode, DiagramBlock relatedBlock, DiagramBlock externalBlock)
        {
            string direction = ExtractDirection(externalCode);
            double relatedX = Canvas.GetLeft(relatedBlock.Visual);
            double relatedY = Canvas.GetTop(relatedBlock.Visual);
            double relatedW = relatedBlock.Visual.Width;
            double relatedH = relatedBlock.Visual.Height;

            double extX = 0, extY = 0;

            switch (direction.ToLower())
            {
                case "left":
                    extX = relatedX - EXTERNAL_ARROW_LENGTH;
                    extY = relatedY + relatedH / 2;
                    break;

                case "right":
                    extX = relatedX + relatedW + EXTERNAL_ARROW_LENGTH;
                    extY = relatedY + relatedH / 2;
                    break;

                case "top":
                    extX = relatedX + relatedW / 2;
                    extY = relatedY - EXTERNAL_ARROW_LENGTH;
                    break;

                case "bottom":
                    extX = relatedX + relatedW / 2;
                    extY = relatedY + relatedH + EXTERNAL_ARROW_LENGTH;
                    break;
            }

            Canvas.SetLeft(externalBlock.Visual, extX);
            Canvas.SetTop(externalBlock.Visual, extY);
        }

        /// <summary>
        /// Извлекает направление из кода external блока
        /// </summary>
        private string ExtractDirection(string externalCode)
        {
            string lower = externalCode.ToLower();
            if (lower.Contains("left")) return "left";
            if (lower.Contains("right")) return "right";
            if (lower.Contains("top")) return "top";
            if (lower.Contains("bottom")) return "bottom";
            return "unknown";
        }

        /// <summary>
        /// Создаёт визуальное представление блока
        /// </summary>
        private Border CreateBlockVisual(string name, string code, double w, double h)
        {
            TextFormatterHelper.CalculateTextSize(name, w - 16, h);

            var border = new Border
            {
                Width = w,
                Height = h,
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

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

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

        /// <summary>
        /// Автолейаут FEO диаграммы (топологическая сортировка)
        /// </summary>
        public void AutoLayoutFEO(FEODiagram diagram, double startX = 350, double baseLayerY = 120, double layerStepY = 160, double blockStepX = 340)
        {
            var ids = diagram.Components.Select(c => c.Code).ToHashSet();
            var edges = diagram.Arrows
                .Where(a => ids.Contains(a.From) && ids.Contains(a.To) && a.From != a.To)
                .ToList();

            // Топологическая сортировка
            var incoming = ids.ToDictionary(code => code, code => new List<string>());

            foreach (var arrow in edges)
            {
                incoming[arrow.To].Add(arrow.From);
            }

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

            int maxLayer = layer.Count > 0 ? layer.Values.Max() : 0;

            var byLayer = new List<List<FEOComponent>>();

            for (int i = 0; i <= maxLayer; i++)
            {
                byLayer.Add(new List<FEOComponent>());
            }

            foreach (var cmp in diagram.Components)
            {
                byLayer[layer[cmp.Code]].Add(cmp);
            }

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

            // Минимальное смещение вправо
            double minX = diagram.Components.Min(c => c.X);
            double shift = Math.Max(0, 100 - minX);

            if (shift > 0)
            {
                foreach (var cmp in diagram.Components)
                {
                    cmp.X += shift;
                }
            }
        }
    }
}