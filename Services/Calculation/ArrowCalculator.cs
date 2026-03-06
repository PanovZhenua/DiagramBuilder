using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DiagramBuilder.Models;

namespace DiagramBuilder.Services.Core
{
    public class ArrowSegment
    {
        public Point Start { get; set; }
        public Point End { get; set; }
        public string Direction { get; set; }
    }

    public static class ArrowCalculator
    {
        private static Dictionary<string, DiagramBlock> allBlocks;

        public static void SetAllBlocks(Dictionary<string, DiagramBlock> blocks)
        {
            allBlocks = blocks;
        }

        /// <summary>
        /// ✅ УЛУЧШЕННЫЙ PreprocessArrows с умным распределением
        /// </summary>
        public static List<ArrowData> PreprocessArrows(List<ArrowData> arrows, Dictionary<string, DiagramBlock> blocks)
        {
            var processed = new List<ArrowData>();

            // Группируем стрелки по блоку и стороне
            var groups = arrows.GroupBy(a =>
            {
                string blockCode;
                string side = a.Type ?? "connect";

                if (side == "left" || side == "top" || side == "bottom")
                    blockCode = a.To;
                else
                    blockCode = a.From; // right или connect

                return (blockCode, side);
            });

            foreach (var group in groups)
            {
                var blockCode = group.Key.blockCode;
                var side = group.Key.side;

                // Разделяем на external и connect стрелки
                var externalArrows = group.Where(a => a.Type != "connect").ToList();
                var connectArrows = group.Where(a => a.Type == "connect").ToList();

                int totalCount = externalArrows.Count + connectArrows.Count;
                int idx = 0;

                // ✅ УМНОЕ РАСПРЕДЕЛЕНИЕ: connect стрелки позиционируются по направлению потока
                if (side == "connect")
                {
                    // Для connect стрелок справа блока
                    foreach (var arrow in group.OrderBy(a => GetTargetBlockY(a, blocks)))
                    {
                        processed.Add(new ArrowData
                        {
                            From = arrow.From,
                            To = arrow.To,
                            Label = arrow.Label,
                            Type = arrow.Type,
                            IndexOnSide = idx,
                            TotalOnSide = totalCount
                        });
                        idx++;
                    }
                }
                else if (side == "left" || side == "right")
                {
                    // ✅ Для левой/правой стороны: external сверху, connect внизу
                    var sortedConnects = connectArrows
                        .OrderBy(a => GetTargetBlockY(a, blocks))
                        .ToList();

                    // Сначала external стрелки
                    foreach (var arrow in externalArrows)
                    {
                        processed.Add(new ArrowData
                        {
                            From = arrow.From,
                            To = arrow.To,
                            Label = arrow.Label,
                            Type = arrow.Type,
                            IndexOnSide = idx,
                            TotalOnSide = totalCount
                        });
                        idx++;
                    }

                    // Потом connect стрелки (отсортированные по Y целевого блока)
                    foreach (var arrow in sortedConnects)
                    {
                        processed.Add(new ArrowData
                        {
                            From = arrow.From,
                            To = arrow.To,
                            Label = arrow.Label,
                            Type = arrow.Type,
                            IndexOnSide = idx,
                            TotalOnSide = totalCount
                        });
                        idx++;
                    }
                }
                else
                {
                    // Для top/bottom - стандартное распределение
                    foreach (var arrow in externalArrows)
                    {
                        processed.Add(new ArrowData
                        {
                            From = arrow.From,
                            To = arrow.To,
                            Label = arrow.Label,
                            Type = arrow.Type,
                            IndexOnSide = idx,
                            TotalOnSide = totalCount
                        });
                        idx++;
                    }

                    foreach (var arrow in connectArrows)
                    {
                        processed.Add(new ArrowData
                        {
                            From = arrow.From,
                            To = arrow.To,
                            Label = arrow.Label,
                            Type = arrow.Type,
                            IndexOnSide = idx,
                            TotalOnSide = totalCount
                        });
                        idx++;
                    }
                }
            }

            return processed;
        }

        /// <summary>
        /// ✅ НОВЫЙ МЕТОД: определяет Y координату целевого блока для сортировки
        /// </summary>
        private static double GetTargetBlockY(ArrowData arrow, Dictionary<string, DiagramBlock> blocks)
        {
            // Для connect стрелок смотрим на TO блок
            if (arrow.Type == "connect" && blocks.ContainsKey(arrow.To))
            {
                return blocks[arrow.To].Top;
            }

            // Для external стрелок возвращаем минимум (они должны быть сверху)
            return double.MinValue;
        }

        public static List<ArrowSegment> CalculateArrowPath(
            DiagramBlock fromBlock,
            DiagramBlock toBlock,
            string arrowType,
            int indexOnSide = 0,
            int totalOnSide = 1)
        {
            if (arrowType == null)
                arrowType = "connect";

            switch (arrowType.ToLower())
            {
                case "left":
                    return CalculateLeftArrow(toBlock, indexOnSide, totalOnSide);
                case "right":
                    return CalculateRightArrow(fromBlock, indexOnSide, totalOnSide);
                case "top":
                    return CalculateTopArrow(toBlock, indexOnSide, totalOnSide);
                case "bottom":
                    return CalculateBottomArrow(toBlock, indexOnSide, totalOnSide);
                case "connect":
                default:
                    return CalculateConnectArrow(fromBlock, toBlock, indexOnSide, totalOnSide);
            }
        }

        private static List<ArrowSegment> CalculateLeftArrow(DiagramBlock toBlock, int index, int total)
        {
            var segments = new List<ArrowSegment>();
            double endX = toBlock.Left;
            double startX = endX - 80;
            double endY = CalculateDistributedY(toBlock, index, total);

            segments.Add(new ArrowSegment
            {
                Start = new Point(startX, endY),
                End = new Point(endX, endY),
                Direction = "right"
            });

            return segments;
        }

        private static List<ArrowSegment> CalculateRightArrow(DiagramBlock fromBlock, int index, int total)
        {
            var segments = new List<ArrowSegment>();
            double startX = fromBlock.Right;
            double endX = startX + 80;
            double startY = CalculateDistributedY(fromBlock, index, total);

            segments.Add(new ArrowSegment
            {
                Start = new Point(startX, startY),
                End = new Point(endX, startY),
                Direction = "right"
            });

            return segments;
        }

        private static List<ArrowSegment> CalculateTopArrow(DiagramBlock toBlock, int index, int total)
        {
            var segments = new List<ArrowSegment>();
            double minTop = GetMinTopY();
            double startY = minTop - 50; // ✅ Фиксированная высота (50px отступ)
            double endX = CalculateDistributedX(toBlock, index, total);
            double endY = toBlock.Top;

            segments.Add(new ArrowSegment
            {
                Start = new Point(endX, startY),
                End = new Point(endX, endY),
                Direction = "down"
            });

            return segments;
        }

        private static List<ArrowSegment> CalculateBottomArrow(DiagramBlock toBlock, int index, int total)
        {
            var segments = new List<ArrowSegment>();
            double maxBottom = GetMaxBottomY();
            double startY = maxBottom + 50; // ✅ Фиксированная высота (50px отступ)
            double endX = CalculateDistributedX(toBlock, index, total);
            double endY = toBlock.Bottom;

            segments.Add(new ArrowSegment
            {
                Start = new Point(endX, startY),
                End = new Point(endX, endY),
                Direction = "up"
            });

            return segments;
        }

        private static List<ArrowSegment> CalculateConnectArrow(
            DiagramBlock fromBlock,
            DiagramBlock toBlock,
            int index = 0,
            int total = 1)
        {
            var segments = new List<ArrowSegment>();
            if (fromBlock == null || toBlock == null)
                return segments;

            double connectYOffset = 18.0;
            double fromY = fromBlock.Top + fromBlock.Visual.Height / 2 + connectYOffset;
            double toY = toBlock.Top + toBlock.Visual.Height / 2;

            double startX = fromBlock.Right;
            double startY = fromY;
            double endX = toBlock.Left;
            double endY = toY;

            string endDirection = "right";

            if (Math.Abs(startY - endY) < 10)
            {
                segments.Add(new ArrowSegment
                {
                    Start = new Point(startX, startY),
                    End = new Point(endX, endY),
                    Direction = endDirection
                });
            }
            else
            {
                double midX = (startX + endX) / 2;
                segments.Add(new ArrowSegment
                {
                    Start = new Point(startX, startY),
                    End = new Point(midX, startY),
                    Direction = "right"
                });
                segments.Add(new ArrowSegment
                {
                    Start = new Point(midX, startY),
                    End = new Point(midX, endY),
                    Direction = endY > startY ? "down" : "up"
                });
                segments.Add(new ArrowSegment
                {
                    Start = new Point(midX, endY),
                    End = new Point(endX, endY),
                    Direction = endDirection
                });
            }

            return segments;
        }

        /// <summary>
        /// ✅ УЛУЧШЕННОЕ распределение по Y (для left/right сторон)
        /// </summary>
        private static double CalculateDistributedY(DiagramBlock block, int index, int total)
        {
            if (total <= 1)
                total = 1;

            if (total == 1)
                return block.Top + block.Visual.Height / 2;

            // ✅ Делим сторону на (n+1) отрезков
            double step = block.Visual.Height / (total + 1);
            return block.Top + step * (index + 1);
        }

        /// <summary>
        /// ✅ УЛУЧШЕННОЕ распределение по X (для top/bottom сторон)
        /// </summary>
        private static double CalculateDistributedX(DiagramBlock block, int index, int total)
        {
            if (total <= 1)
                total = 1;

            if (total == 1)
                return block.Left + block.Visual.Width / 2;

            // ✅ Делим сторону на (n+1) отрезков
            double step = block.Visual.Width / (total + 1);
            return block.Left + step * (index + 1);
        }

        private static double GetMinTopY()
        {
            if (allBlocks == null || allBlocks.Count == 0)
                return 200;
            return allBlocks.Values.Where(b => b.Visual.Width > 1).Min(b => b.Top);
        }

        private static double GetMaxBottomY()
        {
            if (allBlocks == null || allBlocks.Count == 0)
                return 300;
            return allBlocks.Values.Where(b => b.Visual.Width > 1).Max(b => b.Bottom);
        }
    }
}
