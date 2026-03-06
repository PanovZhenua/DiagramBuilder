using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DiagramBuilder.Models;

namespace DiagramBuilder.Services.Management
{
    /// <summary>
    /// Менеджер для перетаскивания стрелок ERD с автоматическим выбором стороны
    /// </summary>
    public class ERDArrowDragManager
    {
        private readonly Canvas canvas;
        private bool arrowDraggingEnabled = false;

        // Данные диаграммы
        private Dictionary<string, List<Shape>> relationshipVisuals;
        private Dictionary<string, ERDRelationship> relationships;
        private Dictionary<string, ERDEntity> entities;
        private Dictionary<string, FrameworkElement> entityVisuals;

        // Состояние перетаскивания
        private bool isDragging;
        private double dragStartX;
        private Polyline selectedArrow;
        private Path selectedArrowHead;
        private string selectedRelationshipId;
        private double currentOffset; // текущий отступ (может быть отрицательным)

        private const double MIN_STUB_OFFSET = 10.0;
        private const double MAX_STUB_OFFSET = 300.0;

        public ERDArrowDragManager(Canvas canvas)
        {
            this.canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            relationshipVisuals = new Dictionary<string, List<Shape>>();
            relationships = new Dictionary<string, ERDRelationship>();
            entities = new Dictionary<string, ERDEntity>();
            entityVisuals = new Dictionary<string, FrameworkElement>();
        }

        public void SetArrowDraggingEnabled(bool enabled)
        {
            arrowDraggingEnabled = enabled;
        }

        public void SetRelationshipVisuals(Dictionary<string, List<Shape>> visuals)
        {
            relationshipVisuals = visuals ?? new Dictionary<string, List<Shape>>();
        }

        public void SetRelationships(List<ERDRelationship> rels)
        {
            relationships.Clear();
            if (rels != null)
            {
                foreach (var rel in rels)
                    relationships[rel.Id] = rel;
            }
        }

        public void SetEntities(Dictionary<string, ERDEntity> ents)
        {
            entities = ents ?? new Dictionary<string, ERDEntity>();
        }

        public void SetEntityVisuals(Dictionary<string, FrameworkElement> visuals)
        {
            entityVisuals = visuals ?? new Dictionary<string, FrameworkElement>();
        }

        public void AttachArrowEvents()
        {
            foreach (var kvp in relationshipVisuals)
            {
                string relationshipId = kvp.Key;
                var shapes = kvp.Value;

                var polyline = shapes.OfType<Polyline>().FirstOrDefault();
                if (polyline != null)
                {
                    AttachArrowEvent(polyline, relationshipId);
                }
            }
        }

        private void AttachArrowEvent(Polyline polyline, string relationshipId)
        {
            if (polyline == null) return;

            polyline.Cursor = Cursors.SizeWE;

            polyline.MouseLeftButtonDown += (s, e) =>
            {
                if (!arrowDraggingEnabled) return;

                var poly = s as Polyline;
                if (poly == null || poly.Points.Count < 2) return;

                Point clickPos = e.GetPosition(canvas);
                dragStartX = clickPos.X;

                // Вычисляем текущий отступ с учётом направления
                if (poly.Points.Count >= 2)
                {
                    Point p0 = poly.Points[0];
                    Point p1 = poly.Points[1];
                    currentOffset = p1.X - p0.X; // может быть отрицательным!
                }
                else
                {
                    currentOffset = 24.0;
                }

                isDragging = true;
                selectedArrow = poly;
                selectedRelationshipId = relationshipId;

                if (relationshipVisuals.TryGetValue(relationshipId, out var shapes))
                {
                    selectedArrowHead = shapes.OfType<Path>().FirstOrDefault();
                }

                poly.CaptureMouse();
                poly.StrokeThickness = 3;

                e.Handled = true;
            };

            polyline.MouseMove += Arrow_MouseMove;
            polyline.MouseLeftButtonUp += Arrow_MouseLeftButtonUp;
            polyline.MouseLeave += Arrow_MouseLeave;
        }

        private void Arrow_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging || selectedArrow == null) return;
            if (!relationships.TryGetValue(selectedRelationshipId, out var rel)) return;
            if (!entities.TryGetValue(rel.FromEntityId, out var fromEntity)) return;
            if (!entities.TryGetValue(rel.ToEntityId, out var toEntity)) return;
            if (!entityVisuals.TryGetValue(rel.FromEntityId, out var fromVisual)) return;
            if (!entityVisuals.TryGetValue(rel.ToEntityId, out var toVisual)) return;

            Point currentPos = e.GetPosition(canvas);
            double deltaX = currentPos.X - dragStartX;

            // ✅ НОВЫЙ ОТСТУП (без ограничений в пределах блока)
            double newOffset = currentOffset + deltaX;

            // Ограничиваем только по модулю
            if (Math.Abs(newOffset) < MIN_STUB_OFFSET)
                newOffset = Math.Sign(newOffset) * MIN_STUB_OFFSET;
            if (Math.Abs(newOffset) > MAX_STUB_OFFSET)
                newOffset = Math.Sign(newOffset) * MAX_STUB_OFFSET;

            // ✅ ОПРЕДЕЛЯЕМ ПОЗИЦИИ БЛОКОВ
            double fromLeft = Canvas.GetLeft(fromVisual);
            double toLeft = Canvas.GetLeft(toVisual);

            // Определяем где находится цель относительно источника
            bool targetIsOnRight = toLeft > fromLeft;

            // ✅ НОВАЯ ЛОГИКА: смотрим на знак offset для выбора стороны
            bool fromRightSide;
            bool toRightSide;

            if (Math.Abs(newOffset) < MIN_STUB_OFFSET * 1.5)
            {
                // Малый отступ - стандартная стрелка
                if (targetIsOnRight)
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
            else if (newOffset > 0)
            {
                // Положительный отступ (вправо)
                if (targetIsOnRight)
                {
                    // Цель справа
                    double distanceBetween = toLeft - (fromLeft + fromEntity.Width);

                    if (newOffset < distanceBetween * 0.8)
                    {
                        // Нормальная стрелка: справа → слева
                        fromRightSide = true;
                        toRightSide = false;
                    }
                    else
                    {
                        // Большой отступ: петля справа → справа
                        fromRightSide = true;
                        toRightSide = true;
                    }
                }
                else
                {
                    // Цель слева → всегда петля справа
                    fromRightSide = true;
                    toRightSide = true;
                }
            }
            else // newOffset < 0
            {
                // Отрицательный отступ (влево)
                if (targetIsOnRight)
                {
                    // Цель справа → всегда петля слева
                    fromRightSide = false;
                    toRightSide = false;
                }
                else
                {
                    // Цель слева
                    double distanceBetween = fromLeft - (toLeft + toEntity.Width);

                    if (Math.Abs(newOffset) < distanceBetween * 0.8)
                    {
                        // Нормальная стрелка: слева → справа
                        fromRightSide = false;
                        toRightSide = true;
                    }
                    else
                    {
                        // Большой отступ: петля слева → слева
                        fromRightSide = false;
                        toRightSide = false;
                    }
                }
            }

            // ✅ СТРОИМ ТОЧКИ ПОДКЛЮЧЕНИЯ С УЧЁТОМ ВЫБРАННЫХ СТОРОН
            Point pFrom = GetConnectionPoint(fromEntity, fromVisual, rel.FromFieldName, fromRightSide);
            Point pTo = GetConnectionPoint(toEntity, toVisual, rel.ToFieldName, toRightSide);

            // ✅ СТРОИМ ПУТЬ С ЗАДАННЫМ ОТСТУПОМ
            var points = BuildPathWithOffset(pFrom, pTo, newOffset, fromRightSide, toRightSide);

            selectedArrow.Points = new PointCollection(points);

            // ✅ ОБНОВЛЯЕМ НАКОНЕЧНИК
            if (selectedArrowHead != null && points.Count >= 2)
            {
                Point beforeLast = points[points.Count - 2];
                Point last = points[points.Count - 1];
                selectedArrowHead.Data = CreateArrowHeadGeometry(beforeLast, last);
            }

            e.Handled = true;
        }

        private void Arrow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isDragging) return;

            var polyline = sender as Polyline;
            if (polyline == null) return;

            isDragging = false;
            polyline.ReleaseMouseCapture();
            polyline.StrokeThickness = 2;

            selectedArrow = null;
            selectedArrowHead = null;
            selectedRelationshipId = null;

            e.Handled = true;
        }

        private void Arrow_MouseLeave(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                var polyline = sender as Polyline;
                if (polyline != null)
                {
                    isDragging = false;
                    polyline.ReleaseMouseCapture();
                    polyline.StrokeThickness = 2;
                }

                selectedArrow = null;
                selectedArrowHead = null;
                selectedRelationshipId = null;
            }
        }

        /// <summary>
        /// Строит путь с заданным отступом
        /// </summary>
        private List<Point> BuildPathWithOffset(Point from, Point to, double offset, bool fromRightSide, bool toRightSide)
        {
            var points = new List<Point>();
            double dy = Math.Abs(from.Y - to.Y);

            if (dy < 2.0)
            {
                // Прямая линия (редко)
                points.Add(from);
                points.Add(to);
            }
            else
            {
                // ✅ 4-точечный путь с заданным отступом
                // offset может быть положительным (вправо) или отрицательным (влево)
                double midX = from.X + offset;

                points.Add(from);                       // 1. выход из источника
                points.Add(new Point(midX, from.Y));    // 2. горизонтальный выступ
                points.Add(new Point(midX, to.Y));      // 3. вертикальный переход
                points.Add(to);                         // 4. вход в цель
            }

            return points;
        }

        /// <summary>
        /// Получает точку подключения на нужной стороне блока
        /// </summary>
        private Point GetConnectionPoint(ERDEntity entity, FrameworkElement visual, string fieldName, bool rightSide)
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

            double y = top + 2.0 + 34.0 + index * 28.0 + 14.0; // OuterStroke + HeaderHeight + RowHeight*index + RowHeight/2
            double x = rightSide ? left + entity.Width : left;

            return new Point(x, y);
        }

        /// <summary>
        /// Создаёт геометрию наконечника стрелки
        /// </summary>
        private PathGeometry CreateArrowHeadGeometry(Point from, Point to)
        {
            Vector direction = to - from;
            if (direction.Length < 0.1) return new PathGeometry();

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

            return geometry;
        }

        public void ClearSelection()
        {
            foreach (var shapes in relationshipVisuals.Values)
            {
                var polyline = shapes.OfType<Polyline>().FirstOrDefault();
                if (polyline != null)
                    polyline.StrokeThickness = 2;
            }
        }
    }
}
