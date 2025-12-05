using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using DiagramBuilder.Models;

namespace DiagramBuilder.Services.Management
{
    public class SnapHelper
    {
        private const double SnapThreshold = 10.0; // пикселей для прилипания

        /// <summary>
        /// Выравнивает блок по другим блокам (выравнивание по краям и центрам)
        /// </summary>
        public static Point SnapToGrid(UIElement dragged, Dictionary<string, DiagramBlock> allBlocks, Point proposed, Point mousePosOnCanvas)
        {
            if (!(dragged is FrameworkElement fe))
                return proposed;

            double left = proposed.X;
            double top = proposed.Y;
            double width = fe.ActualWidth;
            double height = fe.ActualHeight;

            double centerX = left + width / 2;
            double centerY = top + height / 2;

            const double SnapActivationPx = 22.0;  // где магнит хватает (удобно)
            const double SnapReleasePx = 34.0;     // где магнит полностью отпускает

            double? snapX = null, snapY = null;
            double minSnapXDistance = double.MaxValue, minSnapYDistance = double.MaxValue;

            foreach (var block in allBlocks.Values)
            {
                if (block.Visual == dragged)
                    continue;
                double oLeft = Canvas.GetLeft(block.Visual);
                double oTop = Canvas.GetTop(block.Visual);
                double oWidth = block.Visual.Width;
                double oHeight = block.Visual.Height;
                double oCenterX = oLeft + oWidth / 2;
                double oCenterY = oTop + oHeight / 2;

                // --- Snap по горизонтали (центры по X) ---
                double distToOcenterX = Math.Abs(mousePosOnCanvas.X - oCenterX);
                if (distToOcenterX < SnapActivationPx && distToOcenterX < minSnapXDistance)
                {
                    snapX = oCenterX - width / 2;
                    minSnapXDistance = distToOcenterX;
                }

                // --- Snap по вертикали (центры по Y) ---
                double distToOcenterY = Math.Abs(mousePosOnCanvas.Y - oCenterY);
                if (distToOcenterY < SnapActivationPx && distToOcenterY < minSnapYDistance)
                {
                    snapY = oCenterY - height / 2;
                    minSnapYDistance = distToOcenterY;
                }
            }

            // Если мы уже примерно "прилипли" — держим блок на линии чуть дальше (порог SnapReleasePx)
            if (snapX.HasValue && Math.Abs(mousePosOnCanvas.X - (snapX.Value + width / 2)) < SnapReleasePx)
                left = snapX.Value;

            if (snapY.HasValue && Math.Abs(mousePosOnCanvas.Y - (snapY.Value + height / 2)) < SnapReleasePx)
                top = snapY.Value;

            return new Point(left, top);
        }

        public static Point SnapJunctionToBlocks(Point center, Dictionary<string, DiagramBlock> blocks)
        {
            const double SnapThreshold = 18.0;
            Point bestPoint = center;
            double bestDist = double.MaxValue;

            foreach (var block in blocks.Values)
            {
                if (block.Visual is FrameworkElement fe)
                {
                    double left = Canvas.GetLeft(fe);
                    double top = Canvas.GetTop(fe);
                    double right = left + fe.ActualWidth;
                    double bottom = top + fe.ActualHeight;
                    double cX = left + fe.ActualWidth / 2;
                    double cY = top + fe.ActualHeight / 2;

                    var points = new[]
                    {
                new Point(cX, top),      // верхний центр
                new Point(cX, bottom),   // нижний центр
                new Point(left, cY),     // центральный слева
                new Point(right, cY)     // центральный справа
            };

                    foreach (var p in points)
                    {
                        double dist = (center - p).Length;
                        if (dist < SnapThreshold && dist < bestDist)
                        {
                            bestDist = dist;
                            bestPoint = p;
                        }
                    }
                }
            }
            return bestPoint;
        }


    }
}
