using System.Collections.Generic;

namespace DiagramBuilder.Models 
{
    /// <summary>
    /// Актор (Actor) в Use Case диаграмме.
    /// </summary>
    public class UseCaseActor
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }

        public double Width { get; set; } = 80.0;
        public double Height { get; set; } = 100.0;
    }

    public class UseCaseElement
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 160.0;
        public double Height { get; set; } = 80.0;
    }

    public class UseCaseLink
    {
        public string Id { get; set; }
        public string FromId { get; set; }
        public string ToId { get; set; }
        public string Type { get; set; } = "association";
        public string Label { get; set; }
    }

    public class UseCaseDiagram
    {
        public List<UseCaseActor> Actors { get; set; } = new List<UseCaseActor>();
        public List<UseCaseElement> UseCases { get; set; } = new List<UseCaseElement>();
        public List<UseCaseLink> Links { get; set; } = new List<UseCaseLink>();
    }
}
