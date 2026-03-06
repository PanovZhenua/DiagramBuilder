using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

public static class SimpleCanvasExporter
{
    public static bool ExportToPng(Canvas canvas, string filePath, int dpi = 300)
    {
        canvas.UpdateLayout();
        var bounds = GetCanvasContentBounds(canvas);

        int pixelWidth = (int)(bounds.Width * dpi / 96.0);
        int pixelHeight = (int)(bounds.Height * dpi / 96.0);

        if (pixelWidth < 1 || pixelHeight < 1)
            return false;

        var rtb = new RenderTargetBitmap(
            pixelWidth, pixelHeight,
            dpi, dpi, PixelFormats.Pbgra32);

        var vis = new DrawingVisual();
        using (var dc = vis.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, bounds.Width, bounds.Height));

            var vb = new VisualBrush(canvas)
            {
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top
            };

            dc.PushTransform(new TranslateTransform(-bounds.Left, -bounds.Top));
            dc.DrawRectangle(vb, null, bounds); // <-- только bounds, а не new Rect(bounds)
            dc.Pop();
        }
        rtb.Render(vis);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            encoder.Save(fs);

        return true;
    }

    public static bool ExportFromDialog(Canvas canvas, int dpi = 300)
    {
        var dlg = new SaveFileDialog
        {
            FileName = $"Diagram_{DateTime.Now:yyyyMMdd_HHmmss}",
            Filter = "PNG-файл (*.png)|*.png|JPEG (*.jpg)|*.jpg|BMP (*.bmp)|*.bmp",
            DefaultExt = ".png"
        };
        if (dlg.ShowDialog() == true)
        {
            string ext = System.IO.Path.GetExtension(dlg.FileName).ToLower();
            if (ext == ".jpg")
                return ExportToJpg(canvas, dlg.FileName, dpi);
            if (ext == ".bmp")
                return ExportToBmp(canvas, dlg.FileName, dpi);
            return ExportToPng(canvas, dlg.FileName, dpi);
        }
        return false;
    }

    public static bool ExportToJpg(Canvas canvas, string filePath, int dpi = 300)
    {
        canvas.UpdateLayout();
        var bounds = GetCanvasContentBounds(canvas);
        int pixelWidth = (int)(bounds.Width * dpi / 96.0);
        int pixelHeight = (int)(bounds.Height * dpi / 96.0);

        var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
        var vis = new DrawingVisual();
        using (var dc = vis.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, bounds.Width, bounds.Height));
            var vb = new VisualBrush(canvas) { Stretch = Stretch.None };
            dc.PushTransform(new TranslateTransform(-bounds.Left, -bounds.Top));
            dc.DrawRectangle(vb, null, bounds);
            dc.Pop();
        }
        rtb.Render(vis);

        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            encoder.Save(fs);
        return true;
    }

    public static bool ExportToBmp(Canvas canvas, string filePath, int dpi = 300)
    {
        canvas.UpdateLayout();
        var bounds = GetCanvasContentBounds(canvas);
        int pixelWidth = (int)(bounds.Width * dpi / 96.0);
        int pixelHeight = (int)(bounds.Height * dpi / 96.0);

        var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
        var vis = new DrawingVisual();
        using (var dc = vis.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, bounds.Width, bounds.Height));
            var vb = new VisualBrush(canvas) { Stretch = Stretch.None };
            dc.PushTransform(new TranslateTransform(-bounds.Left, -bounds.Top));
            dc.DrawRectangle(vb, null, bounds);
            dc.Pop();
        }
        rtb.Render(vis);

        var encoder = new BmpBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            encoder.Save(fs);
        return true;
    }

    public static Rect GetCanvasContentBounds(Canvas canvas)
    {
        double xmin = double.PositiveInfinity, ymin = double.PositiveInfinity;
        double xmax = double.NegativeInfinity, ymax = double.NegativeInfinity;

        foreach (UIElement el in canvas.Children)
        {
            if (el is FrameworkElement fe)
            {
                double x = Canvas.GetLeft(fe);
                double y = Canvas.GetTop(fe);
                if (double.IsNaN(x)) x = 0;
                if (double.IsNaN(y)) y = 0;
                xmin = Math.Min(xmin, x);
                ymin = Math.Min(ymin, y);
                xmax = Math.Max(xmax, x + (fe.Width > 0 ? fe.Width : fe.ActualWidth));
                ymax = Math.Max(ymax, y + (fe.Height > 0 ? fe.Height : fe.ActualHeight));
            }
        }
        if (double.IsInfinity(xmin) || double.IsInfinity(ymin))
            return new Rect(0, 0, canvas.Width, canvas.Height);
        return new Rect(xmin, ymin, xmax - xmin, ymax - ymin);
    }
}
