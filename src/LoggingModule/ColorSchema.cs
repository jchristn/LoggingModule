namespace SyslogLogging
{
    using System;

    /// <summary>
    /// Colors to use when writing to the console.
    /// </summary>
    public class ColorSchema
    {
        private ColorScheme _Debug = new ColorScheme(ConsoleColor.DarkGray, ConsoleColor.Black);
        private ColorScheme _Info = new ColorScheme(ConsoleColor.Gray, ConsoleColor.Black);
        private ColorScheme _Warn = new ColorScheme(ConsoleColor.DarkRed, ConsoleColor.Black);
        private ColorScheme _Error = new ColorScheme(ConsoleColor.Red, ConsoleColor.Black);
        private ColorScheme _Alert = new ColorScheme(ConsoleColor.DarkYellow, ConsoleColor.Black);
        private ColorScheme _Critical = new ColorScheme(ConsoleColor.Yellow, ConsoleColor.Black);
        private ColorScheme _Emergency = new ColorScheme(ConsoleColor.White, ConsoleColor.Red);

        /// <summary>
        /// The color to use for debug messages. Default is dark gray on black.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
        public ColorScheme Debug
        {
            get { return _Debug; }
            set { _Debug = value ?? throw new ArgumentNullException(nameof(Debug)); }
        }

        /// <summary>
        /// The color to use for informational messages. Default is gray on black.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
        public ColorScheme Info
        {
            get { return _Info; }
            set { _Info = value ?? throw new ArgumentNullException(nameof(Info)); }
        }

        /// <summary>
        /// The color to use for warning messages. Default is dark red on black.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
        public ColorScheme Warn
        {
            get { return _Warn; }
            set { _Warn = value ?? throw new ArgumentNullException(nameof(Warn)); }
        }

        /// <summary>
        /// The color to use for error messages. Default is red on black.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
        public ColorScheme Error
        {
            get { return _Error; }
            set { _Error = value ?? throw new ArgumentNullException(nameof(Error)); }
        }

        /// <summary>
        /// The color to use for alert messages. Default is dark yellow on black.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
        public ColorScheme Alert
        {
            get { return _Alert; }
            set { _Alert = value ?? throw new ArgumentNullException(nameof(Alert)); }
        }

        /// <summary>
        /// The color to use for critical messages. Default is yellow on black.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
        public ColorScheme Critical
        {
            get { return _Critical; }
            set { _Critical = value ?? throw new ArgumentNullException(nameof(Critical)); }
        }

        /// <summary>
        /// The color to use for emergency messages. Default is white on red.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
        public ColorScheme Emergency
        {
            get { return _Emergency; }
            set { _Emergency = value ?? throw new ArgumentNullException(nameof(Emergency)); }
        }

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public ColorSchema()
        {

        }
    }
}