using System.Collections.Generic;

public class FEOComponent
{
    public string Code { get; set; }
    public string Name { get; set; }
    public double X { get; set; } // Положение в разметке
    public double Y { get; set; }
    public double Width { get; set; } = 180;
    public double Height { get; set; } = 60;
    public List<FEOArrow> Outputs { get; } = new List<FEOArrow>();
    public List<FEOArrow> Inputs { get; } = new List<FEOArrow>();
}
