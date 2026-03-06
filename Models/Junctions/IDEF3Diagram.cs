using DiagramBuilder.Services;
using System.Collections.Generic;

namespace DiagramBuilder.Models
{
    public class IDEF3Diagram
    {
        public List<IDEF3UOW> UnitOfWorks { get; set; } = new List<IDEF3UOW>();
        public List<IDEF3Junction> Junctions { get; set; } = new List<IDEF3Junction>();
        public List<IDEF3Link> Links { get; set; } = new List<IDEF3Link>();
    }
}
