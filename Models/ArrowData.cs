namespace DiagramBuilder.Models
{
    public class ArrowData
    {
        public string From { get; set; }
        public string To { get; set; }
        public string Label { get; set; }
        public string Type { get; set; }
        public int IndexOnSide { get; set; } // Индекс стрелки на стороне блока
        public int TotalOnSide { get; set; } // Общее кол-во стрелок на этой стороне
    }
}
