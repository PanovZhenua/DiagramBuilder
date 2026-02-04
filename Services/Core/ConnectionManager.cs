using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DiagramBuilder.Models;

namespace DiagramBuilder.Services.Core
{
    /// <summary>
    /// Менеджер связей между блоками (для NodeTree)
    /// </summary>
    public class ConnectionManager
    {
        private readonly Canvas canvas;
        private readonly Dictionary<string, List<Connection>> connections = new Dictionary<string, List<Connection>>();

        public ConnectionManager(Canvas canvas)
        {
            this.canvas = canvas;
        }

        /// <summary>
        /// Добавляет связь между блоками
        /// </summary>
        public void AddConnection(DiagramBlock parent, DiagramBlock child, List<Line> lines)
        {
            var connection = new Connection
            {
                Parent = parent,
                Child = child,
                Lines = lines
            };

            if (!connections.ContainsKey(parent.Code))
            {
                connections[parent.Code] = new List<Connection>();
            }
            connections[parent.Code].Add(connection);

            if (!connections.ContainsKey(child.Code))
            {
                connections[child.Code] = new List<Connection>();
            }
            connections[child.Code].Add(connection);
        }

        /// <summary>
        /// Обновляет связи для конкретного блока
        /// </summary>
        public void UpdateConnectionsForBlock(string blockCode)
        {
            if (!connections.ContainsKey(blockCode))
                return;

            foreach (var conn in connections[blockCode])
            {
                UpdateConnection(conn);
            }
        }

        /// <summary>
        /// НОВЫЙ МЕТОД: Обновляет все связи
        /// </summary>
        public void UpdateAllConnections()
        {
            var processedConnections = new HashSet<Connection>();

            foreach (var connectionList in connections.Values)
            {
                foreach (var conn in connectionList)
                {
                    if (!processedConnections.Contains(conn))
                    {
                        UpdateConnection(conn);
                        processedConnections.Add(conn);
                    }
                }
            }
        }

        /// <summary>
        /// Обновляет конкретную связь
        /// </summary>
        private void UpdateConnection(Connection conn)
        {
            if (conn.Parent == null || conn.Child == null || conn.Lines.Count < 3)
                return;

            // Вычисляем новые координаты
            double parentCenterX = Canvas.GetLeft(conn.Parent.Visual) + conn.Parent.Visual.Width / 2;
            double parentBottom = Canvas.GetTop(conn.Parent.Visual) + conn.Parent.Visual.Height;

            double childCenterX = Canvas.GetLeft(conn.Child.Visual) + conn.Child.Visual.Width / 2;
            double childTop = Canvas.GetTop(conn.Child.Visual);

            double midY = parentBottom + 30;

            // Обновляем линии
            if (conn.Lines.Count >= 3)
            {
                // Вертикальная от родителя
                conn.Lines[0].X1 = parentCenterX;
                conn.Lines[0].Y1 = parentBottom;
                conn.Lines[0].X2 = parentCenterX;
                conn.Lines[0].Y2 = midY;

                // Горизонтальная
                conn.Lines[1].X1 = parentCenterX;
                conn.Lines[1].Y1 = midY;
                conn.Lines[1].X2 = childCenterX;
                conn.Lines[1].Y2 = midY;

                // Вертикальная к ребёнку
                conn.Lines[2].X1 = childCenterX;
                conn.Lines[2].Y1 = midY;
                conn.Lines[2].X2 = childCenterX;
                conn.Lines[2].Y2 = childTop;
            }
        }

        /// <summary>
        /// Создаёт линию (вспомогательный метод)
        /// </summary>
        public Line CreateLine(Point start, Point end, Brush stroke, double thickness)
        {
            Line line = new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = stroke,
                StrokeThickness = thickness
            };

            canvas.Children.Add(line);
            return line;
        }

        /// <summary>
        /// Очищает все связи
        /// </summary>
        public void Clear()
        {
            foreach (var connectionList in connections.Values)
            {
                foreach (var conn in connectionList)
                {
                    foreach (var line in conn.Lines)
                    {
                        canvas.Children.Remove(line);
                    }
                }
            }
            connections.Clear();
        }

        /// <summary>
        /// Класс связи между блоками
        /// </summary>
        private class Connection
        {
            public DiagramBlock Parent { get; set; }
            public DiagramBlock Child { get; set; }
            public List<Line> Lines { get; set; }
        }
    }
}
