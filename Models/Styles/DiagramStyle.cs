using System.Windows.Media;

namespace DiagramBuilder.Services.Core
{
    public enum DiagramStyleType
    {
        ClassicBlackWhite,       // строгий чёрно-белый
        SoftPastel,
        Presentation,  // светло-голубой
        Blueprint
    }

    public class DiagramStyle
    {
        public Brush BlockFill { get; set; }
        public Brush BlockBorder { get; set; }
        public Brush BlockShadow { get; set; }
        public Brush Text { get; set; }
        public Brush CodeText { get; set; }
        public Brush Line { get; set; }
        public Brush LineArrowHead { get; set; }
        public Brush JunctionFill { get; set; }
        public Brush JunctionBorder { get; set; }
        public Brush LabelBackground { get; set; }
        public Brush LabelText { get; set; }

        public static DiagramStyle GetStyle(DiagramStyleType style)
        {
            switch (style)
            {
                case DiagramStyleType.SoftPastel:
                    return new DiagramStyle
                    {
                        BlockFill = new SolidColorBrush(Color.FromRgb(255, 253, 235)),
                        BlockBorder = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                        BlockShadow = Brushes.LightGray,
                        Text = new SolidColorBrush(Color.FromRgb(25, 35, 45)),
                        CodeText = new SolidColorBrush(Color.FromRgb(60, 120, 200)),
                        Line = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                        LineArrowHead = new SolidColorBrush(Color.FromRgb(60, 120, 200)),
                        JunctionFill = Brushes.White,
                        JunctionBorder = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                        LabelBackground = Brushes.WhiteSmoke,
                        LabelText = Brushes.Black
                    };
                case DiagramStyleType.Presentation:
                    return new DiagramStyle
                    {
                        BlockFill = new SolidColorBrush(Color.FromRgb(237, 245, 251)),
                        BlockBorder = new SolidColorBrush(Color.FromRgb(56, 111, 160)),
                        BlockShadow = new SolidColorBrush(Color.FromRgb(200, 220, 255)),
                        Text = new SolidColorBrush(Color.FromRgb(25, 35, 45)),
                        CodeText = new SolidColorBrush(Color.FromRgb(40, 84, 143)),
                        Line = new SolidColorBrush(Color.FromRgb(40, 84, 143)),
                        LineArrowHead = new SolidColorBrush(Color.FromRgb(40, 84, 143)),
                        JunctionFill = new SolidColorBrush(Color.FromRgb(224, 240, 255)),
                        JunctionBorder = new SolidColorBrush(Color.FromRgb(40, 84, 143)),
                        LabelBackground = Brushes.White,
                        LabelText = Brushes.Black
                    };
                case DiagramStyleType.Blueprint:
                    return new DiagramStyle
                    {
                        BlockFill = Brushes.White,
                        BlockBorder = new SolidColorBrush(Color.FromRgb(84, 139, 212)),
                        BlockShadow = Brushes.LightGray,
                        Text = Brushes.Black,
                        CodeText = new SolidColorBrush(Color.FromRgb(84, 139, 212)),
                        Line = new SolidColorBrush(Color.FromRgb(84, 139, 212)),
                        LineArrowHead = new SolidColorBrush(Color.FromRgb(84, 139, 212)),
                        JunctionFill = Brushes.White,
                        JunctionBorder = new SolidColorBrush(Color.FromRgb(84, 139, 212)),
                        LabelBackground = Brushes.White,
                        LabelText = Brushes.Black
                    };
                default: // Classic строгий
                    return new DiagramStyle
                    {
                        BlockFill = Brushes.White,
                        BlockBorder = Brushes.Black,
                        BlockShadow = Brushes.LightGray,
                        Text = Brushes.Black,
                        CodeText = Brushes.Navy,
                        Line = Brushes.Black,
                        LineArrowHead = Brushes.Black,
                        JunctionFill = Brushes.White,
                        JunctionBorder = Brushes.Black,
                        LabelBackground = Brushes.White,
                        LabelText = Brushes.Black
                    };
            }
        }

        // Для обратной совместимости:
        public static DiagramStyle GetStyleFromName(string name)
        {
            if (name == "ClassicBlackWhite")
                return GetStyle(DiagramStyleType.ClassicBlackWhite);
            if (name == "SoftPastel")
                return GetStyle(DiagramStyleType.SoftPastel);
            if (name == "Blueprint")
                return GetStyle(DiagramStyleType.Blueprint);
            if (name == "Presentation")
                return GetStyle(DiagramStyleType.Presentation);
            return GetStyle(DiagramStyleType.Presentation);
        }
    }
}
