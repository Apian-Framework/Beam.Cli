using System;
using System.Linq;
using System.Collections.Generic;
using P2pNet;
using Apian;
using BeamGameCode;
using UniLog;
using static UniLog.UniLogger; // for SID()

using System.Threading.Tasks;

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

        Dictionary<int, Action<BeamGameMode, object>> modeStartActions;
        Dictionary<int, Action<BeamGameMode, object>> modeEndActions;

        // Start is called before the first frame update
        public BeamCliFrontend(BeamUserSettings startupSettings)
        {
            feBikes = new Dictionary<string, FrontendBike>();
            userSettings = startupSettings;
            logger = UniLogger.GetLogger("Frontend");
            SetupModeActions();

            // FIXME: Old test for crypto signing. Just kept for reference. Needs to be deleted
            //
            // cryptoThing = EthForApian.Create();

            // if (string.IsNullOrEmpty(userSettings.cryptoAcctJSON))
            // {
            //     string addr =  cryptoThing.CreateAccount();
            //     string json = cryptoThing.GetJsonForAccount("password");
            //     DisplayMessage(MessageSeverity.Info, $"Created new Eth acct: {addr}");
            //     userSettings.cryptoAcctJSON = json;
            //     UserSettingsMgr.Save(userSettings);
            // } else {
            //     string addr = cryptoThing.CreateAccountFromJson("password", userSettings.cryptoAcctJSON);
            //     DisplayMessage(MessageSeverity.Info, $"Loaded Eth acct: {addr} from settings");
            // }

            // // Stupid temporary test
            // string msg = "Ya Ya! Ya Ya ya.";
            // string sig = cryptoThing.EncodeUTF8AndSign(msg);

            // DisplayMessage(MessageSeverity.Info, $"Message: {msg}");
            // DisplayMessage(MessageSeverity.Info, $"Signature: {sig}");

            // string recAddr = cryptoThing.EncodeUTF8AndEcRecover(msg, sig);
            // DisplayMessage(MessageSeverity.Info, $"Recovered addr: {recAddr}");

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

        private readonly Dictionary<ApianGroupMember.Status, string> statusNames = new Dictionary<ApianGroupMember.Status, string>{
            {ApianGroupMember.Status.New, "New"},
            {ApianGroupMember.Status.Joining, "Joining"},
            {ApianGroupMember.Status.SyncingState, "SyncingState"},
            {ApianGroupMember.Status.SyncingClock, "SyncingClock"},
            {ApianGroupMember.Status.Active, "Active"},
            {ApianGroupMember.Status.Gone, "Gone"}
        };

        public void OnGroupMemberStatus(string groupId, string peerAddr, ApianGroupMember.Status newStatus, ApianGroupMember.Status prevStatus)
        {
            if ( peerAddr == appCore?.LocalPlayerAddr )
            {
                Console.WriteLine( $">>> Local Peer is: \"{statusNames[newStatus]}\"");
            }

        }

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
        public void OnResumeMode(BeamGameMode mode, object param) {}
        public void OnPauseMode(BeamGameMode mode, object param) {}


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
            beamAppl.ChainIdEvt += OnChainIdEvt;
        }
        protected void OnEndNetworkMode(BeamGameMode mode, object param)
        {
            logger.Info($"OnEndNetworkMode(): No longer listening for network events");
            beamAppl.GameAnnounceEvt -= OnGameAnnounceEvt;
            beamAppl.PeerJoinedEvt -= OnPeerJoinedNetEvt;
            beamAppl.PeerLeftEvt -= OnPeerLeftNetEvt;
            beamAppl.ChainIdEvt -= OnChainIdEvt;
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

        protected void DisplayGame(BeamGameInfo info, BeamGameStatus status)
        {
            string gameId = $"{info.GameName}: {info.GroupType}";
            string memberInf = $"{status.MemberCount} ({info.MemberLimits.MinMembers}/{info.MemberLimits.MaxMembers})";
            string playerInf = $"{status.PlayerCount} ({info.MemberLimits.MinPlayers}/{info.MemberLimits.MaxPlayers}";
            string validatorInf = $"{status.ValidatorCount} ({info.MemberLimits.MinValidators}/{info.MemberLimits.MaxValidators})";
            Console.WriteLine($" {gameId}, Members: {memberInf}, Players: {playerInf}, Validators: {validatorInf}");
        }

        protected void DisplayPeer(BeamNetworkPeer peer)
        {
            PeerNetworkStats stats = beamAppl.beamGameNet.GetPeerNetStats(peer.PeerAddr);
            Console.WriteLine($"{SID(peer.PeerAddr)}: Lag: {stats?.NetLagMs}, Sigma: {stats?.NetLagSigma:F3}, LHF: {stats?.MsSinceLastHeardFrom}, Name: {peer.Name}");
        }


        public void UpdateNetworkInfo()
        {
            BeamNetInfo netInfo = beamAppl.NetInfo;

            netInfo.UpdateAllGamesStatus(beamAppl.beamGameNet);

            Console.WriteLine($"\n** Network Info: Name: {netInfo.NetName}, Peers: {netInfo.PeerCount}, Games: {netInfo.GameCount}");
            if (netInfo.PeerCount > 0)
            {
                Console.WriteLine($"Peers:");
                foreach (var peer in netInfo.BeamPeers.Values)
                    DisplayPeer(peer);
            }
            if (netInfo.GameCount > 0)
            {
                Console.WriteLine($"Games:");
                foreach (var game in netInfo.BeamGames.Values)
                    DisplayGame(game.GameInfo, game.GameStatus);
            }
            Console.WriteLine($"");
        }



        public void OnNetworkReady()
        {
            logger.Info($"OnNetworkReady(). Pushing ModeNetPlay");
            beamAppl.OnPushModeReq(BeamModeFactory.kNetPlay, null);
        }

        public BeamUserSettings GetUserSettings() => userSettings;

        private void OnNewCoreState(object sender, NewCoreStateEventArgs csArgs)
        {
            BeamCoreState newCoreState = csArgs.coreState as BeamCoreState;
            newCoreState.PlaceFreedEvt += OnPlaceFreedEvt;
            newCoreState.PlacesClearedEvt += OnPlacesClearedEvt;
            newCoreState.SquareAddEvt += OnSquareAddEvt;
            newCoreState.SquareDelEvt += OnSquareDelEvt;
        }


        // Game code calls with a list of the currently existing games
        // Since this is the CLI app, we mostly ignore that and fetch the "gameName" cli parameter
        // and use that (in a gui App we'd display the list + have a way for the player to
        // enter params for a new game)

        // In the general GUI app case this is an async frontend gui thing
        // and ends with the frontend setting the result for a passed-in TaskCompletionResult

        // In THIS case, we just return (but have to await something to be async)
        public async Task<GameSelectedEventArgs> SelectGameAsync(IDictionary<string, BeamGameAnnounceData> existingGames)
        {
            // gameName cli param can end in:
            //  '+' = means join the game if it exists, create if not
            //  '*' = means create if it oes not exist. Error if it's already there
            //  '' = "nothing" means join if it's there, or error
            string gameName = null;
            GameSelectedEventArgs.ReturnCode result;
            BeamGameInfo gameInfo;

            // FIXME: There's a pile of copypasta shared between this method and:
            //   public void SelectGame(IDictionary<string, BeamGameAnnounceData> existingGames)
            // Make it go away

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
                result =  (argStr.EndsWith("*")) || (argStr.EndsWith("+") && ! existingGames.ContainsKey(gameName)) ? GameSelectedEventArgs.ReturnCode.kCreate
                    : GameSelectedEventArgs.ReturnCode.kJoin;


                // Note that this is only used if the group is being created.
                string anchorAddr = null; // default to no anchor
                string anchorAlgo = ApianGroupInfo.AnchorPostsNone; // default to no posting
                if ( result == GameSelectedEventArgs.ReturnCode.kCreate )
                {
                    if (userSettings.tempSettings.TryGetValue("anchorAlgo", out anchorAlgo))
                    {
                        anchorAddr = userSettings.anchorContractAddr;
                        logger.Warn($"Requested anchor posting algorithm: {anchorAlgo}");
                    }
                }

                // TODO: does the frontend have any busniess selecting an agreement type?
                // Hmm. Actually, it kinda does: a user might well want to choose from a set of them.
                gameInfo = existingGames.TryGetValue(gameName, out BeamGameAnnounceData gameAnnounceData)
                    ? gameAnnounceData.GameInfo
                    :  beamAppl.beamGameNet.CreateBeamGameInfo(gameName, groupType, anchorAddr, anchorAlgo, new GroupMemberLimits(), userSettings.blockCntX, userSettings.blockCntZ);

                logger.Info($"Selected Game: {gameInfo.GameName} MaxPlayers: {gameInfo.MemberLimits.MaxPlayers}");
            }
            else
                throw new Exception($"gameName setting missing.");

               bool joinAsValidator =  userSettings.GetTempSetting("validateOnly")  == "true";

            await Task.Delay(0); // Yuk, But usually this is an async UI operation
            return new GameSelectedEventArgs(gameInfo, result, joinAsValidator);
        }

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
                result =  (argStr.EndsWith("*")) || (argStr.EndsWith("+") && ! existingGames.ContainsKey(gameName)) ? GameSelectedEventArgs.ReturnCode.kCreate
                    : GameSelectedEventArgs.ReturnCode.kJoin;

                // Note that this only matters if the group is being created.
                string anchorAlgo = ApianGroupInfo.AnchorPostsNone; // default to no posting
                string anchorAddr = null; // default to no anchor
                if ( result == GameSelectedEventArgs.ReturnCode.kCreate )
                {
                    if (userSettings.tempSettings.TryGetValue("anchorAlgo", out anchorAlgo))
                    {
                        anchorAddr = userSettings.anchorContractAddr;
                        logger.Warn($"Requested anchor posting algorithm: {anchorAlgo}");
                    }
                }

                gameInfo = existingGames.TryGetValue(gameName, out BeamGameAnnounceData gameAnnounceData)
                    ? gameAnnounceData.GameInfo
                    :  beamAppl.beamGameNet.CreateBeamGameInfo(gameName, groupType, anchorAddr, anchorAlgo, new GroupMemberLimits(), userSettings.blockCntX, userSettings.blockCntZ);
            }
            else
                throw new Exception($"gameName setting missing.");

            // Info about BeamGameInfo creation lives in BeamGameNet
            bool joinAsValidator =  userSettings.GetTempSetting("validateOnly")  == "true";
            beamAppl.OnGameSelected( new GameSelectedEventArgs(gameInfo, result, joinAsValidator));
        }

        //
        // Event handlers
        //

        // Network information events

        public void OnPeerJoinedNetEvt(object sender, PeerJoinedEventArgs args)
        {
            BeamNetworkPeer p = args.peer;
            logger.Info($"OnPeerJoinedEvt() name: {p.Name}, Id: {SID(p.PeerAddr)}");
            UpdateNetworkInfo();
        }

        public void OnPeerLeftNetEvt(object sender, PeerLeftEventArgs args)
        {
            logger.Info($"OnPeerLeftEvt(): {SID(args.peerAddr)}");
            UpdateNetworkInfo();
        }

        public void OnGroupLeaderChanged(string groupId, string newLeaderAddr, string lname)
        {
            string where = newLeaderAddr == appCore.LocalPlayerAddr ? "local" : "remote";
            string msg = (lname != null) ? $"{lname} {SID(newLeaderAddr)}"  : SID(newLeaderAddr);
            Console.WriteLine( $"Group Leader ({where}): {msg}");
        }

        public void OnGameAnnounceEvt(object sender, GameAnnounceEventArgs args)
        {
            UpdateNetworkInfo();
        }

        public void OnChainIdEvt(object sender, ChainIdEventArgs args)
        {
            Console.WriteLine( $"Connected to Blockchain: \"{userSettings.curBlockchain}\" (id: {args.chainId})");
        }

        // Players

        public void OnPlayerJoinedEvt(object sender, PlayerJoinedEventArgs args)
        {
            // Player joined means a group has been joined AND is synced (ready to go)
            if ( args.player.PlayerAddr == appCore.LocalPlayerAddr )
            {
                 logger.Info($"*** Successfully joined Apian group: {args.groupChannel}");
            }
        }

        public void OnPlayerMissingEvt(object sender, PlayerLeftEventArgs args)
        {
            logger.Info($"*** Player {SID(args.playerAddr)} is MISSING!!! from group {args.groupChannel}");
        }
        public void OnPlayerReturnedEvt(object sender, PlayerLeftEventArgs args)
        {
            logger.Info($"*** Player {SID(args.playerAddr)} has RETURNED!!! to group {args.groupChannel}");
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
            logger.Info($"OnNewBikeEvt(). Id: {SID(ib.bikeId)}, Local: {ib.playerAddr == appCore.LocalPlayerAddr}, AI: {ib.ctrlType == BikeFactory.AiCtrl}");
            FrontendBike b = FeBikeFactory.Create(ib, ib.playerAddr == appCore.LocalPlayerAddr);
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
            // string bikeOwner = bike.peerAddr;

            // BeamPlace place = args.p;
            // IBike createdBy = place.bike;  // TODO: CRASH happens here. When no longer useful make it bulletproof
            //                                // (would rather make it not happen - not sure if that's possible)
            // string placeOwner = createdBy.peerAddr;

            logger.Info($"OnPlaceHitEvt. Place: {args.p?.GetPos(appCore.GetGround()).ToString()}  Bike: {SID(args.ib?.bikeId)}");
        }

        public void OnPlaceClaimedEvt(object sender, BeamPlaceEventArgs args)
        {
            BeamPlace p = args?.p;
            logger.Verbose($"OnPlaceClaimedEvt. Pos: {p?.GetPos(appCore.GetGround()).ToString()} Bike: {SID(p.bike.bikeId)}");
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

        public void OnSquareAddEvt(object sender, BeamSquareEventArgs args)
        {
            logger.Debug($"OnSquareAdded(): pos: {args.posHash} Team: {args.team.Name}");
        }

        public void OnSquareDelEvt(object sender, BeamSquareEventArgs args)
        {
            logger.Debug($"OnSquareRemoved(): pos: {args.posHash}");
        }

        public void OnReadyToPlay(object sender, EventArgs e)
        {
            logger.Error($"OnReadyToPlay() - doesn't work anymore");
            //logger.Info($"OnReadyToPlay()");
            //&&& backend.OnSwitchModeReq(BeamModeFactory.kPlay, null);
        }

    }



    public class InteractiveBeamCliFE : BeamCliFrontend
    {

        protected class CliCommand {
            public string prompt;
            public Action act;

            public CliCommand(string p, Action a) { prompt = p; act = a; }
        };

        protected Dictionary<ConsoleKey, CliCommand> commands;




        public InteractiveBeamCliFE(BeamUserSettings startupSettings) : base(startupSettings)
        {
            Console.Clear();

            commands= new Dictionary<ConsoleKey, CliCommand>()
            {
                {ConsoleKey.N, new CliCommand("n) NetInfo", DoNetInfo)},
                {ConsoleKey.P, new CliCommand("p) Pause", DoPause)},
                {ConsoleKey.R, new CliCommand("r) Resume", DoResume)},
                {ConsoleKey.Q, new CliCommand("q) Quit", DoQuit)},
            };

        }

        public override void Loop(float frameSecs)
        {
            base.Loop(frameSecs);
            Render();

            while (Console.KeyAvailable)
            {
                ConsoleKeyInfo k = Console.ReadKey();
                if ( k.Modifiers == 0 )
                {
                    if ( commands.TryGetValue(k.Key, out CliCommand cmd) ) {
                       cmd.act();
                    }

 //                   if (commands.ContainsKey(k.Key))
 //                       commands[k.Key].act();
                }

            }

        }

        protected void Render() {

            string prompt = string.Join(" ", commands.Values.Select( (cmd) => cmd.prompt).ToArray());

            string disp = "---------------------------------------------------------------------------------------------------\n"
                          + $"{prompt}                                                                                        \n"
                          + "---------------------------------------------------------------------------------------------------\n";

            //  (x, y) = Console.GetCursorPosition();
            int x = Console.CursorLeft;
            int y = Console.CursorTop;
            WriteAt(disp, 0, 0);
            Console.SetCursorPosition(x, y);
        }

        protected void WriteAt(string s, int x, int y)
        {
            try
            {
                Console.SetCursorPosition(x, y);
                Console.Write(s);
            }
            catch (ArgumentOutOfRangeException e)
            {
                Console.Clear();
                Console.WriteLine(e.Message);
            }
        }


        public void DoPause()
        {
            BeamApplication ba = beamAppl as BeamApplication;
            BeamGameNet bgn = ba.beamGameNet as BeamGameNet;
		    string groupId = ba.mainAppCore.ApianGroupId;
            bgn.SendPauseReq(groupId, "I asked for it", "555");
        }

        public void DoResume()
        {
            BeamApplication ba = beamAppl as BeamApplication;
            BeamGameNet bgn = ba.beamGameNet as BeamGameNet;
		    string groupId = ba.mainAppCore.ApianGroupId;
            bgn.SendResumeReq(groupId, "555");
        }

        public void DoNetInfo()
        {
            UpdateNetworkInfo();
        }

        public void DoQuit()
        {
            beamAppl.OnPopModeReq(null);
        }

    }

}
