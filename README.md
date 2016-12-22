Sentry Target for NLog
======================

**NLog Sentry** is a custom target for [NLog](http://nlog-project.org/) enabling you to send logging messages to the [Sentry](http://getsentry.com) logging service.

## Configuration

To use the Sentry target, simply add it an extension in the NLog.config file and place the NLog.Targets.Sentry.dll in the same location as the NLog.dll & NLog.config files.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">

  <extensions>
    <add assembly="NLog.Targets.Sentry" />
  </extensions>

  <targets async="true">
    <target name="Sentry" type="Sentry" dsn="<your sentry dsn>"/>
  </targets>

  <rules>
    <logger name="*"  appendTo="Sentry" minLevel="Error"/>
  </rules>
</nlog>
```
The package is also available through NuGet as "NLog.Targets.Sentry".
