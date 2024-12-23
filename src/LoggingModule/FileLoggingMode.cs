namespace SyslogLogging
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Specify whether or not log messages should be appended to a file.
    /// Disabled: file logging will not be used.
    /// SingleLogFile: all messages will be appended to a single file.
    /// FileWithDate: all messages will be appended to a file, where the name of the file is the supplied filename followed by '.yyyyMMdd'.
    /// </summary>
    public enum FileLoggingMode
    {
        /// <summary>
        /// File logging will not be used.
        /// </summary>
        Disabled = 0,
        /// <summary>
        /// All messages will be appended to a single file.
        /// </summary>
        SingleLogFile = 1,
        /// <summary>
        /// All messages will be appended to a file, where the name of the file is the supplied filename followed by '.yyyyMMdd'.
        /// </summary>
        FileWithDate = 2
    } 
}
