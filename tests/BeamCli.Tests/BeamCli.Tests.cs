using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Newtonsoft.Json;
using Moq;
using Moq.Protected;
using BeamCli;
using BeamGameCode;

namespace BeamCliTests
{
    [TestFixture]

    public class CliDriverTests
    {
        public class CliDriverWrapper : CliDriver
        {
            public void CallInit(BeamUserSettings bus )
            {
                base.Init(bus);
            }
        }

        [Test]
        public void CliDriver_ConstructorWorks()
        {
            CliDriver drv = new CliDriver();
            long tfms = drv.targetFrameMs;

            Assert.That(drv, Is.Not.Null);
            Assert.That(tfms, Is.EqualTo(16)); // default val
        }

        [Test]
        public void CliDriver_Init()
        {
            Mock<BeamUserSettings> settings = new Mock<BeamUserSettings>();

            CliDriverWrapper drvw = new CliDriverWrapper();
            drvw.CallInit(settings.Object);

            Assert.That(drvw.fe, Is.InstanceOf<BeamCliFrontend>());
            Assert.That(drvw.bgn, Is.InstanceOf<BeamGameNet>());
            Assert.That(drvw.appl, Is.InstanceOf<BeamApplication>());
        }


    }

}