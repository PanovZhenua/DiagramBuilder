// =================== Services/Management/DragDropManager.cs ===================
using DiagramBuilder.Models;
using DiagramBuilder.Services.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DiagramBuilder.Services.Management
{
    public class DragDropManager
    {
        private readonly Canvas canvas;
        private bool blockDraggingEnabled = true;
        private bool labelDraggingEnabled = false;
        private bool snapEnabled = false;
        private Dictionary<string, DiagramBlock> allBlocks = new Dictionary<string, DiagramBlock>();
        private Dictionary<FrameworkElement, string> blockTypes = new Dictionary<FrameworkElement, string>();

        private List<ArrowData> rawArrowData = new List<ArrowData>();
        private Action<List<ArrowData>> onArrowsNeedRecalculation;

        // ✅ GRID SNAPPING (сетка с фиксированным шагом)
        private const double GRID_SIZE = 15.0; // Шаг сетки 15px

        private bool isDragging;
        private Point dragStart;
        private double initialLeft;
        private double initialTop;
        private FrameworkElement currentElement;
        private Action onBlockMoved;

        public DragDropManager(Canvas canvas)
        {
            this.canvas = canvas;
        }

        public void SetBlockDraggingEnabled(bool enabled)
        {
            blockDraggingEnabled = enabled;
        }

        public void SetLabelDraggingEnabled(bool enabled)
        {
            labelDraggingEnabled = enabled;
        }

        public void SetSnapEnabled(bool enabled)
        {
            snapEnabled = enabled;
        }

        public void SetBlocks(Dictionary<string, DiagramBlock> blocks)
        {
            this.allBlocks = blocks ?? new Dictionary<string, DiagramBlock>();
        }

        public void SetBlockTypes(Dictionary<FrameworkElement, string> types)
        {
            this.blockTypes = types ?? new Dictionary<FrameworkElement, string>();
        }

        public void SetRawArrowData(List<ArrowData> arrows)
        {
            this.rawArrowData = arrows ?? new List<ArrowData>();
        }

        public void SetArrowRecalculationCallback(Action<List<ArrowData>> callback)
        {
            this.onArrowsNeedRecalculation = callback;
        }

        public void SetOnBlockMovedCallback(Action callback)
        {
            onBlockMoved = callback;
        }

        public void AttachBlockEvents(FrameworkElement element)
        {
            if (element == null) return;

            element.MouseLeftButtonDown += Element_MouseLeftButtonDown;
            element.MouseMove += Element_MouseMove;
            element.MouseLeftButtonUp += Element_MouseLeftButtonUp;
            element.MouseLeave += Element_MouseLeave;
        }

        private void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!blockDraggingEnabled) return;

            var element = sender as FrameworkElement;
            if (element == null) return;

            isDragging = true;
            currentElement = element;
            dragStart = e.GetPosition(canvas);
            initialLeft = Canvas.GetLeft(element);
            initialTop = Canvas.GetTop(element);

            if (double.IsNaN(initialLeft)) initialLeft = 0;
            if (double.IsNaN(initialTop)) initialTop = 0;

            element.CaptureMouse();

            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
                mainWindow.SelectedElement = element;

            e.Handled = true;
        }

        private void Element_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging) return;

            var element = sender as FrameworkElement;
            if (element == null) return;

            Point currentPos = e.GetPosition(canvas);
            Vector offset = currentPos - dragStart;

            double newLeft = initialLeft + offset.X;
            double newTop = initialTop + offset.Y;

            bool shiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            // ✅ GRID SNAPPING (отключается при Shift)
            if (snapEnabled && !shiftPressed)
            {
                newLeft = Math.Round(newLeft / GRID_SIZE) * GRID_SIZE;
                newTop = Math.Round(newTop / GRID_SIZE) * GRID_SIZE;
            }

            Canvas.SetLeft(element, newLeft);
            Canvas.SetTop(element, newTop);

            UpdateBlockPosition(element, newLeft, newTop);

            if (rawArrowData.Count > 0 && onArrowsNeedRecalculation != null)
            {
                onArrowsNeedRecalculation(rawArrowData);
            }
            else
            {
                onBlockMoved?.Invoke();
            }

            e.Handled = true;
        }

        private void Element_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isDragging) return;

            var element = sender as FrameworkElement;
            if (element == null) return;

            isDragging = false;
            element.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void Element_MouseLeave(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                var element = sender as FrameworkElement;
                if (element != null)
                {
                    isDragging = false;
                    element.ReleaseMouseCapture();
                }
            }
        }

        private void UpdateBlockPosition(FrameworkElement element, double left, double top)
        {
            foreach (var block in allBlocks.Values)
            {
                if (block?.Visual == element)
                {
                    block.X = left;
                    block.Y = top;
                    break;
                }
            }
        }

        // ✅ Перетаскивание подписей
        public void AttachLabelEvents(FrameworkElement label)
        {
            if (label == null) return;

            bool isLabelDragging = false;
            Point labelDragStart = new Point();
            double labelInitialLeft = 0;
            double labelInitialTop = 0;

            label.MouseLeftButtonDown += (s, e) =>
            {
                if (!labelDraggingEnabled) return;

                isLabelDragging = true;
                labelDragStart = e.GetPosition(canvas);
                labelInitialLeft = Canvas.GetLeft(label);
                labelInitialTop = Canvas.GetTop(label);

                if (double.IsNaN(labelInitialLeft)) labelInitialLeft = 0;
                if (double.IsNaN(labelInitialTop)) labelInitialTop = 0;

                label.CaptureMouse();
                e.Handled = true;
            };

            label.MouseMove += (s, e) =>
            {
                if (!isLabelDragging) return;

                Point currentPos = e.GetPosition(canvas);
                Vector offset = currentPos - labelDragStart;

                double newLeft = labelInitialLeft + offset.X;
                double newTop = labelInitialTop + offset.Y;

                Canvas.SetLeft(label, newLeft);
                Canvas.SetTop(label, newTop);

                e.Handled = true;
            };

            label.MouseLeftButtonUp += (s, e) =>
            {
                if (!isLabelDragging) return;

                isLabelDragging = false;
                label.ReleaseMouseCapture();
                e.Handled = true;
            };

            label.MouseLeave += (s, e) =>
            {
                if (isLabelDragging)
                {
                    isLabelDragging = false;
                    label.ReleaseMouseCapture();
                }
            };
        }

        // ✅ Универсальное перетаскивание (для DFD, ERD и т.д.)
        public void AttachDragGeneric(FrameworkElement element, Action<double, double> onDrag)
        {
            if (element == null) return;

            bool isGenericDragging = false;
            Point genericDragStart = new Point();
            double genericInitialLeft = 0;
            double genericInitialTop = 0;

            element.MouseLeftButtonDown += (s, e) =>
            {
                if (!blockDraggingEnabled) return;

                isGenericDragging = true;
                genericDragStart = e.GetPosition(canvas);
                genericInitialLeft = Canvas.GetLeft(element);
                genericInitialTop = Canvas.GetTop(element);

                if (double.IsNaN(genericInitialLeft)) genericInitialLeft = 0;
                if (double.IsNaN(genericInitialTop)) genericInitialTop = 0;

                element.CaptureMouse();

                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                    mainWindow.SelectedElement = element;

                e.Handled = true;
            };

            element.MouseMove += (s, e) =>
            {
                if (!isGenericDragging) return;

                Point currentPos = e.GetPosition(canvas);
                Vector offset = currentPos - genericDragStart;

                double newLeft = genericInitialLeft + offset.X;
                double newTop = genericInitialTop + offset.Y;

                bool shiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

                // ✅ GRID SNAPPING (отключается при Shift)
                if (snapEnabled && !shiftPressed)
                {
                    newLeft = Math.Round(newLeft / GRID_SIZE) * GRID_SIZE;
                    newTop = Math.Round(newTop / GRID_SIZE) * GRID_SIZE;
                }

                Canvas.SetLeft(element, newLeft);
                Canvas.SetTop(element, newTop);

                onDrag?.Invoke(newLeft, newTop);
                e.Handled = true;
            };

            element.MouseLeftButtonUp += (s, e) =>
            {
                if (!isGenericDragging) return;

                isGenericDragging = false;
                element.ReleaseMouseCapture();
                e.Handled = true;
            };

            element.MouseLeave += (s, e) =>
            {
                if (isGenericDragging)
                {
                    isGenericDragging = false;
                    element.ReleaseMouseCapture();
                }
            };
        }
    }
}
