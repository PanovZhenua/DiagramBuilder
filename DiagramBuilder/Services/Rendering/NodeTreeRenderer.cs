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
    public class NodeTreeRenderer
    {
        private readonly Canvas canvas;
        private readonly Dictionary<string, DiagramBlock> blocks;
        private readonly ConnectionManager connectionManager;
        private readonly DiagramStyle style;
        private const double BlockWidth = 200;
        private const double BlockHeight = 60;
        private const double MinHorizontalGap = 48; // Минимальное расстояние между блоками по горизонтали

        public NodeTreeRenderer(Canvas canvas, Dictionary<string, DiagramBlock> blocks,
            ConnectionManager connectionManager, DiagramStyle style)
        {
            this.canvas = canvas;
            this.blocks = blocks;
            this.connectionManager = connectionManager;
            this.style = style ?? DiagramStyle.GetStyle(DiagramStyleType.ClassicBlackWhite);
        }

        public void RenderNodeTree(List<DiagramParser.NodeData> nodes)
        {
            if (nodes == null) return;

            // Auto-layout с минимальным расстоянием между siblings
            var levelDict = new Dictionary<int, List<DiagramParser.NodeData>>();
            foreach (var node in nodes)
            {
                int depth = GetNodeDepth(nodes, node);
                if (!levelDict.ContainsKey(depth))
                    levelDict[depth] = new List<DiagramParser.NodeData>();
                levelDict[depth].Add(node);
            }

            double xStart = 80;
            double yGap = 110;

            foreach (var pair in levelDict.OrderBy(kvp => kvp.Key))
            {
                double y = pair.Key * (BlockHeight + yGap);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    var node = pair.Value[i];
                    double x = xStart + i * (BlockWidth + MinHorizontalGap);
                    node.X = x;
                    node.Y = y;
                }
            }

            // Рисуем блоки
            foreach (var node in nodes)
            {
                var block = CreateNodeBlock(node.Name, node.Code, node.X, node.Y);
                blocks[node.Code] = block;
            }

            // Cвязи между родителями и детьми
            foreach (var node in nodes.Where(n => !string.IsNullOrEmpty(n.Parent)))
            {
                if (blocks.ContainsKey(node.Parent) && blocks.ContainsKey(node.Code))
                {
                    CreateTreeConnection(blocks[node.Parent], blocks[node.Code]);
                }
            }
        }

        // --- Для автокомпоновки --- //
        private int GetNodeDepth(List<DiagramParser.NodeData> nodes, DiagramParser.NodeData node)
        {
            int depth = 0;
            string current = node.Parent;
            var byCode = nodes.ToDictionary(n => n.Code, n => n);
            while (!string.IsNullOrEmpty(current) && byCode.ContainsKey(current))
            {
                depth++;
                current = byCode[current].Parent;
            }
            return depth;
        }

        private DiagramBlock CreateNodeBlock(string text, string code, double x, double y)
        {
            Border border = new Border
            {
                Width = BlockWidth,
                Height = BlockHeight,
                Background = style.BlockFill,
                BorderBrush = style.BlockBorder,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(6),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = (style.BlockShadow as SolidColorBrush).Color,
                    BlurRadius = 7,
                    Opacity = 0.35,
                    Direction = 320,
                    ShadowDepth = 3
                },
                Cursor = Cursors.Hand
            };

            Grid grid = new Grid
            {
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock textBlock = new TextBlock
            {
                Text = text,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = style.Text,
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(8, 6, 8, 8),   // <--- именно Padding, не Margin!
                                                       // увеличенный нижний padding для "воздуха" под второй строкой
                                                       // Margin = new Thickness(0)
            };
            Grid.SetRow(textBlock, 0);

            TextBlock codeBlock = new TextBlock
            {
                Text = code,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = style.CodeText,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
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

        private void CreateTreeConnection(DiagramBlock parent, DiagramBlock child)
        {
            double parentCenterX = Canvas.GetLeft(parent.Visual) + parent.Visual.Width / 2;
            double parentBottom = Canvas.GetTop(parent.Visual) + parent.Visual.Height;

            double childCenterX = Canvas.GetLeft(child.Visual) + child.Visual.Width / 2;
            double childTop = Canvas.GetTop(child.Visual);

            double midY = parentBottom + 28;

            List<Line> lines = new List<Line>();

            Line line1 = CreateLine(
                new Point(parentCenterX, parentBottom),
                new Point(parentCenterX, midY));
            lines.Add(line1);

            Line line2 = CreateLine(
                new Point(parentCenterX, midY),
                new Point(childCenterX, midY));
            lines.Add(line2);

            Line line3 = CreateLine(
                new Point(childCenterX, midY),
                new Point(childCenterX, childTop));
            lines.Add(line3);

            connectionManager.AddConnection(parent, child, lines);
        }

        private Line CreateLine(Point start, Point end)
        {
            Line line = new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = style.Line,
                StrokeThickness = 2
            };
            canvas.Children.Add(line);
            return line;
        }

        public void UpdateConnections()
        {
            connectionManager.UpdateAllConnections();
        }
    }
}
