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
                    else if (typeStr == "DOCUMENTFLOW" || typeStr == "DOCUMENT_FLOW" ||
                             typeStr == "DOCFLOW" || typeStr == "DOCUMENT FLOW")
                        return DiagramType.DocumentFlow;
                    else if (typeStr == "ERD")
                        return DiagramType.ERD;
                    else if (typeStr == "USECASE" || typeStr == "USE_CASE" || typeStr == "USE CASE")
                        return DiagramType.UseCase;
                }

                // Формат 2: Упоминание типа в комментарии
                string upperLine = trimmed.ToUpper();

                if (upperLine.Contains("DOCUMENTFLOW") || upperLine.Contains("DOCUMENT FLOW") ||
                    upperLine.Contains("ДОКУМЕНТООБОРОТ") || upperLine.Contains("СХЕМА ДОКУМЕНТООБОРОТА"))
                {
                    return DiagramType.DocumentFlow;
                }
                else if (upperLine.Contains("DFD") || upperLine.Contains("DATA FLOW DIAGRAM") || upperLine.Contains("ПОТОКОВ ДАННЫХ"))
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
                else if (upperLine.Contains("ERD") || upperLine.Contains("ENTITY-RELATIONSHIP") ||
                         upperLine.Contains("СУЩНОСТЬ-СВЯЗЬ"))
                {
                    return DiagramType.ERD;
                }
                else if (upperLine.Contains("USECASE") || upperLine.Contains("USE CASE") ||
                         upperLine.Contains("USE-CASE") || upperLine.Contains("ВАРИАНТЫ ИСПОЛЬЗОВАНИЯ") ||
                         upperLine.Contains("ДИАГРАММА ВАРИАНТОВ"))
                {
                    return DiagramType.UseCase;
                }

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
                case DiagramType.DocumentFlow:
                    return "DocumentFlow - Схема документооборота";
                case DiagramType.ERD:
                    return "ERD - диаграмма сущность‑связь (таблицы и связи PK→FK)";
                case DiagramType.UseCase:
                    return "UseCase - Диаграмма вариантов использования";
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
                case DiagramType.DocumentFlow:
                    return GetDocumentFlowSample();
                case DiagramType.ERD:
                    return GetERDSample();
                case DiagramType.UseCase:
                    return GetUseCaseSample();
                default:
                    return "";
            }
        }

        private string GetDocumentFlowSample()
        {
            return @"#TYPE:DOCUMENTFLOW
# DFD-представление схемы документооборота
# Используются стандартные элементы DFD:
#   ACTOR  -> внешний объект (люди/роли)
#   PROCESS -> операция с документами
#   STORE   -> архив / база
#   ARROW   -> движение документа

ACTOR|OP|Оператор|80|220
ACTOR|SOP|Старший оператор|80|360
ACTOR|DIR|Директор|520|320
ACTOR|DBA|Администратор БД|520|130

PROCESS|P1|Оформление отчёта о работе|260|230
PROCESS|P2|Формирование планового отчёта|260|360
PROCESS|P3|Подготовка отчёта о ведении БД|520|210

STORE|ST1|Архив отчётов|360|460

ARROW|OP|P1|Исходные данные
ARROW|P1|SOP|Отчёт о работе
ARROW|DIR|P1|Указания к работе
ARROW|SOP|P2|Сводные данные
ARROW|P2|DIR|Плановый отчёт
ARROW|DBA|P3|Журналы БД
ARROW|P3|DIR|Отчёт о ведении БД
ARROW|P2|ST1|Сохранение планового отчёта
ARROW|P3|ST1|Сохранение отчёта о БД";
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

        private string GetERDSample()
        {
            return @"#TYPE:ERD

TABLE Клиент
FIELD КлиентID INT PK
FIELD ФИО NVARCHAR(200)
FIELD ДатаРождения DATE
FIELD Телефон NVARCHAR(50)
END

TABLE Заказ
FIELD ЗаказID INT PK
FIELD ДатаЗаказа DATE
FIELD КлиентID INT FK
FIELD УслугаID INT FK
END

TABLE Услуга
FIELD УслугаID INT PK
FIELD Название NVARCHAR(200)
FIELD Стоимость DECIMAL
END

REL Клиент.КлиентID -> Заказ.КлиентID
REL Услуга.УслугаID -> Заказ.УслугаID";
        }

        private string GetUseCaseSample()
        {
            return @"#TYPE:USECASE
# UseCase - Диаграмма вариантов использования

// Акторы
ACTOR|A1|Пользователь|100|200
ACTOR|A2|Администратор|700|200

// Варианты использования
USECASE|UC1|Войти в систему|350|150
USECASE|UC2|Просмотреть данные|350|250
USECASE|UC3|Проверить права|550|200
USECASE|UC4|Управление пользователями|550|300

// Связи
LINK|A1|UC1|association
LINK|A1|UC2|association
LINK|UC1|UC3|include
LINK|A2|UC4|association
LINK|UC2|UC3|include";
        }
    }
}
