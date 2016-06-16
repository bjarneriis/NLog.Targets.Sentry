using System;
using System.Collections.Generic;
using Moq;
using NLog.Config;
using NUnit.Framework;
using SharpRaven;
using SharpRaven.Data;

namespace NLog.Targets.Sentry.UnitTests
{
    [TestFixture]
    class SentryTargetTests
    {
        [SetUp]
        public void Setup()
        {
            LogManager.ThrowExceptions = true;
        }

        [TearDown]
        public void Teardown()
        {
            LogManager.ThrowExceptions = false;
        }

        [Test]
        public void TestPublicConstructor()
        {
            Assert.DoesNotThrow(() => new SentryTarget());
            Assert.Throws<NLogConfigurationException>(() =>
            {
                var sentryTarget = new SentryTarget();
                var configuration = new LoggingConfiguration();
                configuration.AddTarget("NLogSentry", sentryTarget);
                configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, sentryTarget));
                LogManager.Configuration = configuration;
            });
        }

        [Test]
        public void TestBadDsn()
        {
            Assert.Throws<ArgumentException>(() => new SentryTarget(null) { Dsn = "http://localhost" });
        }

        [Test]
        public void TestLoggingToSentry()
        {
            var sentryClient = new Mock<IRavenClient>();
            ErrorLevel lErrorLevel = ErrorLevel.Debug;
            IDictionary<string, string> lTags = null;
            Exception lException = null;

            sentryClient
                .Setup(x => x.Capture(It.IsAny<SentryEvent>()))
                .Callback((SentryEvent sentryEvent) =>
                {
                    lException = sentryEvent.Exception;
                    lErrorLevel = sentryEvent.Level;
                    lTags = sentryEvent.Tags;
                })
                .Returns("Done");

            // Setup NLog
            var sentryTarget = new SentryTarget(sentryClient.Object)
            {
                Dsn = "http://25e27038b1df4930b93c96c170d95527:d87ac60bb07b4be8908845b23e914dae@test/4",
            };
            var configuration = new LoggingConfiguration();
            configuration.AddTarget("NLogSentry", sentryTarget);
            configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, sentryTarget));
            LogManager.Configuration = configuration;

            try
            {
                throw new Exception("Oh No!");
            }
            catch (Exception e)
            {
                var logger = LogManager.GetCurrentClassLogger();
                logger.Error(e, "Error Message");
            }

            Assert.AreEqual(lException.Message, "Oh No!");
            Assert.IsEmpty(lTags);
            Assert.AreEqual(lErrorLevel, ErrorLevel.Error);
        }




        [Test]
        public void TestLoggingToSentry_SendLogEventInfoPropertiesAsTags()
        {
            var sentryClient = new Mock<IRavenClient>();
            ErrorLevel lErrorLevel = ErrorLevel.Debug;
            IDictionary<string, string> lTags = null;
            Exception lException = null;

            sentryClient
                .Setup(x => x.Capture(It.IsAny<SentryEvent>()))
                .Callback((SentryEvent sentryEvent) =>
                {
                    lException = sentryEvent.Exception;
                    lErrorLevel = sentryEvent.Level;
                    lTags = sentryEvent.Tags;
                })
                .Returns("Done");

            // Setup NLog
            var sentryTarget = new SentryTarget(sentryClient.Object)
            {
                Dsn = "http://25e27038b1df4930b93c96c170d95527:d87ac60bb07b4be8908845b23e914dae@test/4",
                SendLogEventInfoPropertiesAsTags = true,
            };
            var configuration = new LoggingConfiguration();
            configuration.AddTarget("NLogSentry", sentryTarget);
            configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, sentryTarget));
            LogManager.Configuration = configuration;

            var tag1Value = "abcde";

            try
            {
                throw new Exception("Oh No!");
            }
            catch (Exception e)
            {
                var logger = LogManager.GetCurrentClassLogger();

                var logEventInfo = LogEventInfo.Create(LogLevel.Error, "default", e, message: "Error Message", formatProvider: null);
                logEventInfo.Properties.Add("tag1", tag1Value);
                logger.Log(logEventInfo);
            }

            Assert.AreEqual(lException.Message, "Oh No!");
            Assert.AreEqual(lTags["tag1"], tag1Value);
            Assert.AreEqual(lErrorLevel, ErrorLevel.Error);
        }
    }
}
