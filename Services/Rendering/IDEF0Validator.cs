using System.Collections.Generic;
using System.Linq;
using DiagramBuilder.Models;

namespace DiagramBuilder.Services
{
    /// <summary>
    /// Валидатор диаграмм IDEF0
    /// </summary>
    public static class IDEF0Validator
    {
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; }
            public List<string> Warnings { get; set; }

            public ValidationResult()
            {
                Errors = new List<string>();
                Warnings = new List<string>();
                IsValid = true;
            }
        }

        public static ValidationResult Validate(Dictionary<string, DiagramBlock> blocks, List<DiagramArrow> arrows)
        {
            var result = new ValidationResult();

            // Проверка: минимум один блок
            if (blocks.Count == 0)
            {
                result.Errors.Add("❌ Диаграмма должна содержать хотя бы один блок");
                result.IsValid = false;
                return result;
            }

            // Проверка: наличие входных и выходных стрелок
            foreach (var block in blocks.Values)
            {
                // ИСПРАВЛЕНО: правильное сравнение блоков
                var incomingArrows = arrows.Where(a => a.ToBlock != null && a.ToBlock.Code == block.Code).ToList();
                var outgoingArrows = arrows.Where(a => a.FromBlock != null && a.FromBlock.Code == block.Code).ToList();

                // ИСПРАВЛЕНО: правильный подсчёт стрелок
                if (incomingArrows.Count == 0)
                {
                    result.Warnings.Add($"⚠ Блок {block.Code} не имеет входящих стрелок");
                }

                if (outgoingArrows.Count == 0)
                {
                    result.Warnings.Add($"⚠ Блок {block.Code} не имеет исходящих стрелок");
                }
            }

            // Проверка: наличие кода блока
            foreach (var block in blocks.Values)
            {
                if (string.IsNullOrWhiteSpace(block.Code))
                {
                    result.Errors.Add($"❌ Блок без кода обнаружен");
                    result.IsValid = false;
                }

                if (string.IsNullOrWhiteSpace(block.Text))
                {
                    result.Warnings.Add($"⚠ Блок {block.Code} не имеет описания");
                }
            }

            // Проверка: уникальность кодов блоков
            var duplicates = blocks.GroupBy(b => b.Key)
                                   .Where(g => g.Count() > 1)
                                   .Select(g => g.Key)
                                   .ToList();

            if (duplicates.Count > 0)
            {
                result.Errors.Add($"❌ Обнаружены дублирующиеся коды блоков: {string.Join(", ", duplicates)}");
                result.IsValid = false;
            }

            // Проверка: стрелки указывают на существующие блоки
            foreach (var arrow in arrows)
            {
                if (arrow.FromBlock == null)
                {
                    result.Warnings.Add("⚠ Найдена стрелка с отсутствующим источником");
                }

                if (arrow.ToBlock == null)
                {
                    result.Warnings.Add("⚠ Найдена стрелка с отсутствующей целью");
                }
            }

            return result;
        }
    }
}
