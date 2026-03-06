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
    public class UseCaseRenderer
    {
        private readonly Canvas canvas;
        private DiagramStyle style;

        public Dictionary<string, FrameworkElement> ActorVisuals { get; }
            = new Dictionary<string, FrameworkElement>();

        public Dictionary<string, FrameworkElement> UseCaseVisuals { get; }
            = new Dictionary<string, FrameworkElement>();

        public Dictionary<string, List<UIElement>> LinkVisuals { get; }
            = new Dictionary<string, List<UIElement>>();

        // Хранение надписей для drag-n-drop
        private List<TextBlock> linkLabels = new List<TextBlock>();

        public UseCaseRenderer(Canvas canvas, DiagramStyle style)
        {
            this.canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            this.style = style ?? DiagramStyle.GetStyle(DiagramStyleType.Presentation);
        }

        public void Clear()
        {
            // Удаляем все визуалы
            foreach (var visual in ActorVisuals.Values)
            {
                if (canvas.Children.Contains(visual))
                    canvas.Children.Remove(visual);
            }

            foreach (var visual in UseCaseVisuals.Values)
            {
                if (canvas.Children.Contains(visual))
                    canvas.Children.Remove(visual);
            }

            foreach (var linkParts in LinkVisuals.Values)
            {
                foreach (var part in linkParts)
                {
                    if (canvas.Children.Contains(part))
                        canvas.Children.Remove(part);
                }
            }

            ActorVisuals.Clear();
            UseCaseVisuals.Clear();
            LinkVisuals.Clear();
            linkLabels.Clear();
        }

        public void RenderDiagram(UseCaseDiagram diagram)
        {
            if (diagram == null) return;

            Clear();

            // Рисуем акторов
            foreach (var actor in diagram.Actors)
            {
                var visual = CreateActorVisual(actor);
                ActorVisuals[actor.Id] = visual;
                canvas.Children.Add(visual);
                Canvas.SetLeft(visual, actor.X);
                Canvas.SetTop(visual, actor.Y);
                Panel.SetZIndex(visual, 10);
            }

            // Рисуем use cases (овалы)
            foreach (var useCase in diagram.UseCases)
            {
                var visual = CreateUseCaseVisual(useCase);
                UseCaseVisuals[useCase.Id] = visual;
                canvas.Children.Add(visual);
                Canvas.SetLeft(visual, useCase.X);
                Canvas.SetTop(visual, useCase.Y);
                Panel.SetZIndex(visual, 10);
            }

            // Рисуем связи
            foreach (var link in diagram.Links)
            {
                var elements = CreateLinkVisual(link, diagram);
                LinkVisuals[link.Id] = elements;

                foreach (var elem in elements)
                {
                    Panel.SetZIndex(elem, 5);
                    canvas.Children.Add(elem);
                }
            }
        }

        // ========== СОЗДАНИЕ АКТОРА (ЧЕЛОВЕЧКА) ==========

        private FrameworkElement CreateActorVisual(UseCaseActor actor)
        {
            var container = new Canvas
            {
                Width = actor.Width,
                Height = actor.Height,
                Background = Brushes.Transparent
            };

            // Параметры человечка
            double centerX = actor.Width / 2.0;
            double headRadius = 12.0;
            double bodyHeight = 30.0;
            double armLength = 20.0;
            double legLength = 25.0;

            // Голова
            var head = new Ellipse
            {
                Width = headRadius * 2,
                Height = headRadius * 2,
                Stroke = style.BlockBorder,
                StrokeThickness = 2.0,
                Fill = Brushes.White
            };
            Canvas.SetLeft(head, centerX - headRadius);
            Canvas.SetTop(head, 5);
            container.Children.Add(head);

            double neckY = 5 + headRadius * 2;

            // Тело (вертикальная линия)
            var body = new Line
            {
                X1 = centerX,
                Y1 = neckY,
                X2 = centerX,
                Y2 = neckY + bodyHeight,
                Stroke = style.BlockBorder,
                StrokeThickness = 2.0
            };
            container.Children.Add(body);

            // Руки (горизонтальная линия)
            double armY = neckY + bodyHeight * 0.3;
            var arms = new Line
            {
                X1 = centerX - armLength,
                Y1 = armY,
                X2 = centerX + armLength,
                Y2 = armY,
                Stroke = style.BlockBorder,
                StrokeThickness = 2.0
            };
            container.Children.Add(arms);

            // Ноги (две линии)
            double bodyEndY = neckY + bodyHeight;
            var leftLeg = new Line
            {
                X1 = centerX,
                Y1 = bodyEndY,
                X2 = centerX - armLength * 0.7,
                Y2 = bodyEndY + legLength,
                Stroke = style.BlockBorder,
                StrokeThickness = 2.0
            };
            container.Children.Add(leftLeg);

            var rightLeg = new Line
            {
                X1 = centerX,
                Y1 = bodyEndY,
                X2 = centerX + armLength * 0.7,
                Y2 = bodyEndY + legLength,
                Stroke = style.BlockBorder,
                StrokeThickness = 2.0
            };
            container.Children.Add(rightLeg);

            // УЛУЧШЕННЫЙ ТЕКСТ С АВТОМАТИЧЕСКИМ ПЕРЕНОСОМ И УМЕНЬШЕНИЕМ РАЗМЕРА
            string text = actor.Name ?? "";
            int charCount = text.Length;
            bool hasSpaces = text.Contains(" ");

            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = style.Text,
                TextAlignment = TextAlignment.Center,
                Width = actor.Width,
                Background = Brushes.Transparent
            };

            // Логика размера шрифта и переноса
            if (charCount <= 10)
            {
                textBlock.FontSize = 14;
                textBlock.FontWeight = FontWeights.Normal;
                textBlock.TextWrapping = TextWrapping.NoWrap;
            }
            else if (hasSpaces)
            {
                // Если есть пробелы - переносим слова
                textBlock.FontSize = 12;
                textBlock.FontWeight = FontWeights.Normal;
                textBlock.TextWrapping = TextWrapping.Wrap;
            }
            else
            {
                // Если одно длинное слово - уменьшаем шрифт
                textBlock.FontSize = charCount > 15 ? 10 : 11;
                textBlock.FontWeight = FontWeights.Normal;
                textBlock.TextWrapping = TextWrapping.Wrap;
            }

            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            double textY = bodyEndY + legLength + 5;
            Canvas.SetLeft(textBlock, 0);
            Canvas.SetTop(textBlock, textY);
            container.Children.Add(textBlock);

            return container;
        }

        // ========== СОЗДАНИЕ USE CASE (ОВАЛА) ==========

        private FrameworkElement CreateUseCaseVisual(UseCaseElement useCase)
        {
            var ellipse = new Ellipse
            {
                Width = useCase.Width,
                Height = useCase.Height,
                Stroke = style.BlockBorder,
                StrokeThickness = 2.0,
                Fill = style.BlockFill
            };

            var textBlock = new TextBlock
            {
                Text = useCase.Name,
                FontSize = 14,
                FontWeight = FontWeights.Normal,
                Foreground = style.Text,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Width = useCase.Width - 20,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            var grid = new Grid
            {
                Width = useCase.Width,
                Height = useCase.Height
            };

            grid.Children.Add(ellipse);
            grid.Children.Add(textBlock);

            return grid;
        }

        // ========== СОЗДАНИЕ СВЯЗЕЙ ==========

        private List<UIElement> CreateLinkVisual(UseCaseLink link, UseCaseDiagram diagram)
        {
            var result = new List<UIElement>();

            // Получаем точки с отступом от границ
            Point fromPoint = GetElementEdgePoint(link.FromId, link.ToId, diagram);
            Point toPoint = GetElementEdgePoint(link.ToId, link.FromId, diagram);

            if (fromPoint == default || toPoint == default)
                return result;

            // Определяем тип линии
            bool isDashed = link.Type == "include" || link.Type == "extend";

            var line = new Line
            {
                X1 = fromPoint.X,
                Y1 = fromPoint.Y,
                X2 = toPoint.X,
                Y2 = toPoint.Y,
                Stroke = style.Line,
                StrokeThickness = 2.0
            };

            if (isDashed)
            {
                line.StrokeDashArray = new DoubleCollection { 5, 3 };
            }

            result.Add(line);

            // Добавляем наконечник
            var arrowHead = CreateArrowHead(fromPoint, toPoint, link.Type == "association");
            if (arrowHead != null)
                result.Add(arrowHead);

            // Добавляем текст для include/extend
            if (link.Type == "include" || link.Type == "extend")
            {
                double midX = (fromPoint.X + toPoint.X) / 2.0;
                double midY = (fromPoint.Y + toPoint.Y) / 2.0;

                var label = new TextBlock
                {
                    Text = $"<<{link.Type}>>",
                    FontSize = 12,
                    FontStyle = FontStyles.Italic,
                    Foreground = style.Text,
                    Background = Brushes.White,
                    Padding = new Thickness(2)
                };

                Canvas.SetLeft(label, midX + 5);
                Canvas.SetTop(label, midY - 10);
                Panel.SetZIndex(label, 15);

                result.Add(label);
                linkLabels.Add(label);  // Сохраняем для drag-n-drop
            }

            return result;
        }

        // Получение точки на краю элемента с учетом направления
        private Point GetElementEdgePoint(string elementId, string targetId, UseCaseDiagram diagram)
        {
            Point center = GetElementCenter(elementId, diagram);
            Point targetCenter = GetElementCenter(targetId, diagram);

            if (center == default || targetCenter == default)
                return default;

            // Вектор от элемента к цели
            Vector direction = targetCenter - center;
            if (direction.Length < 0.1)
                return center;

            direction.Normalize();

            // ОТСТУП ОТ КРАЯ (в пикселях)
            const double offset = 8.0;

            // Проверяем тип элемента
            var actor = diagram.Actors.FirstOrDefault(a => a.Id == elementId);
            if (actor != null)
            {
                // Для актора: прямоугольная граница с отступом
                double halfW = actor.Width / 2.0 + offset;
                double halfH = actor.Height / 2.0 + offset;

                double dx = direction.X;
                double dy = direction.Y;

                // Находим пересечение с прямоугольником
                double tx = dx == 0 ? double.MaxValue : halfW / Math.Abs(dx);
                double ty = dy == 0 ? double.MaxValue : halfH / Math.Abs(dy);
                double t = Math.Min(tx, ty);

                return new Point(center.X + direction.X * t, center.Y + direction.Y * t);
            }

            var useCase = diagram.UseCases.FirstOrDefault(u => u.Id == elementId);
            if (useCase != null)
            {
                // Для use case: точка на контуре эллипса с отступом
                double a = useCase.Width / 2.0 + offset;   // Полуось X с отступом
                double b = useCase.Height / 2.0 + offset;  // Полуось Y с отступом

                // Параметрическое уравнение эллипса
                double angle = Math.Atan2(direction.Y * a, direction.X * b);
                double edgeX = center.X + a * Math.Cos(angle);
                double edgeY = center.Y + b * Math.Sin(angle);

                return new Point(edgeX, edgeY);
            }

            return center;
        }

        // Создание наконечника стрелки
        private Polygon CreateArrowHead(Point from, Point to, bool isSimpleArrow = false)
        {
            Vector direction = to - from;
            if (direction.Length < 0.1) return null;

            direction.Normalize();
            Vector perpendicular = new Vector(-direction.Y, direction.X);

            const double arrowLength = 10.0;
            const double arrowWidth = 5.0;

            Point tip = to;
            Point left = tip - direction * arrowLength + perpendicular * arrowWidth;
            Point right = tip - direction * arrowLength - perpendicular * arrowWidth;

            var arrow = new Polygon
            {
                Points = new PointCollection { tip, left, right },
                Stroke = style.Line,
                StrokeThickness = 1.5
            };

            // Для association стрелка без заливки (просто контур)
            if (isSimpleArrow)
            {
                arrow.Fill = Brushes.White;
            }
            else
            {
                arrow.Fill = style.Line;  // Для include/extend закрашенная стрелка
            }

            return arrow;
        }

        // Получение центра элемента
        private Point GetElementCenter(string id, UseCaseDiagram diagram)
        {
            var actor = diagram.Actors.FirstOrDefault(a => a.Id == id);
            if (actor != null)
            {
                double centerX = actor.X + actor.Width / 2.0;
                double centerY = actor.Y + actor.Height / 2.0;
                return new Point(centerX, centerY);
            }

            var useCase = diagram.UseCases.FirstOrDefault(u => u.Id == id);
            if (useCase != null)
            {
                double centerX = useCase.X + useCase.Width / 2.0;
                double centerY = useCase.Y + useCase.Height / 2.0;
                return new Point(centerX, centerY);
            }

            return default;
        }

        // Получение всех визуалов (для drag-n-drop)
        public Dictionary<string, FrameworkElement> GetAllVisuals()
        {
            var all = new Dictionary<string, FrameworkElement>();

            foreach (var kv in ActorVisuals)
                all[kv.Key] = kv.Value;

            foreach (var kv in UseCaseVisuals)
                all[kv.Key] = kv.Value;

            return all;
        }

        // Получение надписей для drag-n-drop
        public List<TextBlock> GetLinkLabels()
        {
            return new List<TextBlock>(linkLabels);
        }

        // Обновление связей после перемещения элементов
        public void UpdateLinks(UseCaseDiagram diagram)
        {
            if (diagram == null) return;

            // Удаляем ВСЕ старые связи
            foreach (var linkParts in LinkVisuals.Values)
            {
                foreach (var part in linkParts)
                {
                    if (canvas.Children.Contains(part))
                        canvas.Children.Remove(part);
                }
            }

            LinkVisuals.Clear();
            linkLabels.Clear();

            // Рисуем заново
            foreach (var link in diagram.Links)
            {
                var elements = CreateLinkVisual(link, diagram);
                LinkVisuals[link.Id] = elements;

                foreach (var elem in elements)
                {
                    Panel.SetZIndex(elem, 5);
                    canvas.Children.Add(elem);
                }
            }
        }
    }
}
