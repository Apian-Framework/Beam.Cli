using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Nito.AsyncEx;
using BeamGameCode;
using UniLog;

namespace BeamCli
{
    public static class Program
    {
        public class CliOptions
        {
            [Option(
	            Default = null,
	            HelpText = "Join this game. Else create a game")]
            public string GameName {get; set;}

            [Option(
	            Default = false,
	            HelpText = "Create temporary crypto acct")]
            public bool TempAcct {get; set;}

            [Option(
	            Default = null,
	            HelpText = "Apian Network name" )]
            public string NetName {get; set;}

            [Option(
	            Default = null,
	            HelpText = "Apian Group consensus mechanism (if creating)" )]
            public string GroupType {get; set;}


            [Option(
	            Default = null,
	            HelpText = "Start with this GameMode" )]
            public string StartMode {get; set;}

            [Option(
	            Default = null,
	            HelpText = "Local Bike Control Type (ai, player)")]
            public string BikeCtrl {get; set;}

            [Option(
	            Default = null,
	            HelpText = "User settings file basename (Default: beamsettings)")]
            public string Settings {get; set;}

            [Option(
	            Default = false,
	            HelpText = "Interactive CLI Frontend")]
            public bool Interactive {get; set;}

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

        public static BeamUserSettings GetSettings(string[] args)
        {
            BeamUserSettings settings;
            try {
                 settings = UserSettingsMgr.Load();
            } catch (UserSettingsException ex) {
                Console.WriteLine($"WARNING: {ex.Message}. Using default settings.");
                settings = BeamUserSettings.CreateDefault();
            }

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

                        if (o.TempAcct)
                            settings.tempSettings["tempAcct"] = "true";

                        if (o.Interactive)
                            settings.tempSettings["interactive"] = "true";

                        if (o.NetName != null)
                            settings.apianNetworkName = o.NetName;

                        if (o.GameName != null)
                            settings.tempSettings["gameName"] = o.GameName;

                        if (o.GroupType != null)
                            settings.tempSettings["groupType"] = o.GroupType;

                       if (o.StartMode != null)
                            settings.startMode = o.StartMode;

                        // TODO: would rather have the frontend implmentation determine this somehow
                        if (o.BikeCtrl != null)
                            settings.localPlayerCtrlType = o.BikeCtrl;

                    }).WithNotParsed(o =>
                    {
                        // --help, --version, or any error results in this getting called
                        settings = null;
                    });

            return settings;
        }


        public static int Main(string[] args)
        {
            try
            {
                return AsyncContext.Run(() => AsyncMain(args));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return -1;
            }
        }

        static async Task<int> AsyncMain(string[] args)
        {
            BeamUserSettings settings = GetSettings(args);
            if (settings != null)
            {
                // TODO: UniLogger settings loading and saving should all happen in the same place, rather
                // than some here, some in BeamUserSettings and some in UniLogger itself
                UniLogger.DefaultLevel = UniLogger.LevelFromName(settings.defaultLogLevel);
                UniLogger.SetupLevels(settings.logLevels);

                UserSettingsMgr.Save(settings);
                CliDriver drv = new CliDriver();
                return await drv.Run(settings);
            }
            return -1;
        }
    }


    public class CliDriver
    {
        public long targetFrameMs {get; private set;} = 16;

        public BeamApplication appl;

        public BeamCliFrontend fe;

        public BeamGameNet bgn;

        public async Task<int> Run(BeamUserSettings settings) {
            _Init(settings);
            return await LoopUntilDone();
        }


        protected void _Init(BeamUserSettings settings)
        {
            if ( settings.tempSettings.ContainsKey("interactive")  && settings.tempSettings["interactive"] == "true" )
            {
                string s = "Creating Interactive Frontend";
                Console.Write($"{s}");
                fe = new InteractiveBeamCliFE(settings);
            } else {
                fe = new BeamCliFrontend(settings);
            }

            bgn = new BeamGameNet(); // TODO: config/settings?
            appl = new BeamApplication(bgn, fe);
            appl.Start(settings.startMode);
        }

        protected async Task<int> LoopUntilDone()
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

                await Task.Delay(waitMs);
                //Thread.Sleep(waitMs);
            }
            return 0;
        }

        protected bool Loop(int frameMs)
        {
            float frameSecs = (float)frameMs / 1000f;
            bgn.Update(); // dispatches incoming messages
            bool keepRunning = appl.Loop(frameSecs); // Do game code loop
            fe.Loop(frameSecs); // update frontend
            return keepRunning;
        }

        private long _TimeMs() =>  DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

    }
}

