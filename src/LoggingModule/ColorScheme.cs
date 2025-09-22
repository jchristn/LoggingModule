namespace SyslogLogging
{
    using System;

    /// <summary>
    /// Color scheme for logging messages.
    /// </summary>
    public class ColorScheme
    {
        /// <summary>
        /// Foreground color. Must be a valid ConsoleColor value.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when value is not a valid ConsoleColor.</exception>
        public ConsoleColor Foreground
        {
            get { return _Foreground; }
            set
            {
                if (!Enum.IsDefined(typeof(ConsoleColor), value))
                    throw new ArgumentException($"Invalid console color: {value}", nameof(Foreground));
                _Foreground = value;
            }
        }

        /// <summary>
        /// Background color. Must be a valid ConsoleColor value.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when value is not a valid ConsoleColor.</exception>
        public ConsoleColor Background
        {
            get { return _Background; }
            set
            {
                if (!Enum.IsDefined(typeof(ConsoleColor), value))
                    throw new ArgumentException($"Invalid console color: {value}", nameof(Background));
                _Background = value;
            }
        }

        private ConsoleColor _Foreground = Console.ForegroundColor;
        private ConsoleColor _Background = Console.BackgroundColor;

        /// <summary>
        /// Instantiates a new color scheme with default colors.
        /// </summary>
        public ColorScheme()
        {

        }

        /// <summary>
        /// Instantiates a new color scheme.
        /// </summary>
        /// <param name="foreground">Foreground color.</param>
        /// <param name="background">Background color.</param>
        /// <exception cref="ArgumentException">Thrown when foreground or background is not a valid ConsoleColor.</exception>
        public ColorScheme(ConsoleColor foreground, ConsoleColor background)
        {
            Foreground = foreground;
            Background = background;
        }
    }
}