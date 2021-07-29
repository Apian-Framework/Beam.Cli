using System;
using UniLog;
using UnityEngine;
using BeamGameCode;
using BikeControl;

namespace BeamCli
{
    public static class FeBikeFactory
    {
        public static FrontendBike Create(IBike ib, bool isLocal)
        {
            FrontendBike feb = null;
            if (isLocal)
            {
                switch (ib.ctrlType)
                {
                case BikeFactory.LocalPlayerCtrl:
                    feb = new PlayerBike();
                    break;
                case BikeFactory.AiCtrl:
                    feb = new AiBike();
                    break;
                }
            }
            else
                feb = new RemoteBike();

            return feb;
        }
    }

    public abstract class FrontendBike
    {
        public static readonly System.Type[] bikeClassTypes = {
            typeof(FrontendBike), // remote bike. Nothing to do here.
            typeof(AiBike),  // AiCtrl - AI - controlled local bike
            typeof(PlayerBike) // LocalPlayerCtrl - a human on this machine
        };

        public IBike bb;
        protected IBeamAppCore appCore;
        protected IBikeControl control;
        protected abstract void CreateControl();

        public UniLogger Logger;

        public virtual void Setup(IBike beBike, IBeamApplication appl, IBeamAppCore core)
        {
            Logger = UniLogger.GetLogger("Frontend");  // use the FE logger
            appCore = core;
            bb = beBike;
            CreateControl();
            control.Setup(appl, core, beBike);
        }

        public virtual void Loop(long curTime, int frameMs)
        {
            control.Loop(curTime, frameMs);
        }

    }

    public class AiBike : FrontendBike
    {
        protected override void CreateControl()
        {
            Logger.Verbose($"AiBike.CreateControl()");
            control = new AiControl();
        }
    }
    public class PlayerBike : FrontendBike
    {
        protected override void CreateControl()
        {
            Logger.Verbose($"PlayerBike.CreateControl()");
            control = new PlayerControl();
        }
    }

    public class RemoteBike : FrontendBike
    {
        protected override void CreateControl()
        {
            Logger.Verbose($"PlayerBike.RemoteBike()");
            control = new RemoteControl();
        }
    }
}