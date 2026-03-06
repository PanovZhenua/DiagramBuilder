namespace DiagramBuilder.Models
{
    // Типы поддерживаемых диаграмм
    public enum DiagramType
    {
        IDEF0,      // Функциональная модель (текущая)
        NodeTree,   // Дерево узлов
        FEO,        // For Exposition Only
        IDEF3,      // Процессная модель
        DFD         // Потоки данных
    }
}
