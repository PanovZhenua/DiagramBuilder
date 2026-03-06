using System.Collections.Generic;

namespace DiagramBuilder.Models
{
    public class FEOComponent
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 120;
        public double Height { get; set; } = 60;
    }

    // ✅ НОВЫЙ КЛАСС
    public class FEOAnnotation
    {
        public string ArrowFromId { get; set; }
        public string ArrowToId { get; set; }
        public string Text { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; } = 20;
    }

    public class FEODiagram
    {
        public List<FEOComponent> Components { get; set; } = new List<FEOComponent>();
        public List<ArrowData> Arrows { get; set; } = new List<ArrowData>();
        public List<FEOAnnotation> Annotations { get; set; } = new List<FEOAnnotation>(); // ✅ НОВОЕ!
    }
}
