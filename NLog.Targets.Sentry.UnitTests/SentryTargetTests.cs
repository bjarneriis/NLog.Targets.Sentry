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
            var sentryTarget = new SentryTarget { Dsn = "http://localhost" };
            var configuration = new LoggingConfiguration();
            configuration.AddTarget("NLogSentry", sentryTarget);
            configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, sentryTarget));
            LogManager.Configuration = configuration;
            try
            {
                LogManager.GetCurrentClassLogger().Info("Test");
                Assert.Fail("Expected exception not raised");
            }
            catch (NLogRuntimeException ex)
            {
                Assert.IsInstanceOf<ArgumentException>(ex.InnerException);
            }
        }

        [Test]
        public void TestLoggingToSentry()
        {
            var sentryClient = new Mock<IRavenClient>();
            ErrorLevel lErrorLevel = ErrorLevel.Debug;
            IDictionary<string, string> lTags = null;
            Exception lException = null;

            sentryClient
                .Setup(x => x.CaptureException(It.IsAny<Exception>(), It.IsAny<SentryMessage>(), It.IsAny<ErrorLevel>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<object>()))
                .Callback((Exception exception, SentryMessage msg, ErrorLevel lvl, IDictionary<string, string> d, object extra) =>
                {
                    lException = exception;
                    lErrorLevel = lvl;
                    lTags = d;
                })
                .Returns("Done");

            // Setup NLog
            var sentryTarget = new SentryTarget(() => sentryClient.Object)
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
                logger.ErrorException("Error Message", e);
            }

            Assert.IsTrue(lException.Message == "Oh No!");
            Assert.IsTrue(lTags == null);
            Assert.IsTrue(lErrorLevel == ErrorLevel.Error);
        }




        [Test]
        public void TestLoggingToSentry_SendLogEventInfoPropertiesAsTags()
        {
            var sentryClient = new Mock<IRavenClient>();
            ErrorLevel lErrorLevel = ErrorLevel.Debug;
            IDictionary<string, string> lTags = null;
            Exception lException = null;

            sentryClient
                .Setup(x => x.CaptureException(It.IsAny<Exception>(), It.IsAny<SentryMessage>(), It.IsAny<ErrorLevel>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<object>()))
                .Callback((Exception exception, SentryMessage msg, ErrorLevel lvl, IDictionary<string, string> d, object extra) =>
                {
                    lException = exception;
                    lErrorLevel = lvl;
                    lTags = d;
                })
                .Returns("Done");

            // Setup NLog
            var sentryTarget = new SentryTarget(() => sentryClient.Object)
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

                var logEventInfo = LogEventInfo.Create(LogLevel.Error, "default", "Error Message", e);
                logEventInfo.Properties.Add("tag1", tag1Value);
                logger.Log(logEventInfo);
            }

            Assert.IsTrue(lException.Message == "Oh No!");
            Assert.IsTrue(lTags != null);
            Assert.IsTrue(lErrorLevel == ErrorLevel.Error);
        }

        [TestCase("Trace", 0, ErrorLevel.Debug)]
        [TestCase("Debug", 1, ErrorLevel.Debug)]
        [TestCase("Info",  2, ErrorLevel.Info)]
        [TestCase("Warn",  3, ErrorLevel.Warning)]
        [TestCase("Error", 4, ErrorLevel.Error)]
        [TestCase("Fatal", 5, ErrorLevel.Fatal)]
        [TestCase("Off",   6, null)]
        public void TestLevelMappings(string name, int ordinal, ErrorLevel? expectedErrorLevel)
        {
            var level = LogLevel.FromString(name);
            Assert.AreEqual(level, LogLevel.FromOrdinal(ordinal));
            var errorLevel = SentryTarget.TryGetErrorLevel(level);
            Assert.AreEqual(expectedErrorLevel, errorLevel);
        }
    }
}
