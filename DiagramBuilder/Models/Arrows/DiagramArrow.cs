using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace DiagramBuilder.Models
{
    public class DiagramArrow
    {
        public DiagramBlock FromBlock { get; set; }
        public DiagramBlock ToBlock { get; set; }
        public List<Line> Lines { get; set; } = new List<Line>();
        public Polyline Polyline { get; set; }
        public Polygon ArrowHead { get; set; }
        public TextBlock Label { get; set; }
        public string FromId { get; set; }
        public string ToId { get; set; }
        public string ArrowType { get; set; }
        public int IndexOnSide { get; set; } = 0;
        public int TotalOnSide { get; set; } = 1;
    }
}
