// Services/Rendering/DocumentFlowRenderer.cs (ИСПРАВЛЕННАЯ ВЕРСИЯ)
using DiagramBuilder.Models;
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
    public class DocumentFlowRenderer
    {
        private readonly Canvas canvas;
        private readonly Dictionary<string, (FrameworkElement visual, string type, object data)> blockVisuals =
            new Dictionary<string, (FrameworkElement, string, object)>();
        private readonly List<TextBlock> arrowLabels = new List<TextBlock>();

        public DocumentFlowRenderer(Canvas canvas)
        {
            this.canvas = canvas;
        }

        public void Render(DocumentFlowDiagram diagram)
        {
            // ✅ НЕ очищаем canvas полностью, только стрелки
            RemoveArrows();

            // ✅ Обновляем или создаём блоки
            var existingIds = blockVisuals.Keys.ToList();

            foreach (var proc in diagram.Processes)
            {
                if (blockVisuals.ContainsKey(proc.Id))
                {
                    UpdateProcessVisual(blockVisuals[proc.Id].visual, proc);
                }
                else
                {
                    var visual = CreateProcessVisual(proc);
                    Canvas.SetLeft(visual, proc.X);
                    Canvas.SetTop(visual, proc.Y);
                    Panel.SetZIndex(visual, 30);
                    canvas.Children.Add(visual);
                    blockVisuals[proc.Id] = (visual, "PROCESS", proc);
                    AttachResizeEvents(visual, proc, diagram);
                }
            }

            foreach (var entity in diagram.Entities)
            {
                if (blockVisuals.ContainsKey(entity.Id))
                {
                    UpdateEntityVisual(blockVisuals[entity.Id].visual, entity);
                }
                else
                {
                    var visual = CreateEntityVisual(entity);
                    Canvas.SetLeft(visual, entity.X);
                    Canvas.SetTop(visual, entity.Y);
                    Panel.SetZIndex(visual, 25);
                    canvas.Children.Add(visual);
                    blockVisuals[entity.Id] = (visual, "ENTITY", entity);
                    AttachResizeEvents(visual, entity, diagram);
                }
            }

            foreach (var doc in diagram.Documents)
            {
                if (blockVisuals.ContainsKey(doc.Id))
                {
                    UpdateDocumentVisual(blockVisuals[doc.Id].visual, doc);
                }
                else
                {
                    var visual = CreateDocumentVisual(doc);
                    Canvas.SetLeft(visual, doc.X);
                    Canvas.SetTop(visual, doc.Y);
                    Panel.SetZIndex(visual, 24);
                    canvas.Children.Add(visual);
                    blockVisuals[doc.Id] = (visual, "DOCFLOW", doc);
                    AttachResizeEvents(visual, doc, diagram);
                }
            }

            RenderArrows(diagram);
        }

        private void RemoveArrows()
        {
            var toRemove = canvas.Children.OfType<Line>().ToList();
            var polyRemove = canvas.Children.OfType<Polygon>().ToList();

            foreach (var line in toRemove)
                canvas.Children.Remove(line);

            foreach (var poly in polyRemove)
                canvas.Children.Remove(poly);

            foreach (var lbl in arrowLabels)
                canvas.Children.Remove(lbl);

            arrowLabels.Clear();
        }

        private void UpdateProcessVisual(FrameworkElement visual, DocFlowProcess proc)
        {
            if (visual is Border border)
            {
                border.Width = proc.Width;
                var textBlock = border.Child as TextBlock;
                if (textBlock != null)
                {
                    textBlock.MaxWidth = proc.Width - 16;
                    RebuildTextBlock(textBlock, proc.Name, proc.Width - 20);
                    textBlock.Measure(new Size(proc.Width - 16, double.PositiveInfinity));
                    border.Height = Math.Max(textBlock.DesiredSize.Height + 16, proc.MinHeight);
                }
            }
        }

        private void UpdateEntityVisual(FrameworkElement visual, DocFlowEntity entity)
        {
            if (visual is Grid grid)
            {
                grid.Width = entity.Width;

                // ✅ Объявляем ellipse ДО использования
                Ellipse ellipse = null;
                TextBlock textBlock = null;

                if (grid.Children.Count > 0 && grid.Children[0] is Ellipse ell)
                {
                    ellipse = ell;
                    ellipse.Width = entity.Width;
                }

                if (grid.Children.Count > 1 && grid.Children[1] is TextBlock tb)
                {
                    textBlock = tb;
                    textBlock.MaxWidth = entity.Width - 20;
                    RebuildTextBlock(textBlock, entity.Name, entity.Width - 24);
                    textBlock.Measure(new Size(entity.Width - 20, double.PositiveInfinity));
                    double height = Math.Max(textBlock.DesiredSize.Height + 20, entity.MinHeight);

                    grid.Height = height;

                    // ✅ Проверяем что ellipse не null перед использованием
                    if (ellipse != null)
                    {
                        ellipse.Height = height;
                    }
                }
            }
        }

        private void UpdateDocumentVisual(FrameworkElement visual, DocFlowDocument doc)
        {
            if (visual is Canvas canvasDoc)
            {
                canvasDoc.Width = doc.Width;

                // Перестраиваем весь документ
                canvasDoc.Children.Clear();

                var textBlock = new TextBlock
                {
                    FontSize = 13,
                    FontWeight = FontWeights.Normal,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Foreground = Brushes.Black,
                    Padding = new Thickness(8, 8, 8, 8),
                    TextWrapping = TextWrapping.NoWrap,
                    MaxWidth = doc.Width - 20,
                    IsHitTestVisible = false
                };

                RebuildTextBlock(textBlock, doc.Name, doc.Width - 24);
                textBlock.Measure(new Size(doc.Width - 20, double.PositiveInfinity));
                double textHeight = textBlock.DesiredSize.Height;
                double height = Math.Max(textHeight + 24, doc.MinHeight);

                canvasDoc.Height = height;

                var leftLine = new Line
                {
                    X1 = 0,
                    Y1 = 0,
                    X2 = 0,
                    Y2 = height - 10,
                    Stroke = Brushes.Black,
                    StrokeThickness = 2
                };
                canvasDoc.Children.Add(leftLine);

                var rightLine = new Line
                {
                    X1 = doc.Width,
                    Y1 = 0,
                    X2 = doc.Width,
                    Y2 = height - 10,
                    Stroke = Brushes.Black,
                    StrokeThickness = 2
                };
                canvasDoc.Children.Add(rightLine);

                var topLine = new Line
                {
                    X1 = 0,
                    Y1 = 0,
                    X2 = doc.Width,
                    Y2 = 0,
                    Stroke = Brushes.Black,
                    StrokeThickness = 2
                };
                canvasDoc.Children.Add(topLine);

                var wavyPath = CreateWavyBottomLine(doc.Width, height - 10);
                canvasDoc.Children.Add(wavyPath);

                Canvas.SetLeft(textBlock, 10);
                Canvas.SetTop(textBlock, 10);
                canvasDoc.Children.Add(textBlock);
            }
        }

        private void RebuildTextBlock(TextBlock textBlock, string text, double maxWidth)
        {
            string displayText = text.Replace("\\n", "\n");
            textBlock.Inlines.Clear();

            string[] lines = displayText.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var wrappedLines = SmartWrap(lines[i], maxWidth);
                foreach (var wLine in wrappedLines)
                {
                    textBlock.Inlines.Add(new System.Windows.Documents.Run(wLine));
                    if (wLine != wrappedLines.Last())
                        textBlock.Inlines.Add(new System.Windows.Documents.LineBreak());
                }

                if (i < lines.Length - 1)
                    textBlock.Inlines.Add(new System.Windows.Documents.LineBreak());
            }
        }

        /// <summary>
        /// ✅ ALT + движение мыши для изменения ширины
        /// </summary>
        private void AttachResizeEvents(FrameworkElement visual, object data, DocumentFlowDiagram diagram)
        {
            bool isResizing = false;
            double startX = 0;
            double originalWidth = 0;

            visual.MouseMove += (s, e) =>
            {
                if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                {
                    visual.Cursor = Cursors.SizeWE;

                    if (e.LeftButton == MouseButtonState.Pressed)
                    {
                        if (!isResizing)
                        {
                            isResizing = true;
                            startX = e.GetPosition(canvas).X;

                            if (data is DocFlowProcess proc)
                                originalWidth = proc.Width;
                            else if (data is DocFlowEntity entity)
                                originalWidth = entity.Width;
                            else if (data is DocFlowDocument doc)
                                originalWidth = doc.Width;
                        }

                        double currentX = e.GetPosition(canvas).X;
                        double delta = currentX - startX;
                        double newWidth = Math.Max(80, originalWidth + delta);

                        if (data is DocFlowProcess proc2)
                        {
                            proc2.Width = newWidth;
                        }
                        else if (data is DocFlowEntity entity2)
                        {
                            entity2.Width = newWidth;
                        }
                        else if (data is DocFlowDocument doc2)
                        {
                            doc2.Width = newWidth;
                        }

                        Render(diagram);
                        e.Handled = true;
                    }
                }
                else
                {
                    visual.Cursor = Cursors.Arrow;
                }
            };

            visual.MouseUp += (s, e) =>
            {
                isResizing = false;
            };

            visual.MouseLeave += (s, e) =>
            {
                visual.Cursor = Cursors.Arrow;
                isResizing = false;
            };
        }

        private FrameworkElement CreateProcessVisual(DocFlowProcess proc)
        {
            var textBlock = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.Normal,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.Black,
                Padding = new Thickness(8, 6, 8, 6),
                TextWrapping = TextWrapping.NoWrap,
                MaxWidth = proc.Width - 16
            };

            RebuildTextBlock(textBlock, proc.Name, proc.Width - 20);
            textBlock.Measure(new Size(proc.Width - 16, double.PositiveInfinity));
            double height = Math.Max(textBlock.DesiredSize.Height + 16, proc.MinHeight);

            var border = new Border
            {
                Width = proc.Width,
                Height = height,
                Background = Brushes.White,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(2),
                Child = textBlock
            };

            return border;
        }

        private FrameworkElement CreateEntityVisual(DocFlowEntity entity)
        {
            var textBlock = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.Normal,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.Black,
                Padding = new Thickness(6, 4, 6, 4),
                TextWrapping = TextWrapping.NoWrap,
                MaxWidth = entity.Width - 20,
                IsHitTestVisible = false
            };

            RebuildTextBlock(textBlock, entity.Name, entity.Width - 24);
            textBlock.Measure(new Size(entity.Width - 20, double.PositiveInfinity));
            double height = Math.Max(textBlock.DesiredSize.Height + 20, entity.MinHeight);

            var ellipse = new Ellipse
            {
                Width = entity.Width,
                Height = height,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };

            var grid = new Grid { Width = entity.Width, Height = height };
            grid.Children.Add(ellipse);
            grid.Children.Add(textBlock);

            return grid;
        }

        private FrameworkElement CreateDocumentVisual(DocFlowDocument doc)
        {
            var textBlock = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.Normal,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Foreground = Brushes.Black,
                Padding = new Thickness(8, 8, 8, 8),
                TextWrapping = TextWrapping.NoWrap,
                MaxWidth = doc.Width - 20,
                IsHitTestVisible = false
            };

            RebuildTextBlock(textBlock, doc.Name, doc.Width - 24);
            textBlock.Measure(new Size(doc.Width - 20, double.PositiveInfinity));
            double textHeight = textBlock.DesiredSize.Height;
            double height = Math.Max(textHeight + 24, doc.MinHeight);

            var canvasDoc = new Canvas
            {
                Width = doc.Width,
                Height = height,
                Background = Brushes.White,
                ClipToBounds = false
            };

            var leftLine = new Line
            {
                X1 = 0,
                Y1 = 0,
                X2 = 0,
                Y2 = height - 10,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            canvasDoc.Children.Add(leftLine);

            var rightLine = new Line
            {
                X1 = doc.Width,
                Y1 = 0,
                X2 = doc.Width,
                Y2 = height - 10,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            canvasDoc.Children.Add(rightLine);

            var topLine = new Line
            {
                X1 = 0,
                Y1 = 0,
                X2 = doc.Width,
                Y2 = 0,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            canvasDoc.Children.Add(topLine);

            var wavyPath = CreateWavyBottomLine(doc.Width, height - 10);
            canvasDoc.Children.Add(wavyPath);

            Canvas.SetLeft(textBlock, 10);
            Canvas.SetTop(textBlock, 10);
            canvasDoc.Children.Add(textBlock);

            return canvasDoc;
        }

        private List<string> SmartWrap(string text, double maxWidth)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                result.Add(text);
                return result;
            }

            var words = text.Split(' ');
            var currentLine = "";

            foreach (var word in words)
            {
                string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                var testBlock = new TextBlock
                {
                    Text = testLine,
                    FontSize = 13,
                    TextWrapping = TextWrapping.NoWrap
                };
                testBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                if (testBlock.DesiredSize.Width <= maxWidth)
                {
                    currentLine = testLine;
                }
                else
                {
                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        if (word.Length <= 2 && currentLine.Length + word.Length + 1 <= 30)
                        {
                            currentLine += " " + word;
                        }
                        else
                        {
                            result.Add(currentLine);
                            currentLine = word;
                        }
                    }
                    else
                    {
                        result.Add(word);
                        currentLine = "";
                    }
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
                result.Add(currentLine);

            return result;
        }

        private Path CreateWavyBottomLine(double width, double yPos)
        {
            var path = new Path
            {
                Stroke = Brushes.Black,
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };

            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = new Point(0, yPos) };

            double segmentWidth = width / 2.0;
            double amplitude = 8;

            figure.Segments.Add(new BezierSegment(
                new Point(segmentWidth * 0.25, yPos),
                new Point(segmentWidth * 0.5, yPos + amplitude),
                new Point(segmentWidth, yPos),
                true));

            figure.Segments.Add(new BezierSegment(
                new Point(segmentWidth + segmentWidth * 0.25, yPos),
                new Point(segmentWidth + segmentWidth * 0.5, yPos - amplitude),
                new Point(width, yPos),
                true));

            geometry.Figures.Add(figure);
            path.Data = geometry;

            return path;
        }

        private void RenderArrows(DocumentFlowDiagram diagram)
        {
            var centers = GetBlockCenters();

            foreach (var arrow in diagram.Arrows)
            {
                if (!centers.ContainsKey(arrow.FromId) || !centers.ContainsKey(arrow.ToId))
                    continue;

                var fromCenter = centers[arrow.FromId];
                var toCenter = centers[arrow.ToId];

                var toBlock = blockVisuals[arrow.ToId].visual;
                var intersection = GetIntersectionWithBlock(toBlock, fromCenter, toCenter);

                DrawArrow(fromCenter, intersection, arrow.Label);
            }
        }

        private void DrawArrow(Point start, Point end, string label)
        {
            var line = new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            Panel.SetZIndex(line, 20);
            canvas.Children.Add(line);

            double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            double arrowHeadLength = 12.0;
            double arrowHeadAngle = 0.45;

            Point p1 = new Point(
                end.X - arrowHeadLength * Math.Cos(angle - arrowHeadAngle),
                end.Y - arrowHeadLength * Math.Sin(angle - arrowHeadAngle));

            Point p2 = new Point(
                end.X - arrowHeadLength * Math.Cos(angle + arrowHeadAngle),
                end.Y - arrowHeadLength * Math.Sin(angle + arrowHeadAngle));

            var poly = new Polygon
            {
                Points = new PointCollection { new Point(end.X, end.Y), p1, p2 },
                Fill = Brushes.Black,
                Stroke = Brushes.Black,
                StrokeThickness = 0.5
            };
            Panel.SetZIndex(poly, 20);
            canvas.Children.Add(poly);

            if (!string.IsNullOrWhiteSpace(label))
            {
                var tb = new TextBlock
                {
                    Text = label,
                    FontSize = 11,
                    Foreground = Brushes.Black,
                    Background = Brushes.White,
                    Padding = new Thickness(3, 1, 3, 1)
                };

                double midX = (start.X + end.X) / 2;
                double midY = (start.Y + end.Y) / 2;

                Canvas.SetLeft(tb, midX - 40);
                Canvas.SetTop(tb, midY - 12);
                Panel.SetZIndex(tb, 90);
                canvas.Children.Add(tb);
                arrowLabels.Add(tb);
            }
        }

        private Dictionary<string, Point> GetBlockCenters()
        {
            var centers = new Dictionary<string, Point>();
            foreach (var kv in blockVisuals)
            {
                var fe = kv.Value.visual;
                double left = Canvas.GetLeft(fe);
                double top = Canvas.GetTop(fe);
                centers[kv.Key] = new Point(left + fe.Width / 2, top + fe.Height / 2);
            }
            return centers;
        }

        private Point GetIntersectionWithBlock(FrameworkElement block, Point from, Point to)
        {
            double left = Canvas.GetLeft(block);
            double top = Canvas.GetTop(block);
            double width = block.Width;
            double height = block.Height;

            if (block is Grid grid && grid.Children.Count > 0 && grid.Children[0] is Ellipse)
            {
                return GetIntersectionWithEllipse(left, top, width, height, from, to);
            }
            else
            {
                return GetIntersectionWithRect(left, top, width, height, from, to);
            }
        }

        private Point GetIntersectionWithEllipse(double left, double top, double width, double height, Point from, Point to)
        {
            double cx = left + width / 2;
            double cy = top + height / 2;
            double rx = width / 2;
            double ry = height / 2;

            double dx = to.X - from.X;
            double dy = to.Y - from.Y;

            double a = (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry);
            double b = 2 * ((from.X - cx) * dx / (rx * rx) + (from.Y - cy) * dy / (ry * ry));
            double c = ((from.X - cx) * (from.X - cx)) / (rx * rx) + ((from.Y - cy) * (from.Y - cy)) / (ry * ry) - 1;

            double discriminant = b * b - 4 * a * c;
            if (discriminant < 0) return to;

            double t1 = (-b + Math.Sqrt(discriminant)) / (2 * a);
            double t2 = (-b - Math.Sqrt(discriminant)) / (2 * a);
            double t = Math.Abs(t1) < Math.Abs(t2) ? t1 : t2;

            if (t < 0.01) t = Math.Max(t1, t2);

            return new Point(from.X + dx * t, from.Y + dy * t);
        }

        private Point GetIntersectionWithRect(double left, double top, double width, double height, Point from, Point to)
        {
            Rect rect = new Rect(left, top, width, height);
            double dx = to.X - from.X;
            double dy = to.Y - from.Y;

            var candidates = new List<Point>();
            double[] ts = new double[4];

            if (Math.Abs(dx) > 0.01)
            {
                ts[0] = (rect.Left - from.X) / dx;
                ts[1] = (rect.Right - from.X) / dx;
            }

            if (Math.Abs(dy) > 0.01)
            {
                ts[2] = (rect.Top - from.Y) / dy;
                ts[3] = (rect.Bottom - from.Y) / dy;
            }

            foreach (double t in ts)
            {
                if (t > 0.01 && t <= 1)
                {
                    double ix = from.X + dx * t;
                    double iy = from.Y + dy * t;
                    if (rect.Contains(ix, iy))
                        candidates.Add(new Point(ix, iy));
                }
            }

            if (candidates.Count > 0)
                return candidates.OrderBy(pt => Distance(pt, to)).First();

            return to;
        }

        private double Distance(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public Dictionary<string, FrameworkElement> GetBlockVisuals()
        {
            return blockVisuals.ToDictionary(k => k.Key, v => v.Value.visual);
        }

        public Dictionary<FrameworkElement, string> GetBlockTypes()
        {
            return blockVisuals.ToDictionary(k => k.Value.visual, v => v.Value.type);
        }

        public List<TextBlock> GetArrowLabels()
        {
            return arrowLabels;
        }
    }
}
