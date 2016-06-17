using System;
using System.Collections.Generic;
using System.Linq;
using NLog.Config;
using NLog.Layouts;
using SharpRaven;
using SharpRaven.Data;

// ReSharper disable CheckNamespace
namespace NLog.Targets
// ReSharper restore CheckNamespace
{
    [Target("Sentry")]
    public class SentryTarget : Target
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
        [Obsolete("Use target filter conditions instead. See: https://github.com/NLog/NLog/wiki/Conditions")]
        public bool IgnoreEventsWithNoException { get; set; }

        /// <summary>
        /// Determines whether event properites will be sent to sentry as Tags or not
        /// </summary>
        public bool SendLogEventInfoPropertiesAsTags { get; set; }

        /// <summary>
        /// A comma separated list of NLog property names to be used as tags.
        /// </summary>
        public string TagProperties { get; set; }

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
            var sentryEvent = ToSentryEvent(logEvent);
            if (sentryEvent == null) return;
            var client = CreateClient(logEvent);
            client.Capture(sentryEvent);
        }

        private IRavenClient CreateClient(LogEventInfo logEvent)
        {
            var client = ravenClientFactory != null ? ravenClientFactory() : new RavenClient(new Dsn(Dsn));
            client.Logger = logEvent.LoggerName;
            return client;
        }

        private SentryEvent ToSentryEvent(LogEventInfo logEvent)
        {
            var level = TryGetErrorLevel(logEvent.Level);
            // Level is set to "Off", so exit.
            if (level == null)
            {
                return null;
            }

            var sentryEvent = CreateSentryEvent(logEvent);
            if (IgnoreEventsWithNoException && sentryEvent.Exception == null)
            {
                return null;
            }

            sentryEvent.Level = level.Value;
            AppendEventDetails(sentryEvent, logEvent.Properties);
            return sentryEvent;
        }

        private static SentryEvent CreateSentryEvent(LogEventInfo logEvent)
        {
            if (logEvent.Exception != null)
            {
                return new SentryEvent(logEvent.Exception);
            }
            else
            {
                return new SentryEvent(new SentryMessage(logEvent.FormattedMessage));
            }
        }

        private void AppendEventDetails(SentryEvent sentryEvent, IDictionary<object, object> properties)
        {
            var propertiesAsStrings = ConvertPropertiesToStrings(properties);
            if (SendLogEventInfoPropertiesAsTags)
            {
                foreach (var tag in propertiesAsStrings)
                {
                    sentryEvent.Tags.Add(tag);
                }
            }
            else
            {
                foreach (var tagPropertyKey in RenderTagProperties())
                {
                    string propertyValue;
                    if (propertiesAsStrings.TryGetValue(tagPropertyKey, out propertyValue))
                    {
                        sentryEvent.Tags.Add(tagPropertyKey, propertyValue);
                        propertiesAsStrings.Remove(tagPropertyKey);
                    }
                }

                sentryEvent.Extra = propertiesAsStrings;
            }
        }

        private IEnumerable<string> RenderTagProperties()
        {
            if (string.IsNullOrWhiteSpace(TagProperties))
            {
                return Enumerable.Empty<string>();
            }

            return new HashSet<string>(TagProperties.Split(',').Select(s => s.Trim()));
        }

        private static Dictionary<string, string> ConvertPropertiesToStrings(IDictionary<object, object> properties)
        {
            return (
                from property in properties
                let stringKey = ToStringOrNull(property.Key)
                let stringValue = ToStringOrNull(property.Value)
                where stringKey != null && stringValue != null
                group stringValue by stringKey)
                .ToDictionary(x => x.Key, x => string.Join(",", x));
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

