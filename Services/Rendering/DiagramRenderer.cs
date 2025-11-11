using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DiagramBuilder.Models;
using DiagramBuilder.Services.Core;

namespace DiagramBuilder.Services.Rendering
{
    /// <summary>
    /// Рендерер диаграмм IDEF0
    /// </summary>
    public class DiagramRenderer
    {
        private readonly Canvas canvas;
        private readonly List<DiagramArrow> activeArrows = new List<DiagramArrow>();

        public DiagramRenderer(Canvas canvas)
        {
            this.canvas = canvas;
        }

        /// <summary>
        /// Создаёт блок диаграммы IDEF0
        /// </summary>
        public DiagramBlock CreateBlock(string text, string code, double x, double y, double width, double height)
        {
            text = text.Replace("\\n", "\n");

            Border blockBorder = new Border
            {
                Width = width,
                Height = height,
                Background = Brushes.White,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(3),
                Cursor = Cursors.Hand
            };

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock textBlock = new TextBlock
            {
                Text = text,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8, 6, 8, 6)
            };
            Grid.SetRow(textBlock, 0);

            TextBlock codeBlock = new TextBlock
            {
                Text = code,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.DarkBlue,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 5, 3)
            };
            Grid.SetRow(codeBlock, 1);

            grid.Children.Add(textBlock);
            grid.Children.Add(codeBlock);
            blockBorder.Child = grid;

            Canvas.SetLeft(blockBorder, x);
            Canvas.SetTop(blockBorder, y);
            Panel.SetZIndex(blockBorder, 100);
            canvas.Children.Add(blockBorder);

            DiagramBlock block = new DiagramBlock
            {
                Visual = blockBorder,
                Label = textBlock,
                CodeLabel = codeBlock,
                Code = code,
                Text = text
            };

            return block;
        }

        /// <summary>
        /// Создаёт стрелку между блоками
        /// </summary>
        public DiagramArrow CreateArrow(DiagramBlock fromBlock, DiagramBlock toBlock,
    string fromId, string toId, string labelText, string arrowType)
        {
            labelText = labelText?.Replace("\\n", "\n");

            // Вычисляем путь стрелки
            var segments = ArrowCalculator.CalculateArrowPath(fromBlock, toBlock, arrowType);

            if (segments == null || segments.Count == 0)
                return null;

            List<Line> lines = new List<Line>();

            // Создаём линию для КАЖДОГО сегмента
            foreach (var segment in segments)
            {
                Line line = new Line
                {
                    X1 = segment.Start.X,
                    Y1 = segment.Start.Y,
                    X2 = segment.End.X,
                    Y2 = segment.End.Y,
                    Stroke = Brushes.Black,
                    StrokeThickness = 2
                };
                Panel.SetZIndex(line, 50);
                canvas.Children.Add(line);
                lines.Add(line);
            }

            // Создаём наконечник стрелки
            var lastSegment = segments[segments.Count - 1];
            Polygon arrowHead = CreateArrowHead(lastSegment.End, lastSegment.Direction);
            Panel.SetZIndex(arrowHead, 51);
            canvas.Children.Add(arrowHead);

            // Создаём подпись
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
                    Foreground = Brushes.Black,
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

            // ВАЖНО: Добавляем ВСЕ стрелки в activeArrows (включая left, right, top, bottom)
            activeArrows.Add(arrow);
            return arrow;
        }

        /// <summary>
        /// Обновляет все стрелки (при перемещении блоков)
        /// </summary>
        public void UpdateArrows()
        {
            // ВАЖНО: Обновляем allBlocks в ArrowCalculator перед обновлением стрелок
            // Это нужно для пересчёта min/max координат для top/bottom стрелок
            ArrowCalculator.SetAllBlocks(GetAllBlocks());

            foreach (var arrow in activeArrows)
            {
                UpdateSingleArrow(arrow);
            }
        }

        /// <summary>
        /// НОВЫЙ МЕТОД: Получает все блоки из activeArrows
        /// </summary>
        private Dictionary<string, DiagramBlock> GetAllBlocks()
        {
            var blocks = new Dictionary<string, DiagramBlock>();

            foreach (var arrow in activeArrows)
            {
                if (arrow.FromBlock != null && !blocks.ContainsKey(arrow.FromBlock.Code))
                {
                    blocks[arrow.FromBlock.Code] = arrow.FromBlock;
                }
                if (arrow.ToBlock != null && !blocks.ContainsKey(arrow.ToBlock.Code))
                {
                    blocks[arrow.ToBlock.Code] = arrow.ToBlock;
                }
            }

            return blocks;
        }

        /// <summary>
        /// Обновляет одну стрелку
        /// </summary>
        private void UpdateSingleArrow(DiagramArrow arrow)
        {
            if (arrow == null)
                return;

            // Пересчитываем путь стрелки
            var newSegments = ArrowCalculator.CalculateArrowPath(
                arrow.FromBlock, arrow.ToBlock, arrow.ArrowType ?? "connect");

            if (newSegments == null || newSegments.Count == 0)
                return;

            // Синхронизируем количество линий
            while (arrow.Lines.Count < newSegments.Count)
            {
                Line newLine = new Line
                {
                    Stroke = Brushes.Black,
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

            // Обновляем координаты всех линий
            for (int i = 0; i < newSegments.Count; i++)
            {
                arrow.Lines[i].X1 = newSegments[i].Start.X;
                arrow.Lines[i].Y1 = newSegments[i].Start.Y;
                arrow.Lines[i].X2 = newSegments[i].End.X;
                arrow.Lines[i].Y2 = newSegments[i].End.Y;
            }

            // Обновляем наконечник
            if (arrow.ArrowHead != null && newSegments.Count > 0)
            {
                var lastSegment = newSegments[newSegments.Count - 1];
                UpdateArrowHead(arrow.ArrowHead, lastSegment.End, lastSegment.Direction);
            }

            // Обновляем подпись
            if (arrow.Label != null && newSegments.Count > 0)
            {
                Point newLabelPos = CalculateLabelPosition(newSegments);
                Canvas.SetLeft(arrow.Label, newLabelPos.X);
                Canvas.SetTop(arrow.Label, newLabelPos.Y);
            }
        }

        /// <summary>
        /// Создаёт наконечник стрелки
        /// </summary>
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
                Fill = Brushes.Black,
                Points = points
            };
        }

        /// <summary>
        /// Обновляет наконечник стрелки
        /// </summary>
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
        }

        /// <summary>
        /// Вычисляет позицию для подписи стрелки
        /// </summary>
        private Point CalculateLabelPosition(List<ArrowSegment> segments)
        {
            if (segments.Count == 0)
                return new Point(0, 0);

            ArrowSegment middleSegment = segments[segments.Count / 2];
            double midX = (middleSegment.Start.X + middleSegment.End.X) / 2;
            double midY = (middleSegment.Start.Y + middleSegment.End.Y) / 2;

            return new Point(midX - 30, midY - 20);
        }

        /// <summary>
        /// Очищает canvas
        /// </summary>
        public void Clear()
        {
            canvas.Children.Clear();
            activeArrows.Clear();
        }

        public DiagramArrow CreateArrowWithDistribution(
    DiagramBlock fromBlock, DiagramBlock toBlock,
    string fromId, string toId, string labelText, string arrowType,
    int indexOnSide, int totalOnSide)
        {
            labelText = labelText?.Replace("\\n", "\n");

            // Вычисляем путь с учётом индекса
            var segments = ArrowCalculator.CalculateArrowPath(
                fromBlock, toBlock, arrowType, indexOnSide, totalOnSide);

            if (segments == null || segments.Count == 0)
                return null;

            // Остальной код как в обычном CreateArrow...
            List<Line> lines = new List<Line>();

            foreach (var segment in segments)
            {
                Line line = new Line
                {
                    X1 = segment.Start.X,
                    Y1 = segment.Start.Y,
                    X2 = segment.End.X,
                    Y2 = segment.End.Y,
                    Stroke = Brushes.Black,
                    StrokeThickness = 2
                };
                Panel.SetZIndex(line, 50);
                canvas.Children.Add(line);
                lines.Add(line);
            }

            var lastSegment = segments[segments.Count - 1];
            Polygon arrowHead = CreateArrowHead(lastSegment.End, lastSegment.Direction);
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
                    Foreground = Brushes.Black,
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

    }
}
