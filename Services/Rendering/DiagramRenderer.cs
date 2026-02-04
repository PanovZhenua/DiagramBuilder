using DiagramBuilder.Models;
using DiagramBuilder.Services.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DiagramBuilder.Services.Rendering
{
    public class DiagramRenderer
    {
        private readonly Canvas canvas;
        private readonly List<DiagramArrow> activeArrows = new List<DiagramArrow>();
        private DiagramStyle style;
        private Dictionary<string, DiagramBlock> externalBlocks = new Dictionary<string, DiagramBlock>();

        public DiagramRenderer(Canvas canvas, DiagramStyle style = null)
        {
            this.canvas = canvas;
            this.style = style ?? DiagramStyle.GetStyle(DiagramStyleType.ClassicBlackWhite);
        }

        public void SetStyle(DiagramStyle newStyle)
        {
            style = newStyle ?? DiagramStyle.GetStyle(DiagramStyleType.ClassicBlackWhite);
        }

        public DiagramBlock CreateBlock(string text, string code, double x, double y, double width = 200, double height = 80)
        {
            var (finalHeight, lines) = TextFormatterHelper.CalculateTextSize(text, width - 16, height);

            var border = new Border
            {
                Width = width,
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
                Cursor = Cursors.Hand,
                Child = new Grid
                {
                    RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto }
            },
                    Children =
            {
                TextFormatterHelper.CreateAutoWrapTextBlock(text, style, width - 16),
                new TextBlock
                {
                    Text = code,
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = style.CodeText,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 5)
                }
            }
                }
            };

            Grid.SetRow((TextBlock)((Grid)border.Child).Children[0], 0);
            Grid.SetRow((TextBlock)((Grid)border.Child).Children[1], 1);

            Canvas.SetLeft(border, x);
            Canvas.SetTop(border, y);
            Panel.SetZIndex(border, 100);
            canvas.Children.Add(border);

            return new DiagramBlock
            {
                Visual = border,
                Label = (TextBlock)((Grid)border.Child).Children[0],
                CodeLabel = (TextBlock)((Grid)border.Child).Children[1],
                Code = code,
                Text = text,
                Lines = lines
            };
        }

        // ✅ Добавить в DiagramRenderer после CreateBlock
        public void AttachResizeEvents(DiagramBlock block, Action onResize)
        {
            if (block?.Visual == null) return;

            bool isResizing = false;
            double startX = 0;
            double originalWidth = 0;

            block.Visual.MouseMove += (s, e) =>
            {
                if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                {
                    block.Visual.Cursor = Cursors.SizeWE;

                    if (e.LeftButton == MouseButtonState.Pressed)
                    {
                        if (!isResizing)
                        {
                            isResizing = true;
                            startX = e.GetPosition(canvas).X;
                            originalWidth = block.Visual.Width;
                        }

                        double currentX = e.GetPosition(canvas).X;
                        double delta = currentX - startX;
                        double newWidth = Math.Max(100, originalWidth + delta);

                        // Обновляем ширину блока
                        if (block.Visual is Border border)
                        {
                            border.Width = newWidth;

                            // Пересчитываем текст
                            var finalHeight = TextFormatterHelper.CalculateTextSize(
                                block.Text, newWidth - 16, 80).Item1;
                            border.Height = finalHeight;
                        }

                        onResize?.Invoke();
                        e.Handled = true;
                    }
                }
                else
                {
                    block.Visual.Cursor = Cursors.Hand;
                }
            };

            block.Visual.MouseUp += (s, e) =>
            {
                isResizing = false;
            };

            block.Visual.MouseLeave += (s, e) =>
            {
                block.Visual.Cursor = Cursors.Hand;
                isResizing = false;
            };
        }

        public DiagramArrow CreateArrow(
            DiagramBlock fromBlock, DiagramBlock toBlock,
            string fromId, string toId, string labelText, string arrowType)
        {
            labelText = labelText?.Replace("\\n", "\n");
            var segments = ArrowCalculator.CalculateArrowPath(fromBlock, toBlock, arrowType);

            if (segments == null || segments.Count == 0)
                return null;

            List<Line> lines = new List<Line>();
            foreach (var segment in segments)
            {
                Line line = new Line
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

            var lastSegment = segments[segments.Count - 1];
            Polygon arrowHead = CreateArrowHead(lastSegment.End, lastSegment.Direction);
            arrowHead.Fill = style.Line;
            Panel.SetZIndex(arrowHead, 51);
            canvas.Children.Add(arrowHead);

            TextBlock label = null;
            if (!string.IsNullOrEmpty(labelText))
            {
                Point labelPos = CalculateLabelPosition(segments);
                label = new TextBlock
                {
                    Text = labelText,
                    FontSize = 10,
                    FontWeight = FontWeights.Medium,
                    FontStyle = FontStyles.Italic,
                    Foreground = style.Text,
                    TextAlignment = TextAlignment.Center,
                    Background = Brushes.White,
                    Padding = new Thickness(3, 1, 3, 1)
                };

                Canvas.SetLeft(label, labelPos.X);
                Canvas.SetTop(label, labelPos.Y);
                Panel.SetZIndex(label, 52);
                canvas.Children.Add(label);
            }

            DiagramArrow arrow = new DiagramArrow
            {
                FromBlock = fromBlock,
                ToBlock = toBlock,
                Lines = lines,
                ArrowHead = arrowHead,
                Label = label,
                FromId = fromId,
                ToId = toId,
                ArrowType = arrowType
            };

            activeArrows.Add(arrow);
            return arrow;
        }

        public DiagramArrow CreateArrowWithDistribution(
        DiagramBlock fromBlock, DiagramBlock toBlock,
        string fromId, string toId, string labelText, string arrowType,
        int indexOnSide, int totalOnSide)
        {
            // ✅ ОБРАБОТКА EXTERNAL БЛОКОВ
            if (fromBlock == null && fromId.StartsWith("external", StringComparison.OrdinalIgnoreCase))
            {
                fromBlock = GetOrCreateExternalBlock(fromId);
            }
            if (toBlock == null && toId.StartsWith("external", StringComparison.OrdinalIgnoreCase))
            {
                toBlock = GetOrCreateExternalBlock(toId);
            }

            if (fromBlock == null || toBlock == null)
                return null;

            labelText = labelText?.Replace("\\n", "\n");
            var segments = ArrowCalculator.CalculateArrowPath(
                fromBlock, toBlock, arrowType, indexOnSide, totalOnSide);

            if (segments == null || segments.Count == 0)
                return null;

            List<Line> lines = new List<Line>();
            foreach (var segment in segments)
            {
                Line line = new Line
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

            var lastSegment = segments[segments.Count - 1];
            Polygon arrowHead = CreateArrowHead(lastSegment.End, lastSegment.Direction);
            arrowHead.Fill = style.Line;
            Panel.SetZIndex(arrowHead, 51);
            canvas.Children.Add(arrowHead);

            TextBlock label = null;
            if (!string.IsNullOrEmpty(labelText))
            {
                Point labelPos = CalculateLabelPosition(segments);
                label = new TextBlock
                {
                    Text = labelText,
                    FontSize = 10,
                    FontWeight = FontWeights.Medium,
                    FontStyle = FontStyles.Italic,
                    Foreground = style.Text,
                    Background = Brushes.White,
                    Padding = new Thickness(3, 1, 3, 1)
                };

                Canvas.SetLeft(label, labelPos.X);
                Canvas.SetTop(label, labelPos.Y);
                Panel.SetZIndex(label, 52);
                canvas.Children.Add(label);
            }

            DiagramArrow arrow = new DiagramArrow
            {
                FromBlock = fromBlock,
                ToBlock = toBlock,
                Lines = lines,
                ArrowHead = arrowHead,
                Label = label,
                FromId = fromId,
                ToId = toId,
                ArrowType = arrowType,
                IndexOnSide = indexOnSide,
                TotalOnSide = totalOnSide
            };

            activeArrows.Add(arrow);
            return arrow;
        }

        private DiagramBlock GetOrCreateExternalBlock(string code)
        {
            if (externalBlocks.ContainsKey(code))
                return externalBlocks[code];

            // Определяем позицию external блока по направлению
            Point position = DetermineExternalPosition(code);

            // Создаём невидимый блок размером 1x1
            var border = new Border
            {
                Width = 1,
                Height = 1,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent
            };

            Canvas.SetLeft(border, position.X);
            Canvas.SetTop(border, position.Y);
            Panel.SetZIndex(border, 1);
            canvas.Children.Add(border);

            var block = new DiagramBlock
            {
                Code = code,
                Text = code,
                Visual = border
            };

            externalBlocks[code] = block;
            return block;
        }

        // ✅ НОВЫЙ МЕТОД: определение позиции external блока
        private Point DetermineExternalPosition(string code)
        {
            string lower = code.ToLower();

            // Находим все обычные блоки
            var allVisuals = canvas.Children.OfType<Border>()
                .Where(b => b.Width > 10) // Только реальные блоки
                .ToList();

            if (allVisuals.Count == 0)
                return new Point(0, 0);

            double minX = allVisuals.Min(b => Canvas.GetLeft(b));
            double maxX = allVisuals.Max(b => Canvas.GetLeft(b) + b.Width);
            double minY = allVisuals.Min(b => Canvas.GetTop(b));
            double maxY = allVisuals.Max(b => Canvas.GetTop(b) + b.Height);
            double centerX = (minX + maxX) / 2;
            double centerY = (minY + maxY) / 2;

            const double OFFSET = 100;

            if (lower.Contains("left"))
                return new Point(minX - OFFSET, centerY);
            else if (lower.Contains("right"))
                return new Point(maxX + OFFSET, centerY);
            else if (lower.Contains("top"))
                return new Point(centerX, minY - OFFSET);
            else if (lower.Contains("bottom"))
                return new Point(centerX, maxY + OFFSET);
            else
                return new Point(minX - OFFSET, centerY); // По умолчанию слева
        }

        public void Clear()
        {
            canvas.Children.Clear();
            activeArrows.Clear();
            externalBlocks.Clear(); // ✅ ОЧИЩАЕМ EXTERNAL
        }

        public void UpdateArrows()
        {
            ArrowCalculator.SetAllBlocks(GetAllBlocks());

            foreach (var arrow in activeArrows)
            {
                UpdateSingleArrow(arrow);
            }
        }

        private Dictionary<string, DiagramBlock> GetAllBlocks()
        {
            var blocks = new Dictionary<string, DiagramBlock>();
            foreach (var arrow in activeArrows)
            {
                if (arrow.FromBlock != null && !blocks.ContainsKey(arrow.FromBlock.Code))
                    blocks[arrow.FromBlock.Code] = arrow.FromBlock;
                if (arrow.ToBlock != null && !blocks.ContainsKey(arrow.ToBlock.Code))
                    blocks[arrow.ToBlock.Code] = arrow.ToBlock;
            }
            return blocks;
        }

        private void UpdateSingleArrow(DiagramArrow arrow)
        {
            if (arrow == null) return;
            var newSegments = ArrowCalculator.CalculateArrowPath(
                arrow.FromBlock, arrow.ToBlock, arrow.ArrowType ?? "connect",
                arrow.IndexOnSide, arrow.TotalOnSide);

            if (newSegments == null || newSegments.Count == 0) return;

            while (arrow.Lines.Count < newSegments.Count)
            {
                Line newLine = new Line
                {
                    Stroke = style.Line,
                    StrokeThickness = 2
                };
                Panel.SetZIndex(newLine, 50);
                canvas.Children.Add(newLine);
                arrow.Lines.Add(newLine);
            }
            while (arrow.Lines.Count > newSegments.Count)
            {
                Line extraLine = arrow.Lines[arrow.Lines.Count - 1];
                canvas.Children.Remove(extraLine);
                arrow.Lines.RemoveAt(arrow.Lines.Count - 1);
            }
            for (int i = 0; i < newSegments.Count; i++)
            {
                arrow.Lines[i].X1 = newSegments[i].Start.X;
                arrow.Lines[i].Y1 = newSegments[i].Start.Y;
                arrow.Lines[i].X2 = newSegments[i].End.X;
                arrow.Lines[i].Y2 = newSegments[i].End.Y;
            }
            if (arrow.ArrowHead != null && newSegments.Count > 0)
            {
                var lastSegment = newSegments[newSegments.Count - 1];
                UpdateArrowHead(arrow.ArrowHead, lastSegment.End, lastSegment.Direction);
                arrow.ArrowHead.Fill = style.Line;
            }
            if (arrow.Label != null && newSegments.Count > 0)
            {
                Point newLabelPos = CalculateLabelPosition(newSegments);
                Canvas.SetLeft(arrow.Label, newLabelPos.X);
                Canvas.SetTop(arrow.Label, newLabelPos.Y);
                arrow.Label.Foreground = style.Text;
            }
        }

        private Polygon CreateArrowHead(Point tip, string direction)
        {
            PointCollection points = new PointCollection();
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

        private void UpdateArrowHead(Polygon arrowHead, Point tip, string direction)
        {
            PointCollection points = new PointCollection();
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
            arrowHead.Points = points;
            arrowHead.Fill = style.Line;
        }

        private Point CalculateLabelPosition(List<ArrowSegment> segments)
        {
            if (segments.Count == 0)
                return new Point(0, 0);

            ArrowSegment middleSegment = segments[segments.Count / 2];
            double midX = (middleSegment.Start.X + middleSegment.End.X) / 2;
            double midY = (middleSegment.Start.Y + middleSegment.End.Y) / 2;

            return new Point(midX - 30, midY - 20);
        }

    }
}
