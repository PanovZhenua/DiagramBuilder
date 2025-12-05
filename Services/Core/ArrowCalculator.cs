using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DiagramBuilder.Models;

namespace DiagramBuilder.Services.Core
{
    /// <summary>
    /// Сегмент стрелки между двумя точками
    /// </summary>
    public class ArrowSegment
    {
        public Point Start { get; set; } // Начало сегмента
        public Point End { get; set; }   // Конец сегмента
        public string Direction { get; set; } // Направление ("up", "down", "right")
    }

    /// <summary>
    /// Класс для вычисления траекторий стрелок между блоками схемы
    /// </summary>
    public static class ArrowCalculator
    {
        private static Dictionary<string, DiagramBlock> allBlocks; // Все блоки схемы для глобальных расчетов

        // ================== БАЗОВЫЕ МЕТОДЫ ==================

        /// <summary>Передать коллекцию всех блоков для последующих вычислений</summary>
        public static void SetAllBlocks(Dictionary<string, DiagramBlock> blocks)
        {
            allBlocks = blocks;
        }

        /// <summary>
        /// Препроцессинг стрелок для наглядного распределения по сторонам: 
        /// сначала идут обычные (plain), потом connect (связанные)
        /// </summary>
        public static List<ArrowData> PreprocessArrows(List<ArrowData> arrows, Dictionary<string, DiagramBlock> blocks)
        {
            var processed = new List<ArrowData>();

            // Группировка стрелок по блоку и стороне
            var groups = arrows.GroupBy(a =>
            {
                string blockCode = "";
                string side = a.Type ?? "connect";
                if (side == "left" || side == "top" || side == "bottom") blockCode = a.To;
                else blockCode = a.From; // "right" и "connect"
                return blockCode + "|" + side; // уникальный ключ группы
            });

            foreach (var group in groups)
            {
                var plain = group.Where(a => a.Type != "connect").ToList();
                var connected = group.Where(a => a.Type == "connect").ToList();

                int totalCount = plain.Count + connected.Count;
                int idx = 0;
                foreach (var arrow in plain)
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
                foreach (var arrow in connected)
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
            return processed;
        }

        /// <summary>
        /// Основной точка входа: рассчитывает сегменты стрелки по типу и позиции
        /// </summary>
        public static List<ArrowSegment> CalculateArrowPath(
            DiagramBlock fromBlock,
            DiagramBlock toBlock,
            string arrowType,
            int indexOnSide = 0,
            int totalOnSide = 1)
        {
            if (arrowType == null) arrowType = "connect";
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

        // ================== МЕТОДЫ ДЛЯ РАССЧЕТА СТРЕЛОК ==================

        /// <summary>Короткая стрелка влево от блока</summary>
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

        /// <summary>Короткая стрелка вправо от блока</summary>
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

        /// <summary>Стрелка сверху вниз к блоку</summary>
        private static List<ArrowSegment> CalculateTopArrow(DiagramBlock toBlock, int index, int total)
        {
            var segments = new List<ArrowSegment>();
            double minTop = GetMinTopY();
            double startY = minTop - 60;
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

        /// <summary>Стрелка снизу вверх к блоку</summary>
        private static List<ArrowSegment> CalculateBottomArrow(DiagramBlock toBlock, int index, int total)
        {
            var segments = new List<ArrowSegment>();
            double maxBottom = GetMaxBottomY();
            double startY = maxBottom + 60;
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

        /// <summary>
        /// Прямая или сложная connect стрелка между блоками
        /// (если блоки на одной оси - прямая, иначе излом)
        /// </summary>
        private static List<ArrowSegment> CalculateConnectArrow(
            DiagramBlock fromBlock, DiagramBlock toBlock,
            int index = 0, int total = 1)
        {
            var segments = new List<ArrowSegment>();
            if (fromBlock == null || toBlock == null) return segments;

            double connectYOffset = 18.0; // смещение для связи: чуть ниже центра
            double fromY = fromBlock.Top + fromBlock.Visual.Height / 2;
            double toY = toBlock.Top + toBlock.Visual.Height / 2 + connectYOffset;
            double startX = fromBlock.Right;
            double startY = fromY + connectYOffset;
            double endX = toBlock.Left;
            double endY = toY;

            // Определяем, к какой стороне ближе точка входа в toBlock
            string endDirection;
            if (Math.Abs(endY - toBlock.Top) < Math.Abs(endY - toBlock.Bottom) &&
                Math.Abs(endY - toBlock.Top) < Math.Abs(endX - toBlock.Left) &&
                Math.Abs(endY - toBlock.Top) < Math.Abs(endX - toBlock.Right))
            {
                endDirection = "down"; // входим сверху в блок
            }
            else if (Math.Abs(endY - toBlock.Bottom) < Math.Abs(endY - toBlock.Top) &&
                     Math.Abs(endY - toBlock.Bottom) < Math.Abs(endX - toBlock.Left) &&
                     Math.Abs(endY - toBlock.Bottom) < Math.Abs(endX - toBlock.Right))
            {
                endDirection = "up"; // входим снизу в блок
            }
            else
            {
                endDirection = "right"; // по умолчанию считаем, что входим слева блока
            }

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

        // ========== Распределение стрелок на стороне блока ==========
        private static double CalculateDistributedY(DiagramBlock block, int index, int total)
        {
            if (total < 1) total = 1;
            if (total == 1) return block.Top + block.Visual.Height / 2;
            double step = block.Visual.Height / (total + 1);
            return block.Top + step * (index + 1);
        }

        private static double CalculateDistributedX(DiagramBlock block, int index, int total)
        {
            if (total < 1) total = 1;
            if (total == 1) return block.Left + block.Visual.Width / 2;
            double step = block.Visual.Width / (total + 1);
            return block.Left + step * (index + 1);
        }

        // ========== Глобальные минимумы и максимумы для canvas ==========
        private static double GetMinTopY()
        {
            if (allBlocks == null || allBlocks.Count == 0)
                return 200;
            return allBlocks.Values.Min(b => b.Top);
        }

        private static double GetMaxBottomY()
        {
            if (allBlocks == null || allBlocks.Count == 0)
                return 300;
            return allBlocks.Values.Max(b => b.Bottom);
        }
    }
}
