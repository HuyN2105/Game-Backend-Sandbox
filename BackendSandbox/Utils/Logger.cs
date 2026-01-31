using System;

namespace BackendSandbox.Utils
{
    public static class Logger
    {
        // Equivalent to std::mutex
        private static readonly object Lock = new object();

        // Helper to emulate C++ fmt's {:^9} (center alignment)
        private static string CenterString(string text, int width)
        {
            if (string.IsNullOrEmpty(text))
                return new string(' ', width);

            int totalPadding = width - text.Length;
            if (totalPadding <= 0)
                return text;

            int padLeft = totalPadding / 2;
            return text.PadLeft(padLeft + text.Length).PadRight(width);
        }

        // Internal generic log function
        private static void Log(ConsoleColor color, string level, string format, params object[] args)
        {
            // Equivalent to std::lock_guard<std::mutex>
            lock (Lock)
            {
                // Set color for the tag
                Console.ForegroundColor = color;

                // Print Level (Centered like {:^9})
                Console.Write($"[{CenterString(level, 9)}] ");

                // Reset color for the message
                Console.ResetColor();

                // Print the formatted message
                // This handles "{0}", "{1}" style formatting automatically
                if (args.Length > 0)
                {
                    Console.WriteLine(format, args);
                }
                else
                {
                    Console.WriteLine(format);
                }
            }
        }

        // Public API

        public static void Info(string format, params object[] args)
        {
            Log(ConsoleColor.Cyan, "INFO", format, args);
        }

        public static void Success(string format, params object[] args)
        {
            Log(ConsoleColor.Green, "SUCCESS", format, args);
        }

        public static void Warn(string format, params object[] args)
        {
            Log(ConsoleColor.Yellow, "WARN", format, args);
        }

        public static void Error(string format, params object[] args)
        {
            Log(ConsoleColor.Red, "ERROR", format, args);
        }
    }
}