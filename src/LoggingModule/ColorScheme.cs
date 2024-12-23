namespace SyslogLogging
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Colors to use when writing to the console.
    /// </summary>
    public class ColorSchema
    {
        private ConsoleColor currentForeground = Console.ForegroundColor;

        /// <summary>
        /// The color to use for debug messages.  Default is dark gray on black.
        /// </summary>
        public ColorScheme Debug = new ColorScheme(ConsoleColor.DarkGray, ConsoleColor.Black);

        /// <summary>
        /// The color to use for informational messages.  Default is gray on black.
        /// </summary>
        public ColorScheme Info = new ColorScheme(ConsoleColor.Gray, ConsoleColor.Black);

        /// <summary>
        /// The color to use for warning messages.  Default is dark red on black.
        /// </summary>
        public ColorScheme Warn = new ColorScheme(ConsoleColor.DarkRed, ConsoleColor.Black);

        /// <summary>
        /// The color to use for error messages.  Default is red on black.
        /// </summary>
        public ColorScheme Error = new ColorScheme(ConsoleColor.Red, ConsoleColor.Black);

        /// <summary>
        /// The color to use for alert messages.  Default is dark yellow on black.
        /// </summary>
        public ColorScheme Alert = new ColorScheme(ConsoleColor.DarkYellow, ConsoleColor.Black);

        /// <summary>
        /// The color to use for critical messages.  Default is yellow on black.
        /// </summary>
        public ColorScheme Critical = new ColorScheme(ConsoleColor.Yellow, ConsoleColor.Black);

        /// <summary>
        /// The color to use for emergency messages.  Default is white on red.
        /// </summary>
        public ColorScheme Emergency = new ColorScheme(ConsoleColor.White, ConsoleColor.Red);
    }

    /// <summary>
    /// Color scheme for logging messages.
    /// </summary>
    public class ColorScheme
    {
        /// <summary>
        /// Foreground color.
        /// </summary>
        public ConsoleColor Foreground = Console.ForegroundColor;

        /// <summary>
        /// Background color.
        /// </summary>
        public ConsoleColor Background = Console.BackgroundColor;

        /// <summary>
        /// Instantiates a new color scheme.
        /// </summary>
        /// <param name="foreground">Foreground color.</param>
        /// <param name="background">Background color.</param>
        public ColorScheme(ConsoleColor foreground, ConsoleColor background)
        {
            Foreground = foreground;
            Background = background;
        }
    }
}
