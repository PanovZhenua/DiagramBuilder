using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DiagramBuilder.Models;
using DiagramBuilder.Services.Core;
using DiagramBuilder.Services.Management;

namespace DiagramBuilder.Services.Rendering
{
    public class ERDRenderer
    {
        private readonly Canvas canvas;
        private DiagramStyle style;

        private const double HeaderHeight = 34.0;
        private const double RowHeight = 28.0;
        private const double LeftColumnWidth = 42.0;
        private const double OuterStroke = 2.0;
        private const double InnerStroke = 1.4;
        private const double CornerRadius = 6.0;
        private const double BottomPadding = 8.0;

        private ERDArrowDragManager arrowDragManager;
        private ERDDiagram currentDiagram;
        private Action<FrameworkElement, string> onEntityVisualRecreated;

        public Dictionary<string, FrameworkElement> EntityVisuals { get; }
            = new Dictionary<string, FrameworkElement>();

        public Dictionary<string, List<Shape>> RelationshipVisuals { get; }
            = new Dictionary<string, List<Shape>>();

        public ERDRenderer(Canvas canvas, DiagramStyle style)
        {
            this.canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            this.style = style ?? DiagramStyle.GetStyle(DiagramStyleType.Presentation);

            this.arrowDragManager = new ERDArrowDragManager(canvas);
            this.arrowDragManager.SetArrowDraggingEnabled(true);
        }

        public void SetEntityRecreatedCallback(Action<FrameworkElement, string> callback)
        {
            onEntityVisualRecreated = callback;
        }

        public void Clear()
        {
            EntityVisuals.Clear();
            RelationshipVisuals.Clear();
            arrowDragManager?.ClearSelection();
        }

        public void RenderDiagram(ERDDiagram diagram)
        {
            if (diagram == null) return;

            currentDiagram = diagram;
            Clear();

            foreach (var entity in diagram.Entities)
            {
                var visual = CreateEntityVisual(entity);
                EntityVisuals[entity.Id] = visual;

                canvas.Children.Add(visual);
                Canvas.SetLeft(visual, entity.X);
                Canvas.SetTop(visual, entity.Y);
                Panel.SetZIndex(visual, 10);

                AttachResizeEvents(visual, entity, diagram);
            }

            foreach (var rel in diagram.Relationships)
            {
                var shapes = CreateRelationshipVisual(rel, diagram);
                RelationshipVisuals[rel.Id] = shapes;

                foreach (var shape in shapes)
                {
                    Panel.SetZIndex(shape, 5);
                    canvas.Children.Add(shape);
                }
            }

            arrowDragManager.SetRelationshipVisuals(RelationshipVisuals);
            arrowDragManager.SetRelationships(diagram.Relationships);
            arrowDragManager.SetEntities(diagram.Entities.ToDictionary(e => e.Id));
            arrowDragManager.SetEntityVisuals(EntityVisuals);
            arrowDragManager.AttachArrowEvents();
        }

        public void UpdateRelationshipsForEntity(ERDDiagram diagram, string entityId)
        {
            if (diagram == null || string.IsNullOrEmpty(entityId))
                return;

            var affected = diagram.Relationships
                .Where(r => r.FromEntityId == entityId || r.ToEntityId == entityId)
                .ToList();

            foreach (var rel in affected)
            {
                if (RelationshipVisuals.TryGetValue(rel.Id, out var oldShapes))
                {
                    foreach (var s in oldShapes)
                        canvas.Children.Remove(s);
                }

                var newShapes = CreateRelationshipVisual(rel, diagram);
                RelationshipVisuals[rel.Id] = newShapes;

                foreach (var s in newShapes)
                {
                    Panel.SetZIndex(s, 5);
                    canvas.Children.Add(s);
                }
            }

            arrowDragManager.SetRelationshipVisuals(RelationshipVisuals);
            arrowDragManager.SetRelationships(diagram.Relationships);
            arrowDragManager.AttachArrowEvents();
        }

        // ========== RESIZE ЛОГИКА ==========

        private void AttachResizeEvents(FrameworkElement visual, ERDEntity entity, ERDDiagram diagram)
        {
            bool isResizing = false;
            double startX = 0;
            double originalWidth = entity.Width;
            const double EdgeThreshold = 8.0;

            visual.MouseMove += (s, e) =>
            {
                bool isAltPressed = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);

                if (isResizing)
                {
                    // ✅ ИСПРАВЛЕНИЕ: Плавное изменение ширины без пересоздания визуала
                    double currentX = e.GetPosition(canvas).X;
                    double delta = currentX - startX;
                    double newWidth = Math.Max(150, originalWidth + delta);

                    // Обновляем только Width у Border, НЕ пересоздаем визуал
                    if (visual is Border border)
                    {
                        border.Width = newWidth;
                        entity.Width = newWidth;

                        // Обновляем внутренние элементы под новую ширину
                        if (border.Child is Canvas innerCanvas)
                        {
                            foreach (UIElement child in innerCanvas.Children)
                            {
                                if (child is Line line)
                                {
                                    // Обновляем горизонтальные линии
                                    if (Math.Abs(line.Y1 - line.Y2) < 0.1) // горизонтальная линия
                                    {
                                        line.X2 = newWidth - OuterStroke;
                                    }
                                }
                            }

                            // Обновляем позицию текста заголовка (центрирование)
                            var headerText = innerCanvas.Children.OfType<TextBlock>().FirstOrDefault();
                            if (headerText != null)
                            {
                                headerText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                                double headerTextX = (newWidth - headerText.DesiredSize.Width) / 2.0;
                                Canvas.SetLeft(headerText, Math.Round(headerTextX));
                            }
                        }

                        // Обновляем связи
                        UpdateRelationshipsForEntity(diagram, entity.Id);
                    }

                    e.Handled = true;
                }
                else
                {
                    // Показываем курсор SizeWE только при ALT и на границе
                    if (isAltPressed)
                    {
                        Point pos = e.GetPosition(visual);
                        bool nearLeftEdge = pos.X <= EdgeThreshold;
                        bool nearRightEdge = pos.X >= entity.Width - EdgeThreshold;

                        visual.Cursor = (nearLeftEdge || nearRightEdge) ? Cursors.SizeWE : Cursors.Arrow;
                    }
                    else
                    {
                        visual.Cursor = Cursors.Arrow;
                    }
                }
            };

            visual.MouseLeftButtonDown += (s, e) =>
            {
                bool isAltPressed = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);

                if (isAltPressed)
                {
                    Point pos = e.GetPosition(visual);
                    bool nearLeftEdge = pos.X <= EdgeThreshold;
                    bool nearRightEdge = pos.X >= entity.Width - EdgeThreshold;

                    if (nearLeftEdge || nearRightEdge)
                    {
                        isResizing = true;
                        startX = e.GetPosition(canvas).X;
                        originalWidth = entity.Width;
                        visual.CaptureMouse();
                        e.Handled = true;
                    }
                }
            };

            visual.MouseLeftButtonUp += (s, e) =>
            {
                if (isResizing)
                {
                    isResizing = false;
                    visual.ReleaseMouseCapture();
                    visual.Cursor = Cursors.Arrow;

                    // ✅ После окончания resize - полная перерисовка один раз
                    FinalizeEntityResize(entity, diagram);

                    e.Handled = true;
                }
            };

            visual.MouseLeave += (s, e) =>
            {
                if (!isResizing)
                {
                    visual.Cursor = Cursors.Arrow;
                }
            };
        }

        private void FinalizeEntityResize(ERDEntity entity, ERDDiagram diagram)
        {
            // Полностью перерисовываем сущность после завершения resize
            if (!EntityVisuals.TryGetValue(entity.Id, out var oldVisual))
                return;

            canvas.Children.Remove(oldVisual);

            var newVisual = CreateEntityVisual(entity);
            EntityVisuals[entity.Id] = newVisual;
            canvas.Children.Add(newVisual);
            Canvas.SetLeft(newVisual, entity.X);
            Canvas.SetTop(newVisual, entity.Y);
            Panel.SetZIndex(newVisual, 10);

            AttachResizeEvents(newVisual, entity, diagram);

            // ✅ Уведомляем LoadERD о том, что визуал пересоздан (для переподписки drag-n-drop)
            onEntityVisualRecreated?.Invoke(newVisual, entity.Id);

            UpdateRelationshipsForEntity(diagram, entity.Id);
        }

        // ========== СОЗДАНИЕ ВИЗУАЛА СУЩНОСТИ ==========

        private FrameworkElement CreateEntityVisual(ERDEntity entity)
        {
            var pk = entity.Fields.Where(f => f.IsPrimaryKey).ToList();
            var other = entity.Fields.Where(f => !f.IsPrimaryKey).ToList();

            int totalRows = entity.Fields.Count;
            double bodyHeight = totalRows * RowHeight;
            double totalHeight = HeaderHeight + bodyHeight + BottomPadding - 2;

            entity.Width = Math.Max(entity.Width, 180);
            entity.Height = totalHeight;

            var border = new Border
            {
                Width = entity.Width,
                Height = totalHeight,
                Background = Brushes.White,
                BorderBrush = style.BlockBorder,
                BorderThickness = new Thickness(OuterStroke),
                CornerRadius = new CornerRadius(CornerRadius),
                SnapsToDevicePixels = true
            };
            border.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);

            var root = new Canvas();
            border.Child = root;

            // Заголовок
            var headerText = new TextBlock
            {
                Text = entity.Name,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = style.Text
            };
            headerText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            double headerTextX = (entity.Width - headerText.DesiredSize.Width) / 2.0;
            double headerTextY = OuterStroke + (HeaderHeight - headerText.DesiredSize.Height) / 2.0;
            Canvas.SetLeft(headerText, Math.Round(headerTextX));
            Canvas.SetTop(headerText, Math.Round(headerTextY));
            root.Children.Add(headerText);

            // Линия под заголовком
            double topLineY = OuterStroke + HeaderHeight;
            var headerLine = new Line
            {
                X1 = OuterStroke - 3,
                X2 = entity.Width - OuterStroke,
                Y1 = topLineY,
                Y2 = topLineY,
                Stroke = style.BlockBorder,
                StrokeThickness = InnerStroke
            };
            headerLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
            root.Children.Add(headerLine);

            // Вертикальная линия
            double bodyTop = topLineY;
            double bodyBottom = OuterStroke + HeaderHeight + bodyHeight;
            double vLineX = OuterStroke + LeftColumnWidth;

            var vertLine = new Line
            {
                X1 = vLineX,
                X2 = vLineX,
                Y1 = bodyTop,
                Y2 = bodyBottom,
                Stroke = style.BlockBorder,
                StrokeThickness = InnerStroke
            };
            vertLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
            root.Children.Add(vertLine);

            // Линия между PK и остальными
            if (pk.Count > 0 && other.Count > 0)
            {
                double sepY = bodyTop + pk.Count * RowHeight;
                var sep = new Line
                {
                    X1 = OuterStroke - 3,
                    X2 = entity.Width - OuterStroke,
                    Y1 = sepY,
                    Y2 = sepY,
                    Stroke = style.BlockBorder,
                    StrokeThickness = InnerStroke
                };
                sep.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                root.Children.Add(sep);
            }

            // Поля
            double currentY = bodyTop;

            foreach (var field in entity.Fields)
            {
                bool isPk = field.IsPrimaryKey;
                bool isFk = field.IsForeignKey;

                string leftText = isPk ? "PK" : (isFk ? "FK" : string.Empty);
                if (!string.IsNullOrEmpty(leftText))
                {
                    var role = new TextBlock
                    {
                        Text = leftText,
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Foreground = style.Text
                    };
                    role.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                    double roleX = OuterStroke + (LeftColumnWidth - role.DesiredSize.Width) / 2.0;
                    double roleY = currentY + (RowHeight - role.DesiredSize.Height) / 2.0;

                    Canvas.SetLeft(role, Math.Round(roleX));
                    Canvas.SetTop(role, Math.Round(roleY));
                    root.Children.Add(role);
                }

                var name = new TextBlock
                {
                    Text = field.Name,
                    FontSize = 16,
                    FontWeight = isPk ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = style.Text
                };
                name.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                double nameX = vLineX + 10;
                double nameY = currentY + (RowHeight - name.DesiredSize.Height) / 2.0;

                Canvas.SetLeft(name, Math.Round(nameX));
                Canvas.SetTop(name, Math.Round(nameY));
                root.Children.Add(name);

                currentY += RowHeight;
            }

            return border;
        }

        // ========== СВЯЗИ ==========

        private List<Shape> CreateRelationshipVisual(ERDRelationship rel, ERDDiagram diagram)
        {
            var result = new List<Shape>();

            if (!EntityVisuals.TryGetValue(rel.FromEntityId, out var fromVisual) ||
                !EntityVisuals.TryGetValue(rel.ToEntityId, out var toVisual))
                return result;

            var fromEntity = diagram.Entities.First(e => e.Id == rel.FromEntityId);
            var toEntity = diagram.Entities.First(e => e.Id == rel.ToEntityId);

            double fromLeft = Canvas.GetLeft(fromVisual);
            double fromTop = Canvas.GetTop(fromVisual);
            double toLeft = Canvas.GetLeft(toVisual);
            double toTop = Canvas.GetTop(toVisual);

            double fromCenterY = fromTop + fromEntity.Height / 2.0;
            double toCenterY = toTop + toEntity.Height / 2.0;
            double dyCenters = Math.Abs(fromCenterY - toCenterY);

            const double sameRowThreshold = 30.0;

            bool sameRowByCenter = dyCenters < sameRowThreshold;
            bool toOnRight = toLeft > fromLeft;

            bool fromRightSide;
            bool toRightSide;

            if (sameRowByCenter)
            {
                if (toOnRight)
                {
                    fromRightSide = true;
                    toRightSide = false;
                }
                else
                {
                    fromRightSide = false;
                    toRightSide = true;
                }
            }
            else
            {
                fromRightSide = toOnRight;
                toRightSide = !toOnRight;
            }

            Point pFrom = GetConnectionPoint(fromEntity, fromVisual, rel.FromFieldName, fromRightSide);
            Point pTo = GetConnectionPoint(toEntity, toVisual, rel.ToFieldName, toRightSide);

            var points = BuildPath(pFrom, pTo, fromRightSide, toRightSide, sameRowByCenter, toOnRight);

            var polyline = new Polyline
            {
                Points = new PointCollection(points),
                Stroke = style.Line,
                StrokeThickness = OuterStroke,
                SnapsToDevicePixels = true
            };
            polyline.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
            result.Add(polyline);

            if (points.Count >= 2)
            {
                Point beforeLast = points[points.Count - 2];
                Point last = points[points.Count - 1];

                var arrow = CreateArrowHeadPath(beforeLast, last);
                if (arrow != null)
                    result.Add(arrow);
            }

            return result;
        }

        private List<Point> BuildPath(
            Point from,
            Point to,
            bool fromRightSide,
            bool toRightSide,
            bool sameRowByCenter,
            bool toOnRight)
        {
            var points = new List<Point>();
            const double stub = 24.0;

            if (sameRowByCenter)
            {
                double dy = Math.Abs(from.Y - to.Y);

                if (dy < 2.0)
                {
                    points.Add(from);
                    points.Add(to);
                }
                else
                {
                    double dir = fromRightSide ? 1.0 : -1.0;
                    double midX = from.X + dir * stub;

                    points.Add(from);
                    points.Add(new Point(midX, from.Y));
                    points.Add(new Point(midX, to.Y));
                    points.Add(to);
                }
            }
            else
            {
                double dy = Math.Abs(from.Y - to.Y);

                if (dy < 2.0)
                {
                    points.Add(from);
                    points.Add(to);
                }
                else
                {
                    double midX = fromRightSide ? from.X + stub : from.X - stub;

                    points.Add(from);
                    points.Add(new Point(midX, from.Y));
                    points.Add(new Point(midX, to.Y));
                    points.Add(to);
                }
            }

            return points;
        }

        private Path CreateArrowHeadPath(Point from, Point to)
        {
            Vector direction = to - from;
            if (direction.Length < 0.1) return null;

            direction.Normalize();
            Vector perpendicular = new Vector(-direction.Y, direction.X);

            const double arrowLength = 10.0;
            const double arrowWidth = 6.0;

            Point tip = to;
            Point left = tip - direction * arrowLength + perpendicular * arrowWidth;
            Point right = tip - direction * arrowLength - perpendicular * arrowWidth;

            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = tip };
            figure.Segments.Add(new LineSegment(left, true));
            figure.Segments.Add(new LineSegment(right, true));
            figure.IsClosed = true;
            geometry.Figures.Add(figure);

            var path = new Path
            {
                Data = geometry,
                Fill = style.LineArrowHead,
                Stroke = style.LineArrowHead,
                StrokeThickness = 1.0,
                SnapsToDevicePixels = true
            };
            path.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);

            return path;
        }

        private Point GetConnectionPoint(ERDEntity entity, FrameworkElement visual,
                                         string fieldName, bool rightSide)
        {
            var field = entity.GetFieldByName(fieldName) ?? entity.GetPrimaryKeyField();
            int index = 0;
            if (field != null)
            {
                for (int i = 0; i < entity.Fields.Count; i++)
                {
                    if (entity.Fields[i] == field)
                    {
                        index = i;
                        break;
                    }
                }
            }

            double left = Canvas.GetLeft(visual);
            double top = Canvas.GetTop(visual);

            double y = top + OuterStroke + HeaderHeight + index * RowHeight + RowHeight / 2.0;
            double x = rightSide ? left + entity.Width : left;

            return new Point(x, y);
        }

        public Dictionary<string, List<Shape>> GetRelationshipVisuals()
        {
            return RelationshipVisuals;
        }
    }
}
