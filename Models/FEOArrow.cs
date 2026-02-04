public class FEOArrow
{
    public FEOComponent From { get; set; }
    public FEOComponent To { get; set; }
    public string Label { get; set; }
    public string SideFrom { get; set; } = "right";
    public string SideTo { get; set; } = "left";

    // Координаты для label
    public double? LabelX { get; set; }
    public double? LabelY { get; set; }
}
