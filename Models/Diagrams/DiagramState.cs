using DiagramBuilder.Services;
using DiagramBuilder.Services.Core;
using DiagramBuilder.Services.Parsing;
using DiagramBuilder.Services.Rendering;
using System.Collections.Generic;
using System.Windows;

namespace DiagramBuilder.Models
{
    public class DiagramState
    {
        // IDEF0
        public List<ArrowData> RawArrowData { get; set; } = new List<ArrowData>();

        // FEO
        public FEODiagram CurrentFEO { get; set; }
        public Dictionary<string, DiagramBlock> FeoBlocks { get; set; } = new Dictionary<string, DiagramBlock>();
        public List<DiagramArrow> FeoArrows { get; set; } = new List<DiagramArrow>();
        public FEORenderer FeoRenderer { get; set; }

        // NodeTree
        public List<DiagramParser.NodeData> NodeTreeData { get; set; }
        public NodeTreeRenderer NodeTreeRenderer { get; set; }

        // IDEF3
        public List<IDEF3UOW> LastUows { get; set; } = new List<IDEF3UOW>();
        public List<IDEF3Junction> LastJunctions { get; set; } = new List<IDEF3Junction>();
        public List<IDEF3Link> LastLinks { get; set; } = new List<IDEF3Link>();
        public IDEF3Renderer Idef3Renderer { get; set; }

        // DFD
        public DFDDiagram CurrentDFD { get; set; }
        public DFDRenderer DfdRenderer { get; set; }
        public Dictionary<string, FrameworkElement> DfdBlockVisuals { get; set; } = new Dictionary<string, FrameworkElement>();

        // DocumentFlow
        public DocumentFlowDiagram CurrentDocumentFlow { get; set; }
        public DocumentFlowRenderer DocumentFlowRenderer { get; set; }
        public Dictionary<string, FrameworkElement> DocumentFlowBlockVisuals { get; set; } = new Dictionary<string, FrameworkElement>();

        // ERD
        public ERDDiagram CurrentERD { get; set; }
        public ERDRenderer ErdRenderer { get; set; }
        public Dictionary<string, FrameworkElement> ErdEntityVisuals { get; set; } = new Dictionary<string, FrameworkElement>();

        // UseCase
        public UseCaseDiagram CurrentUseCase { get; set; }
        public UseCaseRenderer UseCaseRenderer { get; set; }
        public Dictionary<string, FrameworkElement> UseCaseVisuals { get; set; } = new Dictionary<string, FrameworkElement>();  // ✅ ДОБАВИЛ ИНИЦИАЛИЗАЦИЮ
    }
}
