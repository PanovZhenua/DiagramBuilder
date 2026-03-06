using System;
using System.Collections.Generic;
using System.Linq;
using DiagramBuilder.Models;

namespace DiagramBuilder.Services.Parsing
{
    /// <summary>Парсинг текстовых описаний диаграмм в модели.</summary>
    public static class DiagramParser
    {
        #region Вспомогательные модели для IDEF0/FEO

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

            // ✅ Добавлены для IDEF0 распределения стрелок
            public int IndexOnSide { get; set; }
            public int TotalOnSide { get; set; }
        }

        // ✅ КЛАСС ДЛЯ ЗИГЗАГ-АННОТАЦИЙ
        public class AnnotationData
        {
            public string ArrowFromId { get; set; }  // От какого блока идёт connect стрелка
            public string ArrowToId { get; set; }    // К какому блоку идёт connect стрелка
            public string Text { get; set; }         // Текст аннотации
            public double OffsetX { get; set; }      // Смещение точки привязки по X (опционально)
            public double OffsetY { get; set; }      // Смещение точки привязки по Y (опционально)
        }

        #endregion

        #region IDEF0 / FEO

        /// <summary>
        /// Парсинг текста IDEF0/FEO в блоки, стрелки и аннотации.
        /// Формат:
        ///   BLOCK|Code|Text|X|Y|Width|Height
        ///   ARROW|from|to|label|type
        ///   ANNOTATION|fromBlock|toBlock|text[|offsetX|offsetY]
        ///   или
        ///   ZIGZAG|fromBlock|toBlock|text[|offsetX|offsetY]
        /// </summary>
        public static Tuple<List<BlockData>, List<ArrowData>, List<AnnotationData>> Parse(string text)
        {
            var blocks = new List<BlockData>();
            var arrows = new List<ArrowData>();
            var annotations = new List<AnnotationData>();

            if (string.IsNullOrWhiteSpace(text))
                return Tuple.Create(blocks, arrows, annotations);

            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
                    continue;

                if (line.StartsWith("BLOCK", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Split('|');
                    if (parts.Length < 7)
                        continue;

                    var b = new BlockData
                    {
                        Code = parts[1].Trim(),
                        Text = parts[2].Trim(),
                        X = ParseDouble(parts[3]),
                        Y = ParseDouble(parts[4]),
                        Width = ParseDouble(parts[5]),
                        Height = ParseDouble(parts[6])
                    };
                    blocks.Add(b);
                }
                else if (line.StartsWith("ARROW", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Split('|');
                    if (parts.Length < 3)
                        continue;

                    var a = new ArrowData
                    {
                        From = parts[1].Trim(),
                        To = parts[2].Trim(),
                        Label = parts.Length >= 4 ? parts[3].Trim() : string.Empty,
                        Type = parts.Length >= 5 ? parts[4].Trim() : "connect"
                    };
                    arrows.Add(a);
                }
                else if (line.StartsWith("ANNOTATION", StringComparison.OrdinalIgnoreCase) ||
                         line.StartsWith("ZIGZAG", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Split('|');
                    if (parts.Length < 4)
                        continue;

                    var annot = new AnnotationData
                    {
                        ArrowFromId = parts[1].Trim(),
                        ArrowToId = parts[2].Trim(),
                        Text = parts[3].Trim(),
                        OffsetX = parts.Length >= 5 ? ParseDouble(parts[4]) : 0,
                        OffsetY = parts.Length >= 6 ? ParseDouble(parts[5]) : 20
                    };
                    annotations.Add(annot);
                }
            }

            return Tuple.Create(blocks, arrows, annotations);
        }

        private static double ParseDouble(string s)
        {
            double value;
            if (double.TryParse(s.Trim(), System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out value))
            {
                return value;
            }
            return 0.0;
        }

        #endregion

        #region NodeTree

        public class NodeData
        {
            public string Code { get; set; }
            public string Text { get; set; }
            public int Level { get; set; }
            public string ParentCode { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
        }

        /// <summary>
        /// Парсер NodeTree.
        /// Формат: NODE|Code|Text|Level|ParentCode|X|Y
        /// </summary>
        public static List<NodeData> ParseNodeTree(string text)
        {
            var nodes = new List<NodeData>();

            if (string.IsNullOrWhiteSpace(text))
                return nodes;

            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
                    continue;

                if (!line.StartsWith("NODE", StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] parts = line.Split('|');
                if (parts.Length < 5)
                    continue;

                int level = 0;
                int.TryParse(parts[3].Trim(), out level);

                nodes.Add(new NodeData
                {
                    Code = parts[1].Trim(),
                    Text = parts[2].Trim(),
                    Level = level,
                    ParentCode = parts.Length >= 5 && !string.IsNullOrWhiteSpace(parts[4]) ? parts[4].Trim() : null,
                    X = parts.Length >= 6 ? ParseDouble(parts[5]) : 0,
                    Y = parts.Length >= 7 ? ParseDouble(parts[6]) : 0
                });
            }

            return nodes;
        }

        #endregion

        #region ERD

        /// <summary>
        /// Парсинг ERD-описания в модель ERDDiagram.
        /// Синтаксис:
        ///   TYPE ERD
        ///   TABLE Имя
        ///     FIELD Имя Тип [PK] [FK ЦелеваяСущность.Поле]
        ///   END
        ///   REL ИмяСущности.Поле -> ИмяСущности.Поле
        /// </summary>
        public static ERDDiagram ParseERD(string text)
        {
            var diagram = new ERDDiagram();
            if (string.IsNullOrWhiteSpace(text))
                return diagram;

            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            ERDEntity currentEntity = null;
            int entityIndex = 0;

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith("TYPE:", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (line.StartsWith("//") || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("TABLE", StringComparison.OrdinalIgnoreCase))
                {
                    string name = line.Substring("TABLE".Length).Trim();
                    if (string.IsNullOrWhiteSpace(name))
                        name = "Table" + (entityIndex + 1);

                    currentEntity = new ERDEntity
                    {
                        Id = "E" + entityIndex,
                        Name = name
                    };

                    diagram.Entities.Add(currentEntity);
                    entityIndex++;
                    continue;
                }

                if (string.Equals(line, "END", StringComparison.OrdinalIgnoreCase))
                {
                    currentEntity = null;
                    continue;
                }

                if (line.StartsWith("FIELD", StringComparison.OrdinalIgnoreCase) && currentEntity != null)
                {
                    string body = line.Substring("FIELD".Length).Trim();
                    if (string.IsNullOrWhiteSpace(body))
                        continue;

                    string fieldName = string.Empty;
                    string fieldType = string.Empty;
                    bool isPk = false;
                    bool isFk = false;
                    string fkEntity = null;
                    string fkField = null;

                    bool hasPK = body.Contains(" PK");
                    bool hasFK = body.Contains(" FK");

                    string mainPart = body;

                    if (hasFK)
                    {
                        isFk = true;
                        int fkIndex = body.IndexOf(" FK");
                        mainPart = body.Substring(0, fkIndex).Trim();

                        string fkPart = body.Substring(fkIndex + 3).Trim();
                        if (!string.IsNullOrWhiteSpace(fkPart))
                            fkEntity = fkPart;
                    }

                    if (hasPK)
                    {
                        isPk = true;
                        mainPart = mainPart.Replace(" PK", "").Trim();
                    }

                    string[] tokens = mainPart.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length >= 1)
                    {
                        fieldName = tokens[0];
                        if (tokens.Length >= 2)
                            fieldType = tokens[1];
                    }

                    var field = new ERDField
                    {
                        Name = fieldName,
                        Type = fieldType,
                        IsPrimaryKey = isPk,
                        IsForeignKey = isFk,
                        ForeignEntityName = fkEntity,
                        ForeignFieldName = fkField,
                        RowIndex = currentEntity.Fields.Count
                    };

                    currentEntity.Fields.Add(field);
                    continue;
                }

                if (line.StartsWith("REL", StringComparison.OrdinalIgnoreCase))
                {
                    string body = line.Substring("REL".Length).Trim();

                    string[] relParts = body.Split(new[] { "->" }, StringSplitOptions.RemoveEmptyEntries);
                    if (relParts.Length != 2)
                        continue;

                    string left = relParts[0].Trim();
                    string right = relParts[1].Trim();

                    string fromEntityName = null;
                    string fromFieldName = null;
                    string toEntityName = null;
                    string toFieldName = null;

                    foreach (var ent in diagram.Entities)
                    {
                        if (left.StartsWith(ent.Name + ".", StringComparison.OrdinalIgnoreCase))
                        {
                            fromEntityName = ent.Name;
                            fromFieldName = left.Substring(ent.Name.Length + 1);
                            break;
                        }
                    }

                    foreach (var ent in diagram.Entities)
                    {
                        if (right.StartsWith(ent.Name + ".", StringComparison.OrdinalIgnoreCase))
                        {
                            toEntityName = ent.Name;
                            toFieldName = right.Substring(ent.Name.Length + 1);
                            break;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(fromEntityName) ||
                        string.IsNullOrWhiteSpace(fromFieldName) ||
                        string.IsNullOrWhiteSpace(toEntityName) ||
                        string.IsNullOrWhiteSpace(toFieldName))
                        continue;

                    var rel = new ERDRelationship
                    {
                        Id = "R" + diagram.Relationships.Count,
                        FromEntityId = FindEntityIdByName(diagram, fromEntityName),
                        FromFieldName = fromFieldName,
                        ToEntityId = FindEntityIdByName(diagram, toEntityName),
                        ToFieldName = toFieldName
                    };

                    if (!string.IsNullOrEmpty(rel.FromEntityId) && !string.IsNullOrEmpty(rel.ToEntityId))
                        diagram.Relationships.Add(rel);
                }
            }

            return diagram;
        }

        private static string FindEntityIdByName(ERDDiagram diagram, string name)
        {
            if (diagram == null || diagram.Entities == null)
                return null;

            foreach (ERDEntity e in diagram.Entities)
            {
                if (string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
                    return e.Id;
            }

            return null;
        }

        #endregion

        #region IDEF3

        public class IDEF3UowData
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

        public class IDEF3JunctionData
        {
            public string Id { get; set; }
            public string Type { get; set; }
        }

        public class IDEF3LinkData
        {
            public string From { get; set; }
            public string To { get; set; }
        }

        /// <summary>
        /// Парсер IDEF3.
        /// Формат:
        ///   UOW|id|name|x|y|width|height
        ///   JUNCTION|id|type|x|y
        ///   LINK|from|to|
        /// </summary>
        public static Tuple<List<IDEF3UowData>, List<IDEF3JunctionData>, List<IDEF3LinkData>> ParseIDEF3(string text)
        {
            var uows = new List<IDEF3UowData>();
            var junctions = new List<IDEF3JunctionData>();
            var links = new List<IDEF3LinkData>();

            if (string.IsNullOrWhiteSpace(text))
                return Tuple.Create(uows, junctions, links);

            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
                    continue;

                if (line.StartsWith("UOW", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Split('|');
                    if (parts.Length >= 2)
                    {
                        uows.Add(new IDEF3UowData
                        {
                            Id = parts[1].Trim(),
                            Name = parts.Length >= 3 ? parts[2].Trim() : parts[1].Trim()
                        });
                    }
                }
                else if (line.StartsWith("JUNCTION", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Split('|');
                    if (parts.Length >= 3)
                    {
                        junctions.Add(new IDEF3JunctionData
                        {
                            Id = parts[1].Trim(),
                            Type = parts[2].Trim()
                        });
                    }
                }
                else if (line.StartsWith("LINK", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Split('|');
                    if (parts.Length >= 3)
                    {
                        links.Add(new IDEF3LinkData
                        {
                            From = parts[1].Trim(),
                            To = parts[2].Trim()
                        });
                    }
                }
            }

            return Tuple.Create(uows, junctions, links);
        }

        #endregion

        #region DFD

        /// <summary>
        /// Парсит текст DFD в DFDDiagram.
        /// Формат:
        ///   ENTITY|id|name|x|y
        ///   PROCESS|id|name|x|y
        ///   STORE|id|name|x|y
        ///   DOCFLOW|id|name|x|y
        ///   ARROW|from|to|label
        /// </summary>
        public static DFDDiagram ParseDFD(string text)
        {
            var diagram = new DFDDiagram();

            if (string.IsNullOrWhiteSpace(text))
                return diagram;

            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
                    continue;

                string[] parts = line.Split('|');
                if (parts.Length < 2)
                    continue;

                string type = parts[0].ToUpperInvariant();
                double x, y;

                if ((type == "ENTITY" || type == "PROCESS" || type == "STORE" || type == "DOCFLOW") &&
                    parts.Length >= 5 &&
                    double.TryParse(parts[3].Trim(), out x) &&
                    double.TryParse(parts[4].Trim(), out y))
                {
                    string id = parts[1].Trim();
                    string name = parts[2].Trim();

                    switch (type)
                    {
                        case "ENTITY":
                            diagram.Entities.Add(new DFDEntity { Id = id, Name = name, X = x, Y = y });
                            break;
                        case "PROCESS":
                            diagram.Processes.Add(new DFDProcess { Id = id, Name = name, X = x, Y = y });
                            break;
                        case "STORE":
                            diagram.Stores.Add(new DFDStore { Id = id, Name = name, X = x, Y = y });
                            break;
                        case "DOCFLOW":
                            if (diagram.DocFlows == null) diagram.DocFlows = new List<DFDDocFlow>();
                            diagram.DocFlows.Add(new DFDDocFlow { Id = id, Name = name, X = x, Y = y });
                            break;
                    }
                }
                else if (type == "ARROW" && parts.Length >= 3)
                {
                    diagram.Arrows.Add(new DFDArrow
                    {
                        FromId = parts[1].Trim(),
                        ToId = parts[2].Trim(),
                        Label = parts.Length >= 4 ? parts[3].Trim() : string.Empty
                    });
                }
            }

            return diagram;
        }

        #endregion

        #region DocumentFlow (схема документооборота)

        /// <summary>
        /// #TYPE:DOCFLOW
        /// PROCESS|id|name|x|y|width
        /// ENTITY|id|name|x|y|width
        /// DOCFLOW|id|name|x|y|width
        /// ARROW|from|to|label
        /// </summary>
        public static DocumentFlowDiagram ParseDocumentFlow(string text)
        {
            var diagram = new DocumentFlowDiagram();
            if (string.IsNullOrWhiteSpace(text)) return diagram;

            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
                    continue;

                string[] parts = line.Split('|');
                if (parts.Length < 2) continue;

                string type = parts[0].Trim().ToUpperInvariant();

                if (type == "PROCESS" && parts.Length >= 5)
                {
                    var proc = new DocFlowProcess
                    {
                        Id = parts[1].Trim(),
                        Name = parts[2].Trim(),
                        X = ParseDouble(parts[3]),
                        Y = ParseDouble(parts[4])
                    };
                    if (parts.Length >= 6) proc.Width = ParseDouble(parts[5]);
                    diagram.Processes.Add(proc);
                }
                else if (type == "ENTITY" && parts.Length >= 5)
                {
                    var entity = new DocFlowEntity
                    {
                        Id = parts[1].Trim(),
                        Name = parts[2].Trim(),
                        X = ParseDouble(parts[3]),
                        Y = ParseDouble(parts[4])
                    };
                    if (parts.Length >= 6) entity.Width = ParseDouble(parts[5]);
                    diagram.Entities.Add(entity);
                }
                else if (type == "DOCFLOW" && parts.Length >= 5)
                {
                    var doc = new DocFlowDocument
                    {
                        Id = parts[1].Trim(),
                        Name = parts[2].Trim(),
                        X = ParseDouble(parts[3]),
                        Y = ParseDouble(parts[4])
                    };
                    if (parts.Length >= 6) doc.Width = ParseDouble(parts[5]);
                    diagram.Documents.Add(doc);
                }
                else if (type == "ARROW" && parts.Length >= 3)
                {
                    diagram.Arrows.Add(new DocFlowArrow
                    {
                        FromId = parts[1].Trim(),
                        ToId = parts[2].Trim(),
                        Label = parts.Length >= 4 ? parts[3].Trim() : string.Empty
                    });
                }
            }

            return diagram;
        }

        #endregion

        #region UseCase

        /// <summary>
        /// #TYPE:USECASE
        /// ACTOR|id|name|x|y
        /// USECASE|id|name|x|y|width|height
        /// LINK|fromId|toId|type
        /// </summary>
        public static UseCaseDiagram ParseUseCase(string text)
        {
            var diagram = new UseCaseDiagram();
            if (string.IsNullOrWhiteSpace(text)) return diagram;

            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            int linkIndex = 0;

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
                    continue;

                string[] parts = line.Split('|');
                if (parts.Length < 2) continue;

                string type = parts[0].Trim().ToUpperInvariant();

                if (type == "ACTOR" && parts.Length >= 5)
                {
                    diagram.Actors.Add(new UseCaseActor
                    {
                        Id = parts[1].Trim(),
                        Name = parts[2].Trim(),
                        X = ParseDouble(parts[3]),
                        Y = ParseDouble(parts[4])
                    });
                }
                else if (type == "USECASE" && parts.Length >= 5)
                {
                    var useCase = new UseCaseElement
                    {
                        Id = parts[1].Trim(),
                        Name = parts[2].Trim(),
                        X = ParseDouble(parts[3]),
                        Y = ParseDouble(parts[4])
                    };

                    if (parts.Length >= 6) useCase.Width = ParseDouble(parts[5]);
                    if (parts.Length >= 7) useCase.Height = ParseDouble(parts[6]);

                    diagram.UseCases.Add(useCase);
                }
                else if (type == "LINK" && parts.Length >= 4)
                {
                    string linkType = parts[3].Trim().ToLowerInvariant();
                    if (linkType != "association" && linkType != "include" && linkType != "extend")
                        linkType = "association";

                    diagram.Links.Add(new UseCaseLink
                    {
                        Id = "L" + linkIndex,
                        FromId = parts[1].Trim(),
                        ToId = parts[2].Trim(),
                        Type = linkType
                    });

                    linkIndex++;
                }
            }

            return diagram;
        }

        #endregion

        #region ComponentDiagram (+ workflow inside)

        /// <summary>
        /// Парсинг диаграммы компонентов в модель ComponentDiagram.
        ///
        /// #TYPE:COMPONENTDIAGRAM (может быть в тексте, игнорируется)
        ///
        /// COMPONENT|id|name|x|y|width|height
        /// DATABASE|id|name|x|y|width|height
        /// LINK|fromId|toId|label
        ///
        /// Workflow внутри этого же типа:
        /// STARTEND|id|text|x|y|width|height
        /// PROCESS|id|text|x|y|width|height
        /// DATA|id|text|x|y|width|height
        /// FORM|id|text|x|y|width|height
        /// WFDATABASE|id|text|x|y|width|height
        ///
        /// width/height опциональны (если не заданы — остаются значения по умолчанию из модели).
        /// </summary>
        public static ComponentDiagram ParseComponentDiagram(string text)
        {
            var diagram = new ComponentDiagram();
            if (string.IsNullOrWhiteSpace(text))
                return diagram;

            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
                    continue;

                if (line.StartsWith("TYPE:", StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] parts = line.Split('|');
                if (parts.Length < 1)
                    continue;

                string type = parts[0].Trim().ToUpperInvariant();

                if (type == "COMPONENT")
                {
                    if (parts.Length < 5) continue;

                    var node = new ComponentNode
                    {
                        Id = parts[1].Trim(),
                        Name = parts[2].Trim(),
                        X = ParseDouble(parts[3]),
                        Y = ParseDouble(parts[4])
                    };

                    if (parts.Length >= 6) node.Width = ParseDouble(parts[5]);
                    if (parts.Length >= 7) node.Height = ParseDouble(parts[6]);

                    diagram.Components.Add(node);
                }
                else if (type == "DATABASE")
                {
                    if (parts.Length < 5) continue;

                    var db = new DatabaseNode
                    {
                        Id = parts[1].Trim(),
                        Name = parts[2].Trim(),
                        X = ParseDouble(parts[3]),
                        Y = ParseDouble(parts[4])
                    };

                    if (parts.Length >= 6) db.Width = ParseDouble(parts[5]);
                    if (parts.Length >= 7) db.Height = ParseDouble(parts[6]);

                    diagram.Databases.Add(db);
                }
                else if (type == "LINK")
                {
                    if (parts.Length < 3) continue;

                    diagram.Links.Add(new ComponentLink
                    {
                        FromId = parts[1].Trim(),
                        ToId = parts[2].Trim(),
                        Label = parts.Length >= 4 ? parts[3].Trim() : string.Empty
                    });
                }
                else if (type == "STARTEND" || type == "PROCESS" || type == "DATA" || type == "FORM" || type == "WFDATABASE")
                {
                    if (parts.Length < 5) continue;

                    WorkflowNodeType nodeType = WorkflowNodeType.Process;

                    if (type == "STARTEND") nodeType = WorkflowNodeType.StartEnd;
                    else if (type == "PROCESS") nodeType = WorkflowNodeType.Process;
                    else if (type == "DATA") nodeType = WorkflowNodeType.Data;
                    else if (type == "FORM") nodeType = WorkflowNodeType.Form;
                    else if (type == "WFDATABASE") nodeType = WorkflowNodeType.Database;

                    var wf = new WorkflowNode
                    {
                        Id = parts[1].Trim(),
                        Text = parts[2].Trim(),
                        Type = nodeType,
                        X = ParseDouble(parts[3]),
                        Y = ParseDouble(parts[4])
                    };

                    if (parts.Length >= 6) wf.Width = ParseDouble(parts[5]);
                    if (parts.Length >= 7) wf.Height = ParseDouble(parts[6]);

                    diagram.WorkflowNodes.Add(wf);
                }
            }

            return diagram;
        }

        #endregion
    }
}
