using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DiagramBuilder.Models;
using DiagramBuilder.Models.Blocks;
using DiagramBuilder.Services.Core;
using DiagramBuilder.Services.Rendering;
using System.Linq;
using System.Windows.Media;

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
            List<DiagramArrow> arrows,
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

        // Универсальный drag для любого блока
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

        // Универсальный drag для подписей (TextBlock)
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

        // Универсальный универсальный drag без привязки к модели (например — DFD block или label)
        public void AttachDragGeneric(UIElement element, Action<double, double> onMoved)
        {
            Point dragStart = new Point();
            bool isDragging = false;

            element.MouseLeftButtonDown += (s, e) =>
            {
                dragStart = e.GetPosition(element.GetParent<Canvas>());
                isDragging = true;
                element.CaptureMouse();
                e.Handled = true;
            };

            element.MouseMove += (s, e) =>
            {
                if (!isDragging) return;
                Canvas canvas = element.GetParent<Canvas>();
                Point pos = e.GetPosition(canvas);
                double dx = pos.X - dragStart.X;
                double dy = pos.Y - dragStart.Y;
                double left = Canvas.GetLeft(element) + dx;
                double top = Canvas.GetTop(element) + dy;
                Canvas.SetLeft(element, left);
                Canvas.SetTop(element, top);
                dragStart = pos;
                onMoved?.Invoke(left, top);
                e.Handled = true;
            };

            element.MouseLeftButtonUp += (s, e) =>
            {
                isDragging = false;
                element.ReleaseMouseCapture();
                e.Handled = true;
            };
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

            // Shift -- отключить snap
            bool shiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            Point mousePosOnCanvas = e.GetPosition(canvas);

            if (snapEnabled && !shiftPressed)
            {
                Point snapped = SnapHelper.SnapToGrid(draggedElement, blocks,
                    new Point(newLeft, newTop), mousePosOnCanvas);
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

        public void AttachDFDBlockDrag(FrameworkElement block, DFDDiagram dfd, DFDRenderer renderer, string blockId)
        {
            Point dragStart = new Point();
            bool isDragging = false;

            block.MouseLeftButtonDown += (s, e) =>
            {
                var canvas = block.Parent as Canvas;
                dragStart = e.GetPosition(canvas);
                isDragging = true;
                block.CaptureMouse();
                e.Handled = true;
            };

            block.MouseMove += (s, e) =>
            {
                if (!isDragging) return;
                var canvas = block.Parent as Canvas;
                var pos = e.GetPosition(canvas);
                double dx = pos.X - dragStart.X;
                double dy = pos.Y - dragStart.Y;
                double left = Canvas.GetLeft(block) + dx;
                double top = Canvas.GetTop(block) + dy;
                Canvas.SetLeft(block, left);
                Canvas.SetTop(block, top);
                dragStart = pos;

                // обновляем координаты в модели и перерисовываем стрелки
                UpdateDFDBlockPosition(dfd, blockId, left, top);
                renderer.RenderArrows(dfd);

                e.Handled = true;
            };

            block.MouseLeftButtonUp += (s, e) =>
            {
                if (isDragging)
                {
                    isDragging = false;
                    block.ReleaseMouseCapture();

                    double finalLeft = Canvas.GetLeft(block);
                    double finalTop = Canvas.GetTop(block);

                    UpdateDFDBlockPosition(dfd, blockId, finalLeft, finalTop);
                    renderer.RenderArrows(dfd);

                    // Перерегистрировать подписи стрелок
                    var labels = renderer.GetArrowLabels();
                    foreach (var label in labels)
                        AttachLabelEventsSafely(label);
                }
                e.Handled = true;
            };
        }

        // Метод для безопасной регистрации drag для подписей
        private void AttachLabelEventsSafely(TextBlock label)
        {
            Point labelDragStart = new Point();
            bool isLabelDragging = false;

            label.MouseLeftButtonDown += (s, e) =>
            {
                if (!labelDraggingEnabled) return;

                // Найти родительский Canvas
                Canvas canvas = VisualTreeHelper.GetParent(label) as Canvas;
                if (canvas == null) return;

                labelDragStart = e.GetPosition(canvas);
                isLabelDragging = true;
                label.CaptureMouse();
                e.Handled = true;
            };

            label.MouseMove += (s, e) =>
            {
                if (!isLabelDragging || !labelDraggingEnabled) return;

                // Найти родительский Canvas
                Canvas canvas = VisualTreeHelper.GetParent(label) as Canvas;
                if (canvas == null) return;

                var pos = e.GetPosition(canvas);
                double dx = pos.X - labelDragStart.X;
                double dy = pos.Y - labelDragStart.Y;
                double left = Canvas.GetLeft(label) + dx;
                double top = Canvas.GetTop(label) + dy;
                Canvas.SetLeft(label, left);
                Canvas.SetTop(label, top);
                labelDragStart = pos;
                e.Handled = true;
            };

            label.MouseLeftButtonUp += (s, e) =>
            {
                if (isLabelDragging)
                {
                    isLabelDragging = false;
                    label.ReleaseMouseCapture();
                }
                e.Handled = true;
            };
        }

        private void UpdateDFDBlockPosition(DFDDiagram dfd, string blockId, double x, double y)
        {
            var proc = dfd.Processes.FirstOrDefault(p => p.Id == blockId);
            if (proc != null) { proc.X = x; proc.Y = y; }

            var entity = dfd.Entities.FirstOrDefault(ent => ent.Id == blockId);
            if (entity != null) { entity.X = x; entity.Y = y; }

            var store = dfd.Stores.FirstOrDefault(s => s.Id == blockId);
            if (store != null) { store.X = x; store.Y = y; }
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
            renderer?.UpdateArrows();
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
