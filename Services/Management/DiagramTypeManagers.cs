using System;
using DiagramBuilder.Models;

namespace DiagramBuilder.Services
{
    /// <summary>
    /// Менеджер типов диаграмм с автоопределением
    /// </summary>
    public class DiagramTypeManagers
    {
        private DiagramType currentType = DiagramType.IDEF0;

        public DiagramType CurrentType
        {
            get => currentType;
            set => currentType = value;
        }

        /// <summary>
        /// Автоматическое определение типа диаграммы из текста
        /// </summary>
        public DiagramType DetectDiagramType(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return DiagramType.IDEF0;

            string[] lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                // Формат 1: Директива #TYPE:
                if (trimmed.StartsWith("#TYPE:", StringComparison.OrdinalIgnoreCase))
                {
                    string typeStr = trimmed.Substring(6).Trim().ToUpper();

                    if (typeStr == "IDEF0")
                        return DiagramType.IDEF0;
                    else if (typeStr == "NODETREE" || typeStr == "NODE_TREE" || typeStr == "NODE")
                        return DiagramType.NodeTree;
                    else if (typeStr == "FEO")
                        return DiagramType.FEO;
                    else if (typeStr == "IDEF3")
                        return DiagramType.IDEF3;
                    else if (typeStr == "DFD" || typeStr == "DATA FLOW DIAGRAM")
                        return DiagramType.DFD;
                }

                // Формат 2: Упоминание типа в комментарии
                string upperLine = trimmed.ToUpper();

                if (upperLine.Contains("DFD") || upperLine.Contains("DATA FLOW DIAGRAM") || upperLine.Contains("ПОТОКОВ ДАННЫХ"))
                {
                    return DiagramType.DFD;
                }
                else if (upperLine.Contains("IDEF0") || upperLine.Contains("ФУНКЦИОНАЛЬНАЯ МОДЕЛЬ"))
                {
                    return DiagramType.IDEF0;
                }
                else if (upperLine.Contains("ДЕРЕВО УЗЛОВ") || upperLine.Contains("NODE TREE") ||
                         upperLine.Contains("NODETREE") || upperLine.Contains("ИЕРАРХИЧЕСКАЯ СТРУКТУРА"))
                {
                    return DiagramType.NodeTree;
                }
                else if (upperLine.Contains("FEO") || upperLine.Contains("FOR EXPOSITION ONLY") ||
                         upperLine.Contains("АЛЬТЕРНАТИВНОЕ ПРЕДСТАВЛЕНИЕ"))
                {
                    return DiagramType.FEO;
                }
                else if (upperLine.Contains("IDEF3") || upperLine.Contains("ПРОЦЕССНАЯ МОДЕЛЬ") ||
                         upperLine.Contains("WORKFLOW"))
                {
                    return DiagramType.IDEF3;
                }

                // Если встретили первую строку данных - прекращаем поиск
                if (!trimmed.StartsWith("#"))
                    break;
            }

            return DiagramType.IDEF0;
        }

        /// <summary>
        /// Получить описание типа диаграммы
        /// </summary>
        public string GetDescription(DiagramType type)
        {
            switch (type)
            {
                case DiagramType.IDEF0:
                    return "IDEF0 - Функциональная модель";
                case DiagramType.NodeTree:
                    return "Дерево узлов";
                case DiagramType.FEO:
                    return "FEO - Альтернативное представление";
                case DiagramType.IDEF3:
                    return "IDEF3 - Процессная модель";
                case DiagramType.DFD:
                    return "DFD — Диаграмма потоков данных";
                default:
                    return "Неизвестный тип";
            }
        }

        /// <summary>
        /// Получить пример текста для типа диаграммы
        /// </summary>
        public string GetSampleText(DiagramType type)
        {
            switch (type)
            {
                case DiagramType.IDEF0:
                    return GetIDEF0Sample();
                case DiagramType.NodeTree:
                    return GetNodeTreeSample();
                case DiagramType.FEO:
                    return GetFEOSample();
                case DiagramType.IDEF3:
                    return GetIDEF3Sample();
                case DiagramType.DFD:
                    return GetDFDSample();
                default:
                    return "";
            }
        }

        private string GetIDEF0Sample()
        {
            return @"#TYPE:IDEF0
# IDEF0 - Функциональная модель
# Формат: BLOCK|код|текст|x|y|ширина|высота
# Формат: ARROW|откуда|куда|текст|тип

BLOCK|A1|Принять заявку|200|245|235|100
BLOCK|A2|Оформить приказ|200|480|245|100
BLOCK|A3|Оформить трудовой договор|520|480|250|100

ARROW|external_left|A1|Заявка|left
ARROW|external_top1|A1|Трудовой\nкодекс|top
ARROW|A1|A2||vertical
ARROW|A2|A3|Подписанный\nприказ|connect
ARROW|external_bottom1|A2|Руководитель\nорганизации|bottom";
        }

        private string GetNodeTreeSample()
        {
            return @"#TYPE:NODETREE
# Дерево узлов - Иерархическая структура

NODE|A0|Деятельность компании|0||300|50
NODE|A1|Продажи и маркетинг|1|A0|150|180
NODE|A2|Сборка и тестирование|1|A0|400|180
NODE|A3|Отгрузка|1|A0|650|180";
        }

        private string GetFEOSample()
        {
            return @"#TYPE:FEO
# FEO - Альтернативное представление

BLOCK|A1|Принять заявку|200|245|235|100
BLOCK|A2|Оформить приказ|200|480|245|100
ARROW|A1|A2|Основной путь|connect";
        }

        private string GetIDEF3Sample()
        {
            return @"#TYPE:IDEF3
# IDEF3 - Процессная модель

UOW|1|Подготовка компонентов|100|200|180|80
UOW|2|Установка материнской платы|350|200|220|80
LINK|1|2";
        }

        private string GetDFDSample()
        {
            return @"#TYPE:DFD
# DFD - Диаграмма потоков данных
# Формат: PROCESS|id|название|x|y
#         STORE|id|название|x|y
#         ENTITY|id|название|x|y
#         ARROW|from|to|label

PROCESS|P1|Сбор данных|100|100
PROCESS|P2|Обработка|300|200
STORE|S1|Архив|200|300
ENTITY|E1|Оператор|50|250
ARROW|E1|P1|Данные от оператора
ARROW|P1|P2|Поток данных
ARROW|P2|S1|Запись в архив";
        }
    }
}
