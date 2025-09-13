using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerdanskGameBot.Ext
{
    public static class LoggingExtensions
    {
        public static IServiceCollection AddNLog(this IServiceCollection svc, LogFactory lf,
            string? prefix = null, Action<ILoggingBuilder>? configure = null)
        {
            svc.TryAddSingleton(lf);
            svc.TryAddSingleton(typeof(ILogger<>), typeof(PrefixedLogger<>));

            if (!string.IsNullOrWhiteSpace(prefix))
            {
                svc.TryAddSingleton<ILoggerFactory, PrefixedLoggerFactory>();
                svc.TryAddSingleton(new LoggerPrefix(prefix!));
            }

            if (configure is not null)
                configure(new PrefixedLoggingBuilder(svc));

            return svc;
        }
    }

    internal class PrefixedLoggerFactory(LogFactory nlogFactory, LoggerPrefix? prf) : ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() { }

        Microsoft.Extensions.Logging.ILogger ILoggerFactory.CreateLogger(string categoryName)
            => new PrefixedLogger(categoryName, nlogFactory, prf);
    }

    internal class PrefixedLogger<T>(LogFactory lf, LoggerPrefix? prf)
        : PrefixedLogger(typeof(T).FullName!, lf, prf), ILogger<T>
    { }

    internal class PrefixedLogger(string name, LogFactory lf, LoggerPrefix? prf)
        : Microsoft.Extensions.Logging.ILogger
    {
        public LoggerFilterOptions? LoggerFilterOptions { get; private set; }

        private readonly Logger _logger = lf.GetLogger(name);
        private readonly string _prefix = prf?.Prefix ?? string.Empty;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => _logger.PushScopeNested(state);

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
            => _logger?.IsEnabled(GetMELogLevel(logLevel)) ?? false;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = _prefix + formatter(state, exception);
            _logger?.Log(GetMELogLevel(logLevel), exception, message);
        }

        private static NLog.LogLevel GetMELogLevel(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            return logLevel switch
            {
                Microsoft.Extensions.Logging.LogLevel.Trace => NLog.LogLevel.Trace,
                Microsoft.Extensions.Logging.LogLevel.Debug => NLog.LogLevel.Debug,
                Microsoft.Extensions.Logging.LogLevel.Information => NLog.LogLevel.Info,
                Microsoft.Extensions.Logging.LogLevel.Warning => NLog.LogLevel.Warn,
                Microsoft.Extensions.Logging.LogLevel.Error => NLog.LogLevel.Error,
                Microsoft.Extensions.Logging.LogLevel.Critical => NLog.LogLevel.Fatal,
                _ => NLog.LogLevel.Off
            };
        }
    }

    internal class LoggerPrefix(string prefix)
    {
        public string Prefix => prefix;
    }

    internal class PrefixedLoggingBuilder(IServiceCollection services) : ILoggingBuilder
    {
        public IServiceCollection Services => services;
    }
}
