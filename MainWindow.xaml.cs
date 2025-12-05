using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DiagramBuilder.Models;
using DiagramBuilder.Models.Blocks;
using DiagramBuilder.Services;
using DiagramBuilder.Services.Core;
using DiagramBuilder.Services.Export;
using DiagramBuilder.Services.Management;
using DiagramBuilder.Services.Rendering;

namespace DiagramBuilder
{
    public partial class MainWindow : Window
    {
        // === Поля для DFD ===
        private DFDDiagram currentDFD;
        private DFDRenderer dfdRenderer;
        private Dictionary<string, FrameworkElement> dfdBlockVisuals;
        private Dictionary<string, FrameworkElement> blockVisuals = new Dictionary<string, FrameworkElement>();

        // === Остальные поля ===
        private Dictionary<string, DiagramBlock> blocks;
        private readonly List<DiagramArrow> arrows;
        private readonly DiagramRenderer renderer;
        private readonly ConnectionManager connectionManager;
        private readonly DragDropManager dragDropManager;
        private readonly DiagramTypeManagers typeManager;
        private const double MoveStep = 5.0;
        private UIElement selectedElement;

        private readonly FEODiagram currentFEO;
        private readonly Dictionary<string, DiagramBlock> feoBlocks;
        private readonly List<DiagramArrow> feoArrows;
        private FEORenderer feoRenderer;

        private List<IDEF3UOW> lastUows;
        private List<IDEF3Junction> lastJunctions;
        private List<IDEF3Link> lastLinks;
        private IDEF3Renderer idef3Renderer;

        private DiagramRenderer diagramRenderer;
        private List<ArrowData> rawArrowData;

        private NodeTreeRenderer nodeTreeRenderer;
        private List<DiagramParser.NodeData> nodeTreeData;

        private DiagramType currentDiagramType;
        private DiagramStyleType currentStyleType;
        private DiagramStyle CurrentStyle { get { return DiagramStyle.GetStyle(currentStyleType); } }

        private double currentZoom;
        private const double MIN_ZOOM = 0.2;
        private const double MAX_ZOOM = 3.0;
        private const double ZOOM_STEP = 0.1;
        private bool isPanning;
        private Point panStartMouse;
        private Point panStartOffset;

        public MainWindow()
        {
            InitializeComponent();

            blocks = new Dictionary<string, DiagramBlock>();
            arrows = new List<DiagramArrow>();
            feoBlocks = new Dictionary<string, DiagramBlock>();
            feoArrows = new List<DiagramArrow>();
            lastUows = new List<IDEF3UOW>();
            lastJunctions = new List<IDEF3Junction>();
            lastLinks = new List<IDEF3Link>();
            rawArrowData = new List<ArrowData>();
            nodeTreeData = new List<DiagramParser.NodeData>();
            blockVisuals = new Dictionary<string, FrameworkElement>();
            currentZoom = 1.0;

            renderer = new DiagramRenderer(DiagramCanvas, CurrentStyle);
            connectionManager = new ConnectionManager(DiagramCanvas);
            dragDropManager = new DragDropManager(blocks, arrows, renderer, connectionManager);
            typeManager = new DiagramTypeManagers();
            currentDiagramType = DiagramType.IDEF0;
            currentStyleType = DiagramStyleType.ClassicBlackWhite;

            ChkLockLabels.IsChecked = true;
            dragDropManager.SetLabelDraggingEnabled(false);

            CanvasScrollViewer.PreviewMouseWheel += CanvasScrollViewer_PreviewMouseWheel;
            CanvasScrollViewer.PreviewMouseDown += CanvasScrollViewer_PreviewMouseDown;
            CanvasScrollViewer.PreviewMouseMove += CanvasScrollViewer_PreviewMouseMove;
            CanvasScrollViewer.PreviewMouseUp += CanvasScrollViewer_PreviewMouseUp;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            PreviewKeyUp += MainWindow_PreviewKeyUp;

            DiagramCanvas.Focusable = true;
            CanvasScrollViewer.Focusable = true;
            Loaded += (s, e) => CanvasScrollViewer.Focus();
            DiagramTextBox.PreviewMouseDown += DiagramTextBox_MouseDown;

            LoadSampleText();
            UpdateStatusBar();
        }

        public UIElement SelectedElement
        {
            get { return selectedElement; }
            set { selectedElement = value; }
        }

        // ==== Загрузка примера схемы текста ====
        private void LoadSampleText()
        {
            DiagramTextBox.Text = typeManager.GetSampleText(DiagramType.IDEF0);
        }

        // ==== Панорамирование (пробел + мышь) ====
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

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (DiagramTextBox.IsKeyboardFocusWithin)
                return;

            if (SelectedElement == null)
                return;

            bool moved = false;
            double left = Canvas.GetLeft(SelectedElement);
            double top = Canvas.GetTop(SelectedElement);
            double moveStep = MoveStep;

            switch (e.Key)
            {
                case Key.Up:
                    top -= moveStep;
                    moved = true;
                    break;
                case Key.Down:
                    top += moveStep;
                    moved = true;
                    break;
                case Key.Left:
                    left -= moveStep;
                    moved = true;
                    break;
                case Key.Right:
                    left += moveStep;
                    moved = true;
                    break;
            }

            if (moved)
            {
                Canvas.SetLeft(SelectedElement, left);
                Canvas.SetTop(SelectedElement, top);

                // --- Для обычных блоков (IDEF0, FEO, NodeTree) ---
                if (SelectedElement is Border)
                {
                    var block = dragDropManager.GetBlockByVisual(SelectedElement);
                    if (block != null)
                    {
                        block.X = left;
                        block.Y = top;
                        dragDropManager.UpdateBlockConnections(block);
                        dragDropManager.UpdateAllArrows();
                        dragDropManager.NotifyBlockMoved();
                    }
                    // --- ДЛЯ DFD БЛОКОВ ---
                    else if (currentDiagramType == DiagramType.DFD && dfdBlockVisuals != null)
                    {
                        string blockId = dfdBlockVisuals.FirstOrDefault(kv => kv.Value == SelectedElement).Key;
                        if (blockId != null)
                        {
                            var proc = currentDFD.Processes.FirstOrDefault(p => p.Id == blockId);
                            if (proc != null) { proc.X = left; proc.Y = top; }

                            var entity = currentDFD.Entities.FirstOrDefault(ent => ent.Id == blockId);
                            if (entity != null) { entity.X = left; entity.Y = top; }

                            var store = currentDFD.Stores.FirstOrDefault(s => s.Id == blockId);
                            if (store != null) { store.X = left; store.Y = top; }

                            dfdRenderer.Render(currentDFD);
                            dfdBlockVisuals = dfdRenderer.GetBlockVisuals();
                        }
                    }
                }

                // --- Для junction (ellipse) ---
                if (SelectedElement is Ellipse ellipse)
                {
                    double radius = ellipse.Width / 2;
                    double centerX = left + radius;
                    double centerY = top + radius;
                    var snappedCenter = SnapHelper.SnapJunctionToBlocks(new Point(centerX, centerY), blocks);
                    left = snappedCenter.X - radius;
                    top = snappedCenter.Y - radius;
                    Canvas.SetLeft(ellipse, left);
                    Canvas.SetTop(ellipse, top);

                    if ((idef3Renderer?.SetJunctionPositionFromEllipse(ellipse, left, top) ?? false))
                        idef3Renderer.UpdateConnections();
                }

                e.Handled = true;
            }
        }


        // ==== Масштабирование canvas через Ctrl+колесо мыши ====
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

        // ==== Загрузка схемы из текста ====
        private void LoadFromText_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clear_Click(null, null);
                currentDiagramType = typeManager.DetectDiagramType(DiagramTextBox.Text);
                typeManager.CurrentType = currentDiagramType;
                TxtDiagramType.Text = currentDiagramType.ToString();
                TxtStatus.Text = "Загружено: " + typeManager.GetDescription(currentDiagramType);

                switch (currentDiagramType)
                {
                    case DiagramType.IDEF0: LoadIDEF0Diagram(); break;
                    case DiagramType.FEO: LoadFEODiagram(); break;
                    case DiagramType.NodeTree: LoadNodeTreeDiagram(); break;
                    case DiagramType.IDEF3: LoadIDEF3Diagram(); break;
                    case DiagramType.DFD: LoadDFDDiagram(); break;
                }
                ChkLockLabels.IsChecked = true;
                dragDropManager.SetLabelDraggingEnabled(false);
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при загрузке:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "Ошибка загрузки";
            }
        }

        // ==== Загрузка и рендер IDEF0 ====
        private void LoadIDEF0Diagram()
        {
            var parsed = DiagramParser.Parse(DiagramTextBox.Text);
            var blockData = parsed.Item1;
            var arrowData = parsed.Item2;

            foreach (var data in blockData)
            {
                var block = renderer.CreateBlock(data.Text, data.Code, data.X, data.Y, data.Width, data.Height);
                blocks[data.Code] = block;
                dragDropManager.AttachBlockEvents(block.Visual);
            }
            ArrowCalculator.SetAllBlocks(blocks);
            rawArrowData = arrowData.Select(a => new ArrowData { From = a.From, To = a.To, Label = a.Label, Type = a.Type }).ToList();
            RenderArrowsIDEF0();
            dragDropManager.SetOnBlockMovedCallback(OnBlockMoved);
            ShowValidationResult(IDEF0Validator.Validate(blocks, arrows));
        }

        // ==== Перерисовка стрелок после перемещения блока ====
        private void OnBlockMoved()
        {
            if (currentDiagramType != DiagramType.IDEF0 || rawArrowData.Count == 0) return;
            RenderArrowsIDEF0();
            UpdateStatusBar();
        }

        // ==== рендер стрелок IDEF0 с удалением старых ====
        private void RenderArrowsIDEF0()
        {
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

            foreach (var data in ArrowCalculator.PreprocessArrows(rawArrowData, blocks))
            {
                DiagramBlock fromBlock = blocks.ContainsKey(data.From) ? blocks[data.From] : null;
                DiagramBlock toBlock = blocks.ContainsKey(data.To) ? blocks[data.To] : null;
                var arrow = renderer.CreateArrowWithDistribution(fromBlock, toBlock, data.From, data.To, data.Label, data.Type, data.IndexOnSide, data.TotalOnSide);
                if (arrow != null)
                {
                    arrows.Add(arrow);
                    if (arrow.Label != null) dragDropManager.AttachLabelEvents(arrow.Label);
                }
            }

        }

        // ==== Загрузка схемы FEO ====
        private void LoadFEODiagram()
        {
            // 1. Парсим данные
            var parsed = DiagramParser.Parse(DiagramTextBox.Text);
            var blockData = parsed.Item1;
            var arrowData = parsed.Item2;

            var diagram = new FEODiagram();

            // 2. Создаём компоненты (блоки)
            foreach (var block in blockData)
            {
                diagram.Components.Add(new FEOComponent
                {
                    Code = block.Code,
                    Name = block.Text,
                    X = block.X,
                    Y = block.Y,
                    Width = block.Width,
                    Height = block.Height
                });
            }

            // 3. Создаём стандартные стрелки (ArrowData)
            foreach (var ad in arrowData)
            {
                diagram.Arrows.Add(new ArrowData
                {
                    From = ad.From,
                    To = ad.To,
                    Label = ad.Label,
                    Type = ad.Type,
                    IndexOnSide = 0,
                    TotalOnSide = 1
                });
            }

            // 4. Раскладка: автоматически расставить компоненты по слоям
            var feoRenderer = new FEORenderer(DiagramCanvas, dragDropManager, CurrentStyle);
            feoRenderer.Render(diagram, feoBlocks, feoArrows);

            // 5. Собираем визуальные блоки для стрелок и external-портов
            feoBlocks.Clear();
            feoArrows.Clear();
            foreach (var comp in diagram.Components)
                feoBlocks[comp.Code] = new DiagramBlock { Code = comp.Code, Visual = null };

            foreach (var arrow in diagram.Arrows)
            {
                if (!string.IsNullOrEmpty(arrow.From) && arrow.From.StartsWith("external", StringComparison.OrdinalIgnoreCase) && !feoBlocks.ContainsKey(arrow.From))
                {
                    double x = 0, y = 0;
                    if (arrow.From.ToLower().EndsWith("left")) x = -120;
                    if (arrow.From.ToLower().EndsWith("bottom")) y = DiagramCanvas.Height + 75;
                    var border = new Border
                    {
                        Width = 20,
                        Height = 20,
                        Background = Brushes.Transparent,
                        BorderBrush = Brushes.Transparent
                    };
                    Canvas.SetLeft(border, x);
                    Canvas.SetTop(border, y);
                    DiagramCanvas.Children.Add(border);
                    feoBlocks[arrow.From] = new DiagramBlock { Code = arrow.From, Visual = border };
                }
                if (!string.IsNullOrEmpty(arrow.To) && arrow.To.StartsWith("external", StringComparison.OrdinalIgnoreCase) && !feoBlocks.ContainsKey(arrow.To))
                {
                    double x = 0, y = 0;
                    if (arrow.To.ToLower().EndsWith("left")) x = -120;
                    if (arrow.To.ToLower().EndsWith("bottom")) y = DiagramCanvas.Height + 75;
                    var border = new Border
                    {
                        Width = 20,
                        Height = 20,
                        Background = Brushes.Transparent,
                        BorderBrush = Brushes.Transparent
                    };
                    Canvas.SetLeft(border, x);
                    Canvas.SetTop(border, y);
                    DiagramCanvas.Children.Add(border);
                    feoBlocks[arrow.To] = new DiagramBlock { Code = arrow.To, Visual = border };
                }
            }

            // 6. Рендерим диаграмму и стрелки
            feoRenderer.Render(diagram, feoBlocks, feoArrows);

            // 7. При drag'n'drop обновляем стрелки
            dragDropManager.SetOnBlockMovedCallback(() =>
            {
                if (currentDiagramType == DiagramType.FEO && feoRenderer != null && diagram != null)
                    feoRenderer.UpdateArrows(diagram, feoBlocks, feoArrows);
            });

            currentDiagramType = DiagramType.FEO;

            MessageBox.Show(
                $"FEO диаграмма загружена!\n\nБлоков: {diagram.Components.Count}\nСвязей: {diagram.Arrows.Count}",
                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        // ==== Загрузка NodeTree ====
        private void LoadNodeTreeDiagram()
        {
            nodeTreeData = DiagramParser.ParseNodeTree(DiagramTextBox.Text);
            nodeTreeRenderer = new NodeTreeRenderer(DiagramCanvas, blocks, connectionManager, CurrentStyle);
            nodeTreeRenderer.RenderNodeTree(nodeTreeData);
            currentDiagramType = DiagramType.NodeTree;

            foreach (DiagramBlock block in blocks.Values)
                dragDropManager.AttachBlockEvents(block.Visual);

            // Обновление соединений при перетаскивании
            dragDropManager.SetOnBlockMovedCallback(() =>
            {
                if (currentDiagramType == DiagramType.NodeTree && connectionManager != null)
                {
                    connectionManager.UpdateAllConnections();
                }
            });

            MessageBox.Show("Дерево узлов загружено!\n\nУзлов: " + nodeTreeData.Count,
                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ==== Загрузка IDEF3 ====
        private void LoadIDEF3Diagram()
        {
            var parsed = DiagramParser.ParseIDEF3(DiagramTextBox.Text);
            var uowRaw = parsed.Item1;
            var junctionRaw = parsed.Item2;
            var linkRaw = parsed.Item3;

            lastUows = uowRaw.Select(u => new IDEF3UOW { Id = u.Id, Name = u.Name }).ToList();
            lastJunctions = junctionRaw.Select(j => new IDEF3Junction { Id = j.Id, Type = j.Type }).ToList();
            lastLinks = linkRaw.Select(l => new IDEF3Link { From = l.From, To = l.To }).ToList();

            idef3Renderer = new IDEF3Renderer(DiagramCanvas, blocks, CurrentStyle);
            idef3Renderer.RenderIDEF3(lastUows, lastJunctions, lastLinks);

            currentDiagramType = DiagramType.IDEF3;

            dragDropManager.SetOnBlockMovedCallback(delegate
            {
                idef3Renderer?.UpdateConnections();
            });

            foreach (DiagramBlock block in blocks.Values)
                dragDropManager.AttachBlockEvents(block.Visual);

            foreach (IDEF3Junction junc in lastJunctions)
                idef3Renderer.AttachJunctionDragEvents(junc.Id, id => idef3Renderer.UpdateConnections());

            MessageBox.Show(
                "IDEF3 загружена!\n\nРабот (UOW): " + lastUows.Count + "\nПерекрёстков: " + lastJunctions.Count + "\nСвязей: " + lastLinks.Count,
                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ==== Загрузка DFD ====
        private void LoadDFDDiagram()
        {
            currentDFD = new DFDDiagram();
            string[] lines = DiagramTextBox.Text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                    continue;

                string[] parts = trimmed.Split('|');
                if (parts.Length < 2) continue;

                switch (parts[0].ToUpper())
                {
                    case "PROCESS":
                        if (parts.Length >= 5 && double.TryParse(parts[3].Trim(), out double px) && double.TryParse(parts[4].Trim(), out double py))
                            currentDFD.Processes.Add(new DFDProcess { Id = parts[1].Trim(), Name = parts[2].Trim(), X = px, Y = py });
                        break;
                    case "ENTITY":
                        if (parts.Length >= 5 && double.TryParse(parts[3].Trim(), out double ex) && double.TryParse(parts[4].Trim(), out double ey))
                            currentDFD.Entities.Add(new DFDEntity { Id = parts[1].Trim(), Name = parts[2].Trim(), X = ex, Y = ey });
                        break;
                    case "STORE":
                        if (parts.Length >= 5 && double.TryParse(parts[3].Trim(), out double sx) && double.TryParse(parts[4].Trim(), out double sy))
                            currentDFD.Stores.Add(new DFDStore { Id = parts[1].Trim(), Name = parts[2].Trim(), X = sx, Y = sy });
                        break;
                    case "ARROW":
                        if (parts.Length >= 3)
                            currentDFD.Arrows.Add(new DFDArrow { FromId = parts[1].Trim(), ToId = parts[2].Trim(), Label = (parts.Length >= 4 ? parts[3].Trim() : "") });
                        break;
                }
            }

            if (currentDFD.Processes.Count + currentDFD.Entities.Count + currentDFD.Stores.Count == 0)
            {
                MessageBox.Show("Не найдено блоков!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            dfdRenderer = new DFDRenderer(DiagramCanvas);
            dfdRenderer.Render(currentDFD);
            dfdBlockVisuals = dfdRenderer.GetBlockVisuals();

            // Регистрируем drag для блоков
            foreach (var kvp in dfdBlockVisuals)
            {
                string blockId = kvp.Key;
                FrameworkElement visual = kvp.Value;
                dragDropManager.AttachDFDBlockDrag(visual, currentDFD, dfdRenderer, blockId);
            }

            // Регистрируем drag для подписей
            var initialLabels = dfdRenderer.GetArrowLabels();
            foreach (var label in initialLabels)
                dragDropManager.AttachLabelEvents(label);

            currentDiagramType = DiagramType.DFD;

            MessageBox.Show($"DFD: процессов {currentDFD.Processes.Count}, объектов {currentDFD.Entities.Count}, хранилищ {currentDFD.Stores.Count}, потоков {currentDFD.Arrows.Count}",
                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ==== Диалог валидации для IDEF0 ====
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

        // ==== Переключатели блокировки для drag&drop ====
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
                TxtStatus.Text = ChkLockLabels.IsChecked == true ? "Подписи заблокированы" : "Подписи разблокированы";
            }
        }

        // ==== Очистка схемы и статусов ====
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

        // ==== Экспорт схемы в PNG ====
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            bool success = SimpleCanvasExporter.ExportFromDialog(DiagramCanvas, 300);
            TxtStatus.Text = success ? "Экспорт завершён!" : "Ошибка экспорта";
            MessageBox.Show(success ? "Диаграмма успешно экспортирована!" : "Не удалось экспортировать диаграмму.",
                success ? "Экспорт завершён" : "Ошибка", MessageBoxButton.OK,
                success ? MessageBoxImage.Information : MessageBoxImage.Error);
        }

        // ========== Стилизация ==========
        private void StyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Защита от NullReferenceException
            if (DiagramCanvas == null || StyleComboBox == null)
                return;

            // Применение нового стиля
            if (StyleComboBox.SelectedItem is ComboBoxItem selected && selected.Tag is string tag
                && Enum.TryParse(tag, out DiagramStyleType newType))
                currentStyleType = newType;
            else
                currentStyleType = DiagramStyleType.ClassicBlackWhite;

            // Очищаем Canvas полностью (но НЕ коллекции блоков!)
            DiagramCanvas.Children.Clear();

            switch (currentDiagramType)
            {
                case DiagramType.FEO:
                    if (currentFEO != null && feoBlocks != null && feoBlocks.Count > 0)
                    {
                        feoRenderer = new FEORenderer(DiagramCanvas, dragDropManager, CurrentStyle);
                        feoRenderer.Render(currentFEO, feoBlocks, feoArrows);

                        foreach (DiagramBlock block in feoBlocks.Values)
                        {
                            if (block != null && block.Visual != null)
                                dragDropManager.AttachBlockEvents(block.Visual);
                        }
                    }
                    TxtStatus.Text = "Стиль FEO обновлён";
                    break;

                case DiagramType.IDEF3:
                    if (lastUows != null && lastUows.Count > 0 && lastJunctions != null && lastLinks != null)
                    {
                        idef3Renderer = new IDEF3Renderer(DiagramCanvas, blocks, CurrentStyle);
                        idef3Renderer.RenderIDEF3(lastUows, lastJunctions, lastLinks);

                        foreach (DiagramBlock block in blocks.Values)
                        {
                            if (block != null && block.Visual != null)
                                dragDropManager.AttachBlockEvents(block.Visual);
                        }
                        foreach (IDEF3Junction junc in lastJunctions)
                        {
                            if (junc != null)
                                idef3Renderer.AttachJunctionDragEvents(junc.Id, id => idef3Renderer.UpdateConnections());
                        }
                    }
                    TxtStatus.Text = "Стиль IDEF3 обновлён";
                    break;

                case DiagramType.IDEF0:
                    if (blocks != null && blocks.Count > 0 && rawArrowData != null && rawArrowData.Count > 0)
                    {
                        // Новый рендерер с нужным стилем
                        diagramRenderer = new DiagramRenderer(DiagramCanvas, CurrentStyle);

                        // Пересоздаём все ВИЗУАЛЬНЫЕ блоки!
                        var newBlocks = new Dictionary<string, DiagramBlock>();
                        foreach (var blockData in blocks)
                        {
                            var oldBlock = blockData.Value;
                            // Воспроизведи информацию: размеры, текст, position
                            var newBlock = diagramRenderer.CreateBlock(
                                oldBlock.Text,
                                oldBlock.Code,
                                Canvas.GetLeft(oldBlock.Visual),
                                Canvas.GetTop(oldBlock.Visual),
                                oldBlock.Visual.Width,
                                oldBlock.Visual.Height
                            );
                            newBlocks[newBlock.Code] = newBlock;
                            dragDropManager.AttachBlockEvents(newBlock.Visual);
                        }
                        blocks = newBlocks; // пересохрани коллекцию

                        // Перерисуй стрелки
                        RenderArrowsIDEF0();
                    }
                    break;

                case DiagramType.NodeTree:
                    if (nodeTreeData != null && nodeTreeData.Count > 0)
                    {
                        var nodeTreeRenderer = new NodeTreeRenderer(DiagramCanvas, blocks, connectionManager, CurrentStyle);
                        nodeTreeRenderer.RenderNodeTree(nodeTreeData);

                        foreach (DiagramBlock block in blocks.Values)
                        {
                            if (block != null && block.Visual != null)
                                dragDropManager.AttachBlockEvents(block.Visual);
                        }
                    }
                    TxtStatus.Text = "Стиль NodeTree обновлён";
                    break;

                case DiagramType.DFD:
                    if (currentDFD != null)
                    {
                        dfdRenderer = new DFDRenderer(DiagramCanvas);
                        dfdRenderer.Render(currentDFD);
                        dfdBlockVisuals = dfdRenderer.GetBlockVisuals();
                        var arrowLabels = dfdRenderer.GetArrowLabels();

                        // Регистрируем drag для всех блоков
                        foreach (var kvp in dfdBlockVisuals)
                        {
                            string blockId = kvp.Key;
                            FrameworkElement visual = kvp.Value; 
                            dragDropManager.AttachDFDBlockDrag(visual, currentDFD, dfdRenderer, blockId);
                        }

                        // Регистрируем drag для подписей
                        foreach (var label in arrowLabels)
                            dragDropManager.AttachLabelEvents(label);

                        TxtStatus.Text = "DFD загружена";
                    }
                    break;

            }
        }

        private void DiagramTextBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DiagramTextBox.Focus();
        }

        // ==== Обновление status bar ====
        private void UpdateStatusBar()
        {
            TxtBlockCount.Text = blocks.Count.ToString();
            TxtArrowCount.Text = arrows.Count.ToString();
        }

    }
}
