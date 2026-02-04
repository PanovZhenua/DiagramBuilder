// =================== Services/Core/SnapHelper.cs (ПОЛНАЯ ВЕРСИЯ) ===================
using DiagramBuilder.Models;
using System;
using System.Collections.Generic;
using System.Windows;

namespace DiagramBuilder.Services.Core
{
    public static class SnapHelper
    {
        private const double GRID_SIZE = 20.0;
        private const double AXIS_SNAP_THRESHOLD = 10.0; // Магнит по осям X/Y

        /// <summary>
        /// Магнит к сетке (старая логика, оставлена для совместимости)
        /// </summary>
        public static Point SnapToGrid(Point position, bool shiftPressed)
        {
            if (shiftPressed)
                return position;

            double snappedX = Math.Round(position.X / GRID_SIZE) * GRID_SIZE;
            double snappedY = Math.Round(position.Y / GRID_SIZE) * GRID_SIZE;

            if (Math.Abs(position.X - snappedX) < AXIS_SNAP_THRESHOLD &&
                Math.Abs(position.Y - snappedY) < AXIS_SNAP_THRESHOLD)
            {
                return new Point(snappedX, snappedY);
            }

            return position;
        }

        /// <summary>
        /// ✅ НОВЫЙ: Магнит по осям - выравнивает центры блоков по X и/или Y независимо
        /// Если |X1 - X2| < 10px → выравниваем X (вертикальная ось)
        /// Если |Y1 - Y2| < 10px → выравниваем Y (горизонтальная ось)
        /// </summary>
        public static Point SnapCenterToOtherCentersAxisAligned(
    Point draggedCenter,
    Dictionary<string, DiagramBlock> blocks,
    UIElement draggedVisual)
        {
            double snappedX = draggedCenter.X;
            double snappedY = draggedCenter.Y;

            double bestDx = AXIS_SNAP_THRESHOLD;
            double bestDy = AXIS_SNAP_THRESHOLD;

            foreach (var block in blocks.Values)
            {
                if (block?.Visual == null || block.Visual == draggedVisual)
                    continue;

                // ✅ Берём размеры из Visual
                double blockWidth = block.Visual.Width > 0 ? block.Visual.Width : block.Visual.ActualWidth;
                double blockHeight = block.Visual.Height > 0 ? block.Visual.Height : block.Visual.ActualHeight;

                double centerX = block.X + blockWidth / 2.0;
                double centerY = block.Y + blockHeight / 2.0;

                double dx = Math.Abs(draggedCenter.X - centerX);
                double dy = Math.Abs(draggedCenter.Y - centerY);

                // ✅ Выравнивание по оси X (вертикальные центры совпадают)
                if (dx < bestDx)
                {
                    bestDx = dx;
                    snappedX = centerX;
                }

                // ✅ Выравнивание по оси Y (горизонтальные центры совпадают)
                if (dy < bestDy)
                {
                    bestDy = dy;
                    snappedY = centerY;
                }
            }

            return new Point(snappedX, snappedY);
        }

        /// <summary>
        /// ✅ Для Junction (Ellipse) - магнит только к центру UOW блоков
        /// Используется в IDEF3 для движения стрелками
        /// </summary>
        public static Point SnapJunctionToBlocks(Point junctionPos, Dictionary<string, DiagramBlock> blocks)
        {
            return SnapCenterToOtherCentersAxisAligned(junctionPos, blocks, null);
        }

        /// <summary>
        /// Старая логика - магнит к ближайшему центру блока (радиальное расстояние)
        /// Оставлена для совместимости, но рекомендуется использовать SnapCenterToOtherCentersAxisAligned
        /// </summary>
        public static Point SnapToNearestBlock(Point position, Dictionary<string, DiagramBlock> blocks, bool shiftPressed)
        {
            if (shiftPressed || blocks.Count == 0)
                return position;

            double minDistance = double.MaxValue;
            Point bestSnap = position;

            foreach (var block in blocks.Values)
            {
                if (block?.Visual == null)
                    continue;

                double blockCenterX = block.X + (block.Width / 2.0);
                double blockCenterY = block.Y + (block.Height / 2.0);

                double dx = position.X - blockCenterX;
                double dy = position.Y - blockCenterY;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance < minDistance && distance < AXIS_SNAP_THRESHOLD)
                {
                    minDistance = distance;
                    bestSnap = new Point(blockCenterX, blockCenterY);
                }
            }

            return bestSnap;
        }
    }
}
