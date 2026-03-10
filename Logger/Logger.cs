using DirectMusicConverter.Logger.Enums;
using System.Globalization;
using System.Text;

namespace DirectMusicConverter.Logger
{
    internal static class Logger
    {
        private static readonly object Sync = new();
        private static string? _logFilePath;

        internal static bool IsEnabled { get; set; } = true;

        internal static bool WriteToConsole { get; set; } = true;

        internal static bool WriteToFile { get; set; } = true;

        internal static LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

        internal static void Initialize(string logFilePath)
        {
            lock (Sync)
            {
                _logFilePath = logFilePath;

                string? directory = Path.GetDirectoryName(logFilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(logFilePath, string.Empty, Encoding.UTF8);
            }
        }

        internal static void Trace(string source, string message)
        {
            Write(LogLevel.Trace, source, message, null);
        }

        internal static void Debug(string source, string message)
        {
            Write(LogLevel.Debug, source, message, null);
        }

        internal static void Info(string source, string message)
        {
            Write(LogLevel.Info, source, message, null);
        }

        internal static void Warning(string source, string message)
        {
            Write(LogLevel.Warning, source, message, null);
        }

        internal static void Error(string source, string message)
        {
            Write(LogLevel.Error, source, message, null);
        }

        internal static void Error(string source, string message, Exception ex)
        {
            Write(LogLevel.Error, source, message, ex);
        }

        internal static string FormatPointer(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
            {
                return "0x00000000";
            }

            int width = IntPtr.Size == 8 ? 16 : 8;
            ulong value = unchecked((ulong)pointer.ToInt64());
            return "0x" + value.ToString("X" + width.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        internal static string FormatHResult(int hr)
        {
            return "0x" + hr.ToString("X8", CultureInfo.InvariantCulture) + " (" + hr.ToString(CultureInfo.InvariantCulture) + ")";
        }

        internal static string FormatGuid(Guid guid)
        {
            return guid.ToString("D").ToUpperInvariant();
        }

        internal static void DumpBytes(string source, string title, IntPtr buffer, int size)
        {
            if (!IsEnabled || MinimumLevel > LogLevel.Trace || buffer == IntPtr.Zero || size <= 0)
            {
                return;
            }

            byte[] data = new byte[size];
            System.Runtime.InteropServices.Marshal.Copy(buffer, data, 0, size);

            StringBuilder builder = new();
            builder.Append(title);
            builder.Append(" [");
            builder.Append(size);
            builder.Append(" bytes] = ");

            for (int i = 0; i < data.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(data[i].ToString("X2", CultureInfo.InvariantCulture));
            }

            Trace(source, builder.ToString());
        }

        private static void Write(LogLevel level, string source, string message, Exception? ex)
        {
            if (!IsEnabled || level < MinimumLevel)
            {
                return;
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            string line = "[" + timestamp + "] [" + level.ToString().ToUpperInvariant() + "] [" + source + "] " + message;

            if (ex != null)
            {
                line += Environment.NewLine + ex;
            }

            lock (Sync)
            {
                if (WriteToConsole)
                {
                    Console.WriteLine(line);
                }

                if (WriteToFile && !string.IsNullOrWhiteSpace(_logFilePath))
                {
                    File.AppendAllText(_logFilePath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
        }
    }
}