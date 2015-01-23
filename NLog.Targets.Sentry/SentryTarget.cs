using NLog.Config;
using SharpRaven;

namespace NLog.Targets.Sentry
{
    [Target("Sentry")]
    public class SentryTarget : Target
    {
        [RequiredParameter]
        public string Dsn { get; set; }

        protected override void Write(LogEventInfo logEvent)
        {
            var sentryClient = new RavenClient(Dsn);

            if (logEvent.Exception != null)
            {
                sentryClient.CaptureException(logEvent.Exception, logEvent.Message);
            }
            else
            {
                sentryClient.CaptureMessage(logEvent.Message);
            }
        }
    }
}