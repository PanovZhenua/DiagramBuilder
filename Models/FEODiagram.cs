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

    public class FEODiagram
    {
        public List<FEOComponent> Components { get; set; } = new List<FEOComponent>();
        public List<ArrowData> Arrows { get; set; } = new List<ArrowData>();

        public void AutoLayout(double startX = 100, double startY = 100, double stepY = 120)
        {
            for (int i = 0; i < Components.Count; i++)
            {
                Components[i].X = startX;
                Components[i].Y = startY + i * stepY;
            }
        }
    }
}
