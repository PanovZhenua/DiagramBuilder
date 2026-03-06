// Services/Calculation/ComponentSizeCalculator.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DiagramBuilder.Models;

namespace DiagramBuilder.Services.Calculation
{
    /// <summary>
    /// Автоматически рассчитывает Width/Height для ComponentNode, DatabaseNode и WorkflowNode
    /// на основе реального размера текста через WPF FormattedText.
    /// Вызывать ДО рендеринга диаграммы.
    /// </summary>
    public static class ComponentSizeCalculator
    {
        // ---- Настройки для COMPONENT ----
        private const double ComponentFontSize = 30.0;
        private const double ComponentPadX = 70.0;  // горизонтальный отступ (учитываем вкладки слева)
        private const double ComponentPadY = 24.0;  // вертикальный отступ
        private const double ComponentMaxWidth = 320.0; // максимальная ширина блока
        private const double ComponentMinWidth = 120.0;
        private const double ComponentMinHeight = 46.0;

        // ---- Настройки для DATABASE (цилиндр) ----
        private const double DbFontSize = 26.0;
        private const double DbPadX = 32.0;
        private const double DbPadY = 40.0;  // запас под верхний эллипс цилиндра
        private const double DbMaxWidth = 340.0;
        private const double DbMinWidth = 180.0;
        private const double DbMinHeight = 80.0;

        // ---- Настройки для WORKFLOW ----
        private const double WfFontSize = 18.0;
        private const double WfPadX = 28.0;
        private const double WfPadY = 20.0;
        private const double WfMaxWidth = 280.0;
        private const double WfMinWidth = 100.0;
        private const double WfMinHeight = 44.0;

        private static readonly Typeface DefaultTypeface = new Typeface("Segoe UI");

        /// <summary>
        /// Применяет автоматические размеры ко всем элементам диаграммы.
        /// </summary>
        public static void ApplyAutoSize(ComponentDiagram diagram)
        {
            if (diagram == null) return;

            if (diagram.Components != null)
                foreach (var c in diagram.Components)
                    ApplyComponent(c);

            if (diagram.Databases != null)
                foreach (var db in diagram.Databases)
                    ApplyDatabase(db);

            if (diagram.WorkflowNodes != null)
                foreach (var n in diagram.WorkflowNodes)
                    ApplyWorkflow(n);
        }

        // ===========================
        // Component
        // ===========================

        private static void ApplyComponent(ComponentNode c)
        {
            if (c == null) return;

            Size measured = MeasureText(
                c.Name ?? "",
                ComponentFontSize,
                ComponentMaxWidth - ComponentPadX);

            double w = Math.Max(ComponentMinWidth, measured.Width + ComponentPadX);
            double h = Math.Max(ComponentMinHeight, measured.Height + ComponentPadY);

            c.Width = w;
            c.Height = h;
            c.MinWidth = ComponentMinWidth;
            c.MinHeight = ComponentMinHeight;
        }

        // ===========================
        // Database
        // ===========================

        private static void ApplyDatabase(DatabaseNode db)
        {
            if (db == null) return;

            Size measured = MeasureText(
                db.Name ?? "",
                DbFontSize,
                DbMaxWidth - DbPadX);

            double w = Math.Max(DbMinWidth, measured.Width + DbPadX);
            double h = Math.Max(DbMinHeight, measured.Height + DbPadY);

            db.Width = w;
            db.Height = h;
            db.MinWidth = DbMinWidth;
            db.MinHeight = DbMinHeight;
        }

        // ===========================
        // Workflow
        // ===========================

        private static void ApplyWorkflow(WorkflowNode n)
        {
            if (n == null) return;

            // Для трапеции (Data) добавляем extra-отступ под наклонные стороны
            double extraPadX = (n.Type == WorkflowNodeType.Data) ? 40.0 : 0.0;

            Size measured = MeasureText(
                n.Text ?? "",
                WfFontSize,
                WfMaxWidth - WfPadX - extraPadX);

            double w = Math.Max(WfMinWidth, measured.Width + WfPadX + extraPadX);
            double h = Math.Max(WfMinHeight, measured.Height + WfPadY);

            // Для Database-цилиндра в workflow — добавляем высоту под эллипс
            if (n.Type == WorkflowNodeType.Database)
                h = Math.Max(h, measured.Height + WfPadY + 20.0);

            n.Width = w;
            n.Height = h;
            n.MinWidth = WfMinWidth;
            n.MinHeight = WfMinHeight;
        }

        // ===========================
        // Измерение текста
        // ===========================

        /// <summary>
        /// Возвращает размер текста при заданной максимальной ширине строки.
        /// </summary>

        private static Size MeasureText(string text, double fontSize, double maxLineWidth)
        {
            if (string.IsNullOrEmpty(text))
                return new Size(0, fontSize + 4);

            if (maxLineWidth < 10) maxLineWidth = 10;

            double pixelsPerDip = 1.0; // безопасное значение для большинства экранов 96 DPI
            try
            {
                pixelsPerDip = VisualTreeHelper.GetDpi(
                    System.Windows.Application.Current.MainWindow).PixelsPerDip;
            }
            catch { /* fallback */ }

            var ft = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                DefaultTypeface,
                fontSize,
                Brushes.Black,
                pixelsPerDip);

            ft.MaxTextWidth = maxLineWidth;
            ft.MaxTextHeight = double.PositiveInfinity;
            ft.Trimming = TextTrimming.None;

            return new Size(ft.Width, ft.Height);
        }


    }
}
