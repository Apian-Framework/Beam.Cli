using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Newtonsoft.Json;
using Moq;
using Moq.Protected;
using BeamCli;
using UniLog;
using BeamGameCode;
using System.Reflection;

namespace BeamCliTests
{
    [TestFixture]

    public class CliFrontendTests
    {

        [Test]
        public void CliFrontend_ConstructorWorks()
        {
            Mock<BeamUserSettings> settings = new Mock<BeamUserSettings>();

            BeamCliFrontend fe = new BeamCliFrontend(settings.Object);
            Assert.That(fe, Is.InstanceOf<BeamCliFrontend>());
        }

        [Test]
        public void CliFrontend_SetAppCore()
        {
            Mock<BeamUserSettings> mockSettings = new Mock<BeamUserSettings>();
            BeamAppCore realCore = new BeamAppCore();
            BeamCliFrontend fe = new BeamCliFrontend(mockSettings.Object);

            fe.SetAppCore(null);
            Assert.That(fe.appCore, Is.Null);
            fe.SetAppCore(realCore);
            Assert.That(fe.appCore, Is.EqualTo(realCore));
            // All there really is here to test is that the delegates have been attached to the events. ATM I dunno how.
            // (and am not completely sure how much trouble its worth)

        }


    }


}