// Services/Loading/DiagramLoadController.cs
using DiagramBuilder.Models;
using DiagramBuilder.Services.Core;
using DiagramBuilder.Services.Management;
using DiagramBuilder.Services.Parsing;
using DiagramBuilder.Services.Rendering;
using DiagramBuilder.Services.Calculation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DiagramBuilder.Services.Loading
{
    public static class DiagramLoadController
    {
        // =================== ГЛАВНЫЙ МЕТОД ЗАГРУЗКИ ===================
        public static void Load(
            DiagramType type,
            string text,
            Canvas canvas,
            DiagramStyle style,
            DiagramRenderer renderer,
            ConnectionManager connectionManager,
            DragDropManager dragDropManager,
            Dictionary<string, DiagramBlock> blocks,
            List<DiagramArrow> arrows,
            DiagramState diagramState,
            Action onBlockMoved)
        {
            try
            {
                switch (type)
                {
                    case DiagramType.IDEF0:
                        LoadIDEF0(text, canvas, renderer, dragDropManager, blocks, arrows, diagramState, onBlockMoved);
                        break;
                    case DiagramType.FEO:
                        LoadFEO(text, canvas, style, dragDropManager, diagramState);
                        break;
                    case DiagramType.NodeTree:
                        LoadNodeTree(text, canvas, style, connectionManager, blocks, diagramState, dragDropManager);
                        break;
                    case DiagramType.IDEF3:
                        LoadIDEF3(text, canvas, style, blocks, diagramState, dragDropManager);
                        break;
                    case DiagramType.DFD:
                        LoadDFD(text, canvas, dragDropManager, diagramState);
                        break;
                    case DiagramType.DocumentFlow:
                        LoadDocumentFlow(text, canvas, dragDropManager, diagramState);
                        break;
                    case DiagramType.ERD:
                        LoadERD(text, canvas, style, dragDropManager, diagramState);
                        break;
                    case DiagramType.UseCase:
                        LoadUseCase(text, canvas, style, dragDropManager, diagramState);
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка загрузки диаграммы: " + ex.Message, ex);
            }
        }

        // =================== IDEF0 ===================
        private static void LoadIDEF0(
            string text,
            Canvas canvas,
            DiagramRenderer renderer,
            DragDropManager dragDropManager,
            Dictionary<string, DiagramBlock> blocks,
            List<DiagramArrow> arrows,
            DiagramState diagramState,
            Action onBlockMoved)
        {
            try
            {
                var parsed = DiagramParser.Parse(text);
                var blockData = parsed.Item1;
                var arrowData = parsed.Item2;

                diagramState.RawArrowData.Clear();
                foreach (var a in arrowData)
                {
                    diagramState.RawArrowData.Add(new ArrowData
                    {
                        From = a.From,
                        To = a.To,
                        Label = a.Label,
                        Type = a.Type
                    });
                }

                foreach (var data in blockData)
                {
                    var block = renderer.CreateBlock(
                        data.Text,
                        data.Code,
                        data.X,
                        data.Y,
                        data.Width,
                        data.Height);

                    blocks[data.Code] = block;

                    if (!canvas.Children.Contains(block.Visual))
                    {
                        canvas.Children.Add(block.Visual);
                    }

                    dragDropManager.AttachBlockEvents(block.Visual);

                    renderer.AttachResizeEvents(block, () =>
                    {
                        RenderArrowsIDEF0(canvas, renderer, dragDropManager, blocks, arrows, diagramState.RawArrowData);
                    });
                }

                ArrowCalculator.SetAllBlocks(blocks);
                RenderArrowsIDEF0(canvas, renderer, dragDropManager, blocks, arrows, diagramState.RawArrowData);

                dragDropManager.SetOnBlockMovedCallback(onBlockMoved);
                dragDropManager.SetBlocks(blocks);
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка загрузки IDEF0: " + ex.Message, ex);
            }
        }

        public static void RenderArrowsIDEF0(
            Canvas canvas,
            DiagramRenderer renderer,
            DragDropManager dragDropManager,
            Dictionary<string, DiagramBlock> blocks,
            List<DiagramArrow> arrows,
            List<ArrowData> rawArrowData)
        {
            // Удаляем старые стрелки
            foreach (var arrow in arrows)
            {
                if (arrow.Lines != null)
                {
                    foreach (var line in arrow.Lines)
                    {
                        if (line != null && canvas.Children.Contains(line))
                            canvas.Children.Remove(line);
                    }
                }
                if (arrow.ArrowHead != null && canvas.Children.Contains(arrow.ArrowHead))
                    canvas.Children.Remove(arrow.ArrowHead);
                if (arrow.Label != null && canvas.Children.Contains(arrow.Label))
                    canvas.Children.Remove(arrow.Label);
            }
            arrows.Clear();

            var processed = ArrowCalculator.PreprocessArrows(rawArrowData, blocks);

            foreach (var data in processed)
            {
                DiagramBlock fromBlock = blocks.ContainsKey(data.From) ? blocks[data.From] : null;
                DiagramBlock toBlock = blocks.ContainsKey(data.To) ? blocks[data.To] : null;

                var arrow = renderer.CreateArrowWithDistribution(
                    fromBlock, toBlock,
                    data.From, data.To,
                    data.Label, data.Type,
                    data.IndexOnSide, data.TotalOnSide);

                if (arrow != null)
                {
                    arrows.Add(arrow);
                    if (arrow.Label != null)
                        dragDropManager.AttachLabelEvents(arrow.Label);
                }
            }
        }

        // =================== FEO ===================
        private static void LoadFEO(string text, Canvas canvas, DiagramStyle style, DragDropManager dragDropManager, DiagramState diagramState)
        {
            var parsed = DiagramParser.Parse(text);
            var blockData = parsed.Item1;
            var arrowData = parsed.Item2;
            var annotationData = parsed.Item3;  // ✅ НОВОЕ!

            diagramState.CurrentFEO = new FEODiagram();
            diagramState.CurrentFEO.Components.Clear();
            diagramState.CurrentFEO.Arrows.Clear();
            diagramState.CurrentFEO.Annotations.Clear();  // ✅ НОВОЕ!

            foreach (var b in blockData)
            {
                diagramState.CurrentFEO.Components.Add(new FEOComponent
                {
                    Code = b.Code,
                    Name = b.Text,
                    X = b.X,
                    Y = b.Y,
                    Width = b.Width,
                    Height = b.Height
                });
            }

            foreach (var a in arrowData)
            {
                diagramState.CurrentFEO.Arrows.Add(new ArrowData
                {
                    From = a.From,
                    To = a.To,
                    Label = a.Label,
                    Type = a.Type,
                    IndexOnSide = 0,
                    TotalOnSide = 1
                });
            }

            // ✅ НОВОЕ: добавляем аннотации
            foreach (var annot in annotationData)
            {
                diagramState.CurrentFEO.Annotations.Add(new FEOAnnotation
                {
                    ArrowFromId = annot.ArrowFromId,
                    ArrowToId = annot.ArrowToId,
                    Text = annot.Text,
                    OffsetX = annot.OffsetX,
                    OffsetY = annot.OffsetY
                });
            }

            diagramState.FeoRenderer = new FEORenderer(canvas, dragDropManager, style);
            diagramState.FeoRenderer.Render(diagramState.CurrentFEO, diagramState.FeoBlocks, diagramState.FeoArrows);

            dragDropManager.SetOnBlockMovedCallback(() =>
            {
                if (diagramState.FeoRenderer != null && diagramState.CurrentFEO != null)
                {
                    diagramState.FeoRenderer.UpdateArrows(diagramState.CurrentFEO, diagramState.FeoBlocks, diagramState.FeoArrows);
                }
            });
        }

        // =================== NODETREE ===================
        private static void LoadNodeTree(
            string text,
            Canvas canvas,
            DiagramStyle style,
            ConnectionManager connectionManager,
            Dictionary<string, DiagramBlock> blocks,
            DiagramState diagramState,
            DragDropManager dragDropManager)
        {
            diagramState.NodeTreeData = DiagramParser.ParseNodeTree(text);
            diagramState.NodeTreeRenderer = new NodeTreeRenderer(canvas, blocks, connectionManager, style);
            diagramState.NodeTreeRenderer.RenderNodeTree(diagramState.NodeTreeData);

            foreach (var kv in blocks)
                dragDropManager.AttachBlockEvents(kv.Value.Visual);

            dragDropManager.SetOnBlockMovedCallback(() => connectionManager.UpdateAllConnections());
            dragDropManager.SetBlocks(blocks);
        }

        // =================== IDEF3 ===================
        private static void LoadIDEF3(
            string text,
            Canvas canvas,
            DiagramStyle style,
            Dictionary<string, DiagramBlock> blocks,
            DiagramState diagramState,
            DragDropManager dragDropManager)
        {
            var parsed = DiagramParser.ParseIDEF3(text);
            var uowRaw = parsed.Item1;
            var juncRaw = parsed.Item2;
            var linkRaw = parsed.Item3;

            diagramState.LastUows.Clear();
            diagramState.LastJunctions.Clear();
            diagramState.LastLinks.Clear();

            foreach (var u in uowRaw)
                diagramState.LastUows.Add(new IDEF3UOW { Id = u.Id, Name = u.Name });
            foreach (var j in juncRaw)
                diagramState.LastJunctions.Add(new IDEF3Junction { Id = j.Id, Type = j.Type });
            foreach (var l in linkRaw)
                diagramState.LastLinks.Add(new IDEF3Link { From = l.From, To = l.To });

            diagramState.Idef3Renderer = new IDEF3Renderer(canvas, blocks, style);
            diagramState.Idef3Renderer.RenderIDEF3(diagramState.LastUows, diagramState.LastJunctions, diagramState.LastLinks);

            dragDropManager.SetOnBlockMovedCallback(() =>
            {
                if (diagramState.Idef3Renderer != null)
                    diagramState.Idef3Renderer.UpdateConnections();
            });

            foreach (var kv in blocks)
                dragDropManager.AttachBlockEvents(kv.Value.Visual);

            foreach (var j in diagramState.LastJunctions)
                diagramState.Idef3Renderer.AttachJunctionDragEvents(j.Id, delegate (string jid)
                {
                    if (diagramState.Idef3Renderer != null)
                        diagramState.Idef3Renderer.UpdateConnections();
                });

            dragDropManager.SetBlocks(blocks);
        }

        // =================== DFD ===================
        private static void LoadDFD(
            string text,
            Canvas canvas,
            DragDropManager dragDropManager,
            DiagramState diagramState)
        {
            // ✅ Используем ParseDFD для DFD
            var diagram = DiagramParser.ParseDFD(text);

            diagramState.CurrentDFD = diagram;
            diagramState.DfdRenderer = new DFDRenderer(canvas);
            diagramState.DfdRenderer.Render(diagram);

            diagramState.DfdBlockVisuals.Clear();
            var visuals = diagramState.DfdRenderer.GetBlockVisuals();

            foreach (var kv in visuals)
            {
                diagramState.DfdBlockVisuals[kv.Key] = kv.Value;
                string blockId = kv.Key;

                dragDropManager.AttachDragGeneric(kv.Value, (x, y) =>
                {
                    UpdateDFDBlockPosition(diagramState.CurrentDFD, blockId, x, y);
                    diagramState.DfdRenderer.UpdateArrows(diagramState.CurrentDFD);

                    // ВАЖНО: после UpdateArrows() labels пересозданы => подписываем заново
                    foreach (var lbl in diagramState.DfdRenderer.GetArrowLabels())
                        dragDropManager.AttachLabelEvents(lbl);
                });
            }

            foreach (var lbl in diagramState.DfdRenderer.GetArrowLabels())
            {
                dragDropManager.AttachLabelEvents(lbl);
            }

            var blockTypes = diagramState.DfdRenderer.GetBlockTypes();
            dragDropManager.SetBlockTypes(blockTypes);
        }

        // =================== DOCUMENTFLOW ===================
        private static void LoadDocumentFlow(
            string text,
            Canvas canvas,
            DragDropManager dragDropManager,
            DiagramState diagramState)
        {
            // ✅ Используем правильный парсер и рендерер
            var diagram = DiagramParser.ParseDocumentFlow(text);

            diagramState.CurrentDocumentFlow = diagram;
            diagramState.DocumentFlowRenderer = new DocumentFlowRenderer(canvas);
            diagramState.DocumentFlowRenderer.Render(diagram);

            diagramState.DocumentFlowBlockVisuals.Clear();
            var visuals = diagramState.DocumentFlowRenderer.GetBlockVisuals();

            foreach (var kv in visuals)
            {
                diagramState.DocumentFlowBlockVisuals[kv.Key] = kv.Value;
                string blockId = kv.Key;

                dragDropManager.AttachDragGeneric(kv.Value, (x, y) =>
                {
                    UpdateDocumentFlowBlockPosition(diagramState.CurrentDocumentFlow, blockId, x, y);

                    // ✅ Перерисовываем и ЗАНОВО подключаем drag-n-drop
                    diagramState.DocumentFlowRenderer.Render(diagramState.CurrentDocumentFlow);

                    // ✅ ВАЖНО: Переподключаем события после рендера
                    var newVisuals = diagramState.DocumentFlowRenderer.GetBlockVisuals();
                    diagramState.DocumentFlowBlockVisuals.Clear();

                    foreach (var newKv in newVisuals)
                    {
                        diagramState.DocumentFlowBlockVisuals[newKv.Key] = newKv.Value;
                        string newBlockId = newKv.Key;

                        dragDropManager.AttachDragGeneric(newKv.Value, (nx, ny) =>
                        {
                            UpdateDocumentFlowBlockPosition(diagramState.CurrentDocumentFlow, newBlockId, nx, ny);
                            diagramState.DocumentFlowRenderer.Render(diagramState.CurrentDocumentFlow);
                            ReattachDocumentFlowEvents(diagramState, dragDropManager);
                        });
                    }

                    foreach (var lbl in diagramState.DocumentFlowRenderer.GetArrowLabels())
                    {
                        dragDropManager.AttachLabelEvents(lbl);
                    }
                });
            }

            foreach (var lbl in diagramState.DocumentFlowRenderer.GetArrowLabels())
            {
                dragDropManager.AttachLabelEvents(lbl);
            }

            var blockTypes = diagramState.DocumentFlowRenderer.GetBlockTypes();
            dragDropManager.SetBlockTypes(blockTypes);
        }

        // ✅ Вспомогательный метод для переподключения событий
        private static void ReattachDocumentFlowEvents(DiagramState diagramState, DragDropManager dragDropManager)
        {
            var newVisuals = diagramState.DocumentFlowRenderer.GetBlockVisuals();
            diagramState.DocumentFlowBlockVisuals.Clear();

            foreach (var newKv in newVisuals)
            {
                diagramState.DocumentFlowBlockVisuals[newKv.Key] = newKv.Value;
                string newBlockId = newKv.Key;

                dragDropManager.AttachDragGeneric(newKv.Value, (nx, ny) =>
                {
                    UpdateDocumentFlowBlockPosition(diagramState.CurrentDocumentFlow, newBlockId, nx, ny);
                    diagramState.DocumentFlowRenderer.Render(diagramState.CurrentDocumentFlow);
                    ReattachDocumentFlowEvents(diagramState, dragDropManager);
                });
            }

            foreach (var lbl in diagramState.DocumentFlowRenderer.GetArrowLabels())
            {
                dragDropManager.AttachLabelEvents(lbl);
            }

            var blockTypes = diagramState.DocumentFlowRenderer.GetBlockTypes();
            dragDropManager.SetBlockTypes(blockTypes);
        }

        private static void UpdateDocumentFlowBlockPosition(DocumentFlowDiagram diagram, string blockId, double x, double y)
        {
            var proc = diagram.Processes?.Find(p => p.Id == blockId);
            if (proc != null)
            {
                proc.X = x;
                proc.Y = y;
                return;
            }

            var ent = diagram.Entities?.Find(e => e.Id == blockId);
            if (ent != null)
            {
                ent.X = x;
                ent.Y = y;
                return;
            }

            var doc = diagram.Documents?.Find(d => d.Id == blockId);
            if (doc != null)
            {
                doc.X = x;
                doc.Y = y;
            }
        }

        // =================== ERD ===================
        private static void LoadERD(string text, Canvas canvas, DiagramStyle style, DragDropManager dragDropManager, DiagramState diagramState)
        {
            ERDDiagram erd = DiagramParser.ParseERD(text);

            var layout = new ERDLayoutEngine();
            layout.ApplyLayout(erd, 100.0, 100.0);

            diagramState.CurrentERD = erd;
            diagramState.ErdRenderer = new ERDRenderer(canvas, style);

            // ✅ Устанавливаем callback для переподписки drag-n-drop после resize
            diagramState.ErdRenderer.SetEntityRecreatedCallback((visual, entityId) =>
            {
                if (dragDropManager != null && erd?.Entities != null)
                {
                    var ent = erd.Entities.FirstOrDefault(e => e.Id == entityId);
                    if (ent != null)
                    {
                        dragDropManager.AttachDragGeneric(visual, (x, y) =>
                        {
                            ent.X = x;
                            ent.Y = y;
                            diagramState.ErdRenderer?.UpdateRelationshipsForEntity(erd, entityId);
                        });
                    }
                }
            });

            diagramState.ErdRenderer.RenderDiagram(erd);

            diagramState.ErdEntityVisuals = diagramState.ErdRenderer.EntityVisuals;

            var blockTypes = new Dictionary<FrameworkElement, string>();
            foreach (var kv in diagramState.ErdEntityVisuals)
                blockTypes[kv.Value] = "ERDENTITY";

            if (dragDropManager != null)
            {
                foreach (var kv in diagramState.ErdEntityVisuals)
                {
                    string entityId = kv.Key;
                    FrameworkElement visual = kv.Value;
                    string capturedId = entityId;

                    dragDropManager.AttachDragGeneric(visual, (x, y) =>
                    {
                        if (erd?.Entities != null)
                        {
                            var ent = erd.Entities.FirstOrDefault(e => e.Id == capturedId);
                            if (ent != null)
                            {
                                ent.X = x;
                                ent.Y = y;
                                diagramState.ErdRenderer?.UpdateRelationshipsForEntity(erd, capturedId);
                            }
                        }
                    });
                }

                dragDropManager.SetBlockTypes(blockTypes);
            }
        }

        // =================== USECASE ===================
        private static void LoadUseCase(string text, Canvas canvas, DiagramStyle style, DragDropManager dragDropManager, DiagramState diagramState)
        {
            var diagram = DiagramParser.ParseUseCase(text);

            diagramState.CurrentUseCase = diagram;
            diagramState.UseCaseRenderer = new UseCaseRenderer(canvas, style);
            diagramState.UseCaseRenderer.RenderDiagram(diagram);

            diagramState.UseCaseVisuals.Clear();
            var allVisuals = diagramState.UseCaseRenderer.GetAllVisuals();

            var blockTypes = new Dictionary<FrameworkElement, string>();

            foreach (var kv in allVisuals)
            {
                diagramState.UseCaseVisuals[kv.Key] = kv.Value;
                string elementId = kv.Key;

                // Определяем тип элемента
                bool isActor = diagram.Actors.Any(a => a.Id == elementId);
                blockTypes[kv.Value] = isActor ? "ACTOR" : "USECASE";

                dragDropManager.AttachDragGeneric(kv.Value, (x, y) =>
                {
                    UpdateUseCaseElementPosition(diagramState.CurrentUseCase, elementId, x, y);
                    diagramState.UseCaseRenderer.UpdateLinks(diagramState.CurrentUseCase);

                    // Переподключаем drag-n-drop для надписей после обновления
                    foreach (var lbl in diagramState.UseCaseRenderer.GetLinkLabels())
                    {
                        dragDropManager.AttachLabelEvents(lbl);
                    }
                });
            }

            // Подключаем drag-n-drop для надписей <<include>> и <<extend>>
            foreach (var label in diagramState.UseCaseRenderer.GetLinkLabels())
            {
                dragDropManager.AttachLabelEvents(label);
            }

            dragDropManager.SetBlockTypes(blockTypes);
        }

        // Вспомогательный метод для обновления позиции элемента UseCase
        private static void UpdateUseCaseElementPosition(UseCaseDiagram diagram, string elementId, double x, double y)
        {
            if (diagram == null) return;

            // Проверяем акторов
            var actor = diagram.Actors?.Find(a => a.Id == elementId);
            if (actor != null)
            {
                actor.X = x;
                actor.Y = y;
                return;
            }

            // Проверяем use cases
            var useCase = diagram.UseCases?.Find(u => u.Id == elementId);
            if (useCase != null)
            {
                useCase.X = x;
                useCase.Y = y;
            }
        }   

        // =================== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ===================

        private static void UpdateDFDBlockPosition(DFDDiagram diagram, string blockId, double x, double y)
        {
            var proc = diagram.Processes?.Find(p => p.Id == blockId);
            if (proc != null)
            {
                proc.X = x;
                proc.Y = y;
                return;
            }

            var ent = diagram.Entities?.Find(e => e.Id == blockId);
            if (ent != null)
            {
                ent.X = x;
                ent.Y = y;
                return;
            }

            var store = diagram.Stores?.Find(s => s.Id == blockId);
            if (store != null)
            {
                store.X = x;
                store.Y = y;
                return;
            }

            if (diagram.DocFlows != null)
            {
                var docFlow = diagram.DocFlows.Find(d => d.Id == blockId);
                if (docFlow != null)
                {
                    docFlow.X = x;
                    docFlow.Y = y;
                }
            }
        }
    }
}
