namespace DiagramBuilder.Services.Export
{
    /// <summary>
    /// Результат экспорта
    /// </summary>
    public class ExportResult
    {
        public bool IsSuccess { get; private set; }
        public bool IsCancelled { get; private set; }
        public string Message { get; private set; }
        public string FilePath { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int ElementCount { get; private set; }

        public static ExportResult Success(string filePath, int width, int height, int elementCount)
        {
            return new ExportResult
            {
                IsSuccess = true,
                FilePath = filePath,
                Width = width,
                Height = height,
                ElementCount = elementCount,
                Message = $"Диаграмма успешно экспортирована!\n\n" +
                         $"Файл: {filePath}\n" +
                         $"Размер: {width}×{height} px\n" +
                         $"Элементов: {elementCount}"
            };
        }

        public static ExportResult Error(string message)
        {
            return new ExportResult
            {
                IsSuccess = false,
                Message = message
            };
        }

        public static ExportResult Cancelled()
        {
            return new ExportResult
            {
                IsCancelled = true,
                Message = "Экспорт отменён пользователем"
            };
        }
    }
}
