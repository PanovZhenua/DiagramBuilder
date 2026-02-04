using System;
using System.Collections.Generic;
using System.Linq;

namespace DiagramBuilder.Services
{
    /// <summary>
    /// Парсер текстовых описаний диаграмм
    /// </summary>
    public static class DiagramParser
    {
        // ==================== IDEF0 / FEO ====================

        public static (List<BlockData>, List<ArrowData>) Parse(string text)
        {
            var blocks = new List<BlockData>();
            var arrows = new List<ArrowData>();

            var lines = text.Split('\n');

            foreach (var line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("#") || string.IsNullOrWhiteSpace(trimmed))
                    continue;

                if (trimmed.StartsWith("BLOCK|"))
                {
                    var parts = trimmed.Split('|');
                    if (parts.Length >= 6)
                    {
                        blocks.Add(new BlockData
                        {
                            Code = parts[1].Trim(),
                            Text = parts[2].Trim(),
                            X = double.Parse(parts[3].Trim()),
                            Y = double.Parse(parts[4].Trim()),
                            Width = double.Parse(parts[5].Trim()),
                            Height = double.Parse(parts[6].Trim())
                        });
                    }
                }
                else if (trimmed.StartsWith("ARROW|"))
                {
                    var parts = trimmed.Split('|');
                    if (parts.Length >= 3)
                    {
                        arrows.Add(new ArrowData
                        {
                            From = parts[1].Trim(),
                            To = parts[2].Trim(),
                            Label = parts.Length > 3 ? parts[3].Trim() : "",
                            Type = parts.Length > 4 ? parts[4].Trim() : "connect"
                        });
                    }
                }
            }

            return (blocks, arrows);
        }

        // ==================== NODE TREE ====================

        public static List<NodeData> ParseNodeTree(string text)
        {
            var nodes = new List<NodeData>();
            var lines = text.Split('\n');

            foreach (var line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("#") || string.IsNullOrWhiteSpace(trimmed))
                    continue;

                if (trimmed.StartsWith("NODE|"))
                {
                    var parts = trimmed.Split('|');
                    if (parts.Length >= 6)
                    {
                        nodes.Add(new NodeData
                        {
                            Code = parts[1].Trim(),
                            Name = parts[2].Trim(),
                            Level = int.Parse(parts[3].Trim()),
                            Parent = parts[4].Trim(),
                            X = double.Parse(parts[5].Trim()),
                            Y = double.Parse(parts[6].Trim())
                        });
                    }
                }
            }

            return nodes;
        }

        // ==================== IDEF3 ====================

        public static (List<UOWData>, List<JunctionData>, List<LinkData>) ParseIDEF3(string text)
        {
            var uows = new List<UOWData>();
            var junctions = new List<JunctionData>();
            var links = new List<LinkData>();

            var lines = text.Split('\n');

            foreach (var line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("#") || string.IsNullOrWhiteSpace(trimmed))
                    continue;

                if (trimmed.StartsWith("UOW|"))
                {
                    var parts = trimmed.Split('|');
                    if (parts.Length >= 7)
                    {
                        uows.Add(new UOWData
                        {
                            Id = parts[1].Trim(),
                            Name = parts[2].Trim(),
                            X = double.Parse(parts[3].Trim()),
                            Y = double.Parse(parts[4].Trim()),
                            Width = double.Parse(parts[5].Trim()),
                            Height = double.Parse(parts[6].Trim())
                        });
                    }
                }
                else if (trimmed.StartsWith("JUNCTION|"))
                {
                    var parts = trimmed.Split('|');
                    if (parts.Length >= 5)
                    {
                        junctions.Add(new JunctionData
                        {
                            Id = parts[1].Trim(),
                            Type = parts[2].Trim(),
                            X = double.Parse(parts[3].Trim()),
                            Y = double.Parse(parts[4].Trim())
                        });
                    }
                }
                else if (trimmed.StartsWith("LINK|"))
                {
                    var parts = trimmed.Split('|');
                    if (parts.Length >= 3)
                    {
                        links.Add(new LinkData
                        {
                            From = parts[1].Trim(),
                            To = parts[2].Trim()
                        });
                    }
                }
            }

            return (uows, junctions, links);
        }

        // ==================== DATA CLASSES ====================

        public class BlockData
        {
            public string Code { get; set; }
            public string Text { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
        }

        public class ArrowData
        {
            public string From { get; set; }
            public string To { get; set; }
            public string Label { get; set; }
            public string Type { get; set; }
        }

        public class NodeData
        {
            public string Code { get; set; }
            public string Name { get; set; }
            public int Level { get; set; }
            public string Parent { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
        }

        public class UOWData
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
        }

        public class JunctionData
        {
            public string Id { get; set; }
            public string Type { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
        }

        public class LinkData
        {
            public string From { get; set; }
            public string To { get; set; }
        }
    }
}
