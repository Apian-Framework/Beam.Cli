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
            BeamUserSettings settings = BeamUserSettings.CreateDefault();
            BeamCliFrontend fe = new BeamCliFrontend(settings);
            Assert.That(fe, Is.InstanceOf<BeamCliFrontend>());
        }

        [Test]
        public void CliFrontend_SetAppCore()
        {
            BeamUserSettings settings = BeamUserSettings.CreateDefault();
            BeamAppCore realCore = new BeamAppCore("apianSessionId");
            BeamCliFrontend fe = new BeamCliFrontend(settings);

            fe.SetAppCore(null);
            Assert.That(fe.appCore, Is.Null);
            fe.SetAppCore(realCore);
            Assert.That(fe.appCore, Is.EqualTo(realCore));
            // All there really is here to test is that the delegates have been attached to the events. ATM I dunno how.
            // (and am not completely sure how much trouble its worth)

        }


    }


}