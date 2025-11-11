using System.Collections.Generic;
using System.Linq;
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
    /// Рендерер древовидных диаграмм (NodeTree)
    /// С привязкой линий к середине верхней/нижней части блоков
    /// </summary>
    public class NodeTreeRenderer
    {
        private readonly Canvas canvas;
        private readonly Dictionary<string, DiagramBlock> blocks;
        private readonly ConnectionManager connectionManager;

        public NodeTreeRenderer(Canvas canvas, Dictionary<string, DiagramBlock> blocks,
            ConnectionManager connectionManager)
        {
            this.canvas = canvas;
            this.blocks = blocks;
            this.connectionManager = connectionManager;
        }

        /// <summary>
        /// Отрисовывает дерево узлов
        /// </summary>
        public void RenderNodeTree(List<DiagramParser.NodeData> nodes)
        {
            // Сначала создаём все узлы
            foreach (var node in nodes)
            {
                var block = CreateNodeBlock(node.Name, node.Code, node.X, node.Y);
                blocks[node.Code] = block;
            }

            // Затем создаём связи между родителями и детьми
            foreach (var node in nodes.Where(n => !string.IsNullOrEmpty(n.Parent)))
            {
                if (blocks.ContainsKey(node.Parent) && blocks.ContainsKey(node.Code))
                {
                    CreateTreeConnection(blocks[node.Parent], blocks[node.Code]);
                }
            }
        }

        /// <summary>
        /// Создаёт блок узла дерева
        /// </summary>
        private DiagramBlock CreateNodeBlock(string text, string code, double x, double y)
        {
            Border border = new Border
            {
                Width = 200,
                Height = 60,
                Background = new SolidColorBrush(Color.FromRgb(230, 240, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 130, 180)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(5),
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
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(70, 130, 180)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            Grid.SetRow(codeBlock, 1);

            grid.Children.Add(textBlock);
            grid.Children.Add(codeBlock);
            border.Child = grid;

            Canvas.SetLeft(border, x);
            Canvas.SetTop(border, y);
            Panel.SetZIndex(border, 100);
            canvas.Children.Add(border);

            return new DiagramBlock
            {
                Visual = border,
                Label = textBlock,
                CodeLabel = codeBlock,
                Code = code,
                Text = text
            };
        }

        /// <summary>
        /// Создаёт связь между родительским и дочерним узлом
        /// ИСПРАВЛЕНО: привязка к середине верхней и нижней части
        /// </summary>
        private void CreateTreeConnection(DiagramBlock parent, DiagramBlock child)
        {
            // Вычисляем координаты с привязкой к середине
            double parentCenterX = Canvas.GetLeft(parent.Visual) + parent.Visual.Width / 2;
            double parentBottom = Canvas.GetTop(parent.Visual) + parent.Visual.Height;

            double childCenterX = Canvas.GetLeft(child.Visual) + child.Visual.Width / 2;
            double childTop = Canvas.GetTop(child.Visual);

            double midY = parentBottom + 30; // Промежуточная точка по Y

            List<Line> lines = new List<Line>();

            // 1. Вертикальная линия от родителя (из середины нижней части)
            Line line1 = CreateLine(
                new Point(parentCenterX, parentBottom),
                new Point(parentCenterX, midY));
            Panel.SetZIndex(line1, 50);
            lines.Add(line1);

            // 2. Горизонтальная линия
            Line line2 = CreateLine(
                new Point(parentCenterX, midY),
                new Point(childCenterX, midY));
            Panel.SetZIndex(line2, 50);
            lines.Add(line2);

            // 3. Вертикальная линия к ребёнку (в середину верхней части)
            Line line3 = CreateLine(
                new Point(childCenterX, midY),
                new Point(childCenterX, childTop));
            Panel.SetZIndex(line3, 50);
            lines.Add(line3);

            // Добавляем связь в ConnectionManager
            connectionManager.AddConnection(parent, child, lines);
        }

        /// <summary>
        /// Создаёт линию между двумя точками
        /// </summary>
        private Line CreateLine(Point start, Point end)
        {
            Line line = new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = new SolidColorBrush(Color.FromRgb(70, 130, 180)),
                StrokeThickness = 2
            };

            canvas.Children.Add(line);
            return line;
        }

        /// <summary>
        /// Обновляет все связи дерева
        /// </summary>
        public void UpdateConnections()
        {
            connectionManager.UpdateAllConnections();
        }
    }
}
