namespace GoodByeDPI_GUI.Core.Formatters
{
    public static class ByteFormatting
    {
        public static string FormatBytes(long bytes)
        {
            if (bytes == 0) return "0 B";

            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            double number = bytes;

            while (number >= 1024 && counter < suffixes.Length - 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:F1} {suffixes[counter]}";
        }
    }
}