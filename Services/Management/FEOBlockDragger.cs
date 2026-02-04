using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DiagramBuilder.Models;
using DiagramBuilder.Services.Rendering;

namespace DiagramBuilder.Services.Management
{
    public class FEOBlockDragger
    {
        private readonly Canvas canvas;
        private readonly FEORenderer renderer;
        private FEOComponent currentComponent;
        private Point dragStartMouse;
        private double initialX, initialY;
        private bool isDragging;

        public FEOBlockDragger(Canvas canvas, FEORenderer renderer)
        {
            this.canvas = canvas;
            this.renderer = renderer;
        }

        public void AttachDrag(Border border, FEOComponent component)
        {
            border.Tag = component;
            border.MouseLeftButtonDown += OnBlockMouseDown;
            border.MouseMove += OnBlockMouseMove;
            border.MouseLeftButtonUp += OnBlockMouseUp;
        }

        private void OnBlockMouseDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            currentComponent = border?.Tag as FEOComponent;
            if (currentComponent != null)
            {
                isDragging = true;
                dragStartMouse = e.GetPosition(canvas);
                initialX = currentComponent.X;
                initialY = currentComponent.Y;
                border.CaptureMouse();
            }
        }

        private void OnBlockMouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging || currentComponent == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            Point pos = e.GetPosition(canvas);
            currentComponent.X = initialX + (pos.X - dragStartMouse.X);
            currentComponent.Y = initialY + (pos.Y - dragStartMouse.Y);

            renderer.Render();
        }

        private void OnBlockMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                (sender as Border)?.ReleaseMouseCapture();
                currentComponent = null;
            }
        }
    }
}
