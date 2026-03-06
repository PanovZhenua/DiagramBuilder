// Models/DocumentFlowModels.cs
using System.Collections.Generic;

namespace DiagramBuilder.Models
{
    /// <summary>
    /// Схема документооборота
    /// </summary>
    public class DocumentFlowDiagram
    {
        public List<DocFlowProcess> Processes { get; set; } = new List<DocFlowProcess>();
        public List<DocFlowEntity> Entities { get; set; } = new List<DocFlowEntity>();
        public List<DocFlowDocument> Documents { get; set; } = new List<DocFlowDocument>();
        public List<DocFlowArrow> Arrows { get; set; } = new List<DocFlowArrow>();
    }

    /// <summary>
    /// PROCESS - прямоугольник (функция/процесс)
    /// </summary>
    public class DocFlowProcess
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 160; // По умолчанию
        public double MinHeight { get; set; } = 50; // Минимальная высота
    }

    /// <summary>
    /// ENTITY - овал (внешняя сущность/актор)
    /// </summary>
    public class DocFlowEntity
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 140;
        public double MinHeight { get; set; } = 60;
    }

    /// <summary>
    /// DOCFLOW - физический документ (с волнистым низом)
    /// </summary>
    public class DocFlowDocument
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 150;
        public double MinHeight { get; set; } = 55;
    }

    public class DocFlowArrow
    {
        public string FromId { get; set; }
        public string ToId { get; set; }
        public string Label { get; set; }
    }
}
