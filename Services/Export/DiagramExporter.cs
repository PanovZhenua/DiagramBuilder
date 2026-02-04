using DiagramBuilder.Models;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DiagramBuilder.Services
{
    public static class DiagramExporter
    {
        public static string ExportToDesktop(Canvas canvas, double minX, double minY,
            double maxX, double maxY, DiagramType diagramType)
        {
            double margin = 30;
            double width = maxX - minX + 2 * margin;
            double height = maxY - minY + 2 * margin;

            RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                (int)width, (int)height, 96, 96, PixelFormats.Pbgra32);

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));

                VisualBrush vb = new VisualBrush(canvas)
                {
                    Stretch = Stretch.None,
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = new Rect(minX - margin, minY - margin, width, height)
                };

                dc.DrawRectangle(vb, null, new Rect(0, 0, width, height));
            }

            renderBitmap.Render(dv);

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // ИЗМЕНЕНО: добавляем тип диаграммы в имя файла
            string filename = $"{diagramType}_Diagram_{timestamp}.png";
            string filepath = Path.Combine(desktop, filename);

            using (FileStream fs = new FileStream(filepath, FileMode.Create))
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                encoder.Save(fs);
            }

            return filename;
        }
    }
}
