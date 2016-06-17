using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly Func<IRavenClient> ravenClientFactory;

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
        public string Dsn { get; set; }

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
        }

        /// <summary>
        /// Internal constructor, used for unit-testing
        /// </summary>
        /// <param name="createRavenClient">Constructor of a <see cref="IRavenClient"/></param>
        internal SentryTarget(Func<IRavenClient> createRavenClient)
        {
            this.ravenClientFactory = createRavenClient;
        }

        /// <summary>
        /// Writes logging event to the log target.
        /// </summary>
        /// <param name="logEvent">Logging event to be written out.</param>
        protected override void Write(LogEventInfo logEvent)
        {
            var propertiesAsStrings = (
                from property in logEvent.Properties
                let stringKey = ToStringOrNull(property.Key)
                let stringValue = ToStringOrNull(property.Value)
                where stringKey != null && stringValue != null
                group stringValue by stringKey)
                .ToDictionary(x => x.Key, x => string.Join(",", x));

            var tags = SendLogEventInfoPropertiesAsTags
                ? propertiesAsStrings
                : null;

            var extras = SendLogEventInfoPropertiesAsTags
                ? null
                : propertiesAsStrings;

            var client = CreateClient(logEvent);
            // If the log event did not contain an exception and we're not ignoring
            // those kinds of events then we'll send a "Message" to Sentry
            if (logEvent.Exception == null && !IgnoreEventsWithNoException)
            {
                var sentryMessage = new SentryMessage(Layout.Render(logEvent));
                client.CaptureMessage(sentryMessage, LoggingLevelMap[logEvent.Level], extra: extras, tags: tags);
            }
            else if (logEvent.Exception != null)
            {
                var sentryMessage = new SentryMessage(logEvent.FormattedMessage);
                client.CaptureException(logEvent.Exception, extra: extras, level: LoggingLevelMap[logEvent.Level], message: sentryMessage, tags: tags);
            }
        }

        private IRavenClient CreateClient(LogEventInfo logEvent)
        {
            var client = ravenClientFactory != null ? ravenClientFactory() : new RavenClient(new Dsn(Dsn));
            client.Logger = logEvent.LoggerName;
            return client;
        }

        private static string ToStringOrNull(object obj)
        {
            if (obj == null)
            {
                return null;
            }

            return obj.ToString();
        }
    }
}

