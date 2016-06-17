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
            var level = TryGetErrorLevel(logEvent.Level);
            // Level is set to "Off", so exit.
            if (level == null) return;

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
                client.CaptureMessage(sentryMessage, level.Value, extra: extras, tags: tags);
            }
            else if (logEvent.Exception != null)
            {
                var sentryMessage = new SentryMessage(logEvent.FormattedMessage);
                client.CaptureException(logEvent.Exception, extra: extras, level: level.Value, message: sentryMessage, tags: tags);
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

        internal static ErrorLevel? TryGetErrorLevel(LogLevel level)
        {
            if (level == null)
            {
                return null;
            }

            // For ordinals, see https://github.com/NLog/NLog/blob/master/src/NLog/LogLevel.cs
            switch (level.Ordinal)
            {
                case 0: // Trace
                case 1: // Debug
                    return ErrorLevel.Debug;
                case 2:
                    return ErrorLevel.Info;
                case 3:
                    return ErrorLevel.Warning;
                case 4:
                    return ErrorLevel.Error;
                case 5:
                    return ErrorLevel.Fatal;
                case 6: // Off
                    return null;
                default:
                    throw new Exception(string.Format("Unable to map NLog LogLevel of {0} to a Sentry ErrorLevel", level));
            }
        }
    }
}

