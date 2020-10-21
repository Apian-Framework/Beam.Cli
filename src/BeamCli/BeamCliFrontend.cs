using System.ComponentModel;
using System;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BeamGameCode;
using BikeControl;
using UniLog;

namespace BeamCli
{

    public class BeamCliFrontend : IBeamFrontend
    {
        public  Dictionary<string, FrontendBike> feBikes;
        public IBeamAppCore backend;
        protected BeamCliModeHelper _feModeHelper;
        protected BeamUserSettings userSettings;
        public UniLogger logger;

        private long prevGameTime = 0;

        // Start is called before the first frame update
        public BeamCliFrontend(BeamUserSettings startupSettings)
        {
            _feModeHelper = new BeamCliModeHelper(this);
            feBikes = new Dictionary<string, FrontendBike>();
            userSettings = startupSettings;
            logger = UniLogger.GetLogger("Frontend");
        }

        public void SetAppCore(IBeamAppCore back)
        {
            backend = back;
            if (back == null)
                return;

            OnNewCoreState(null, back.CoreData); // initialize

            back.NewCoreStateEvt += OnNewCoreState;
            back.PlayerJoinedEvt += OnPlayerJoinedEvt;
            back.PlayersClearedEvt += OnPlayersClearedEvt;
            back.NewBikeEvt += OnNewBikeEvt;
            back.BikeRemovedEvt += OnBikeRemovedEvt;
            back.BikesClearedEvt +=OnBikesClearedEvt;
            back.PlaceClaimedEvt += OnPlaceClaimedEvt;
            back.PlaceHitEvt += OnPlaceHitEvt;

            back.ReadyToPlayEvt += OnReadyToPlay;

        }

        public virtual void Loop(float frameSecs)
        {
            if (backend == null)
                return;

            long curGameTime = backend.CurrentRunningGameTime;
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

        public void DisplayMessage(MessageSeverity lvl, string msgText)
        {
            string lvlStr = (lvl == MessageSeverity.Info) ? "Info"
                : (lvl == MessageSeverity.Warning) ? "Warning"
                    : "Error";
            Console.WriteLine($"{lvl}: {msgText}");
        }

        public BeamUserSettings GetUserSettings() => userSettings;

        // Backend game modes
        public void OnStartMode(int modeId, object param) =>  _feModeHelper.OnStartMode(modeId, param);
        public void OnEndMode(int modeId, object param) => _feModeHelper.OnEndMode(modeId, param);
        public void DispatchModeCmd(int modeId, int cmdId, object param) => _feModeHelper.DispatchCmd(modeId, cmdId, param);

        public void OnNewCoreState(object sender, BeamCoreState newCoreState)
        {
            newCoreState.PlaceFreedEvt += OnPlaceFreedEvt;
            newCoreState.PlacesClearedEvt += OnPlacesClearedEvt;
            newCoreState.SetupPlaceMarkerEvt += OnSetupPlaceMarkerEvt;
        }


        // Players

        public void OnPeerJoinedGameEvt(object sender, PeerJoinedGameArgs args)
        {
        ///     BeamGroupMember p = args.peer;
        ///     logger.Info($"OnPeerJoinedEvt() name: {p.Name}, Id: {p.PeerId}");
        }

        public void OnPeerLeftGameEvt(object sender, PeerLeftGameArgs args)
        {
            logger.Info($"OnPeerLeftEvt(): {args.p2pId}");
        }

        public void OnPlayerJoinedEvt(object sender, PlayerJoinedArgs args)
        {
            // Player joined means a group has been joined AND is synced (ready to go)
            if ( args.player.PeerId == backend.LocalPeerId )
            {
                 logger.Info($"*** Successfully joined Apian group: {args.groupChannel}");
            }
        }

        public void OnPlayersClearedEvt(object sender, EventArgs e)
        {
            // Probably never will do anything
            logger.Verbose("OnClearPeers() currently does nothing");
        }

        // Bikes
        public void OnNewBikeEvt(object sender, IBike ib)
        {
            logger.Info($"OnNewBikeEvt(). Id: {ib.bikeId}, Local: {ib.peerId == backend.LocalPeerId}, AI: {ib.ctrlType == BikeFactory.AiCtrl}");
            FrontendBike b = FeBikeFactory.Create(ib, ib.peerId == backend.LocalPeerId);
            b.Setup(ib, backend);
            feBikes[ib.bikeId] = b;
        }
        public void OnBikeRemovedEvt(object sender, BikeRemovedData rData)
        {
            logger.Info(string.Format("OnBikeRemovedEvt({0}). Id: {1}", rData.doExplode ? "Boom!" : "", rData.bikeId));
            feBikes.Remove(rData.bikeId);
        }
        public void OnBikesClearedEvt(object sender, EventArgs e)
        {
            logger.Verbose(string.Format("OnBikesClearedEvt()"));
		    feBikes.Clear();
        }


        // Places

        public void OnPlaceHitEvt(object sender, PlaceHitArgs args)
        {
            // This intentionally accesses linked data to show issues
            // IBike bike = args.ib;
            // string bikeOwner = bike.peerId;

            // BeamPlace place = args.p;
            // IBike createdBy = place.bike;  // TODO: CRASH happens here. When no longer useful make it bulletproof
            //                                // (would rather make it not happen - not sure if that's possible)
            // string placeOwner = createdBy.peerId;

            logger.Info($"OnPlaceHitEvt. Place: {args.p?.GetPos().ToString()}  Bike: {args.ib?.bikeId}");
        }

        public void OnPlaceClaimedEvt(object sender, BeamPlace p)
        {
            logger.Verbose($"OnPlaceClaimedEvt. Pos: {p?.GetPos().ToString()} Bike: {p.bike.bikeId}");
        }

        // Ground
        public void OnSetupPlaceMarkerEvt(object sender, BeamPlace p)
        {
            logger.Debug($"OnSetupPlaceMarkerEvt({p.xIdx}, {p.zIdx})");
        }

        public void OnPlaceFreedEvt(object sender, BeamPlace p)
        {
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
