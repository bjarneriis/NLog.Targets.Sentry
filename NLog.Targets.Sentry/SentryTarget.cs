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
        private Dsn dsn;
        private readonly Lazy<IRavenClient> client;

        /// <summary>
        /// Map of NLog log levels to Raven/Sentry log levels
        /// </summary>
        protected static readonly IDictionary<LogLevel, ErrorLevel> LoggingLevelMap = new Dictionary<LogLevel, ErrorLevel>
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
            get { return dsn == null ? null : dsn.ToString(); }
            set { dsn = new Dsn(value); }
        }

        /// <summary>
        /// Determines whether events with no exceptions will be send to Sentry or not
        /// </summary>
        public bool IgnoreEventsWithNoException { get; set; }

        /// <summary>
        /// Determines whether event properites will be sent to sentry as Tags or not
        /// </summary>
        public bool SendLogEventInfoPropertiesAsTags { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public SentryTarget()
        {
            client = new Lazy<IRavenClient>(() => new RavenClient(dsn));
        }

        /// <summary>
        /// Internal constructor, used for unit-testing
        /// </summary>
        /// <param name="ravenClient">A <see cref="IRavenClient"/></param>
        internal SentryTarget(IRavenClient ravenClient) : this()
        {
            client = new Lazy<IRavenClient>(() => ravenClient);
        }

        /// <summary>
        /// Writes logging event to the log target.
        /// </summary>
        /// <param name="logEvent">Logging event to be written out.</param>
        protected override void Write(LogEventInfo logEvent)
        {
            try
            {
                var tags = SendLogEventInfoPropertiesAsTags
                    ? logEvent.Properties.ToDictionary(x => x.Key.ToString(), x => x.Value.ToString())
                    : null;

                var extras = SendLogEventInfoPropertiesAsTags
                    ? null
                    : logEvent.Properties.ToDictionary(x => x.Key.ToString(), x => x.Value.ToString());

                client.Value.Logger = logEvent.LoggerName;

                // If the log event did not contain an exception and we're not ignoring
                // those kinds of events then we'll send a "Message" to Sentry
                if (logEvent.Exception == null && !IgnoreEventsWithNoException)
                {
                    var sentryMessage = new SentryMessage(Layout.Render(logEvent));
                    client.Value.CaptureMessage(sentryMessage, LoggingLevelMap[logEvent.Level], extra: extras, tags: tags);
                }
                else if (logEvent.Exception != null)
                {
                    var sentryMessage = new SentryMessage(logEvent.FormattedMessage);
                    client.Value.CaptureException(logEvent.Exception, extra: extras, level: LoggingLevelMap[logEvent.Level], message: sentryMessage, tags: tags);
                }
            }
            catch (Exception e)
            {
                InternalLogger.Error("Unable to send Sentry request: {0}", e.Message);
            }
        }
    }
}

