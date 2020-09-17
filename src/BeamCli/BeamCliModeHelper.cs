using System;
using System.Collections.Generic;
using UnityEngine;
using BeamGameCode;

namespace BeamCli
{
    public class BeamCliModeHelper : IFrontendModeHelper
    {

        protected abstract class ModeFuncs
        {
            public abstract void OnStart(object parms);
            public abstract void OnEnd(object parms);
            public void HandleCmd(int cmdId, object parms) => _cmdDispatch[cmdId](parms);
            protected Dictionary<int,dynamic> _cmdDispatch;

            public ModeFuncs()
            {
                _cmdDispatch = new Dictionary<int, dynamic>();
            }
        }

        protected Dictionary<int, ModeFuncs> _modeFuncs;
        public IBeamFrontend fe;

        public BeamCliModeHelper(IBeamFrontend _fe)
        {
            fe = _fe;
            _modeFuncs = new Dictionary<int, ModeFuncs>()
            {
                { BeamModeFactory.kSplash, new SplashModeFuncs()},
                { BeamModeFactory.kConnect, new ConnectModeFuncs()},
                { BeamModeFactory.kPlay, new PlayModeFuncs()},
                { BeamModeFactory.kPractice, new PracticeModeFuncs()}
            };
        }

        public void OnStartMode(int modeId, object parms=null)
        {
            _modeFuncs[modeId].OnStart(parms);
        }
        public void DispatchCmd(int modeId, int cmdId, object parms=null)
        {
            _modeFuncs[modeId].HandleCmd(cmdId, parms);
        }
        public void OnEndMode(int modeId, object parms=null)
        {
            _modeFuncs[modeId].OnEnd(parms);
        }

        // Implementations
        class SplashModeFuncs : ModeFuncs
        {
            public SplashModeFuncs() : base()
            {
               _cmdDispatch[ModeSplash.kCmdTargetCamera] = new Action<object>(o => TargetCamera(o as TargetIdParams));
            }

            protected void TargetCamera(TargetIdParams parm)
            {
                TargetIdParams p = (TargetIdParams)parm;
            }

            public override void OnStart(object parms=null)
            {

            }

            public override void OnEnd(object parms=null) {}
        }

        class ConnectModeFuncs : ModeFuncs
        {
            public ConnectModeFuncs() : base() {}
            public override void OnStart(object parms=null)
            {

            }
            public override void OnEnd(object parms=null) {}
        }

        class PlayModeFuncs : ModeFuncs
        {
            public PlayModeFuncs() : base() {}

            public override void OnStart(object parms=null)
            {

            }
            public override void OnEnd(object parms=null)
            {

            }
        }

        class PracticeModeFuncs : PlayModeFuncs
        {
            public PracticeModeFuncs() : base() {}
        }
    }

}
