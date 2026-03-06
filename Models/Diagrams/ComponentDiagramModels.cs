using System.Collections.Generic;

namespace DiagramBuilder.Models
{
    public class ComponentDiagram
    {
        public List<ComponentNode> Components { get; set; } = new List<ComponentNode>();
        public List<DatabaseNode> Databases { get; set; } = new List<DatabaseNode>();

        // Связи общие: могут соединять компоненты, БД и workflow-узлы по Id
        public List<ComponentLink> Links { get; set; } = new List<ComponentLink>();

        // Новое: элементы "Схема работы системы" внутри #TYPE:COMPONENTDIAGRAM
        public List<WorkflowNode> WorkflowNodes { get; set; } = new List<WorkflowNode>();
    }

    /// <summary>
    /// Обычный компонент (UML component-rectangle)
    /// </summary>
    public class ComponentNode
    {
        public string Id { get; set; }
        public string Name { get; set; }

        // Позиция на Canvas
        public double X { get; set; }
        public double Y { get; set; }

        // Размер (для ресайза)
        public double Width { get; set; } = 170;
        public double Height { get; set; } = 46;

        // Ограничения ресайза
        public double MinWidth { get; set; } = 120;
        public double MinHeight { get; set; } = 36;
    }

    /// <summary>
    /// База данных (цилиндр)
    /// </summary>
    public class DatabaseNode
    {
        public string Id { get; set; }
        public string Name { get; set; }

        // Позиция на Canvas
        public double X { get; set; }
        public double Y { get; set; }

        // Размер (для ресайза)
        public double Width { get; set; } = 260;
        public double Height { get; set; } = 62;

        // Ограничения ресайза
        public double MinWidth { get; set; } = 180;
        public double MinHeight { get; set; } = 52;
    }

    /// <summary>
    /// Узел "Схемы работы системы" (flowchart внутри ComponentDiagram)
    /// </summary>
    public class WorkflowNode
    {
        public string Id { get; set; }

        // Текст внутри фигуры
        public string Text { get; set; }

        // Тип фигуры
        public WorkflowNodeType Type { get; set; }

        // Позиция на Canvas
        public double X { get; set; }
        public double Y { get; set; }

        // Размер (для ресайза)
        public double Width { get; set; } = 180;
        public double Height { get; set; } = 56;

        // Ограничения ресайза
        public double MinWidth { get; set; } = 120;
        public double MinHeight { get; set; } = 40;
    }

    public enum WorkflowNodeType
    {
        /// <summary>Круг: начало/конец</summary>
        StartEnd,

        /// <summary>Прямоугольник: процесс</summary>
        Process,

        /// <summary>Цилиндр: база данных (для схемы)</summary>
        Database,

        /// <summary>Трапеция: данные</summary>
        Data,

        /// <summary>Овал: форма/интерфейс</summary>
        Form
    }

    public class ComponentLink
    {
        public string FromId { get; set; }
        public string ToId { get; set; }
        public string Label { get; set; } // опционально
    }
}
