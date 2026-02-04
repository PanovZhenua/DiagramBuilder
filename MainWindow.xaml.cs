// =================== MainWindow.xaml.cs (ИСПРАВЛЕННАЯ ВЕРСИЯ) ===================
using DiagramBuilder.Models;
using DiagramBuilder.Services;
using DiagramBuilder.Services.Core;
using DiagramBuilder.Services.Loading;
using DiagramBuilder.Services.Management;
using DiagramBuilder.Services.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DiagramBuilder
{
    public partial class MainWindow : Window
    {
        private DiagramRenderer renderer;
        private ConnectionManager connectionManager;
        private DragDropManager dragDropManager;
        private DiagramState diagramState;
        private DiagramTypeManagers typeManager;
        private DiagramType currentDiagramType;
        private DiagramStyleType currentStyleType;

        private Dictionary<string, DiagramBlock> blocks = new Dictionary<string, DiagramBlock>();
        private List<DiagramArrow> arrows = new List<DiagramArrow>();

        private UIElement selectedElement;
        public UIElement SelectedElement
        {
            get { return selectedElement; }
            set { selectedElement = value; }
        }

        private const double MoveStep = 5.0;
        private double currentZoom = 1.0;
        private const double MIN_ZOOM = 0.2;
        private const double MAX_ZOOM = 3.0;
        private const double ZOOM_STEP = 0.1;
        private bool isPanning;
        private Point panStartMouse;
        private Point panStartOffset;

        private DiagramStyle CurrentStyle
        {
            get { return DiagramStyle.GetStyle(currentStyleType); }
        }

        public MainWindow()
        {
            InitializeComponent();
            InitializeApplication();
        }

        private void InitializeApplication()
        {
            try
            {
                blocks = new Dictionary<string, DiagramBlock>();
                arrows = new List<DiagramArrow>();
                diagramState = new DiagramState();
                currentStyleType = DiagramStyleType.ClassicBlackWhite;

                renderer = new DiagramRenderer(DiagramCanvas, CurrentStyle);
                connectionManager = new ConnectionManager(DiagramCanvas);
                dragDropManager = new DragDropManager(DiagramCanvas);
                typeManager = new DiagramTypeManagers();
                currentDiagramType = DiagramType.IDEF0;

                // ✅ НЕ вызываем ApplySnapSettings здесь - UI элементы ещё не готовы!

                StyleComboBox.Items.Clear();
                StyleComboBox.Items.Add(new ComboBoxItem { Content = "ClassicBlackWhite", Tag = DiagramStyleType.ClassicBlackWhite });
                StyleComboBox.Items.Add(new ComboBoxItem { Content = "SoftPastel", Tag = DiagramStyleType.SoftPastel });
                StyleComboBox.Items.Add(new ComboBoxItem { Content = "Presentation", Tag = DiagramStyleType.Presentation });
                StyleComboBox.Items.Add(new ComboBoxItem { Content = "Blueprint", Tag = DiagramStyleType.Blueprint });
                if (StyleComboBox.Items.Count > 0)
                    StyleComboBox.SelectedIndex = 0;

                if (ChkLockLabels != null)
                    ChkLockLabels.IsChecked = true;

                dragDropManager.SetLabelDraggingEnabled(false);

                CanvasScrollViewer.PreviewMouseWheel += CanvasScrollViewer_PreviewMouseWheel;
                CanvasScrollViewer.PreviewMouseDown += CanvasScrollViewer_PreviewMouseDown;
                CanvasScrollViewer.PreviewMouseMove += CanvasScrollViewer_PreviewMouseMove;
                CanvasScrollViewer.PreviewMouseUp += CanvasScrollViewer_PreviewMouseUp;

                this.KeyDown += MainWindow_KeyDown;
                this.PreviewKeyDown += MainWindow_PreviewKeyDown;
                this.PreviewKeyUp += MainWindow_PreviewKeyUp;

                this.Focusable = true;
                DiagramCanvas.Focusable = true;

                this.Loaded += (s, e) =>
                {
                    this.Focus();
                    Keyboard.Focus(this);
                };

                DiagramTextBox.PreviewMouseDown += DiagramTextBox_MouseDown;

                DiagramCanvas.MouseLeftButtonDown += (s, e) =>
                {
                    this.Focus();
                    Keyboard.Focus(this);
                };

                LoadSampleText();
                UpdateStatusBar();
                TxtStatus.Text = "Приложение инициализировано";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}\n\n{ex.StackTrace}");
            }
        }

        private void LoadSampleText()
        {
            DiagramTextBox.Text = typeManager.GetSampleText(DiagramType.IDEF0);
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && !isPanning)
            {
                isPanning = true;
                Mouse.OverrideCursor = Cursors.Hand;
                e.Handled = true;
            }
        }

        private void MainWindow_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && isPanning)
            {
                isPanning = false;
                Mouse.OverrideCursor = null;
                e.Handled = true;
            }
        }

        // ✅ ИСПРАВЛЕНО: управление стрелками
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // ✅ Игнорируем если фокус в TextBox
            if (DiagramTextBox.IsKeyboardFocusWithin || DiagramTextBox.IsFocused)
                return;

            if (SelectedElement == null)
                return;

            bool moved = false;
            double left = Canvas.GetLeft(SelectedElement);
            double top = Canvas.GetTop(SelectedElement);

            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            switch (e.Key)
            {
                case Key.Up: top -= MoveStep; moved = true; break;
                case Key.Down: top += MoveStep; moved = true; break;
                case Key.Left: left -= MoveStep; moved = true; break;
                case Key.Right: left += MoveStep; moved = true; break;
            }

            if (!moved) return;

            Canvas.SetLeft(SelectedElement, left);
            Canvas.SetTop(SelectedElement, top);

            // ✅ Обновляем позиции в зависимости от типа
            switch (currentDiagramType)
            {
                case DiagramType.IDEF0:
                    HandleIDEF0KeyboardMove(left, top);
                    break;

                case DiagramType.FEO:
                    HandleFEOKeyboardMove(left, top);
                    break;

                case DiagramType.NodeTree:
                    HandleNodeTreeKeyboardMove(left, top);
                    break;

                case DiagramType.IDEF3:
                    HandleIDEF3KeyboardMove(left, top);
                    break;

                case DiagramType.DFD:
                case DiagramType.DocumentFlow:
                    HandleDFDKeyboardMove(left, top);
                    break;
            }

            e.Handled = true;
        }

        private void HandleIDEF0KeyboardMove(double left, double top)
        {
            foreach (var block in blocks.Values)
            {
                if (block?.Visual == SelectedElement)
                {
                    block.X = left;
                    block.Y = top;

                    if (diagramState.RawArrowData.Count > 0)
                    {
                        DiagramLoadController.RenderArrowsIDEF0(DiagramCanvas, renderer, dragDropManager,
                            blocks, arrows, diagramState.RawArrowData);
                    }
                    break;
                }
            }
        }

        private void HandleFEOKeyboardMove(double left, double top)
        {
            if (diagramState.CurrentFEO == null || diagramState.FeoRenderer == null)
                return;

            foreach (var kv in diagramState.FeoBlocks)
            {
                if (kv.Value?.Visual == SelectedElement)
                {
                    var comp = diagramState.CurrentFEO.Components.FirstOrDefault(c => c.Code == kv.Key);
                    if (comp != null)
                    {
                        comp.X = left;
                        comp.Y = top;

                        // ✅ Обновляем позицию в DiagramBlock
                        kv.Value.X = left;
                        kv.Value.Y = top;

                        diagramState.FeoRenderer.UpdateArrows(diagramState.CurrentFEO, diagramState.FeoBlocks, diagramState.FeoArrows);
                    }
                    break;
                }
            }
        }

        private void HandleNodeTreeKeyboardMove(double left, double top)
        {
            foreach (var block in blocks.Values)
            {
                if (block?.Visual == SelectedElement)
                {
                    block.X = left;
                    block.Y = top;
                    connectionManager?.UpdateAllConnections();
                    break;
                }
            }
        }

        private void HandleIDEF3KeyboardMove(double left, double top)
        {
            foreach (var block in blocks.Values)
            {
                if (block?.Visual == SelectedElement)
                {
                    block.X = left;
                    block.Y = top;
                    diagramState.Idef3Renderer?.UpdateConnections();
                    return;
                }
            }

            if (SelectedElement is Ellipse ellipse)
            {
                bool snapEnabled = ChkSnapToGrid?.IsChecked ?? false;
                bool shiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

                double centerX = left + 20;
                double centerY = top + 20;

                if (snapEnabled && !shiftPressed && blocks.Count > 0)
                {
                    Point snapped = SnapHelper.SnapJunctionToBlocks(new Point(centerX, centerY), blocks);

                    centerX = snapped.X;
                    centerY = snapped.Y;

                    left = centerX - 20;
                    top = centerY - 20;

                    Canvas.SetLeft(SelectedElement, left);
                    Canvas.SetTop(SelectedElement, top);
                }

                if (diagramState.Idef3Renderer?.SetJunctionPositionFromEllipse(ellipse, left, top) == true)
                {
                    diagramState.Idef3Renderer?.UpdateConnections();
                }
            }
        }

        private void HandleDFDKeyboardMove(double left, double top)
        {
            if (diagramState.CurrentDFD == null || diagramState.DfdRenderer == null)
                return;

            string blockId = null;
            foreach (var kv in diagramState.DfdBlockVisuals)
            {
                if (kv.Value == SelectedElement)
                {
                    blockId = kv.Key;
                    break;
                }
            }

            if (blockId != null)
            {
                var proc = diagramState.CurrentDFD.Processes.Find(p => p.Id == blockId);
                if (proc != null) { proc.X = left; proc.Y = top; }

                var ent = diagramState.CurrentDFD.Entities.Find(p => p.Id == blockId);
                if (ent != null) { ent.X = left; ent.Y = top; }

                var store = diagramState.CurrentDFD.Stores.Find(p => p.Id == blockId);
                if (store != null) { store.X = left; store.Y = top; }

                var docFlow = diagramState.CurrentDFD.DocFlows.Find(p => p.Id == blockId);
                if (docFlow != null) { docFlow.X = left; docFlow.Y = top; }

                diagramState.DfdRenderer.UpdateArrows(diagramState.CurrentDFD);
            }
        }

        private void CanvasScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (isPanning && e.ChangedButton == MouseButton.Left)
            {
                CanvasScrollViewer.CaptureMouse();
                panStartMouse = e.GetPosition(CanvasScrollViewer);
                panStartOffset = new Point(CanvasScrollViewer.HorizontalOffset, CanvasScrollViewer.VerticalOffset);
                e.Handled = true;
            }
        }

        private void CanvasScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (isPanning && CanvasScrollViewer.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
            {
                Point cur = e.GetPosition(CanvasScrollViewer);
                Vector d = cur - panStartMouse;
                CanvasScrollViewer.ScrollToHorizontalOffset(panStartOffset.X - d.X);
                CanvasScrollViewer.ScrollToVerticalOffset(panStartOffset.Y - d.Y);
                e.Handled = true;
            }
        }

        private void CanvasScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isPanning && CanvasScrollViewer.IsMouseCaptured && e.ChangedButton == MouseButton.Left)
            {
                CanvasScrollViewer.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void CanvasScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            e.Handled = true;

            Point mouseScrollPos = e.GetPosition(CanvasScrollViewer);
            double oldZoom = currentZoom;
            double newZoom = currentZoom + (e.Delta > 0 ? ZOOM_STEP : -ZOOM_STEP);
            if (newZoom < MIN_ZOOM) newZoom = MIN_ZOOM;
            if (newZoom > MAX_ZOOM) newZoom = MAX_ZOOM;
            if (Math.Abs(newZoom - oldZoom) < 0.001) return;

            double canvasWidth = DiagramCanvas.ActualWidth > 0 ? DiagramCanvas.ActualWidth : DiagramCanvas.Width;
            double canvasHeight = DiagramCanvas.ActualHeight > 0 ? DiagramCanvas.ActualHeight : DiagramCanvas.Height;
            if (canvasWidth <= 0) canvasWidth = 2000;
            if (canvasHeight <= 0) canvasHeight = 1500;

            double relX = (CanvasScrollViewer.HorizontalOffset + mouseScrollPos.X) / (canvasWidth * oldZoom);
            double relY = (CanvasScrollViewer.VerticalOffset + mouseScrollPos.Y) / (canvasHeight * oldZoom);

            currentZoom = newZoom;
            CanvasScaleTransform.ScaleX = currentZoom;
            CanvasScaleTransform.ScaleY = currentZoom;

            double newOffsetX = relX * (canvasWidth * newZoom) - mouseScrollPos.X;
            double newOffsetY = relY * (canvasHeight * newZoom) - mouseScrollPos.Y;

            if (!double.IsNaN(newOffsetX)) CanvasScrollViewer.ScrollToHorizontalOffset(newOffsetX);
            if (!double.IsNaN(newOffsetY)) CanvasScrollViewer.ScrollToVerticalOffset(newOffsetY);

            TxtZoomLevel.Text = string.Format("{0}%", (int)(currentZoom * 100));
            TxtStatus.Text = string.Format("Масштаб: {0}%", (int)(currentZoom * 100));
        }

        private void LoadFromText_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clear_Click(null, null);

                string text = DiagramTextBox.Text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    TxtStatus.Text = "Введите текст диаграммы";
                    return;
                }

                currentDiagramType = typeManager.DetectDiagramType(text);
                typeManager.CurrentType = currentDiagramType;
                TxtDiagramType.Text = currentDiagramType.ToString();
                TxtStatus.Text = "Загружаю: " + typeManager.GetDescription(currentDiagramType);

                DiagramLoadController.Load(
                    currentDiagramType,
                    text,
                    DiagramCanvas,
                    CurrentStyle,
                    renderer,
                    connectionManager,
                    dragDropManager,
                    blocks,
                    arrows,
                    diagramState,
                    OnBlockMoved);

                dragDropManager.SetSnapEnabled(true);

                TxtStatus.Text = "Загружено: " + typeManager.GetDescription(currentDiagramType);

                ApplySnapSettings();
                UpdateStatusBar();

                // ✅ Сбрасываем масштаб и позицию после загрузки
                ResetViewToDefault();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Ошибка загрузки: {ex.Message}";
                MessageBox.Show($"Ошибка: {ex.Message}\n\n{ex.StackTrace}", "Ошибка загрузки", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ✅ НОВЫЙ МЕТОД: сброс вида в начальное состояние
        private void ResetViewToDefault()
        {
            // Сбрасываем масштаб до 100%
            currentZoom = 1.0;
            CanvasScaleTransform.ScaleX = 1.0;
            CanvasScaleTransform.ScaleY = 1.0;
            TxtZoomLevel.Text = "100%";

            // Возвращаемся в левый верхний угол
            CanvasScrollViewer.ScrollToHorizontalOffset(0);
            CanvasScrollViewer.ScrollToVerticalOffset(0);

            TxtStatus.Text = "Вид сброшен (100%, левый верхний угол)";
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                renderer.Clear();
                connectionManager.Clear();
                DiagramCanvas.Children.Clear();
                blocks.Clear();
                arrows.Clear();
                diagramState = new DiagramState();
                currentDiagramType = DiagramType.IDEF0;
                dragDropManager.SetOnBlockMovedCallback(null);
                TxtStatus.Text = "Диаграмма очищена";
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Ошибка очистки: {ex.Message}";
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DiagramCanvas.Children.Count == 0)
                {
                    TxtStatus.Text = "Нечего экспортировать";
                    return;
                }

                // ✅ 1. Сохраняем текущий масштаб
                double savedZoom = currentZoom;
                Point savedScroll = new Point(
                    CanvasScrollViewer.HorizontalOffset,
                    CanvasScrollViewer.VerticalOffset
                );

                // ✅ 2. Сбрасываем масштаб до 100%
                SetZoom(1.0, new Point(0, 0));
                TxtStatus.Text = "Экспорт... (масштаб сброшен до 100%)";

                // ✅ 3. Даём UI время отрисоваться с новым масштабом
                Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

                // ✅ 4. Экспортируем
                bool success = SimpleCanvasExporter.ExportFromDialog(DiagramCanvas, 300);

                // ✅ 5. Возвращаем исходный масштаб и позицию
                SetZoom(savedZoom, savedScroll);

                TxtStatus.Text = success ? "Экспорт завершён!" : "Ошибка экспорта";

                if (success)
                    MessageBox.Show("Диаграмма успешно экспортирована!", "Экспорт завершён",
                        MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Ошибка экспорта: {ex.Message}";
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ✅ НОВЫЙ МЕТОД: установить масштаб и позицию
        private void SetZoom(double zoom, Point scrollPosition)
        {
            currentZoom = Math.Max(MIN_ZOOM, Math.Min(MAX_ZOOM, zoom));

            CanvasScaleTransform.ScaleX = currentZoom;
            CanvasScaleTransform.ScaleY = currentZoom;

            CanvasScrollViewer.ScrollToHorizontalOffset(scrollPosition.X);
            CanvasScrollViewer.ScrollToVerticalOffset(scrollPosition.Y);

            TxtZoomLevel.Text = string.Format("{0}%", (int)(currentZoom * 100));
        }

        private void DiagramTextBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DiagramTextBox.Focus();
        }

        private void ChkLockBlocks_Changed(object sender, RoutedEventArgs e)
        {
            if (dragDropManager != null)
            {
                bool isLocked = ChkLockBlocks.IsChecked ?? false;
                dragDropManager.SetBlockDraggingEnabled(!isLocked);
                TxtStatus.Text = isLocked ? "Блоки заблокированы" : "Блоки разблокированы";
            }
        }

        private void ChkSnapToGrid_Changed(object sender, RoutedEventArgs e)
        {
            if (dragDropManager == null || ChkSnapToGrid == null)
                return;

            ApplySnapSettings();

            bool isEnabled = ChkSnapToGrid.IsChecked ?? false;
            TxtStatus.Text = isEnabled ? "Магнит включен (15px)" : "Магнит выключен";
        }

        // ✅ ИСПРАВЛЕНО: правильная передача blocks
        private void ApplySnapSettings()
        {
            // ✅ Проверяем что все UI элементы инициализированы
            if (ChkSnapToGrid == null || dragDropManager == null)
                return;

            bool isEnabled = ChkSnapToGrid.IsChecked ?? false;

            dragDropManager.SetSnapEnabled(isEnabled);
            dragDropManager.SetBlocks(blocks);

            if (diagramState?.Idef3Renderer != null)
                diagramState.Idef3Renderer.SetSnapEnabled(isEnabled);
        }

        private void ChkLockLabels_Changed(object sender, RoutedEventArgs e)
        {
            if (dragDropManager != null)
            {
                bool isLocked = ChkLockLabels.IsChecked ?? false;
                dragDropManager.SetLabelDraggingEnabled(!isLocked);
                TxtStatus.Text = isLocked ? "Подписи заблокированы" : "Подписи разблокированы";
            }
        }

        private void StyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DiagramCanvas == null || StyleComboBox == null || DiagramCanvas.Children.Count == 0)
                return;

            try
            {
                if (StyleComboBox.SelectedItem is ComboBoxItem selected && selected.Tag is DiagramStyleType newType)
                    currentStyleType = newType;
                else
                    currentStyleType = DiagramStyleType.ClassicBlackWhite;

                string text = DiagramTextBox.Text;

                DiagramCanvas.Children.Clear();
                blocks.Clear();
                arrows.Clear();
                diagramState = new DiagramState();

                renderer = new DiagramRenderer(DiagramCanvas, CurrentStyle);

                DiagramLoadController.Load(
                    currentDiagramType,
                    text,
                    DiagramCanvas,
                    CurrentStyle,
                    renderer,
                    connectionManager,
                    dragDropManager,
                    blocks,
                    arrows,
                    diagramState,
                    OnBlockMoved);

                TxtStatus.Text = "Стиль обновлён";
                ApplySnapSettings();
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Ошибка смены стиля: {ex.Message}";
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private void OnBlockMoved()
        {
            if (currentDiagramType == DiagramType.IDEF0 && diagramState.RawArrowData.Count > 0)
            {
                DiagramLoadController.RenderArrowsIDEF0(DiagramCanvas, renderer, dragDropManager,
                    blocks, arrows, diagramState.RawArrowData);
            }
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            TxtBlockCount.Text = blocks.Count.ToString();
            TxtArrowCount.Text = arrows.Count.ToString();
        }
    }
}
