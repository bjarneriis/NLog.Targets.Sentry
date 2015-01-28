using NLog.Config;
using SharpRaven;

// ReSharper disable CheckNamespace
namespace NLog.Targets
// ReSharper restore CheckNamespace
{
    [Target("Sentry")]
    public class SentryTarget : TargetWithLayout
    {
        [RequiredParameter]
        public string Dsn { get; set; }

        protected override void Write(LogEventInfo logEvent)
        {
            RavenClient sentryClient = new RavenClient(Dsn);
            string message = Layout.Render(logEvent);

            if (logEvent.Exception != null)
            {
                sentryClient.CaptureException(logEvent.Exception, message);
            }
            else
            {
                sentryClient.CaptureMessage(message);
            }
        }
    }
}

