using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using DiagramBuilder.Models;
using DiagramBuilder.Services.Core;
using DiagramBuilder.Services.Rendering;

namespace DiagramBuilder.Services.Management
{
    public class DragDropManager
    {
        private readonly Dictionary<string, DiagramBlock> blocks;
        private readonly DiagramRenderer renderer;
        private readonly ConnectionManager connectionManager;

        private Point dragStart;
        private bool isDragging;
        private UIElement draggedElement;
        private bool blockDraggingEnabled = true;
        private bool labelDraggingEnabled = false;
        private Action onBlockMoved;
        private bool snapEnabled = true; // По умолчанию включено

        public DragDropManager(
            Dictionary<string, DiagramBlock> blocks,
            List<DiagramArrow> arrows, // не используется, для совместимости конструктора
            DiagramRenderer renderer,
            ConnectionManager connectionManager = null)
        {
            this.blocks = blocks;
            this.renderer = renderer;
            this.connectionManager = connectionManager;
        }

        public void SetSnapEnabled(bool enabled)
        {
            snapEnabled = enabled;
        }

        public void SetOnBlockMovedCallback(Action callback) => onBlockMoved = callback;
        public void SetBlockDraggingEnabled(bool enabled) => blockDraggingEnabled = enabled;
        public void SetLabelDraggingEnabled(bool enabled) => labelDraggingEnabled = enabled;

        public void AttachBlockEvents(UIElement element)
        {
            element.MouseLeftButtonDown += (s, e) =>
            {
                ((MainWindow)Application.Current.MainWindow).SelectedElement = element;
                OnBlockMouseDown(s, e);
            };
            element.MouseMove += OnBlockMouseMove;
            element.MouseLeftButtonUp += OnBlockMouseUp;
            if (element is FrameworkElement fe)
                fe.Cursor = blockDraggingEnabled ? Cursors.SizeAll : Cursors.Arrow;
        }

        public void AttachLabelEvents(TextBlock label)
        {
            label.MouseLeftButtonDown += (s, e) =>
            {
                ((MainWindow)Application.Current.MainWindow).SelectedElement = label;
                OnLabelMouseDown(s, e);
            };
            label.MouseMove += OnLabelMouseMove;
            label.MouseLeftButtonUp += OnLabelMouseUp;
            label.Cursor = labelDraggingEnabled ? Cursors.SizeAll : Cursors.Arrow;
        }

        private void OnBlockMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!blockDraggingEnabled) return;
            draggedElement = sender as UIElement;
            dragStart = e.GetPosition(draggedElement.GetParent<Canvas>());
            isDragging = true;
            draggedElement.CaptureMouse();
            e.Handled = true;
        }

        private void OnBlockMouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging || draggedElement == null) return;
            Canvas canvas = draggedElement.GetParent<Canvas>();
            Point currentPos = e.GetPosition(canvas);
            Vector offset = currentPos - dragStart;

            double newLeft = Canvas.GetLeft(draggedElement) + offset.X;
            double newTop = Canvas.GetTop(draggedElement) + offset.Y;

            // Проверяем, зажат ли Shift (отключение snap)
            bool shiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            // Если snap включен и Shift НЕ зажат — применяем выравнивание
            Point mousePosOnCanvas = e.GetPosition(canvas);

            if (snapEnabled && !shiftPressed)
            {
                Point snapped = SnapHelper.SnapToGrid(draggedElement, blocks,
                    new Point(newLeft, newTop),
                    mousePosOnCanvas);
                newLeft = snapped.X;
                newTop = snapped.Y;
            }


            Canvas.SetLeft(draggedElement, newLeft);
            Canvas.SetTop(draggedElement, newTop);

            dragStart = currentPos;

            var block = FindBlockByVisual(draggedElement);
            if (block != null)
            {
                block.X = newLeft;
                block.Y = newTop;
                connectionManager?.UpdateConnectionsForBlock(block.Code);
            }
            renderer?.UpdateArrows();
            onBlockMoved?.Invoke();
            e.Handled = true;
        }

        public DiagramBlock GetBlockByVisual(UIElement visual)
        {
            foreach (var block in blocks.Values)
                if (block.Visual == visual) return block;
            return null;
        }

        public void UpdateBlockConnections(DiagramBlock block)
        {
            if (connectionManager != null && block != null)
                connectionManager.UpdateConnectionsForBlock(block.Code);
        }

        public void UpdateAllArrows()
        {
            if (renderer != null)
                renderer.UpdateArrows();
        }

        public void NotifyBlockMoved()
        {
            onBlockMoved?.Invoke();
        }

        private void OnBlockMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                draggedElement.ReleaseMouseCapture();
                draggedElement = null;
                e.Handled = true;
            }
        }

        private void OnLabelMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!labelDraggingEnabled) return;
            draggedElement = sender as UIElement;
            dragStart = e.GetPosition(draggedElement.GetParent<Canvas>());
            isDragging = true;
            draggedElement.CaptureMouse();
            e.Handled = true;
        }

        private void OnLabelMouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging || draggedElement == null) return;
            Canvas canvas = draggedElement.GetParent<Canvas>();
            Point currentPos = e.GetPosition(canvas);
            Vector offset = currentPos - dragStart;

            double newLeft = Canvas.GetLeft(draggedElement) + offset.X;
            double newTop = Canvas.GetTop(draggedElement) + offset.Y;

            Canvas.SetLeft(draggedElement, newLeft);
            Canvas.SetTop(draggedElement, newTop);

            dragStart = currentPos;
            e.Handled = true;
        }

        private void OnLabelMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                draggedElement.ReleaseMouseCapture();
                draggedElement = null;
                e.Handled = true;
            }
        }

        private DiagramBlock FindBlockByVisual(UIElement visual)
        {
            foreach (var block in blocks.Values)
                if (block.Visual == visual) return block;
            return null;
        }
    }

    public static class VisualTreeHelperExtensions
    {
        public static T GetParent<T>(this DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is T))
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            return parent as T;
        }
    }
}
