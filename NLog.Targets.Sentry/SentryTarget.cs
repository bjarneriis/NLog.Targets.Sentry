using System;
using System.Collections.Generic;
using System.Linq;
using NLog.Common;
using NLog.Config;
using SharpRaven;
using SharpRaven.Data;

// ReSharper disable CheckNamespace
namespace NLog.Targets
// ReSharper restore CheckNamespace
{
    [Target("Sentry")]
    public class SentryTarget : TargetWithLayout
    {
        private Dsn _dsn;
        private readonly Lazy<IRavenClient> _client;

        /// <summary>
        /// Map of NLog log levels to Raven/Sentry log levels
        /// </summary>
        protected static readonly IDictionary<LogLevel, ErrorLevel> LoggingLevelMap = new Dictionary<LogLevel, ErrorLevel>()
        {
            {LogLevel.Debug, ErrorLevel.Debug},
            {LogLevel.Error, ErrorLevel.Error},
            {LogLevel.Fatal, ErrorLevel.Fatal},
            {LogLevel.Info, ErrorLevel.Info},
            {LogLevel.Trace, ErrorLevel.Debug},
            {LogLevel.Warn, ErrorLevel.Warning},
        };

        /// <summary>
        /// The DSN for the Sentry host
        /// </summary>
        [RequiredParameter]
        public string Dsn
        {
            get { return _dsn == null ? null : _dsn.ToString(); }
            set { _dsn = new Dsn(value); }
        }

        /// <summary>
        /// Determins whether event messages will be captured as well as exceptions
        /// </summary>
        public bool CaptureMessages { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public SentryTarget()
        {
            _client = new Lazy<IRavenClient>(() => new RavenClient(_dsn));
        }

        /// <summary>
        /// Internal constructor, used for unit-testing
        /// </summary>
        /// <param name="ravenClient">A <see cref="IRavenClient"/></param>
        internal SentryTarget(IRavenClient ravenClient) : this()
        {
            _client = new Lazy<IRavenClient>(() => ravenClient);
        }

        /// <summary>
        /// Writes logging event to the log target.
        /// </summary>
        /// <param name="logEvent">Logging event to be written out.</param>
        protected override void Write(LogEventInfo logEvent)
        {
            if (logEvent.Level == LogLevel.Off) return;

            try
            {
                var extras = logEvent.Properties.ToDictionary(x => x.Key.ToString(), x => x.Value.ToString());
                _client.Value.Logger = logEvent.LoggerName;

                if (logEvent.Exception == null && CaptureMessages)
                {
                    var message = Layout.Render(logEvent);
                    if (string.IsNullOrEmpty(message)) return;

                    var sentryMessage = new SentryMessage(message);
                    _client.Value.CaptureMessage(sentryMessage, level: LoggingLevelMap[logEvent.Level], extra: extras);
                }
                else if (logEvent.Exception != null)
                {
                    var sentryMessage = new SentryMessage(logEvent.FormattedMessage);
                    _client.Value.CaptureException(logEvent.Exception, extra: extras, level: LoggingLevelMap[logEvent.Level], message: sentryMessage);
                }
            }
            catch (Exception e)
            {
                InternalLogger.Error("Unable to send Sentry request: {0}", e.Message);
            }
        }
    }
}

