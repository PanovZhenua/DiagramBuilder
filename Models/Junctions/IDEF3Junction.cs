using System.Collections.Generic;
using DiagramBuilder.Models;

namespace DiagramBuilder.Services
{
    public class IDEF3Junction
    {
        public string Id { get; set; }
        public string Type { get; set; } // AND, XOR, OR
        public double X { get; set; }
        public double Y { get; set; }
    }
    // Управление типами диаграмм
    public class DiagramTypeManager
    {
        private DiagramType currentType = DiagramType.IDEF0;

        public DiagramType CurrentType
        {
            get => currentType;
            set => currentType = value;
        }

        // Получить описание типа диаграммы
        public string GetDescription(DiagramType type)
        {
            switch (type)
            {
                case DiagramType.IDEF0:
                    return "IDEF0 - Функциональная модель (входы, выходы, управления, механизмы)";
                case DiagramType.NodeTree:
                    return "Дерево узлов - Иерархическая структура без связей";
                case DiagramType.FEO:
                    return "FEO - Альтернативное представление (без синтаксического контроля)";
                case DiagramType.IDEF3:
                    return "IDEF3 - Процессная модель (последовательность работ, перекрёстки)";
                default:
                    return "Неизвестный тип";
            }
        }

        // Получить пример текста для типа диаграммы
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
                default:
                    return "";
            }
        }

        private string GetIDEF0Sample()
        {
            return @"# IDEF0 - Функциональная модель
BLOCK|A1|Принять заявку|200|245|235|100
BLOCK|A2|Оформить приказ|200|480|245|100
ARROW|external_left|A1|Заявка|left
ARROW|external_top1|A1|Трудовой кодекс|top
ARROW|A1|A2||vertical";
        }

        private string GetNodeTreeSample()
        {
            return @"# Дерево узлов - Иерархическая структура
# Формат: NODE|код|имя|уровень|родитель
NODE|A0|Деятельность компании|0|
NODE|A1|Продажи и маркетинг|1|A0
NODE|A2|Сборка и тестирование|1|A0
NODE|A3|Отгрузка и получение|1|A0
NODE|A1.1|Обработка заявок|2|A1
NODE|A1.2|Формирование предложений|2|A1
NODE|A2.1|Сборка настольных ПК|2|A2
NODE|A2.2|Сборка ноутбуков|2|A2";
        }

        private string GetFEOSample()
        {
            return @"# FEO - Альтернативное представление
# Те же блоки, но можно нарушать правила IDEF0
BLOCK|A1|Принять заявку|200|245|235|100
BLOCK|A2|Оформить приказ|200|480|245|100
# Можно добавлять нестандартные связи
ARROW|A1|A2|Альтернативный\nпуть|connect";
        }

        private string GetIDEF3Sample()
        {
            return @"# IDEF3 - Процессная модель
# Формат: UOW|номер|название|x|y|ширина|высота
# Формат: JUNCTION|код|тип|x|y (тип: AND, OR, XOR)
# Формат: LINK|откуда|куда|тип (Precedence, Relational, ObjectFlow)

UOW|1|Подготовка компонентов|100|200|180|80
UOW|2|Установка материнской платы|350|200|200|80
UOW|3|Установка модема|350|320|160|80
UOW|4|Установка CD-ROM|350|440|160|80

JUNCTION|J1|OR|600|300|30
JUNCTION|J2|XOR|800|300|30

LINK|1|2|Precedence
LINK|2|J1|Precedence
LINK|J1|3|Precedence
LINK|J1|4|Precedence";
        }
    }
}
