//#define SINGLE_THREADED
using System;
using System.Collections.Generic;
using Apian;
using BeamGameCode;
using UniLog;
using static UniLog.UniLogger; // for SID()

#if !SINGLE_THREADED
using System.Threading.Tasks;
#endif

namespace BeamCli
{

    public class BeamCliFrontend : IBeamFrontend
    {
        public  Dictionary<string, FrontendBike> feBikes;
        public IBeamApplication beamAppl {get; private set;}
        public IBeamAppCore appCore {get; private set;}
        protected BeamUserSettings userSettings;
        public UniLogger logger;

        private long prevGameTime;
        private int msUntilNetReady; // non-zero if waiting

        Dictionary<int, Action<BeamGameMode, object>> modeStartActions;
        Dictionary<int, Action<BeamGameMode, object>> modeEndActions;

        // Start is called before the first frame update
        public BeamCliFrontend(BeamUserSettings startupSettings)
        {
            feBikes = new Dictionary<string, FrontendBike>();
            userSettings = startupSettings;
            logger = UniLogger.GetLogger("Frontend");
            SetupModeActions();
        }

        public void SetBeamApplication(IBeamApplication appl)
        {
            beamAppl = appl;
        }

        public void SetAppCore(IBeamAppCore core)
        {
            appCore = core;
            if (core == null)
                return;

            OnNewCoreState(null, new NewCoreStateEventArgs(core.CoreState)); // initialize

            core.NewCoreStateEvt += OnNewCoreState;
            core.PlayerJoinedEvt += OnPlayerJoinedEvt;
            core.PlayerMissingEvt += OnPlayerMissingEvt;
            core.PlayerReturnedEvt += OnPlayerReturnedEvt;
            core.PlayersClearedEvt += OnPlayersClearedEvt;
            core.NewBikeEvt += OnNewBikeEvt;
            core.BikeRemovedEvt += OnBikeRemovedEvt;
            core.BikesClearedEvt +=OnBikesClearedEvt;
            core.PlaceClaimedEvt += OnPlaceClaimedEvt;
            core.PlaceHitEvt += OnPlaceHitEvt;

            core.ReadyToPlayEvt += OnReadyToPlay;
        }


        public virtual void Loop(float frameSecs)
        {

            if (msUntilNetReady > 0)
            {
                msUntilNetReady -= (int)(frameSecs * 1000f);
                if (msUntilNetReady <= 0)
                {
                    logger.Info($"Network ready. Proceeding...");
                    msUntilNetReady = 0;
                    beamAppl.OnPushModeReq(BeamModeFactory.kNetPlay, null);
                }
            }


            if (appCore == null)
                return;

            long curGameTime = appCore.CurrentRunningGameTime;
            int frameMs = (int)(curGameTime - prevGameTime);
            prevGameTime = curGameTime;

            if (frameMs > 0 && prevGameTime > 0) // not first loop
            {
                foreach( FrontendBike bike in feBikes.Values)
                {
                    bike.Loop(curGameTime, frameMs);
                }
            }
        }

        //
        // IBeamFrontend API
        //

        //
        // Backend game modes
        //
        protected void SetupModeActions()
        {
            modeStartActions = new Dictionary<int, Action<BeamGameMode, object>>()
            {
                { BeamModeFactory.kSplash, OnStartSplash},
                { BeamModeFactory.kPractice, OnStartPractice},
                { BeamModeFactory.kNetwork, OnStartNetworkMode},
                { BeamModeFactory.kNetPlay, OnStartNetPlay},
            };

            modeEndActions = new Dictionary<int, Action<BeamGameMode, object>>()
            {
                { BeamModeFactory.kSplash, OnEndSplash},
                { BeamModeFactory.kPractice, OnEndPractice},
                { BeamModeFactory.kNetwork, OnEndNetworkMode},
                { BeamModeFactory.kNetPlay, OnEndNetPlay},
            };
        }

        public void OnStartMode(BeamGameMode mode, object param) => modeStartActions[mode.ModeId()](mode, param);
        public void OnEndMode(BeamGameMode mode, object param) => modeEndActions[mode.ModeId()](mode, param);

        protected void OnStartSplash(BeamGameMode mode, object param)
        {
            ((ModeSplash)mode).FeTargetCameraEvt += OnTargetCamera;
        }
        protected void OnEndSplash(BeamGameMode mode, object param)
        {
            ((ModeSplash)mode).FeTargetCameraEvt -= OnTargetCamera;
        }
        protected void OnStartPractice(BeamGameMode mode, object param) {}
        protected void OnEndPractice(BeamGameMode mode, object param) {}

        protected void OnStartNetworkMode(BeamGameMode mode, object param)
        {
            logger.Info($"OnStartNetworkMode(): Listening for network events");
            beamAppl.GameAnnounceEvt += OnGameAnnounceEvt;
            beamAppl.PeerJoinedEvt += OnPeerJoinedNetEvt;
            beamAppl.PeerLeftEvt += OnPeerLeftNetEvt;
        }
        protected void OnEndNetworkMode(BeamGameMode mode, object param)
        {
            logger.Info($"OnEndNetworkMode(): No longer listening for network events");
            beamAppl.GameAnnounceEvt -= OnGameAnnounceEvt;
            beamAppl.PeerJoinedEvt -= OnPeerJoinedNetEvt;
            beamAppl.PeerLeftEvt -= OnPeerLeftNetEvt;
        }

       protected void OnStartNetPlay(BeamGameMode mode, object param) {}
        protected void OnEndNetPlay(BeamGameMode mode, object param) {}


        protected void OnTargetCamera(object sender, StringEventArgs args)
        {
            logger.Info($"OnTargetCamera(): Setting camera to {SID(args.str)}");
        }


        //


        public void DisplayMessage(MessageSeverity lvl, string msgText)
        {
            // Seems like an enum.ToString() is the name of the enum? So this isn't needed,
            // string lvlStr = (lvl == MessageSeverity.Info) ? "Info"
            //     : (lvl == MessageSeverity.Warning) ? "Warning"
            //         : "Error";

            Console.WriteLine($"{lvl}: {msgText}");

            // TODO: should there be a separate HandleUnrecoverableError()
            // API so things like console apps can exit gracfeully? Having it
            // implmented in the FE is a good thing - but it feels a little hokey
            // hanging it onto a DisplayMessage() method
            if (lvl == MessageSeverity.Error)
            {
                beamAppl.ExitApplication();
            }

        }

        public void UpdateNetworkInfo()
        {
            BeamNetInfo netInfo = beamAppl.NetInfo;
            Console.WriteLine($"** Network Info: Name: {netInfo.NetName}, Peers: {netInfo.PeerCount}, Games: {netInfo.GameCount}");
        }

        public void OnNetworkReady()
        {
            if (msUntilNetReady == 0)
            {
                logger.Info($"OnNetworkReady(): waiting 5000ms...");
                msUntilNetReady = 5000; // TODO: define somewhere
            }
        }

        public BeamUserSettings GetUserSettings() => userSettings;

        private void OnNewCoreState(object sender, NewCoreStateEventArgs csArgs)
        {
            BeamCoreState newCoreState = csArgs.coreState as BeamCoreState;
            newCoreState.PlaceFreedEvt += OnPlaceFreedEvt;
            newCoreState.PlacesClearedEvt += OnPlacesClearedEvt;
        }


        // Game code calls with a list of the currently existing games
        // Since this is the CLI app, we mostly ignore that and fetch the "gameName" cli parameter
        // and use that (in a gui App we'd display the list + have a way for the player to
        // enter params for a new game)

        // In the general GUI app case this is an async frontend gui thing
        // and ends with the frontend setting the result for a passed-in TaskCompletionResult

        // In THIS case, we just return (but have to await something to be async)
#if !SINGLE_THREADED
        public async Task<GameSelectedEventArgs> SelectGameAsync(IDictionary<string, BeamGameAnnounceData> existingGames)
        {
            // gameName cli param can end in:
            //  '+' = means join the game if it exists, create if not
            //  '*' = means create if it oes not exist. Error if it's already there
            //  '' = "nothing" means join if it's there, or error
            string gameName = null;
            GameSelectedEventArgs.ReturnCode result;
            BeamGameInfo gameInfo;

            string argStr;
            if (userSettings.tempSettings.TryGetValue("gameName", out argStr))
            {
                string groupType;
                if (userSettings.tempSettings.TryGetValue("groupType", out groupType))
                {
                    if (!BeamApianFactory.ApianGroupTypes.Contains(groupType))
                        throw new Exception($"Unknown Group Type: {groupType}.");
                    logger.Warn($"Requested group type: {groupType}");
                } else {
                    groupType = CreatorSezGroupManager.kGroupType;
                }


                gameName = argStr.TrimEnd( new [] {'+','*'} );
                result =  (argStr.EndsWith("*")) || (argStr.EndsWith("+") && ! existingGames.Keys.Contains(gameName)) ? GameSelectedEventArgs.ReturnCode.kCreate
                    : GameSelectedEventArgs.ReturnCode.kJoin;

                // TODO: does the frontend have any busniess selecting an agreement type?
                // Hmm. Actually, it kinda does: a user might well want to choose from a set of them.
                gameInfo = existingGames.Keys.Contains(gameName) ? existingGames[gameName].GameInfo
                    : beamAppl.beamGameNet.CreateBeamGameInfo(gameName, groupType);

                logger.Info($"Selected Game: {gameInfo.GameName} MaxPlayers: {gameInfo.MaxPlayers}");
            }
            else
                throw new Exception($"gameName setting missing.");

            await Task.Delay(0); // Yuk, But usually this is an async UI operation
            return new GameSelectedEventArgs(gameInfo, result);
        }
#endif
        public void SelectGame(IDictionary<string, BeamGameAnnounceData> existingGames)
        {
            // gameName cli param can end in:
            //  '+' = means join the game if it exists, create if not
            //  '*' = means create if it oes not exist. Error if it's already there
            //  '' = "nothing" means join if it's there, or error
            string gameName = null;
            GameSelectedEventArgs.ReturnCode result;
            BeamGameInfo gameInfo;

            string groupType;
            if (userSettings.tempSettings.TryGetValue("groupType", out groupType))
            {
                if (!BeamApianFactory.ApianGroupTypes.Contains(groupType))
                    throw new Exception($"Unknown Group Type: {groupType}.");
                logger.Warn($"Requested group type: {groupType}");
            } else {
                groupType = CreatorSezGroupManager.kGroupType;
            }

            string argStr;
            if (userSettings.tempSettings.TryGetValue("gameName", out argStr))
            {
                gameName = argStr.TrimEnd( new [] {'+','*'} );
                result =  (argStr.EndsWith("*")) || (argStr.EndsWith("+") && ! existingGames.Keys.Contains(gameName)) ? GameSelectedEventArgs.ReturnCode.kCreate
                    : GameSelectedEventArgs.ReturnCode.kJoin;

                // TODO: does the frontend have any busniess selecting an agreement type?
                // Hmm. Actually, it kinda does: a user might well want to choose from a set of them.
                gameInfo = existingGames.Keys.Contains(gameName) ? existingGames[gameName].GameInfo
                    : beamAppl.beamGameNet.CreateBeamGameInfo(gameName, groupType);
            }
            else
                throw new Exception($"gameName setting missing.");

            // Info about BeamGameInfo creation lives in BeamGameNet

            beamAppl.OnGameSelected( gameInfo, result );
        }

        //
        // Event handlers
        //

        // Network information events

        public void OnPeerJoinedNetEvt(object sender, PeerJoinedEventArgs args)
        {
            BeamNetworkPeer p = args.peer;
            logger.Info($"OnPeerJoinedEvt() name: {p.Name}, Id: {SID(p.PeerId)}");
            UpdateNetworkInfo();
        }

        public void OnPeerLeftNetEvt(object sender, PeerLeftEventArgs args)
        {
            logger.Info($"OnPeerLeftEvt(): {SID(args.p2pId)}");
            UpdateNetworkInfo();
        }

        public void OnGameAnnounceEvt(object sender, GameAnnounceEventArgs args)
        {
            UpdateNetworkInfo();
        }

        // Players

        public void OnPlayerJoinedEvt(object sender, PlayerJoinedEventArgs args)
        {
            // Player joined means a group has been joined AND is synced (ready to go)
            if ( args.player.PeerId == appCore.LocalPeerId )
            {
                 logger.Info($"*** Successfully joined Apian group: {args.groupChannel}");
            }
        }

        public void OnPlayerMissingEvt(object sender, PlayerLeftEventArgs args)
        {
            logger.Info($"*** Player {SID(args.p2pId)} is MISSING!!! from group {args.groupChannel}");
        }
        public void OnPlayerReturnedEvt(object sender, PlayerLeftEventArgs args)
        {
            logger.Info($"*** Player {SID(args.p2pId)} has RETURNED!!! to group {args.groupChannel}");
        }

        public void OnPlayersClearedEvt(object sender, EventArgs e)
        {
            // Probably never will do anything
            logger.Verbose("OnClearPeers() currently does nothing");
        }

        // Bikes
        public void OnNewBikeEvt(object sender, BikeEventArgs args)
        {
            IBike ib = args?.ib;
            logger.Info($"OnNewBikeEvt(). Id: {SID(ib.bikeId)}, Local: {ib.peerId == appCore.LocalPeerId}, AI: {ib.ctrlType == BikeFactory.AiCtrl}");
            FrontendBike b = FeBikeFactory.Create(ib, ib.peerId == appCore.LocalPeerId);
            b.Setup(ib, beamAppl, appCore);
            feBikes[ib.bikeId] = b;
        }
        public void OnBikeRemovedEvt(object sender, BikeRemovedEventArgs rData)
        {
            logger.Info($"OnBikeRemovedEvt({(rData.doExplode ? "Boom!" : "(poof)")}). Id: {SID(rData.bikeId)}");
            feBikes.Remove(rData.bikeId);
        }
        public void OnBikesClearedEvt(object sender, EventArgs e)
        {
            logger.Verbose(string.Format("OnBikesClearedEvt()"));
		    feBikes.Clear();
        }


        // Places

        public void OnPlaceHitEvt(object sender, PlaceHitEventArgs args)
        {
            // This intentionally accesses linked data to show issues
            // IBike bike = args.ib;
            // string bikeOwner = bike.peerId;

            // BeamPlace place = args.p;
            // IBike createdBy = place.bike;  // TODO: CRASH happens here. When no longer useful make it bulletproof
            //                                // (would rather make it not happen - not sure if that's possible)
            // string placeOwner = createdBy.peerId;

            logger.Info($"OnPlaceHitEvt. Place: {args.p?.GetPos().ToString()}  Bike: {SID(args.ib?.bikeId)}");
        }

        public void OnPlaceClaimedEvt(object sender, BeamPlaceEventArgs args)
        {
            BeamPlace p = args?.p;
            logger.Verbose($"OnPlaceClaimedEvt. Pos: {p?.GetPos().ToString()} Bike: {SID(p.bike.bikeId)}");
        }

        // Ground

        public void OnPlaceFreedEvt(object sender, BeamPlaceEventArgs args)
        {
            BeamPlace p = args?.p;
            logger.Debug($"OnFreePlace({p.xIdx}, {p.zIdx})");
        }

        public void OnPlacesClearedEvt(object sender, EventArgs e)
        {
           logger.Debug($"OnClearPlaces()");
        }

        public void OnReadyToPlay(object sender, EventArgs e)
        {
            logger.Error($"OnReadyToPlay() - doesn't work anymore");
            //logger.Info($"OnReadyToPlay()");
            //&&& backend.OnSwitchModeReq(BeamModeFactory.kPlay, null);
        }

    }

    public class IntBeamCliFrontend : BeamCliFrontend
    {
        public IntBeamCliFrontend(BeamUserSettings startupSettings) : base(startupSettings)
        {

        }

        public override void Loop(float frameSecs)
        {
            base.Loop(frameSecs);
        }

    }

}
