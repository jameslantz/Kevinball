using Celeste;
using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace Celeste.Mod.GhostKevinball.Net
{
    [Tracked(false)]
    public class MultiplayerControlSwitch : Entity
    {
        public enum Controller
        {
            Neutral,
            P1, 
            P2
        }

        public static ParticleType P_Fire;

        public static ParticleType P_FireWhite;

        public int tIdx = 0;

        public Controller currentController = Controller.Neutral;

        public ControlSwitch ControlSwitch;

        private SoundSource touchSfx;

        private MTexture border = GFX.Game["objects/touchswitch/container"];

        private Sprite icon = new Sprite(GFX.Game, "objects/touchswitch/icon");

        private Color inactiveColor = Calc.HexToColor("9b9b9b");

        private Color activeColor = Color.White;

        private Color finishColor = Calc.HexToColor("f141df");

        private Color P1Color = Calc.HexToColor("f04141");
        private Color P2Color = Calc.HexToColor("4172ef");

        private float ease;

        private Wiggler wiggler;

        private Vector2 pulse = Vector2.One;

        private float timer;

        public bool timed = false;
        public float currentTimer;
        public float maxTimer;

        private BloomPoint bloom;

        private Level level => (Level)base.Scene;

        public MultiplayerControlSwitch(Vector2 position)
            : base(position)
        {

            base.Depth = 2000;
            base.Add(ControlSwitch = new ControlSwitch(false, 0));

            base.Add(new PlayerCollider(OnPlayer, null, new Hitbox(30f, 30f, -15f, -15f)));
            base.Add(icon);
            base.Add(bloom = new BloomPoint(0f, 16f));
            bloom.Alpha = 0f;
            icon.Add("idle", "", 0f, default(int));
            icon.Add("spin", "", 0.1f, new Chooser<string>("spin", 1f), 0, 1, 2, 3, 4, 5);
            icon.Play("spin", false, false);
            icon.Color = inactiveColor;
            icon.CenterOrigin();
            base.Collider = new Hitbox(16f, 16f, -8f, -8f);
            base.Add(new TheoCrystalCollider(OnTheoCrystal, new Hitbox(20f, 20f, -10f, -10f)));
            base.Add(new SeekerCollider(OnSeeker, new Hitbox(24f, 24f, -12f, -12f)));

            ControlSwitch.OnActivate = delegate
            {
                wiggler.Start();
                for (int i = 0; i < 32; i++)
                {
                    float num = Calc.Random.NextFloat(6.28318548f);
                        //level.Particles.Emit(P_FireWhite, base.Position + Calc.AngleToVector(num, 6f), num);
                    }
                icon.Rate = 4f;
            };

            ControlSwitch.OnDeactivate = delegate
            {
                wiggler.StopAndClear();
                icon.Rate = 0.1f;
                icon.Play("spin", false, false);
            };

            ControlSwitch.OnFinish = delegate
            {
                ease = 0f;
            };
            ControlSwitch.OnStartFinished = delegate
            {
                icon.Rate = 0.1f;
                icon.Play("spin", false, false);
                ease = 1f;
            };

            base.Add(wiggler = Wiggler.Create(0.5f, 4f, delegate (float v)
            {
                pulse = Vector2.One * (1f + v * 0.25f);
            }, false, false));
            base.Add(new VertexLight(Color.White, 0.8f, 16, 32));
            base.Add(touchSfx = new SoundSource());
        }

        public MultiplayerControlSwitch(EntityData data, Vector2 offset)
            : this(data.Position + offset)
        {
        }

        public void TurnOn(uint idIn = 9999)
        {
            //if (!Switch.Activated)
            //{
            //    touchSfx.Play("event:/game/general/touchswitch_any", null, 0f);
            //    if (Switch.Activate())
            //    {
            //        SoundEmitter.Play("event:/game/general/touchswitch_last_oneshot");
            //        base.Add(new SoundSource("event:/game/general/touchswitch_last_cutoff"));
            //    }
            //}

            Controller controllerIn = Controller.Neutral; 
            GhostNetClient client = GhostNetModule.Instance.Client; 
            if(client != null && client.Connection != null)
            {
                if (idIn == 9999)
                    idIn = client.PlayerID;

                if (idIn == client.P1_id)
                    controllerIn = Controller.P1;
                else if (idIn == client.P2_id)
                    controllerIn = Controller.P2;
                else
                    return; 
            }
            else
            {
                return; 
            }

            if(controllerIn != currentController)
            {
                currentController = controllerIn;
                ControlSwitch.controller = controllerIn;

                int startingNum = 8 - client.ControlSwitches.Count; 
                int chimeNum = 0; 
                int p1Chimes = 0;
                int p2Chimes = 0; 

                foreach (MultiplayerControlSwitch mSwitch in client.ControlSwitches)
                {
                    if (mSwitch.currentController == Controller.P1)
                        p1Chimes++; 
                }
                foreach (MultiplayerControlSwitch mSwitch in client.ControlSwitches)
                {
                    if (mSwitch.currentController == Controller.P2)
                        p2Chimes++; 
                }

                int realChimeNum = 0; 
                uint loserID = uint.MaxValue;  
                if(p1Chimes > p2Chimes)
                {
                    chimeNum = p1Chimes + startingNum;
                    realChimeNum = p1Chimes; 
                    loserID = client.P2_id; 
                }
                else
                {
                    chimeNum = p2Chimes + startingNum;
                    realChimeNum = p2Chimes; 
                    loserID = client.P1_id; 
                }

                string str = "";

                if (realChimeNum >= client.ControlSwitches.Count)
                {
                    if (client.PlayerID == loserID)
                        str = "event:/kevinball_8_lose";
                    else
                        str = "event:/kevinball_8_win";
                }
                else
                {
                    if (chimeNum >= 7)
                        chimeNum = 7;

                    str = "event:/kevinball_" + chimeNum.ToString();
                }

                touchSfx.Play(str, null, 0f);

                if (currentController == Controller.P1)
                    icon.Color = P1Color;
                else
                    icon.Color = P2Color; 
            }
        }

        private void OnPlayer(Player player)
        {
            TurnOn();
            //don't send this if we don't control the switch 
            if (GhostNetModule.Instance != null && GhostNetModule.Instance.Client != null && GhostNetModule.Instance.Client.Connection != null)
            {
                GhostNetModule.Instance.Client.OnTriggerMultiplayerControlSwitch(this);
            }
        }

        private void OnTheoCrystal(TheoCrystal theo)
        {
            TurnOn();
        }

        private void OnSeeker(Seeker seeker)
        {
            if (base.SceneAs<Level>().InsideCamera(base.Position, 10f))
            {
                TurnOn();
            }
        }

        public override void Update()
        {
            ease = Calc.Approach(ease, (ControlSwitch.Activated) ? 1f : 0f, Engine.DeltaTime * 2f);
            //icon.Color = Color.Lerp(inactiveColor, (TimedSwitch.Finished && TimedSwitch.Activated) ? finishColor : activeColor, ease);
            //Sprite sprite = icon;
            //sprite.Color *= 0.5f + ((float)Math.Sin((double)timer) + 1f) / 2f * (1f - ease) * 0.5f + 0.5f * ease;
            bloom.Alpha = ease;

            base.Update();
        }

        public override void Render()
        {
            border.DrawCentered(base.Position + new Vector2(0f, -1f), Color.Black);
            border.DrawCentered(base.Position, icon.Color, pulse);
            base.Render();
        }
    }

}