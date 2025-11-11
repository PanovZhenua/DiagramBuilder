using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DiagramBuilder.Models;

namespace DiagramBuilder.Services.Rendering
{
    /// <summary>
    /// Рендерер диаграмм IDEF3 с поддержкой перетаскивания
    /// </summary>
    public class IDEF3Renderer
    {
        private readonly Canvas canvas;
        private readonly Dictionary<string, DiagramBlock> blocks;
        private readonly Dictionary<string, Junction> junctions = new Dictionary<string, Junction>();
        private readonly List<IDEF3Connection> connections = new List<IDEF3Connection>();

        public IDEF3Renderer(Canvas canvas, Dictionary<string, DiagramBlock> blocks)
        {
            this.canvas = canvas;
            this.blocks = blocks;
        }

        /// <summary>
        /// Отрисовка полной IDEF3 диаграммы
        /// </summary>
        public void RenderIDEF3(List<DiagramParser.UOWData> uows,
            List<DiagramParser.JunctionData> junctionData,
            List<DiagramParser.LinkData> links)
        {
            // Создаём UOW блоки (Unit of Work)
            foreach (var uow in uows)
            {
                var block = CreateUOWBlock(uow.Name, uow.Id, uow.X, uow.Y, uow.Width, uow.Height);
                blocks[uow.Id] = block;
            }

            // Создаём перекрёстки (Junction)
            foreach (var junc in junctionData)
            {
                CreateJunction(junc.Id, junc.Type, junc.X, junc.Y);
            }

            // Создаём связи
            foreach (var link in links)
            {
                CreateLink(link.From, link.To);
            }
        }

        /// <summary>
        /// Создаёт блок UOW (Unit of Work)
        /// </summary>
        private DiagramBlock CreateUOWBlock(string text, string id, double x, double y, double width, double height)
        {
            Border border = new Border
            {
                Width = width,
                Height = height,
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 224)),
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
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
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8, 6, 8, 6)
            };
            Grid.SetRow(textBlock, 0);

            TextBlock idBlock = new TextBlock
            {
                Text = id,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            };
            Grid.SetRow(idBlock, 1);

            grid.Children.Add(textBlock);
            grid.Children.Add(idBlock);
            border.Child = grid;

            Canvas.SetLeft(border, x);
            Canvas.SetTop(border, y);
            Panel.SetZIndex(border, 100);
            canvas.Children.Add(border);

            return new DiagramBlock
            {
                Visual = border,
                Label = textBlock,
                CodeLabel = idBlock,
                Code = id,
                Text = text
            };
        }

        /// <summary>
        /// Создаёт перекрёсток (Junction) - AND, OR, XOR
        /// </summary>
        private void CreateJunction(string id, string type, double x, double y)
        {
            Ellipse circle = new Ellipse
            {
                Width = 40,
                Height = 40,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 2,
                Cursor = Cursors.Hand
            };

            TextBlock label = new TextBlock
            {
                Text = type,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                TextAlignment = TextAlignment.Center
            };

            Canvas.SetLeft(circle, x);
            Canvas.SetTop(circle, y);
            Canvas.SetLeft(label, x + 5);
            Canvas.SetTop(label, y + 12);
            Panel.SetZIndex(circle, 100);
            Panel.SetZIndex(label, 101);

            canvas.Children.Add(circle);
            canvas.Children.Add(label);

            junctions[id] = new Junction
            {
                Visual = circle,
                Label = label,
                Id = id,
                Type = type,
                X = x,
                Y = y
            };
        }

        /// <summary>
        /// Создаёт связь между элементами
        /// </summary>
        private void CreateLink(string fromId, string toId)
        {
            Point start = GetConnectionPoint(fromId);
            Point end = GetConnectionPoint(toId);

            Line line = new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = Brushes.Black,
                StrokeThickness = 1.5
            };

            // Наконечник стрелки
            Polygon arrowHead = CreateArrowHead(end, start);

            Panel.SetZIndex(line, 50);
            Panel.SetZIndex(arrowHead, 51);
            canvas.Children.Add(line);
            canvas.Children.Add(arrowHead);

            connections.Add(new IDEF3Connection
            {
                Line = line,
                ArrowHead = arrowHead,
                FromId = fromId,
                ToId = toId
            });
        }

        /// <summary>
        /// Обновляет все связи (при перемещении элементов)
        /// </summary>
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

                UpdateArrowHead(conn.ArrowHead, end, start);
            }
        }

        /// <summary>
        /// Получает точку подключения для элемента (блок или junction)
        /// </summary>
        private Point GetConnectionPoint(string id)
        {
            if (blocks.ContainsKey(id))
            {
                var block = blocks[id];
                return new Point(
                    Canvas.GetLeft(block.Visual) + block.Visual.Width / 2,
                    Canvas.GetTop(block.Visual) + block.Visual.Height / 2
                );
            }
            else if (junctions.ContainsKey(id))
            {
                var j = junctions[id];
                return new Point(j.X + 20, j.Y + 20);
            }
            return new Point(0, 0);
        }

        /// <summary>
        /// Создаёт наконечник стрелки
        /// </summary>
        private Polygon CreateArrowHead(Point tip, Point from)
        {
            double angle = Math.Atan2(tip.Y - from.Y, tip.X - from.X);
            double arrowSize = 8;

            Point p1 = new Point(
                tip.X - arrowSize * Math.Cos(angle - Math.PI / 6),
                tip.Y - arrowSize * Math.Sin(angle - Math.PI / 6)
            );
            Point p2 = new Point(
                tip.X - arrowSize * Math.Cos(angle + Math.PI / 6),
                tip.Y - arrowSize * Math.Sin(angle + Math.PI / 6)
            );

            return new Polygon
            {
                Fill = Brushes.Black,
                Points = new PointCollection { tip, p1, p2 }
            };
        }

        /// <summary>
        /// Обновляет наконечник стрелки
        /// </summary>
        private void UpdateArrowHead(Polygon arrowHead, Point tip, Point from)
        {
            double angle = Math.Atan2(tip.Y - from.Y, tip.X - from.X);
            double arrowSize = 8;

            Point p1 = new Point(
                tip.X - arrowSize * Math.Cos(angle - Math.PI / 6),
                tip.Y - arrowSize * Math.Sin(angle - Math.PI / 6)
            );
            Point p2 = new Point(
                tip.X - arrowSize * Math.Cos(angle + Math.PI / 6),
                tip.Y - arrowSize * Math.Sin(angle + Math.PI / 6)
            );

            arrowHead.Points = new PointCollection { tip, p1, p2 };
        }

        /// <summary>
        /// Подключает события перетаскивания к junction
        /// </summary>
        public void AttachJunctionDragEvents(string junctionId,
            System.Action<string> onDragCallback)
        {
            if (!junctions.ContainsKey(junctionId))
                return;

            var junction = junctions[junctionId];
            Point dragStart = new Point();
            bool isDragging = false;

            junction.Visual.MouseLeftButtonDown += (s, e) =>
            {
                isDragging = true;
                dragStart = e.GetPosition(canvas);
                junction.Visual.CaptureMouse();
                e.Handled = true;
            };

            junction.Visual.MouseMove += (s, e) =>
            {
                if (!isDragging) return;

                Point currentPos = e.GetPosition(canvas);
                Vector offset = currentPos - dragStart;

                junction.X += offset.X;
                junction.Y += offset.Y;

                Canvas.SetLeft(junction.Visual, junction.X);
                Canvas.SetTop(junction.Visual, junction.Y);
                Canvas.SetLeft(junction.Label, junction.X + 5);
                Canvas.SetTop(junction.Label, junction.Y + 12);

                dragStart = currentPos;
                onDragCallback?.Invoke(junctionId);
                e.Handled = true;
            };

            junction.Visual.MouseLeftButtonUp += (s, e) =>
            {
                isDragging = false;
                junction.Visual.ReleaseMouseCapture();
            };
        }

        // ==================== NESTED CLASSES ====================

        public class Junction
        {
            public Ellipse Visual { get; set; }
            public TextBlock Label { get; set; }
            public string Id { get; set; }
            public string Type { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
        }

        public class IDEF3Connection
        {
            public Line Line { get; set; }
            public Polygon ArrowHead { get; set; }
            public string FromId { get; set; }
            public string ToId { get; set; }
        }
    }
}
