using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DiagramBuilder.Models.Blocks
{
    public class DFDProcess
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }

        public static FrameworkElement CreateVisual(DFDProcess proc)
        {
            var textBlock = new TextBlock
            {
                Text = proc.Name,
                FontSize = 14,
                FontWeight = FontWeights.Normal,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.Black,
                Padding = new Thickness(10, 4, 10, 4),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 128,
                IsHitTestVisible = false
            };
            textBlock.Measure(new Size(textBlock.MaxWidth, double.PositiveInfinity));
            double width = System.Math.Max(textBlock.DesiredSize.Width + 20, 92);
            double height = System.Math.Max(textBlock.DesiredSize.Height + 20, 34);

            var border = new Border
            {
                Width = width,
                Height = height,
                Background = Brushes.White,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1.8),
                CornerRadius = new CornerRadius(height / 2),
                Child = textBlock
            };
            return border;
        }

        public static Rect GetTextAreaRect(DFDProcess proc)
        {
            var border = CreateVisual(proc) as Border;
            var tb = (TextBlock)border.Child;
            Point absPos = new Point(proc.X + tb.Padding.Left, proc.Y + tb.Padding.Top);
            double w = border.Width - tb.Padding.Left - tb.Padding.Right;
            double h = border.Height - tb.Padding.Top - tb.Padding.Bottom;
            return new Rect(absPos, new Size(w, h));
        }
    }

    public class DFDEntity
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }

        public static FrameworkElement CreateVisual(DFDEntity entity)
        {
            var textBlock = new TextBlock
            {
                Text = entity.Name,
                FontSize = 14,
                FontWeight = FontWeights.Normal,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.Black,
                Padding = new Thickness(5, 2, 8, 2),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 120,
                IsHitTestVisible = false
            };
            textBlock.Measure(new Size(textBlock.MaxWidth, double.PositiveInfinity));
            double width = System.Math.Max(textBlock.DesiredSize.Width + 24, 92);
            double height = System.Math.Max(textBlock.DesiredSize.Height + 12, 34);

            var canvasEntity = new Canvas
            {
                Width = width,
                Height = height,
                Background = Brushes.Transparent
            };

            // аккуратный прямоугольник (без оффсетов)
            var border = new Border
            {
                Width = width,
                Height = height,
                Background = Brushes.White,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1.8),
                CornerRadius = new CornerRadius(0)
            };
            Canvas.SetLeft(border, 0);
            Canvas.SetTop(border, 0);
            canvasEntity.Children.Add(border);

            // Тень: линия слева толщиной как border
            var shadowLeft = new Line
            {
                X1 = 0.9,
                Y1 = 0.9,
                X2 = 0.9,
                Y2 = height - 0.9,
                Stroke = Brushes.Black,
                StrokeThickness = 3.2 // 2×BorderThickness (чётко)
            };
            canvasEntity.Children.Add(shadowLeft);

            // Тень: линия сверху
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

            // текст
            double textLeft = 6;
            double textWidth = width - textLeft - 6;
            textBlock.Width = textWidth > 0 ? textWidth : 0;
            Canvas.SetLeft(textBlock, textLeft);
            Canvas.SetTop(textBlock, height / 2 - textBlock.DesiredSize.Height / 2);
            canvasEntity.Children.Add(textBlock);

            return canvasEntity;
        }

        public static Rect GetTextAreaRect(DFDEntity entity)
        {
            var dummy = CreateVisual(entity) as Canvas;
            double leftPad = 6; // лев. отступ из CreateVisual
            double topPad = 2;
            double w = dummy.Width - leftPad - 6;
            double h = dummy.Height - 4;
            return new Rect(entity.X + leftPad, entity.Y + topPad, w, h);
        }
    }

    public class DFDStore
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }

        public static FrameworkElement CreateVisual(DFDStore store)
        {
            double barX = 6.0; // небольшой отступ от левого края до полоски
            double barThickness = 1.8;
            double sidePad = 10.0;

            var textBlock = new TextBlock
            {
                Text = store.Name,
                FontSize = 14,
                FontWeight = FontWeights.Normal,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.Black,
                Padding = new Thickness(0, 4, 0, 4),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 140,
                IsHitTestVisible = false
            };
            // Сначала "жирно" рассчитаем габариты для текста с учетом макс ширины
            textBlock.Measure(new Size(textBlock.MaxWidth, double.PositiveInfinity));
            double textWidth = textBlock.DesiredSize.Width;
            double textHeight = textBlock.DesiredSize.Height;

            // width строим так: полоска + отступ (barX+sidePad) + текст + отступ справа (sidePad)
            double width = barX + sidePad + textWidth + sidePad;
            double height = System.Math.Max(textHeight + 12, 36);

            var canvasStore = new Canvas
            {
                Width = width,
                Height = height,
                Background = Brushes.Transparent
            };

            // основной прямоугольник
            var border = new Border
            {
                Width = width,
                Height = height,
                Background = Brushes.White,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(barThickness),
                CornerRadius = new CornerRadius(0)
            };
            Canvas.SetLeft(border, 0);
            Canvas.SetTop(border, 0);
            canvasStore.Children.Add(border);

            // ЛЕВАЯ полоса с зазором
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

            // Текст: НЕ фиксируем ширину ― только максималку!
            Canvas.SetLeft(textBlock, barX + sidePad);
            Canvas.SetTop(textBlock, (height - textHeight) / 2);
            canvasStore.Children.Add(textBlock);

            return canvasStore;
        }

        public static Rect GetTextAreaRect(DFDStore store)
        {
            var dummy = CreateVisual(store) as Canvas;
            double barX = 6.0;
            double sidePad = 10.0;
            double topPad = 4;
            double w = dummy.Width - barX - sidePad - sidePad;
            double h = dummy.Height - topPad - topPad;
            return new Rect(store.X + barX + sidePad, store.Y + topPad, w, h);
        }
    }


    public class DFDArrow
    {
        public string FromId { get; set; }
        public string ToId { get; set; }
        public string Label { get; set; }
    }

    public class DFDDiagram
    {
        public List<DFDProcess> Processes { get; set; }
        public List<DFDEntity> Entities { get; set; }
        public List<DFDStore> Stores { get; set; }
        public List<DFDArrow> Arrows { get; set; }

        public DFDDiagram()
        {
            Processes = new List<DFDProcess>();
            Entities = new List<DFDEntity>();
            Stores = new List<DFDStore>();
            Arrows = new List<DFDArrow>();
        }
    }
}
