// Celeste.Switch
using Celeste;
using Monocle;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.GhostKevinball.Net
{
    [Tracked(false)]
    public class TimedSwitch : Component
    {
        public bool GroundReset;

        public Action OnActivate;

        public Action OnDeactivate;

        public Action OnFinish;

        public Action OnStartFinished;

        public float MaxTimer;
        public float Timer; 

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

        public TimedSwitch(bool groundReset, float maxTimer)
            : base(true, false)
        {
            GroundReset = groundReset;
            MaxTimer = maxTimer; 
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
            if(Activated || Finished)
            {
                Timer += Engine.DeltaTime;
                if (Timer > MaxTimer)
                {
                    Timer = 0;
                    Deactivate(); 
                }
            }
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
            Timer = 0; 
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
            TimedSwitch component = scene.Tracker.GetComponent<TimedSwitch>();
            return component?.Finished ?? false;
        }

        private static bool FinishedCheck(Level level)
        {
            List<Component>.Enumerator enumerator = level.Tracker.GetComponents<TimedSwitch>().GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    if (!((TimedSwitch)enumerator.Current).Activated)
                    {
                        return false;
                    }
                }
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }
            enumerator = level.Tracker.GetComponents<TimedSwitch>().GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    ((TimedSwitch)enumerator.Current).Finish();
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
