using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace OcppSimulator;

public sealed class DualLogger : IDisposable
{
    private readonly StreamWriter _fileWriter;
    private readonly object _sync = new();
    private bool _disposed;
    private bool _logToFile = true;

    public event Action<string>? MessageLogged;

    public DualLogger(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _fileWriter = new StreamWriter(File.Open(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true,
        };
    }

    public void Info(string message) => Write(message);

    public void Error(string message) => Write(message);

    public void Error(Exception exception, string message) => Write($"{message} {exception}");

    public bool IsFileLoggingEnabled => Volatile.Read(ref _logToFile);

    public void SetFileLoggingEnabled(bool enabled)
    {
        Volatile.Write(ref _logToFile, enabled);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_sync)
        {
            _fileWriter.Dispose();
        }

        _disposed = true;
    }

    private void Write(string message)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var formatted = $"{timestamp} - {message}";

        lock (_sync)
        {
            Console.WriteLine(formatted);
            if (Volatile.Read(ref _logToFile))
            {
                _fileWriter.WriteLine(formatted);
            }
        }

        MessageLogged?.Invoke(formatted);
    }
}
