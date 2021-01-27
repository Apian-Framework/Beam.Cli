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

    [TestFixture]

    public class CliProgramTests
    {
        public object SettingByName(BeamUserSettings s, string name )
        {
           Type myType = typeof(BeamUserSettings);
           FieldInfo fi = myType.GetField(name);
           return fi.GetValue(s);
        }

        public class WrappedProgram : Program
        {
            public static BeamUserSettings DoGetSettings(string[] args)
            {
                return GetSettings(args);
            }
        }



        [TestCase("--startmode,2", "startMode", 2)]
        [TestCase("--bikectrl,ai", "localPlayerCtrlType", "ai")]
        [TestCase("--defloglvl,info", "defaultLogLevel", "info")]
        [TestCase("--throwonerror,true", "DefaultThrowOnError", true)]
        [TestCase("--gamespec,foo/bar+", "GameSpec", "foo/bar+")]

        public void CliProgram_GetSettings(string argsString, string settingName, object val)
        {
            var args = argsString.Split(',');
            BeamUserSettings sets = WrappedProgram.DoGetSettings(args);
            Assert.That(sets.version, Is.EqualTo(UserSettingsMgr.currentVersion));

            switch(settingName)
            {
            case "GameSpec":
                Assert.That(sets.tempSettings["gameSpec"], Is.EqualTo(val));
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