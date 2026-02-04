// =================== Models/DFDDiagram.cs (ОБЩИЕ МОДЕЛИ) ===================
using System.Collections.Generic;

namespace DiagramBuilder.Models
{
    public class DFDDiagram
    {
        public List<DFDProcess> Processes { get; set; } = new List<DFDProcess>();
        public List<DFDEntity> Entities { get; set; } = new List<DFDEntity>();
        public List<DFDStore> Stores { get; set; } = new List<DFDStore>();
        public List<DFDDocFlow> DocFlows { get; set; } = new List<DFDDocFlow>();
        public List<DFDArrow> Arrows { get; set; } = new List<DFDArrow>();
    }

    public class DFDProcess
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 140;
        public double Height { get; set; } = 80;
    }

    public class DFDEntity
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 100;
        public double Height { get; set; } = 60;
    }

    public class DFDStore
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 120;
        public double Height { get; set; } = 80;
    }

    public class DFDDocFlow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 150;
        public double Height { get; set; } = 80;
    }

    public class DFDArrow
    {
        public string FromId { get; set; }
        public string ToId { get; set; }
        public string Label { get; set; }
    }
}
