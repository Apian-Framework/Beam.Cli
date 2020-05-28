﻿using System;
using System.Linq;
using System.Threading;
using CommandLine;
using BeamBackend;
using UniLog;

namespace BeamCli
{
    class Program
    {
        public class CliOptions
        {
            [Option(
	            Default = null,
	            HelpText = "Join this game. Else create a game")]
            public string GameId {get; set;}

            [Option(
	            Default = -1,
	            HelpText = "(persistent) Start with this GameMode")]
            public int StartMode {get; set;}

            [Option(
	            Default = null,
	            HelpText = "Local Bike Control Type (ai, player)")]
            public string BikeCtrl {get; set;}

            [Option(
	            Default = null,
	            HelpText = "User settings basename (Default: beamsettings)")]
            public string Settings {get; set;}

            [Option(
	            Default = false,
	            HelpText = "Force default user settings (other than CLI options")]
            public bool ForceDefaultSettings {get; set;}

            [Option(
	            Default = null,
	            HelpText = "(Default: Warn) Default log level.")]
            public string DefLogLvl {get; set;}

            [Option(
	            Default = false,
	            HelpText = "Raise exception on Unilog error")]
            public bool ThrowOnError {get; set;}
        }

        protected static BeamUserSettings GetSettings(string[] args)
        {
            BeamUserSettings settings = UserSettingsMgr.Load();

            Parser.Default.ParseArguments<CliOptions>(args)
                    .WithParsed<CliOptions>(o =>
                    {
                        if (o.Settings != null)
                            settings = UserSettingsMgr.Load(o.Settings);

                        if (o.ForceDefaultSettings)
                            settings = BeamUserSettings.CreateDefault();

                        if (o.ThrowOnError)
                            UniLogger.DefaultThrowOnError = true;

                        if (o.DefLogLvl != null)
                            settings.defaultLogLevel = o.DefLogLvl;

                        if (o.GameId != null)
                            settings.tempSettings["gameId"] = o.GameId;

                       if (o.StartMode != -1)
                            settings.startMode = o.StartMode;

                        // TODO: would rather have the frontend implmentation determine this somehow
                        if (o.BikeCtrl != null)
                            settings.localPlayerCtrlType = o.BikeCtrl;

                    });

            UserSettingsMgr.Save(settings);
            return settings;
        }

        static void Main(string[] args)
        {
            BeamUserSettings settings = GetSettings(args);
            UniLogger.DefaultLevel = UniLogger.LevelFromName(settings.defaultLogLevel);
            UniLogger.SetupLevels(settings.logLevels);
            CliDriver drv = new CliDriver();
            drv.Run(settings);
        }
    }


    class CliDriver
    {
        public long targetFrameMs {get; private set;} = 16;

        public BeamCore core = null;

        public BeamCliFrontend fe = null;
        public BeamGameNet bgn = null;

        public void Run(BeamUserSettings settings) {
            Init(settings);
            LoopUntilDone();
        }

        protected void Init(BeamUserSettings settings)
        {
            fe = new BeamCliFrontend(settings);
            bgn = new BeamGameNet(); // TODO: config/settings?
            core = new BeamCore(bgn, fe);
            core.Start(settings.startMode);
        }

        protected void LoopUntilDone()
        {
            bool keepRunning = true;
            long frameStartMs = _TimeMs() - targetFrameMs;;
            while (keepRunning)
            {
                long prevFrameStartMs = frameStartMs;
                frameStartMs = _TimeMs();

                // call loop
                keepRunning = Loop((int)(frameStartMs - prevFrameStartMs));
                long elapsedMs = _TimeMs() - frameStartMs;

                // wait to maintain desired rate
                int waitMs = (int)(targetFrameMs - elapsedMs);
                //UnityEngine.Debug.Log(string.Format("Elapsed ms: {0}, Wait ms: {1}",elapsedMs, waitMs));
                if (waitMs <= 0)
                    waitMs = 1;
                Thread.Sleep(waitMs);
            }
        }

        protected bool Loop(int frameMs)
        {
            // first dispatch incoming messages
            // while not self.netCmdQueue.empty():
            //     cmd = self.netCmdQueue.get(block=False)
            //     if cmd:
            //         self._dispatch_net_cmd(cmd)
            //         ge_sleep(0)  # yield


            // while not self.feMsgQueue.empty():
            //     cmd = self.feMsgQueue.get(block=False)
            //     if cmd:
            //         self._dispatch_fe_cmd(cmd)
            //         ge_sleep(0)  # yield

            // then update the game

            float frameSecs = (float)frameMs / 1000f;
            bgn.Loop();
            fe.Loop(frameSecs);
            return core.Loop(frameSecs);
        }

        private long _TimeMs() =>  DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

    }
}

