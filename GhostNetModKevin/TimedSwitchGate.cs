// Celeste.SwitchGate
using Celeste;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;


namespace Celeste.Mod.GhostKevinball.Net
{
    public class TimedSwitchGate : Solid
    {
        public static ParticleType P_Behind;

        public static ParticleType P_Dust;

        private MTexture[,] nineSlice;

        private Sprite icon;

        private Vector2 iconOffset;

        private Wiggler wiggler;

        private Vector2 node;

        private SoundSource openSfx;

        private bool persistent;

        private Vector2 ogPosition;
        private Vector2 ogTarget; 

        private Color inactiveColor = Calc.HexToColor("5fcde4");

        private Color activeColor = Color.White;

        private Color finishColor = Calc.HexToColor("f141df");

        public TimedSwitchGate(Vector2 position, float width, float height, Vector2 node, bool persistent, string spriteName)
            : base(position, width, height, false)
        {
            this.node = node;
            this.ogTarget = node; 
            this.ogPosition = position; 
            this.persistent = persistent;
            base.Add(icon = new Sprite(GFX.Game, "objects/switchgate/icon"));
            icon.Add("spin", "", 0.1f, "spin");
            icon.Play("spin", false, false);
            icon.Rate = 0f;
            icon.Color = inactiveColor;
            icon.Position = (iconOffset = new Vector2(width / 2f, height / 2f));
            icon.CenterOrigin();
            base.Add(wiggler = Wiggler.Create(0.5f, 4f, delegate (float f)
            {
                icon.Scale = Vector2.One * (1f + f);
            }, false, false));
            MTexture mTexture = GFX.Game["objects/switchgate/" + spriteName];
            nineSlice = new MTexture[3, 3];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    MTexture[,] array = nineSlice;
                    int num = i;
                    int num2 = j;
                    MTexture subtexture = mTexture.GetSubtexture(new Rectangle(i * 8, j * 8, 8, 8));
                    array[num, num2] = subtexture;
                }
            }
            base.Add(openSfx = new SoundSource());
            base.Add(new LightOcclude(0.5f));
        }

        public TimedSwitchGate(EntityData data, Vector2 offset)
            : this(data.Position + offset, (float)data.Width, (float)data.Height, data.Nodes[0] + offset, data.Bool("persistent", false), data.Attr("sprite", "block"))
        {
        }

        public override void Awake(Scene scene)
        {
            base.Awake(scene);
            if (TimedSwitch.CheckLevelFlag(base.SceneAs<Level>()))
            {
                base.MoveTo(node);
                icon.Rate = 0f;
                icon.SetAnimationFrame(0);
                icon.Color = finishColor;
            }
            else
            {
                base.Add(new Coroutine(Sequence(node, false), true));
            }
        }

        public override void Render()
        {
            float num = base.Collider.Width / 8f - 1f;
            float num2 = base.Collider.Height / 8f - 1f;
            for (int i = 0; (float)i <= num; i++)
            {
                for (int j = 0; (float)j <= num2; j++)
                {
                    int num3 = ((float)i < num) ? Math.Min(i, 1) : 2;
                    int num4 = ((float)j < num2) ? Math.Min(j, 1) : 2;
                    nineSlice[num3, num4].Draw(base.Position + base.Shake + new Vector2((float)(i * 8), (float)(j * 8)));
                }
            }
            icon.Position = iconOffset + base.Shake;
            icon.DrawOutline(1);
            base.Render();
        }

        private IEnumerator Sequence(Vector2 node, bool reverse)
        {
            Vector2 start = base.Position;
            if (reverse)
            {
                while (TimedSwitch.Check(Scene))
                {
                    yield return (object)null;
                }
            }
            else
            {
                while (!TimedSwitch.Check(Scene))
                {
                    yield return (object)null;
                }
            }
            if (persistent)
            {
                TimedSwitch.SetLevelFlag(SceneAs<Level>());
            }
            yield return (object)0.1f;
            openSfx.Play("event:/game/general/touchswitch_gate_open", null, 0f);
            StartShaking(0.5f);
            while (icon.Rate < 1f)
            {
                icon.Color = Color.Lerp(inactiveColor, activeColor, icon.Rate);
                icon.Rate += Engine.DeltaTime * 2f;
                yield return (object)null;
            }
            yield return (object)0.1f;
            int particleAt = 0;
            Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeOut, 2f, true);
            tween.OnUpdate = delegate (Tween t)
            {
                MoveTo(Vector2.Lerp(start, node, t.Eased));
                if (Scene.OnInterval(0.1f))
                {
                    particleAt++;
                    particleAt %= 2;
                    for (int n = 0; (float)n < Width / 8f; n++)
                    {
                        for (int num2 = 0; (float)num2 < Height / 8f; num2++)
                        {
                            if ((n + num2) % 2 == particleAt)
                            {
                                //SceneAs<Level>().ParticlesBG.Emit(P_Behind, base.Position + new Vector2((float)(n * 8), (float)(num2 * 8)) + Calc.Random.Range(Vector2.One * 2f, Vector2.One * 6f));
                            }
                        }
                    }
                }
            };
            Add(tween);
            yield return (object)1.8f;
            bool collidable = base.Collidable;
            base.Collidable = false;
            if (node.X <= start.X)
            {
                Vector2 value = new Vector2(0f, 2f);
                for (int i = 0; (float)i < Height / 8f; i++)
                {
                    Vector2 vector = new Vector2(Left - 1f, Top + 4f + (float)(i * 8));
                    Vector2 point = vector + Vector2.UnitX;
                    if (Scene.CollideCheck<Solid>(vector) && !Scene.CollideCheck<Solid>(point))
                    {
                        //SceneAs<Level>().ParticlesFG.Emit(P_Dust, vector + value, 3.14159274f);
                        //SceneAs<Level>().ParticlesFG.Emit(P_Dust, vector - value, 3.14159274f);
                    }
                }
            }
            if (node.X >= start.X)
            {
                Vector2 value2 = new Vector2(0f, 2f);
                for (int j = 0; (float)j < Height / 8f; j++)
                {
                    Vector2 vector2 = new Vector2(Right + 1f, Top + 4f + (float)(j * 8));
                    Vector2 point2 = vector2 - Vector2.UnitX * 2f;
                    if (Scene.CollideCheck<Solid>(vector2) && !Scene.CollideCheck<Solid>(point2))
                    {
                        //SceneAs<Level>().ParticlesFG.Emit(P_Dust, vector2 + value2, 0f);
                        //SceneAs<Level>().ParticlesFG.Emit(P_Dust, vector2 - value2, 0f);
                    }
                }
            }
            if (node.Y <= start.Y)
            {
                Vector2 value3 = new Vector2(2f, 0f);
                for (int k = 0; (float)k < Width / 8f; k++)
                {
                    Vector2 vector3 = new Vector2(Left + 4f + (float)(k * 8), Top - 1f);
                    Vector2 point3 = vector3 + Vector2.UnitY;
                    if (Scene.CollideCheck<Solid>(vector3) && !Scene.CollideCheck<Solid>(point3))
                    {
                        //SceneAs<Level>().ParticlesFG.Emit(P_Dust, vector3 + value3, -1.57079637f);
                        //SceneAs<Level>().ParticlesFG.Emit(P_Dust, vector3 - value3, -1.57079637f);
                    }
                }
            }
            if (node.Y >= start.Y)
            {
                Vector2 value4 = new Vector2(2f, 0f);
                for (int l = 0; (float)l < Width / 8f; l++)
                {
                    Vector2 vector4 = new Vector2(Left + 4f + (float)(l * 8), Bottom + 1f);
                    Vector2 point4 = vector4 - Vector2.UnitY * 2f;
                    if (Scene.CollideCheck<Solid>(vector4) && !Scene.CollideCheck<Solid>(point4))
                    {
                        //SceneAs<Level>().ParticlesFG.Emit(P_Dust, vector4 + value4, 1.57079637f);
                        //SceneAs<Level>().ParticlesFG.Emit(P_Dust, vector4 - value4, 1.57079637f);
                    }
                }
            }
            base.Collidable = collidable;
            Audio.Play("event:/game/general/touchswitch_gate_finish", base.Position);
            StartShaking(0.2f);
            bool collidable2 = base.Collidable;
            base.Collidable = false;
            base.Collidable = collidable2;
            if(!reverse)
                base.Add(new Coroutine(Sequence(ogPosition, true), true));
            else
                base.Add(new Coroutine(Sequence(ogTarget, false), true));
        }
    }

}
