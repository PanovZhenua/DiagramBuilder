using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DiagramBuilder.Models;
using DiagramBuilder.Services.Core;

namespace DiagramBuilder.Services.Rendering
{
    public abstract class BaseRenderer
    {
        protected readonly Canvas canvas;
        protected DiagramStyle style;

        // Для сохранения состояния стрелок при смене стиля
        protected Dictionary<string, ArrowState> arrowStates = new Dictionary<string, ArrowState>();

        protected BaseRenderer(Canvas canvas, DiagramStyle style = null)
        {
            this.canvas = canvas;
            this.style = style ?? DiagramStyle.GetStyle(DiagramStyleType.ClassicBlackWhite);
        }

        // ============= СОЗДАНИЕ ВНЕШНИХ БЛОКОВ =============

        protected DiagramBlock EnsureExternalBlock(Dictionary<string, DiagramBlock> blocks, string code, string side)
        {
            if (blocks.ContainsKey(code))
                return blocks[code];

            double x = 0, y = 0;
            int pad = 100;
            int cw = (int)(canvas.Width > 0 ? canvas.Width : 1200);
            int ch = (int)(canvas.Height > 0 ? canvas.Height : 700);

            switch (side?.ToLower())
            {
                case "left": x = pad; y = ch / 2; break;
                case "right": x = cw - pad; y = ch / 2; break;
                case "top": x = cw / 2; y = pad; break;
                case "bottom": x = cw / 2; y = ch - pad; break;
                default: x = pad; y = ch / 2; break;
            }

            var border = new Border { Width = 1, Height = 1, Visibility = Visibility.Hidden };
            Canvas.SetLeft(border, x);
            Canvas.SetTop(border, y);
            canvas.Children.Add(border);

            var extBlock = new DiagramBlock { Visual = border, Code = code, X = x, Y = y };
            blocks[code] = extBlock;
            return extBlock;
        }

        // ============= СОЗДАНИЕ БЛОКОВ =============

        public virtual Border CreateBlockVisual(string text, string code, double x, double y, double width = 200, double height = 80)
        {
            double finalHeight;
            List<string> lines;
            (finalHeight, lines) = TextFormatterHelper.CalculateTextSize(text, width - 16, height);

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
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var textBlock = TextFormatterHelper.CreateAutoWrapTextBlock(text, style, width - 16);
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
            Canvas.SetLeft(border, x);
            Canvas.SetTop(border, y);
            canvas.Children.Add(border);
            Panel.SetZIndex(border, 100);

            return border;
        }

        // ============= СОЗДАНИЕ СТРЕЛОК =============

        protected List<Line> CreateArrowLines(List<ArrowSegment> segments)
        {
            var lines = new List<Line>();
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
                Panel.SetZIndex(line, 10);
                canvas.Children.Add(line);
                lines.Add(line);
            }
            return lines;
        }

        protected void UpdateArrowLines(List<Line> lines, List<ArrowSegment> segments)
        {
            if (lines == null || segments == null) return;

            while (lines.Count < segments.Count)
            {
                var newLine = new Line { Stroke = style.Line, StrokeThickness = 2 };
                Panel.SetZIndex(newLine, 10);
                canvas.Children.Add(newLine);
                lines.Add(newLine);
            }

            while (lines.Count > segments.Count)
            {
                canvas.Children.Remove(lines[lines.Count - 1]);
                lines.RemoveAt(lines.Count - 1);
            }

            for (int i = 0; i < segments.Count && i < lines.Count; i++)
            {
                lines[i].X1 = segments[i].Start.X;
                lines[i].Y1 = segments[i].Start.Y;
                lines[i].X2 = segments[i].End.X;
                lines[i].Y2 = segments[i].End.Y;
                lines[i].Stroke = style.Line; // Обновляем цвет при смене стиля
            }
        }

        public virtual Polygon CreateArrowHead(Point tip, string direction)
        {
            var points = new PointCollection();
            BuildArrowHeadPoints(tip, direction, points);
            var polygon = new Polygon { Fill = style.Line, Points = points };
            Panel.SetZIndex(polygon, 20);
            return polygon;
        }

        public virtual void UpdateArrowHead(Polygon arrowHead, Point tip, string direction)
        {
            if (arrowHead == null) return;
            var points = new PointCollection();
            BuildArrowHeadPoints(tip, direction, points);
            arrowHead.Points = points;
            arrowHead.Fill = style.Line;
        }

        private void BuildArrowHeadPoints(Point tip, string direction, PointCollection points)
        {
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
        }

        // ============= ПОДПИСИ СТРЕЛОК =============

        public virtual Point CalculateLabelPosition(List<ArrowSegment> segments)
        {
            if (segments == null || segments.Count == 0)
                return new Point(0, 0);

            var mid = segments[segments.Count / 2];
            double x = (mid.Start.X + mid.End.X) / 2;
            double y = (mid.Start.Y + mid.End.Y) / 2;
            return new Point(x, y - 20);
        }

        protected TextBlock CreateArrowLabel(string text, Point position)
        {
            if (string.IsNullOrEmpty(text)) return null;

            var label = new TextBlock
            {
                Text = text.Replace("\\n", "\n"),
                FontSize = 10,
                FontWeight = FontWeights.Medium,
                FontStyle = FontStyles.Italic,
                Foreground = style.Text,
                Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                Padding = new Thickness(3, 1, 3, 1)
            };
            Canvas.SetLeft(label, position.X - 50);
            Canvas.SetTop(label, position.Y);
            Panel.SetZIndex(label, 25);
            canvas.Children.Add(label);
            return label;
        }

        protected void UpdateArrowLabel(TextBlock label, Point position)
        {
            if (label != null)
            {
                Canvas.SetLeft(label, position.X - 50);
                Canvas.SetTop(label, position.Y);
                label.Foreground = style.Text;
                label.Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
            }
        }

        // ============= ОБНОВЛЕНИЕ СТРЕЛОК ПРИ ПЕРЕМЕЩЕНИИ =============

        public virtual void UpdateArrowsForBlock(DiagramArrow arrow)
        {
            if (arrow?.FromBlock == null || arrow.ToBlock == null) return;

            var newSegments = ArrowCalculator.CalculateArrowPath(
                arrow.FromBlock, arrow.ToBlock,
                arrow.ArrowType ?? "connect",
                arrow.IndexOnSide, arrow.TotalOnSide);

            if (newSegments == null || newSegments.Count == 0) return;

            UpdateArrowLines(arrow.Lines, newSegments);

            if (arrow.ArrowHead != null)
            {
                var lastSeg = newSegments[newSegments.Count - 1];
                UpdateArrowHead(arrow.ArrowHead, lastSeg.End, lastSeg.Direction);
            }

            if (arrow.Label != null)
            {
                var labelPos = CalculateLabelPosition(newSegments);
                UpdateArrowLabel(arrow.Label, labelPos);
            }
        }

        // ============= ЦИКЛИЧЕСКИЕ СТРЕЛКИ (ОГИБАНИЕ) =============

        public virtual List<ArrowSegment> CalculateCyclicArrowPath(
            DiagramBlock fromBlock, DiagramBlock toBlock,
            Dictionary<string, DiagramBlock> allBlocks)
        {
            var segments = new List<ArrowSegment>();

            double x1 = fromBlock.Left;
            double y1 = fromBlock.Top;
            double x2 = toBlock.Left;
            double y2 = toBlock.Top;

            double minX = allBlocks.Values.Min(b => b.Left) - 80;
            double maxX = allBlocks.Values.Max(b => b.Right) + 80;
            double minY = allBlocks.Values.Min(b => b.Top) - 80;

            // Огибающая стрелка слева и сверху
            double leftGap = x1 - 100;
            double topGap = minY - 100;

            // Начало: от правой грани fromBlock вверх-влево
            segments.Add(new ArrowSegment
            {
                Start = fromBlock.BottomPoint,
                End = new Point(fromBlock.Center.X, y1 + 100),
                Direction = "up"
            });

            // Поворот влево
            segments.Add(new ArrowSegment
            {
                Start = new Point(fromBlock.Center.X, y1 + 100),
                End = new Point(leftGap, y1 + 100),
                Direction = "left"
            });

            // Поворот вверх
            segments.Add(new ArrowSegment
            {
                Start = new Point(leftGap, y1 + 100),
                End = new Point(leftGap, topGap),
                Direction = "up"
            });

            // Поворот вправо
            segments.Add(new ArrowSegment
            {
                Start = new Point(leftGap, topGap),
                End = new Point(x2 + toBlock.Width / 2, topGap),
                Direction = "right"
            });

            // Финальный сегмент к toBlock
            segments.Add(new ArrowSegment
            {
                Start = new Point(x2 + toBlock.Width / 2, topGap),
                End = toBlock.TopPoint,
                Direction = "down"
            });

            return segments;
        }

        // ============= СОХРАНЕНИЕ СОСТОЯНИЯ СТРЕЛОК =============

        protected void SaveArrowState(string key, DiagramArrow arrow)
        {
            var state = new ArrowState
            {
                FromId = arrow.FromId,
                ToId = arrow.ToId,
                LabelText = arrow.Label?.Text,
                ArrowType = arrow.ArrowType,
                IndexOnSide = arrow.IndexOnSide,
                TotalOnSide = arrow.TotalOnSide
            };
            arrowStates[key] = state;
        }

        protected ArrowState GetArrowState(string key)
        {
            return arrowStates.ContainsKey(key) ? arrowStates[key] : null;
        }

        public virtual void Clear()
        {
            canvas.Children.Clear();
            arrowStates.Clear();
        }

        // ============= ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ =============

        protected class ArrowState
        {
            public string FromId { get; set; }
            public string ToId { get; set; }
            public string LabelText { get; set; }
            public string ArrowType { get; set; }
            public int IndexOnSide { get; set; }
            public int TotalOnSide { get; set; }
        }
    }
}
