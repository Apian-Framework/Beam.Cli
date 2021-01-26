using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Newtonsoft.Json;
using Moq;
using BeamCli;

namespace BeamCliTests
{
    [TestFixture]
    public class CliDriverTests
    {
        [Test]
        public void CliDriver_ConstructorWorks()
        {
            CliDriver drv = new CliDriver();
            Assert.That(drv, Is.Not.Null);
        }
    }

}