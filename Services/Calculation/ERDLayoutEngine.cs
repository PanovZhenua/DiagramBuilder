using System.Collections.Generic;
using System.Linq;
using DiagramBuilder.Models;

namespace DiagramBuilder.Services.Calculation
{
    /// <summary>Раскладка ERD‑сущностей по слоям и расчёт размеров.</summary>
    public class ERDLayoutEngine
    {
        private const double DefaultHeaderHeight = 30.0;
        private const double DefaultRowHeight = 24.0;
        private const double VerticalPadding = 8.0;
        private const double HorizontalPadding = 12.0;

        private const double DefaultWidth = 220.0;

        private const double HorizontalSpacing = 80.0;
        private const double VerticalSpacing = 80.0;

        public void ApplyLayout(ERDDiagram diagram, double startX = 100.0, double startY = 100.0)
        {
            if (diagram == null || diagram.Entities == null || diagram.Entities.Count == 0)
                return;

            CalculateEntitySizes(diagram.Entities);

            List<List<ERDEntity>> layers = BuildLayers(diagram);

            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                List<ERDEntity> layer = layers[layerIndex];
                double y = startY + layerIndex * (GetMaxHeight(layer) + VerticalSpacing);

                for (int i = 0; i < layer.Count; i++)
                {
                    ERDEntity entity = layer[i];
                    double x = startX + i * (entity.Width + HorizontalSpacing);

                    entity.X = x;
                    entity.Y = y;
                }
            }
        }

        private void CalculateEntitySizes(List<ERDEntity> entities)
        {
            foreach (ERDEntity entity in entities)
            {
                for (int i = 0; i < entity.Fields.Count; i++)
                {
                    entity.Fields[i].RowIndex = i;
                    if (entity.Fields[i].RowHeight <= 0)
                        entity.Fields[i].RowHeight = DefaultRowHeight;
                }

                double header = entity.HeaderHeight > 0 ? entity.HeaderHeight : DefaultHeaderHeight;
                double rowsHeight = entity.Fields.Count * DefaultRowHeight;

                entity.Height = header + rowsHeight + 2.0 * VerticalPadding;
                entity.Width = entity.Width > 0 ? entity.Width : DefaultWidth;
                if (entity.Padding.Left == 0 && entity.Padding.Right == 0 &&
                    entity.Padding.Top == 0 && entity.Padding.Bottom == 0)
                {
                    entity.Padding = new System.Windows.Thickness(HorizontalPadding, VerticalPadding,
                                                                  HorizontalPadding, VerticalPadding);
                }
            }
        }

        private List<List<ERDEntity>> BuildLayers(ERDDiagram diagram)
        {
            var entityById = diagram.Entities.ToDictionary(e => e.Id);
            var inDegree = new Dictionary<string, int>();

            foreach (ERDEntity e in diagram.Entities)
                inDegree[e.Id] = 0;

            foreach (ERDRelationship rel in diagram.Relationships)
            {
                int value;
                if (inDegree.TryGetValue(rel.ToEntityId, out value))
                    inDegree[rel.ToEntityId] = value + 1;
            }

            var result = new List<List<ERDEntity>>();
            var queue = new Queue<ERDEntity>(
                diagram.Entities.Where(e => inDegree[e.Id] == 0));

            var visited = new HashSet<string>();

            while (queue.Count > 0)
            {
                int count = queue.Count;
                var layer = new List<ERDEntity>();

                for (int i = 0; i < count; i++)
                {
                    ERDEntity entity = queue.Dequeue();
                    if (!visited.Add(entity.Id))
                        continue;

                    layer.Add(entity);

                    foreach (ERDRelationship rel in diagram.Relationships.Where(r => r.FromEntityId == entity.Id))
                    {
                        if (!inDegree.ContainsKey(rel.ToEntityId))
                            continue;

                        inDegree[rel.ToEntityId] = inDegree[rel.ToEntityId] - 1;
                        if (inDegree[rel.ToEntityId] == 0)
                        {
                            ERDEntity next;
                            if (entityById.TryGetValue(rel.ToEntityId, out next))
                                queue.Enqueue(next);
                        }
                    }
                }

                if (layer.Count > 0)
                    result.Add(layer);
            }

            // Циклы или "висящие" сущности
            var flat = result.SelectMany(l => l).ToList();
            var remaining = diagram.Entities.Where(e => !flat.Contains(e)).ToList();
            if (remaining.Count > 0)
                result.Add(remaining);

            return result;
        }

        private double GetMaxHeight(List<ERDEntity> entities)
        {
            if (entities == null || entities.Count == 0)
                return 0.0;

            double max = 0.0;
            foreach (ERDEntity e in entities)
            {
                if (e.Height > max)
                    max = e.Height;
            }
            return max;
        }
    }
}
