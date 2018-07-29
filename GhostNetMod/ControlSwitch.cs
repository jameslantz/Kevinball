// Celeste.Switch
using Celeste;
using Monocle;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.GhostKevinball.Net
{
    [Tracked(false)]
    public class ControlSwitch : Component
    {
        public bool GroundReset;

        public Action OnActivate;

        public Action OnDeactivate;

        public Action OnFinish;

        public Action OnStartFinished;

        public MultiplayerControlSwitch.Controller controller = MultiplayerControlSwitch.Controller.Neutral; 

        public bool Activated
        {
            get;
            private set;
        }

        public bool Finished
        {
            get;
            private set;
        }

        public ControlSwitch(bool groundReset, float maxTimer)
            : base(true, false)
        {
            GroundReset = groundReset;
        }

        public override void EntityAdded(Scene scene)
        {
            base.EntityAdded(scene);
            if (CheckLevelFlag(base.SceneAs<Level>()))
            {
                StartFinished();
            }
        }

        public override void Update()
        {
            base.Update();
        }

        public bool Activate()
        {
            if (!Finished && !Activated)
            {
                Activated = true;
                if (OnActivate != null)
                {
                    OnActivate();
                }
                return FinishedCheck(base.SceneAs<Level>());
            }
            return false;
        }

        public void Deactivate()
        {
            Activated = false;
            Finished = false;
            if (OnDeactivate != null)
            {
                OnDeactivate();
            }
        }

        public void Finish()
        {
            Finished = true;
            if (OnFinish != null)
            {
                OnFinish();
            }
        }

        public void StartFinished()
        {
            if (!Finished)
            {
                bool finished = Activated = true;
                Finished = finished;
                if (OnStartFinished != null)
                {
                    OnStartFinished();
                }
            }
        }

        public static bool Check(Scene scene)
        {
            ControlSwitch component = scene.Tracker.GetComponent<ControlSwitch>();
            return component?.Finished ?? false;
        }

        private static bool FinishedCheck(Level level)
        {
            List<Component>.Enumerator enumerator = level.Tracker.GetComponents<ControlSwitch>().GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    if (!((ControlSwitch)enumerator.Current).Activated)
                    {
                        return false;
                    }
                }
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }
            enumerator = level.Tracker.GetComponents<ControlSwitch>().GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    ((ControlSwitch)enumerator.Current).Finish();
                }
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }
            return true;
        }

        public static bool CheckLevelFlag(Level level)
        {
            return level.Session.GetFlag("switches_" + level.Session.Level);
        }

        public static void SetLevelFlag(Level level)
        {
            level.Session.SetFlag("switches_" + level.Session.Level, true);
        }
    }

}
