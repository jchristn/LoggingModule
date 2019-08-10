using System;
using System.Collections.Generic;
using System.Text;

namespace SyslogLogging
{
    /// <summary>
    /// Colors to use when writing to the console.
    /// </summary>
    public class ColorSchema
    {
        ConsoleColor currentForeground = Console.ForegroundColor;
        public ColorScheme Debug = new ColorScheme(ConsoleColor.DarkGray, ConsoleColor.Black);
        public ColorScheme Info = new ColorScheme(ConsoleColor.Gray, ConsoleColor.Black);
        public ColorScheme Warn = new ColorScheme(ConsoleColor.DarkRed, ConsoleColor.Black);
        public ColorScheme Error = new ColorScheme(ConsoleColor.Red, ConsoleColor.Black);
        public ColorScheme Alert = new ColorScheme(ConsoleColor.DarkYellow, ConsoleColor.Black);
        public ColorScheme Critical = new ColorScheme(ConsoleColor.Yellow, ConsoleColor.Black);
        public ColorScheme Emergency = new ColorScheme(ConsoleColor.White, ConsoleColor.Red);
    }

    public class ColorScheme
    {
        public ConsoleColor Foreground = Console.ForegroundColor;
        public ConsoleColor Background = Console.BackgroundColor;

        public ColorScheme(ConsoleColor foreground, ConsoleColor background)
        {
            Foreground = foreground;
            Background = background;
        }
    }
}
