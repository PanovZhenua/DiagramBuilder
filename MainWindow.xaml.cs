using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DiagramBuilder.Models;
using DiagramBuilder.Services;
using DiagramBuilder.Services.Core;
using DiagramBuilder.Services.Export;
using DiagramBuilder.Services.Management;
using DiagramBuilder.Services.Rendering;

namespace DiagramBuilder
{
    public partial class MainWindow : Window
    {
        // ========= ПОЛЯ =========

        private bool isPanning = false;
        private Point panStartMouse, panStartOffset;
        private readonly Dictionary<string, DiagramBlock> blocks = new Dictionary<string, DiagramBlock>();
        private readonly List<DiagramArrow> arrows = new List<DiagramArrow>();
        private List<ArrowData> rawArrowData = new List<ArrowData>();
        private readonly DiagramRenderer renderer;
        private readonly ConnectionManager connectionManager;
        private readonly DragDropManager dragDropManager;
        private readonly DiagramTypeManagers typeManager;
        private FEORenderer feoRenderer;
        private FEOBlockDragger feoBlockDragger;
        private NodeTreeRenderer nodeTreeRenderer;
        private IDEF3Renderer idef3Renderer;
        private DiagramType currentDiagramType = DiagramType.IDEF0;
        private List<DiagramParser.NodeData> nodeTreeData = new List<DiagramParser.NodeData>();
        private double currentZoom = 1.0;
        private const double MIN_ZOOM = 0.2;
        private const double MAX_ZOOM = 3.0;
        private const double ZOOM_STEP = 0.1;

        // ========= КОНСТРУКТОР И ИНИЦИАЛИЗАЦИЯ =========

        public MainWindow()
        {
            InitializeComponent();

            renderer = new DiagramRenderer(DiagramCanvas);
            connectionManager = new ConnectionManager(DiagramCanvas);
            dragDropManager = new DragDropManager(blocks, arrows, renderer, connectionManager);
            typeManager = new DiagramTypeManagers();

            // Отключаем перемещение подписей по-умолчанию
            ChkLockLabels.IsChecked = true;
            dragDropManager.SetLabelDraggingEnabled(false);

            // Регистрация событий зум/пан
            CanvasScrollViewer.PreviewMouseWheel += CanvasScrollViewer_PreviewMouseWheel;
            CanvasScrollViewer.PreviewMouseDown += CanvasScrollViewer_PreviewMouseDown;
            CanvasScrollViewer.PreviewMouseMove += CanvasScrollViewer_PreviewMouseMove;
            CanvasScrollViewer.PreviewMouseUp += CanvasScrollViewer_PreviewMouseUp;

            // Перехват пробела для панорамирования
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
            this.PreviewKeyUp += MainWindow_PreviewKeyUp;

            DiagramCanvas.Focusable = true;
            CanvasScrollViewer.Focusable = true;

            // Фокус на рабочую область при запуске
            this.Loaded += (s, e) => CanvasScrollViewer.Focus();

            LoadSampleText();
            UpdateStatusBar();
        }

        // Пример текста по умолчанию
        private void LoadSampleText()
        {
            DiagramTextBox.Text = typeManager.GetSampleText(DiagramType.IDEF0);
        }

        // ========= ПАНОРАМИРОВАНИЕ ПРОБЕЛОМ =========
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && !isPanning)
            {
                isPanning = true;
                Mouse.OverrideCursor = Cursors.Hand;
                CanvasScrollViewer.Focus();
                e.Handled = true;
            }
        }
        private void MainWindow_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && isPanning)
            {
                isPanning = false;
                Mouse.OverrideCursor = null;
                CanvasScrollViewer.Focus();
                e.Handled = true;
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

        // ========= МАСШТАБИРОВАНИЕ (CTRL+Колесо) =========
        private void CanvasScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control)
                return;
            e.Handled = true;

            Point mouseCanvasPos = e.GetPosition(DiagramCanvas);
            Point mouseScrollPos = e.GetPosition(CanvasScrollViewer);

            double oldZoom = currentZoom;
            double newZoom = currentZoom + (e.Delta > 0 ? ZOOM_STEP : -ZOOM_STEP);
            newZoom = Math.Max(MIN_ZOOM, Math.Min(MAX_ZOOM, newZoom));
            if (Math.Abs(newZoom - oldZoom) < 0.001)
                return;

            double relX = (CanvasScrollViewer.HorizontalOffset + mouseScrollPos.X) / (DiagramCanvas.Width * oldZoom);
            double relY = (CanvasScrollViewer.VerticalOffset + mouseScrollPos.Y) / (DiagramCanvas.Height * oldZoom);

            currentZoom = newZoom;
            CanvasScaleTransform.ScaleX = currentZoom;
            CanvasScaleTransform.ScaleY = currentZoom;

            double newOffsetX = relX * (DiagramCanvas.Width * newZoom) - mouseScrollPos.X;
            double newOffsetY = relY * (DiagramCanvas.Height * newZoom) - mouseScrollPos.Y;
            CanvasScrollViewer.ScrollToHorizontalOffset(newOffsetX);
            CanvasScrollViewer.ScrollToVerticalOffset(newOffsetY);

            TxtZoomLevel.Text = $"{(int)(currentZoom * 100)}%";
            TxtStatus.Text = $"Масштаб: {(int)(currentZoom * 100)}%";
        }

        // ========= ЗАГРУЗКА И РЕНДЕР =========

        private void LoadFromText_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clear_Click(null, null);
                DiagramType detectedType = typeManager.DetectDiagramType(DiagramTextBox.Text);
                typeManager.CurrentType = detectedType;
                currentDiagramType = detectedType;

                TxtDiagramType.Text = detectedType.ToString();
                TxtStatus.Text = $"Загружено: {typeManager.GetDescription(detectedType)}";

                switch (detectedType)
                {
                    case DiagramType.IDEF0: LoadIDEF0Diagram(); break;
                    case DiagramType.FEO: LoadFEODiagram(); break;
                    case DiagramType.NodeTree: LoadNodeTreeDiagram(); break;
                    case DiagramType.IDEF3: LoadIDEF3Diagram(); break;
                }

                ChkLockLabels.IsChecked = true;
                dragDropManager.SetLabelDraggingEnabled(false);
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "Ошибка загрузки";
            }
        }

        private void LoadIDEF0Diagram()
        {
            var (blockData, arrowData) = DiagramParser.Parse(DiagramTextBox.Text);
            foreach (var data in blockData)
            {
                var block = renderer.CreateBlock(data.Text, data.Code, data.X, data.Y, data.Width, data.Height);
                blocks[data.Code] = block;
                dragDropManager.AttachBlockEvents(block.Visual);
            }
            ArrowCalculator.SetAllBlocks(blocks);

            rawArrowData = arrowData.Select(a => new ArrowData
            {
                From = a.From,
                To = a.To,
                Label = a.Label,
                Type = a.Type
            }).ToList();

            var processedArrows = ArrowCalculator.PreprocessArrows(rawArrowData, blocks);

            foreach (var data in processedArrows)
            {
                DiagramBlock fromBlock = blocks.ContainsKey(data.From) ? blocks[data.From] : null;
                DiagramBlock toBlock = blocks.ContainsKey(data.To) ? blocks[data.To] : null;
                var arrow = renderer.CreateArrowWithDistribution(
                    fromBlock, toBlock, data.From, data.To, data.Label, data.Type, data.IndexOnSide, data.TotalOnSide
                );
                if (arrow != null)
                {
                    arrows.Add(arrow);
                    if (arrow.Label != null) dragDropManager.AttachLabelEvents(arrow.Label);
                }
            }
            dragDropManager.SetOnBlockMovedCallback(OnBlockMoved);
            currentDiagramType = DiagramType.IDEF0;

            var validation = IDEF0Validator.Validate(blocks, arrows);
            ShowValidationResult(validation);
        }

        private void OnBlockMoved()
        {
            if (currentDiagramType != DiagramType.IDEF0 || rawArrowData.Count == 0)
                return;
            var processedArrows = ArrowCalculator.PreprocessArrows(rawArrowData, blocks);

            foreach (var arrow in arrows)
            {
                if (arrow.Lines != null)
                    foreach (var line in arrow.Lines)
                        if (line != null)
                            DiagramCanvas.Children.Remove(line);
                if (arrow.ArrowHead != null)
                    DiagramCanvas.Children.Remove(arrow.ArrowHead);
                if (arrow.Label != null)
                    DiagramCanvas.Children.Remove(arrow.Label);
            }
            arrows.Clear();

            foreach (var data in processedArrows)
            {
                DiagramBlock fromBlock = blocks.ContainsKey(data.From) ? blocks[data.From] : null;
                DiagramBlock toBlock = blocks.ContainsKey(data.To) ? blocks[data.To] : null;
                var arrow = renderer.CreateArrowWithDistribution(
                    fromBlock, toBlock, data.From, data.To, data.Label, data.Type, data.IndexOnSide, data.TotalOnSide
                );
                if (arrow != null)
                {
                    arrows.Add(arrow);
                    if (arrow.Label != null) dragDropManager.AttachLabelEvents(arrow.Label);
                }
            }
            UpdateStatusBar();
        }

        private void LoadFEODiagram()
        {
            var (blockData, arrowData) = DiagramParser.Parse(DiagramTextBox.Text);

            // 1. Собираем структуру (модель)
            var diagram = new FEODiagram();
            var code2comp = new Dictionary<string, FEOComponent>();
            foreach (var data in blockData)
            {
                var comp = new FEOComponent
                {
                    Code = data.Code,
                    Name = data.Text,
                    X = data.X,
                    Y = data.Y,
                    Width = data.Width,
                    Height = data.Height
                };
                diagram.Components.Add(comp);
                code2comp[data.Code] = comp;
            }
            foreach (var data in arrowData)
            {
                if (code2comp.ContainsKey(data.From) && code2comp.ContainsKey(data.To))
                {
                    var arrow = new FEOArrow
                    {
                        From = code2comp[data.From],
                        To = code2comp[data.To],
                        Label = data.Label,
                        SideFrom = "right",
                        SideTo = "left"
                    };
                    diagram.Arrows.Add(arrow);
                    arrow.From.Outputs.Add(arrow);
                    arrow.To.Inputs.Add(arrow);
                }
            }

            // 2. Рендер и drag
            feoRenderer = new FEORenderer(DiagramCanvas);
            feoBlockDragger = new FEOBlockDragger(DiagramCanvas, feoRenderer);

            feoRenderer.BlockVisualCallback = (border) =>
            {
                var comp = border.Tag as FEOComponent;
                if (comp != null) feoBlockDragger.AttachDrag(border, comp);
            };
            // Добавить аналогично перетаскивание подписей, если надо

            feoRenderer.SetDiagram(diagram);
            feoRenderer.Render();

            currentDiagramType = DiagramType.FEO;

            MessageBox.Show(
                $"FEO диаграмма загружена!\n\nБлоков: {diagram.Components.Count}\nСвязей: {diagram.Arrows.Count}",
                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadNodeTreeDiagram()
        {
            nodeTreeData = DiagramParser.ParseNodeTree(DiagramTextBox.Text);
            nodeTreeRenderer = new NodeTreeRenderer(DiagramCanvas, blocks, connectionManager);
            nodeTreeRenderer.RenderNodeTree(nodeTreeData);
            currentDiagramType = DiagramType.NodeTree;
            foreach (var block in blocks.Values) dragDropManager.AttachBlockEvents(block.Visual);

            MessageBox.Show($"Дерево узлов загружено!\n\nУзлов: {nodeTreeData.Count}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadIDEF3Diagram()
        {
            var (uows, junctions, links) = DiagramParser.ParseIDEF3(DiagramTextBox.Text);
            idef3Renderer = new IDEF3Renderer(DiagramCanvas, blocks);
            idef3Renderer.RenderIDEF3(uows, junctions, links);
            currentDiagramType = DiagramType.IDEF3;
            dragDropManager.SetOnBlockMovedCallback(() => idef3Renderer?.UpdateConnections());

            foreach (var block in blocks.Values) dragDropManager.AttachBlockEvents(block.Visual);
            foreach (var junc in junctions)
                idef3Renderer.AttachJunctionDragEvents(junc.Id, (id) => idef3Renderer.UpdateConnections());

            MessageBox.Show($"IDEF3 загружена!\n\nРабот (UOW): {uows.Count}\nПерекрёстков: {junctions.Count}\nСвязей: {links.Count}",
                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ========== ВАЛИДАЦИЯ ==========
        private void ShowValidationResult(IDEF0Validator.ValidationResult validation)
        {
            string message = "Диаграмма IDEF0 загружена успешно!";
            if (validation.Warnings.Count > 0)
                message += "\n\n⚠ Предупреждения:\n" + string.Join("\n", validation.Warnings);
            if (!validation.IsValid)
                message += "\n\n❌ ОШИБКИ:\n" + string.Join("\n", validation.Errors);

            MessageBoxImage icon = validation.IsValid
                ? (validation.Warnings.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information)
                : MessageBoxImage.Error;
            MessageBox.Show(message, validation.IsValid ? "Загружено" : "Загружено с ошибками",
                MessageBoxButton.OK, icon);
            TxtStatus.Text = validation.IsValid ? "Диаграмма загружена" : "Загружено с ошибками";
        }

        // ========== БЛОКИРОВКА ==========
        private void ChkLockBlocks_Changed(object sender, RoutedEventArgs e)
        {
            if (dragDropManager != null)
            {
                dragDropManager.SetBlockDraggingEnabled(!(ChkLockBlocks.IsChecked ?? false));
                TxtStatus.Text = ChkLockBlocks.IsChecked == true
                    ? "Блоки заблокированы"
                    : "Блоки разблокированы";
            }
        }
        private void ChkLockLabels_Changed(object sender, RoutedEventArgs e)
        {
            if (dragDropManager != null)
            {
                dragDropManager.SetLabelDraggingEnabled(!(ChkLockLabels.IsChecked ?? false));
                TxtStatus.Text = ChkLockLabels.IsChecked == true
                    ? "Подписи заблокированы"
                    : "Подписи разблокированы";
            }
        }

        // ========== ОЧИСТКА ==========
        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            renderer.Clear();
            connectionManager.Clear();
            blocks.Clear();
            arrows.Clear();
            rawArrowData.Clear();
            nodeTreeData.Clear();
            feoRenderer = null;
            nodeTreeRenderer = null;
            idef3Renderer = null;
            dragDropManager.SetOnBlockMovedCallback(null);
            UpdateStatusBar();
            TxtStatus.Text = "Диаграмма очищена";
        }

        // ========== ЭКСПОРТ ==========
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            bool success = SimpleCanvasExporter.ExportFromDialog(DiagramCanvas, 300);
            if (success)
            {
                TxtStatus.Text = "Экспорт завершён!";
                MessageBox.Show("Диаграмма успешно экспортирована!", "Экспорт завершён", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                TxtStatus.Text = "Ошибка экспорта";
                MessageBox.Show("Не удалось экспортировать диаграмму.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ========== СТАТУС-БАР ==========
        private void UpdateStatusBar()
        {
            TxtBlockCount.Text = blocks.Count.ToString();
            TxtArrowCount.Text = arrows.Count.ToString();
        }
    }
}
