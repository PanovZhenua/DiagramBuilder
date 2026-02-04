using System;
using System.Collections.Generic;
using System.Linq;
using DiagramBuilder.Models;

namespace DiagramBuilder.Services.Layout
{
    /// <summary>
    /// Автоматическая компоновка IDEF3-диаграмм: столбцы, уровни, распределение junction-объектов
    /// </summary>
    public class IDEF3LayoutEngine
    {
        private const double BlockWidth = 180;
        private const double BlockHeight = 80;
        private const double JunctionSize = 40;
        private const double HorizontalSpacing = 200;
        private const double VerticalSpacing = 120;
        private const double StartX = 100;
        private const double StartY = 100;

        public class LayoutResult
        {
            public Dictionary<string, (double X, double Y)> BlockPositions { get; set; } = new Dictionary<string, (double, double)>();
            public Dictionary<string, (double X, double Y)> JunctionPositions { get; set; } = new Dictionary<string, (double, double)>();
        }

        /// <summary>
        /// Размещает блоки и junction в соответствии с топологией связей
        /// </summary>
        public LayoutResult CalculateLayout(List<IDEF3UOW> uows, List<IDEF3Junction> junctions, List<IDEF3Link> links)
        {
            var result = new LayoutResult();
            var graph = BuildGraph(uows, junctions, links);
            var layers = CalculateLayers(graph);
            PlaceNodesInLayers(layers, result, graph);
            return result;
        }

        private Dictionary<string, List<string>> BuildGraph(List<IDEF3UOW> uows, List<IDEF3Junction> junctions, List<IDEF3Link> links)
        {
            var graph = new Dictionary<string, List<string>>();
            foreach (var u in uows) graph[u.Id] = new List<string>();
            foreach (var j in junctions) graph[j.Id] = new List<string>();
            foreach (var link in links)
            {
                if (graph.ContainsKey(link.From))
                    graph[link.From].Add(link.To);
            }
            return graph;
        }

        private List<List<string>> CalculateLayers(Dictionary<string, List<string>> graph)
        {
            var layers = new List<List<string>>();
            var visited = new HashSet<string>();
            var inDegree = new Dictionary<string, int>();
            foreach (var node in graph.Keys) inDegree[node] = 0;
            foreach (var neighbors in graph.Values)
                foreach (var n in neighbors)
                    if (inDegree.ContainsKey(n)) inDegree[n]++;

            var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            while (queue.Count > 0)
            {
                var currentLayer = new List<string>();
                int count = queue.Count;
                for (int i = 0; i < count; i++)
                {
                    var node = queue.Dequeue();
                    currentLayer.Add(node);
                    visited.Add(node);
                    if (graph.ContainsKey(node))
                        foreach (var neighbor in graph[node])
                        {
                            inDegree[neighbor]--;
                            if (inDegree[neighbor] == 0)
                                queue.Enqueue(neighbor);
                        }
                }
                layers.Add(currentLayer);
            }
            return layers;
        }

        private void PlaceNodesInLayers(List<List<string>> layers, LayoutResult result, Dictionary<string, List<string>> graph)
        {
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                var layer = layers[layerIndex];
                double y = StartY + layerIndex * VerticalSpacing; // <-- вертикальный слой
                int count = layer.Count;
                for (int i = 0; i < count; i++)
                {
                    var nodeId = layer[i];
                    double x = StartX + i * HorizontalSpacing;     // <-- блоки по горизонтали
                    if (nodeId.StartsWith("J", StringComparison.OrdinalIgnoreCase))
                        result.JunctionPositions[nodeId] = (x, y);
                    else
                        result.BlockPositions[nodeId] = (x, y);
                }
            }

            // ===== Вставить после размещения блоков/узлов (в конец PlaceNodesInLayers) =====
            foreach (var j in result.JunctionPositions.Keys.ToList())
            {
                // Найти все входящие связи
                var incoming = graph.Where(g => g.Value.Contains(j)).Select(g => g.Key).ToList();
                if (incoming.Count > 0)
                {
                    // Найти X для центра между входами (узлов или junction)
                    var xs = incoming.Select(id =>
                        result.BlockPositions.ContainsKey(id) ? result.BlockPositions[id].X + BlockWidth / 2 :
                        result.JunctionPositions.ContainsKey(id) ? result.JunctionPositions[id].X :
                        0).ToList();
                    double centerX = xs.Count > 0 ? xs.Sum() / xs.Count : result.JunctionPositions[j].X;
                    // Координата Y остаётся как назначена на первом проходе
                    var y = result.JunctionPositions[j].Y;
                    result.JunctionPositions[j] = (centerX, y);
                }
            }

        }

    }
}
