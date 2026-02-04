namespace DiagramBuilder.Models
{
    // Типы поддерживаемых диаграмм
    public enum DiagramType
    {
        IDEF0,          // Функциональная модель (текущая)
        NodeTree,       // Дерево узлов
        FEO,            // For Exposition Only
        IDEF3,          // Процессная модель
        DFD,            // Потоки данных
        DocumentFlow,   // Документооборот
        ERD,            // Entity-Relationship Diagram
        UseCase         // Диаграмма вариантов использования
    }
}
