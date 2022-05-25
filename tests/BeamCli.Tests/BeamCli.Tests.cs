using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;
using NUnit.Framework;
using Newtonsoft.Json;
using Moq;
using Moq.Protected;
using UniLog;
using BeamGameCode;
using BeamCli;

namespace BeamCliTests
{
    [TestFixture]


    public class CliDriverTests
    {
        public class CliDriverWrapper : CliDriver
        {
            public void CallInit(BeamUserSettings bus )
            {
                base._Init(bus);
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
            BeamUserSettings settings = new BeamUserSettings();
            settings.startMode = "network"; // THis is really stupid and not really unit testing

            CliDriverWrapper drvw = new CliDriverWrapper();
            drvw.CallInit(settings);

            Assert.That(drvw.fe, Is.InstanceOf<BeamCliFrontend>());
            Assert.That(drvw.bgn, Is.InstanceOf<BeamGameNet>());
            Assert.That(drvw.appl, Is.InstanceOf<BeamApplication>());
            Assert.That(drvw.fe.beamAppl, Is.EqualTo(drvw.appl));
        }
    }

    [TestFixture]

    public class CliProgramTests
    {
        public object SettingByName(BeamUserSettings s, string name )
        {
           Type myType = typeof(BeamUserSettings);
           FieldInfo fi = myType.GetField(name);
           return fi.GetValue(s);
        }


        [TestCase("--startmode,network", "startMode", "network")]
        [TestCase("--bikectrl,ai", "localPlayerCtrlType", "ai")]
        [TestCase("--defloglvl,info", "defaultLogLevel", "info")]
        [TestCase("--throwonerror,true", "DefaultThrowOnError", true)]
        [TestCase("--gamename,bar+", "gameName", "bar+")]
        public void CliProgram_GetSettings(string argsString, string settingName, object val)
        {
            var args = argsString.Split(',');
            BeamUserSettings sets = Program.GetSettings(args);
            Assert.That(sets.version, Is.EqualTo(UserSettingsMgr.currentVersion));

            switch(settingName)
            {
            case "gameName":
                Assert.That(sets.tempSettings["gameName"], Is.EqualTo(val));
                break;
            case "DefaultThrowOnError":
                Assert.That(UniLogger.DefaultThrowOnError, Is.EqualTo(val));
                break;
            default:
                Assert.That(SettingByName(sets, settingName), Is.EqualTo(val));
                break;
            }

        }

    }

}