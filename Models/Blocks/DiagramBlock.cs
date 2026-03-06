using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace DiagramBuilder.Models
{
    public class DiagramBlock
    {
        public Border Visual { get; set; }
        public TextBlock Label { get; set; }
        public TextBlock CodeLabel { get; set; }
        public string Code { get; set; }
        public string Text { get; set; }
        public double X
        {
            get => Canvas.GetLeft(Visual);
            set => Canvas.SetLeft(Visual, value);
        }
        public double Y
        {
            get => Canvas.GetTop(Visual);
            set => Canvas.SetTop(Visual, value);
        }
        public double Left => X;
        public double Top => Y;
        public double Right => X + Visual.Width;
        public double Bottom => Y + Visual.Height;
        public double Width => Visual.Width;
        public double Height => Visual.Height;
        public Point Center => new Point(Left + Width / 2, Top + Height / 2);
        public Point LeftPoint => new Point(Left, Top + Height / 2);
        public Point RightPoint => new Point(Right, Top + Height / 2);
        public Point TopPoint => new Point(Left + Width / 2, Top);
        public Point BottomPoint => new Point(Left + Width / 2, Bottom);

        public List<string> Lines { get; set; } = new List<string>();
        public int LineCount => Lines?.Count ?? 1;

        public Point GetConnectionPoint(string side)
        {
            switch (side?.ToLower())
            {
                case "left": return LeftPoint;
                case "right": return RightPoint;
                case "top": return TopPoint;
                case "bottom": return BottomPoint;
                default: return Center;
            }
        }
    }
}
