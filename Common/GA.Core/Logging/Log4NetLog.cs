namespace GA.Core.Logging;

using System.Reflection;
using System.Xml;
using log4net;
using log4net.Config;
using log4net.Repository.Hierarchy;

public class Log4NetLog
{
    private static bool _isConfigured;
    private readonly ILog _log;

    static Log4NetLog()
    {
        if (!_isConfigured)
        {
            Configure();
        }
    }

    /// <remarks>
    ///     See https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-3.0
    /// </remarks>
    [UsedImplicitly]
    public Log4NetLog(ILog log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    private static void Configure(string path = "log4net.config")
    {
        var log4NetConfig = new XmlDocument();
        if (log4NetConfig == null)
        {
            throw new ArgumentNullException(nameof(log4NetConfig));
        }

        if (File.Exists(path))
        {
            log4NetConfig.Load(File.OpenRead(path));
            var repo = LogManager.CreateRepository(
                Assembly.GetEntryAssembly() ?? throw new InvalidOperationException(),
                typeof(Hierarchy));
            XmlConfigurator.Configure(repo, log4NetConfig[nameof(log4net)] ?? throw new InvalidOperationException());
        }

        // Mark as configured
        _isConfigured = true;
    }

    public void Debug(string msg)
    {
        if (!_log.IsDebugEnabled)
        {
            return;
        }

        _log.Debug(msg);
    }

    public void Debug(string msg, Exception ex)
    {
        if (!_log.IsDebugEnabled)
        {
            return;
        }

        _log.Debug(msg, ex);
    }

    public void Info(string msg)
    {
        if (!_log.IsInfoEnabled)
        {
            return;
        }

        _log.Info(msg);
    }

    public void Info(string msg, Exception ex)
    {
        if (!_log.IsInfoEnabled)
        {
            return;
        }

        _log.Info(msg, ex);
    }

    public void Warn(string msg)
    {
        if (!_log.IsWarnEnabled)
        {
            return;
        }

        _log.Warn(msg);
    }

    public void Warn(string msg, Exception ex)
    {
        if (!_log.IsWarnEnabled)
        {
            return;
        }

        _log.Warn(msg, ex);
    }

    public void Error(string msg)
    {
        if (!_log.IsErrorEnabled)
        {
            return;
        }

        _log.Error(msg);
    }

    public void Error(string msg, Exception ex)
    {
        if (!_log.IsErrorEnabled)
        {
            return;
        }

        _log.Error(msg, ex);
    }

    public void Fatal(string msg)
    {
        if (!_log.IsFatalEnabled)
        {
            return;
        }

        _log.Fatal(msg);
    }

    public void Fatal(string msg, Exception ex)
    {
        if (!_log.IsFatalEnabled)
        {
            return;
        }

        _log.Fatal(msg, ex);
    }
}
