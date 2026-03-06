using System.Collections.Generic;
using System.Windows;

namespace DiagramBuilder.Models
{
    /// <summary>Поле сущности ERD.</summary>
    public class ERDField
    {
        public string Name { get; set; }
        public string Type { get; set; }

        /// <summary>Признак первичного ключа.</summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>Признак внешнего ключа.</summary>
        public bool IsForeignKey { get; set; }

        /// <summary>Имя сущности, на которую указывает FK.</summary>
        public string ForeignEntityName { get; set; }

        /// <summary>Имя поля в целевой сущности.</summary>
        public string ForeignFieldName { get; set; }

        /// <summary>Индекс строки внутри сущности (0‑based).</summary>
        public int RowIndex { get; set; }

        /// <summary>Высота строки в пикселях.</summary>
        public double RowHeight { get; set; } = 24.0;
    }

    /// <summary>ERD‑сущность (таблица).</summary>
    public class ERDEntity
    {
        /// <summary>Внутренний идентификатор (для связей).</summary>
        public string Id { get; set; }

        /// <summary>Отображаемое имя таблицы.</summary>
        public string Name { get; set; }

        public List<ERDField> Fields { get; set; } = new List<ERDField>();

        /// <summary>Координаты на Canvas.</summary>
        public double X { get; set; }
        public double Y { get; set; }

        /// <summary>Размеры блока.</summary>
        public double Width { get; set; }
        public double Height { get; set; }

        /// <summary>Высота заголовка.</summary>
        public double HeaderHeight { get; set; } = 30.0;

        /// <summary>Внутренние отступы.</summary>
        public Thickness Padding { get; set; } = new Thickness(8, 6, 8, 8);

        public ERDField GetPrimaryKeyField()
        {
            foreach (var field in Fields)
            {
                if (field.IsPrimaryKey)
                    return field;
            }
            return null;
        }

        public ERDField GetFieldByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            foreach (var field in Fields)
            {
                if (string.Equals(field.Name, name, System.StringComparison.OrdinalIgnoreCase))
                    return field;
            }

            return null;
        }
    }

    /// <summary>Связь PK → FK.</summary>
    public class ERDRelationship
    {
        public string Id { get; set; }

        public string FromEntityId { get; set; }
        public string FromFieldName { get; set; }

        public string ToEntityId { get; set; }
        public string ToFieldName { get; set; }
    }

    /// <summary>Полная ERD‑диаграмма.</summary>
    public class ERDDiagram
    {
        public List<ERDEntity> Entities { get; set; } = new List<ERDEntity>();
        public List<ERDRelationship> Relationships { get; set; } = new List<ERDRelationship>();
    }
}
