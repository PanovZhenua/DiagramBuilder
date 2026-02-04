using System.Collections.Generic;
using System.Windows.Shapes;

namespace DiagramBuilder.Models
{
    /// <summary>
    /// Связь между элементами диаграммы (линии, стрелки)
    /// </summary>
    public class ConnectionLine
    {
        public List<Line> Lines { get; set; } = new List<Line>();
        public DiagramBlock FromBlock { get; set; }
        public DiagramBlock ToBlock { get; set; }
        public string FromId { get; set; }
        public string ToId { get; set; }

        public ConnectionLine()
        {
            Lines = new List<Line>();
        }
    }
}
