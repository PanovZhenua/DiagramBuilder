using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DiagramBuilder.Services.Core
{
    public static class TextFormatterHelper
    {
        /// <summary>
        /// Подготавливает многострочный текст с автопереносом и поддержкой \n
        /// </summary>
        public static (double Height, List<string> Lines) CalculateTextSize(
            string text,
            double maxWidth,
            double minHeight = 48,
            double maxHeight = 160,
            double fontSize = 13,
            double lineHeightMultiplier = 1.4)
        {
            if (string.IsNullOrEmpty(text))
                return (minHeight, new List<string>());

            var lines = new List<string>();
            double lineHeight = fontSize * lineHeightMultiplier;

            // Обрабатываем пользовательские \n
            string[] userLines = text.Replace("\\r\\n", "\\n").Split(new[] { "\\n" }, StringSplitOptions.None);

            foreach (var line in userLines)
            {
                if (string.IsNullOrEmpty(line))
                {
                    lines.Add("");
                    continue;
                }

                // Простой перенос по длине (можно улучшить через TextBlock.Measure)
                int charPerLine = (int)(maxWidth / (fontSize * 0.6)); // примерная ширина символа
                int charsUsed = 0;

                while (charsUsed < line.Length)
                {
                    int charsToTake = Math.Min(charPerLine, line.Length - charsUsed);
                    lines.Add(line.Substring(charsUsed, charsToTake));
                    charsUsed += charsToTake;
                }
            }

            int lineCount = Math.Max(1, lines.Count);
            double neededHeight = lineCount * lineHeight + 16; // +padding
            double finalHeight = Math.Max(minHeight, Math.Min(neededHeight, maxHeight));

            return (finalHeight, lines);
        }

        /// <summary>
        /// Создаёт TextBlock с автопереносом и стилем
        /// </summary>
        public static TextBlock CreateAutoWrapTextBlock(
            string text,
            DiagramStyle style,
            double maxWidth = 160,
            double fontSize = 13,
            TextAlignment alignment = TextAlignment.Center)
        {
            return new TextBlock
            {
                Text = text?.Replace("\\n", "\n") ?? "",
                TextAlignment = alignment,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = fontSize,
                FontWeight = FontWeights.SemiBold,
                Foreground = style.Text,
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(8, 6, 8, 8),
                MaxWidth = maxWidth
            };
        }
    }
}
