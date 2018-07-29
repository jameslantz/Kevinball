using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Celeste.Mod.UI;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using Celeste;
using Celeste.Mod;

namespace Celeste.Mod.GhostKevinball.Net
{
    public class MultiplayerVariableSpeedCrushBlock : Solid
    {
        public enum Axes
        {
            Both,
            Horizontal,
            Vertical
        }

        private struct MoveState
        {
            public Vector2 From;

            public Vector2 Direction;

            public MoveState(Vector2 from, Vector2 direction)
            {
                From = from;
                Direction = direction;
            }
        }

        public static ParticleType P_Impact;

        public static ParticleType P_Crushing;

        public static ParticleType P_Activate;

        private const float CrushSpeed = 240f;

        private const float CrushAccel = 500f;

        private const float ReturnSpeed = 60f;

        private const float ReturnAccel = 160f;

        private Color fill = Calc.HexToColor("62222b");

        private Level level;

        private bool canActivate;

        private Vector2 crushDir;

        private List<MoveState> returnStack;

        private Coroutine attackCoroutine;

        private bool canMoveVertically;

        private bool canMoveHorizontally;

        private bool chillOut;

        private bool giant;

        public float cooldown = 0; 

        private Sprite face;

        private string nextFaceDirection;

        private List<Image> idleImages = new List<Image>();

        private List<Image> activeTopImages = new List<Image>();

        private List<Image> activeRightImages = new List<Image>();
        private List<Image> activeRightImages2 = new List<Image>();
        private List<Image> activeRightImages3 = new List<Image>();
        private List<Image> activeRightImages4 = new List<Image>();

        private List<Image> activeLeftImages = new List<Image>();
        private List<Image> activeLeftImages2 = new List<Image>();
        private List<Image> activeLeftImages3 = new List<Image>();
        private List<Image> activeLeftImages4 = new List<Image>();

        private List<Image> activeBottomImages = new List<Image>();

        private Color cdColor = Calc.HexToColor("636363");

        private SoundSource currentMoveLoopSfx;

        private SoundSource returnLoopSfx;

        private bool Submerged => base.Scene.CollideCheck<Water>(new Rectangle((int)(base.Center.X - 4f), (int)base.Center.Y, 8, 4));

        public MultiplayerVariableSpeedCrushBlock(Vector2 position, float width, float height, Axes axes, bool chillOut = false)
        : base(position, width, height, false)
        {
            base.OnDashCollide = OnDashed;
            returnStack = new List<MoveState>();
            this.chillOut = chillOut;
            giant = ((base.Width >= 48f && base.Height >= 48f) & chillOut);
            canActivate = true;
            attackCoroutine = new Coroutine(true);
            attackCoroutine.RemoveOnComplete = false;
            base.Add(attackCoroutine);
            List<MTexture> atlasSubtextures = GFX.Game.GetAtlasSubtextures("objects/crushblock/block");
            MTexture idle;
            switch (axes)
            {
                default:
                    idle = atlasSubtextures[3];
                    canMoveHorizontally = (canMoveVertically = true);
                    break;
                case Axes.Horizontal:
                    idle = atlasSubtextures[1];
                    canMoveHorizontally = true;
                    canMoveVertically = false;
                    break;
                case Axes.Vertical:
                    idle = atlasSubtextures[2];
                    canMoveHorizontally = false;
                    canMoveVertically = true;
                    break;
            }
            base.Add(face = GFX.SpriteBank.Create(giant ? "giant_crushblock_face" : "crushblock_face"));
            face.Position = new Vector2(base.Width, base.Height) / 2f;
            face.Play("idle", false, false);
            face.OnLastFrame = delegate (string f)
            {
                if (f == "hit")
                {
                    face.Play(nextFaceDirection, false, false);
                }
            };
            int num = (int)(base.Width / 8f) - 1;
            int num2 = (int)(base.Height / 8f) - 1;
            AddImage(idle, 0, 0, 0, 0, -1, -1);
            AddImage(idle, num, 0, 3, 0, 1, -1);
            AddImage(idle, 0, num2, 0, 3, -1, 1);
            AddImage(idle, num, num2, 3, 3, 1, 1);
            for (int i = 1; i < num; i++)
            {
                AddImage(idle, i, 0, Calc.Random.Choose(1, 2), 0, 0, -1);
                AddImage(idle, i, num2, Calc.Random.Choose(1, 2), 3, 0, 1);
            }
            for (int j = 1; j < num2; j++)
            {
                AddImage(idle, 0, j, 0, Calc.Random.Choose(1, 2), -1, 0);
                AddImage(idle, num, j, 3, Calc.Random.Choose(1, 2), 1, 0);
            }
            base.Add(new LightOcclude(0.2f));
            base.Add(returnLoopSfx = new SoundSource());
            base.Add(new WaterInteraction(() => crushDir != Vector2.Zero));
        }

        public MultiplayerVariableSpeedCrushBlock(EntityData data, Vector2 offset)
        : this(data.Position + offset, (float)data.Width, (float)data.Height, data.Enum("axes", Axes.Both), data.Bool("chillout", false))
        {
        }

        public override void Added(Scene scene)
        {
            base.Added(scene);
            level = base.SceneAs<Level>();
        }

        public override void Update()
        {
            base.Update();
            if(cooldown > 0 )
                cooldown = cooldown - Engine.DeltaTime;

            if(GhostNetModule.Instance != null && GhostNetModule.Instance.Client != null)
            {
                if (cooldown > 0 || !GhostNetModule.Instance.Client.KevinHittable)
                    face.Color = cdColor;
                else
                    face.Color = Color.White;
            }

            if (crushDir == Vector2.Zero)
            {
                face.Position = new Vector2(base.Width, base.Height) / 2f;
                if (base.CollideCheck<Player>(base.Position + new Vector2(-1f, 0f)))
                {
                    face.X -= 1f;
                }
                else if (base.CollideCheck<Player>(base.Position + new Vector2(1f, 0f)))
                {
                    face.X += 1f;
                }
                else if (base.CollideCheck<Player>(base.Position + new Vector2(0f, -1f)))
                {
                    face.Y -= 1f;
                }
            }
            if (currentMoveLoopSfx != null)
            {
                currentMoveLoopSfx.Param("submerged", (float)(Submerged ? 1 : 0));
            }
            if (returnLoopSfx != null)
            {
                returnLoopSfx.Param("submerged", (float)(Submerged ? 1 : 0));
            }
        }

        public override void Render()
        {
            Vector2 position = base.Position;
            base.Position += base.Shake;
            Draw.Rect(base.X + 2f, base.Y + 2f, base.Width - 4f, base.Height - 4f, fill);
            base.Render();
            base.Position = position;
        }

        private void AddImage(MTexture idle, int x, int y, int tx, int ty, int borderX = 0, int borderY = 0)
        {
            MTexture subtexture = idle.GetSubtexture(tx * 8, ty * 8, 8, 8, null);
            Vector2 vector = new Vector2((float)(x * 8), (float)(y * 8));
            if (borderX != 0)
            {
                Image image = new Image(subtexture);
                image.Color = Color.Black;
                image.Position = vector + new Vector2((float)borderX, 0f);
                base.Add(image);
            }
            if (borderY != 0)
            {
                Image image2 = new Image(subtexture);
                image2.Color = Color.Black;
                image2.Position = vector + new Vector2(0f, (float)borderY);
                base.Add(image2);
            }
            Image image3 = new Image(subtexture);
            image3.Position = vector;
            base.Add(image3);
            idleImages.Add(image3);
            if (borderX == 0 && borderY == 0)
            {
                return;
            }
            if (borderX < 0)
            {
                Image image4 = new Image(GFX.Game["objects/crushblock/lit_left"].GetSubtexture(0, ty * 8, 8, 8, null));
                activeLeftImages.Add(image4);
                image4.Position = vector;
                image4.Visible = false;
                base.Add(image4);

                Image image9 = new Image(GFX.Game["objects/variableSpeedCrushBlock/lit_left_1"].GetSubtexture(0, ty * 8, 8, 8, null));
                activeLeftImages2.Add(image9);
                image9.Position = vector;
                image9.Visible = false;
                base.Add(image9);

                Image image10 = new Image(GFX.Game["objects/variableSpeedCrushBlock/lit_left_2"].GetSubtexture(0, ty * 8, 8, 8, null));
                activeLeftImages3.Add(image10);
                image10.Position = vector;
                image10.Visible = false;
                base.Add(image10);

                Image image11 = new Image(GFX.Game["objects/variableSpeedCrushBlock/lit_left_3"].GetSubtexture(0, ty * 8, 8, 8, null));
                activeLeftImages4.Add(image11);
                image11.Position = vector;
                image11.Visible = false;
                base.Add(image11);

            }
            else if (borderX > 0)
            {
                Image image5 = new Image(GFX.Game["objects/crushblock/lit_right"].GetSubtexture(0, ty * 8, 8, 8, null));
                activeRightImages.Add(image5);
                image5.Position = vector;
                image5.Visible = false;
                base.Add(image5);

                image5 = new Image(GFX.Game["objects/variableSpeedCrushBlock/lit_right_1"].GetSubtexture(0, ty * 8, 8, 8, null));
                activeRightImages2.Add(image5);
                image5.Position = vector;
                image5.Visible = false;
                base.Add(image5);

                image5 = new Image(GFX.Game["objects/variableSpeedCrushBlock/lit_right_2"].GetSubtexture(0, ty * 8, 8, 8, null));
                activeRightImages3.Add(image5);
                image5.Position = vector;
                image5.Visible = false;
                base.Add(image5);

                image5 = new Image(GFX.Game["objects/variableSpeedCrushBlock/lit_right_3"].GetSubtexture(0, ty * 8, 8, 8, null));
                activeRightImages4.Add(image5);
                image5.Position = vector;
                image5.Visible = false;
                base.Add(image5);
            }
            if (borderY < 0)
            {
                Image image6 = new Image(GFX.Game["objects/crushblock/lit_top"].GetSubtexture(tx * 8, 0, 8, 8, null));
                activeTopImages.Add(image6);
                image6.Position = vector;
                image6.Visible = false;
                base.Add(image6);
            }
            else if (borderY > 0)
            {
                Image image7 = new Image(GFX.Game["objects/crushblock/lit_bottom"].GetSubtexture(tx * 8, 0, 8, 8, null));
                activeBottomImages.Add(image7);
                image7.Position = vector;
                image7.Visible = false;
                base.Add(image7);
            }
        }

        private void TurnOffImages()
        {
            List<Image>.Enumerator enumerator = activeLeftImages.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    enumerator.Current.Visible = false;
                }
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }

            enumerator = activeLeftImages2.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    enumerator.Current.Visible = false;
                }
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }

            enumerator = activeLeftImages3.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    enumerator.Current.Visible = false;
                }
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }

            enumerator = activeLeftImages4.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    enumerator.Current.Visible = false;
                }
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }
            enumerator = activeRightImages.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    enumerator.Current.Visible = false;
                }
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }

            enumerator = activeRightImages2.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    enumerator.Current.Visible = false;
                }
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }

            enumerator = activeRightImages3.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    enumerator.Current.Visible = false;
                }
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }

            enumerator = activeRightImages4.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    enumerator.Current.Visible = false;
                }
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }

            enumerator = activeTopImages.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    enumerator.Current.Visible = false;
                }
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }
            enumerator = activeBottomImages.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    enumerator.Current.Visible = false;
                }
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }
        }

        private DashCollisionResults OnDashed(Player player, Vector2 direction)
        {
            if (CanActivate(-direction))
            {
                Attack(-direction);
                if(GhostNetModule.Instance != null && GhostNetModule.Instance.Client != null && GhostNetModule.Instance.Client.Connection != null)
                {
                    GhostNetModule.Instance.Client.OnHitMultiplayerCrush(-direction, this);
                }
                return DashCollisionResults.Rebound;
            }
            return DashCollisionResults.NormalCollision;
        }

        public bool CanActivate(Vector2 direction, bool overrideHittable = false)
        {
            if(cooldown > 0)
            {
                return false;
            }
            if(overrideHittable == false)
            {
                if (GhostNetModule.Instance != null && GhostNetModule.Instance.Client != null)
                {
                    if (!GhostNetModule.Instance.Client.KevinHittable)
                        return false;
                }
            }
            if (giant && direction.X <= 0f)
            {
                return false;
            }
            if (canActivate)
            {
                if (direction.X != 0f && !canMoveHorizontally)
                {
                    return false;
                }
                if (direction.Y != 0f && !canMoveVertically)
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        public void Attack(Vector2 direction)
        {
            Audio.Play("event:/game/06_reflection/crushblock_activate", base.Center);
            if (currentMoveLoopSfx != null)
            {
                currentMoveLoopSfx.Param("end", 1f);
                SoundSource sfx = currentMoveLoopSfx;
                Alarm.Set(this, 0.5f, delegate
                {
                    sfx.RemoveSelf();
                }, Alarm.AlarmMode.Oneshot);
            }
            base.Add(currentMoveLoopSfx = new SoundSource());
            currentMoveLoopSfx.Position = new Vector2(base.Width, base.Height) / 2f;
            currentMoveLoopSfx.Play("event:/game/06_reflection/crushblock_move_loop", null, 0f);
            face.Play("hit", false, false);
            lastCrushDir = crushDir;
            crushDir = direction;
            if (crushDir == lastCrushDir)
            {
                speedIdx = Math.Min(speedIdx + 1, variableSpeeds.Length - 1);
            }
            else
            {
                speedIdx = 0; 
            }

            canActivate = false;
            cooldown = 0f; //MP COOLDOWN 
            attackCoroutine.Replace(AttackSequence());
            base.ClearRemainder();
            TurnOffImages();
            //ActivateParticles(crushDir);
            List<Image>.Enumerator enumerator;
            if (crushDir.X < 0f)
            {
                enumerator = activeLeftImages.GetEnumerator();
                try
                {
                    while (enumerator.MoveNext() && speedIdx == 0)
                    {
                        enumerator.Current.Visible = true;
                    }
                }
                finally
                {
                    ((IDisposable)enumerator).Dispose();
                }

                enumerator = activeLeftImages2.GetEnumerator();
                try
                {
                    while (enumerator.MoveNext() && speedIdx == 1)
                    {
                        enumerator.Current.Visible = true;
                    }
                }
                finally
                {
                    ((IDisposable)enumerator).Dispose();
                }

                enumerator = activeLeftImages3.GetEnumerator();
                try
                {
                    while (enumerator.MoveNext() && speedIdx == 2)
                    {
                        enumerator.Current.Visible = true;
                    }
                }
                finally
                {
                    ((IDisposable)enumerator).Dispose();
                }

                enumerator = activeLeftImages4.GetEnumerator();
                try
                {
                    while (enumerator.MoveNext() && speedIdx == 3)
                    {
                        enumerator.Current.Visible = true;
                    }
                }
                finally
                {
                    ((IDisposable)enumerator).Dispose();
                }

                nextFaceDirection = "left";
            }
            else if (crushDir.X > 0f)
            {
                enumerator = activeRightImages.GetEnumerator();
                try
                {
                    while (enumerator.MoveNext() && speedIdx == 0)
                    {
                        enumerator.Current.Visible = true;
                    }
                }
                finally
                {
                    ((IDisposable)enumerator).Dispose();
                }

                enumerator = activeRightImages2.GetEnumerator();
                try
                {
                    while (enumerator.MoveNext() && speedIdx == 1)
                    {
                        enumerator.Current.Visible = true;
                    }
                }
                finally
                {
                    ((IDisposable)enumerator).Dispose();
                }

                enumerator = activeRightImages3.GetEnumerator();
                try
                {
                    while (enumerator.MoveNext() && speedIdx == 2)
                    {
                        enumerator.Current.Visible = true;
                    }
                }
                finally
                {
                    ((IDisposable)enumerator).Dispose();
                }

                enumerator = activeRightImages4.GetEnumerator();
                try
                {
                    while (enumerator.MoveNext() && speedIdx == 3)
                    {
                        enumerator.Current.Visible = true;
                    }
                }
                finally
                {
                    ((IDisposable)enumerator).Dispose();
                }

                nextFaceDirection = "right";
            }
            else if (crushDir.Y < 0f)
            {
                enumerator = activeTopImages.GetEnumerator();
                try
                {
                    while (enumerator.MoveNext())
                    {
                        enumerator.Current.Visible = true;
                    }
                }
                finally
                {
                    ((IDisposable)enumerator).Dispose();
                }
                nextFaceDirection = "up";
            }
            else if (crushDir.Y > 0f)
            {
                enumerator = activeBottomImages.GetEnumerator();
                try
                {
                    while (enumerator.MoveNext())
                    {
                        enumerator.Current.Visible = true;
                    }
                }
                finally
                {
                    ((IDisposable)enumerator).Dispose();
                }
                nextFaceDirection = "down";
            }
            bool flag = true;
            if (returnStack.Count > 0)
            {
                MoveState moveState = returnStack[returnStack.Count - 1];
                if (moveState.Direction == direction || moveState.Direction == -direction)
                {
                    flag = false;
                }
            }
            if (flag)
            {
                returnStack.Add(new MoveState(base.Position, crushDir));
            }
        }

        private void ActivateParticles(Vector2 dir)
        {
            float direction;
            Vector2 position;
            Vector2 positionRange;
            int num;
            if (dir == Vector2.UnitX)
            {
                direction = 0f;
                position = base.CenterRight - Vector2.UnitX;
                positionRange = Vector2.UnitY * (base.Height - 2f) * 0.5f;
                num = (int)(base.Height / 8f) * 4;
            }
            else if (dir == -Vector2.UnitX)
            {
                direction = 3.14159274f;
                position = base.CenterLeft + Vector2.UnitX;
                positionRange = Vector2.UnitY * (base.Height - 2f) * 0.5f;
                num = (int)(base.Height / 8f) * 4;
            }
            else if (dir == Vector2.UnitY)
            {
                direction = 1.57079637f;
                position = base.BottomCenter - Vector2.UnitY;
                positionRange = Vector2.UnitX * (base.Width - 2f) * 0.5f;
                num = (int)(base.Width / 8f) * 4;
            }
            else
            {
                direction = -1.57079637f;
                position = base.TopCenter + Vector2.UnitY;
                positionRange = Vector2.UnitX * (base.Width - 2f) * 0.5f;
                num = (int)(base.Width / 8f) * 4;
            }
            num += 2;
            //level.Particles.Emit(P_Activate, num, position, positionRange, direction);
        }

        private Vector2[] variableSpeeds = new Vector2[]
           {
               new Vector2(5f, 11f),
               new Vector2(9f, 20f),
               new Vector2(15f, 32f),
               new Vector2(32f, 67f)
           };

        private int speedIdx = 0;
        private Vector2 lastCrushDir = new Vector2(0, 0);

        private IEnumerator AttackSequence()
        {
            Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
            StartShaking(0.4f);
            yield return (object)0.4f;
            if (!chillOut)
            {
                canActivate = true;
            }
            base.StopPlayerRunIntoAnimation = false;
            bool slowing = false;
            float speed2 = 0f;
            Vector2 speeds = variableSpeeds[speedIdx];

            while (true)
            {
                if (!chillOut)
                {
                    speed2 = Calc.Approach(speed2, speeds.X, speeds.Y * Engine.DeltaTime);
                }
                else if (slowing || ((Entity)this).CollideCheck<SolidTiles>(base.Position + crushDir * 256f))
                {
                    speed2 = Calc.Approach(speed2, 24f, 500f * Engine.DeltaTime * 0.25f);
                    if (!slowing)
                    {
                        slowing = true;
                        Alarm.Set(this, 0.5f, delegate
                        {
                            face.Play("hurt", false, false);
                            currentMoveLoopSfx.Stop(true);
                            TurnOffImages();
                        }, Alarm.AlarmMode.Oneshot);
                    }
                }
                else
                {
                    speed2 = Calc.Approach(speed2, speeds.X, speeds.Y * Engine.DeltaTime);
                }
                if (!((crushDir.X == 0f) ? MoveVCheck(speed2 * crushDir.Y * Engine.DeltaTime) : MoveHCheck(speed2 * crushDir.X * Engine.DeltaTime)))
                {
                    if (Scene.OnInterval(0.02f))
                    {
                        Vector2 position;
                        float direction;
                        if (crushDir == Vector2.UnitX)
                        {
                            position = new Vector2(Left + 1f, Calc.Random.Range(Top + 3f, Bottom - 3f));
                            direction = 3.14159274f;
                        }
                        else if (crushDir == -Vector2.UnitX)
                        {
                            position = new Vector2(Right - 1f, Calc.Random.Range(Top + 3f, Bottom - 3f));
                            direction = 0f;
                        }
                        else if (crushDir == Vector2.UnitY)
                        {
                            position = new Vector2(Calc.Random.Range(Left + 3f, Right - 3f), Top + 1f);
                            direction = -1.57079637f;
                        }
                        else
                        {
                            position = new Vector2(Calc.Random.Range(Left + 3f, Right - 3f), Bottom - 1f);
                            direction = 1.57079637f;
                        }
                        //level.Particles.Emit(P_Crushing, position, direction);
                    }
                    yield return (object)null;
                    continue;
                }
                break;
            }
            FallingBlock fallingBlock = CollideFirst<FallingBlock>(base.Position + crushDir);
            if (fallingBlock != null)
            {
                fallingBlock.Triggered = true;
            }
            if (crushDir == -Vector2.UnitX)
            {
                Vector2 value = new Vector2(0f, 2f);
                for (int i = 0; (float)i < Height / 8f; i++)
                {
                    Vector2 vector = new Vector2(Left - 1f, Top + 4f + (float)(i * 8));
                    if (!Scene.CollideCheck<Water>(vector) && Scene.CollideCheck<Solid>(vector))
                    {
                        //SceneAs<Level>().ParticlesFG.Emit(P_Impact, vector + value, 0f);
                        //SceneAs<Level>().ParticlesFG.Emit(P_Impact, vector - value, 0f);
                    }
                }
            }
            else if (crushDir == Vector2.UnitX)
            {
                Vector2 value2 = new Vector2(0f, 2f);
                for (int j = 0; (float)j < Height / 8f; j++)
                {
                    Vector2 vector2 = new Vector2(Right + 1f, Top + 4f + (float)(j * 8));
                    if (!Scene.CollideCheck<Water>(vector2) && Scene.CollideCheck<Solid>(vector2))
                    {
                        //SceneAs<Level>().ParticlesFG.Emit(P_Impact, vector2 + value2, 3.14159274f);
                        //SceneAs<Level>().ParticlesFG.Emit(P_Impact, vector2 - value2, 3.14159274f);
                    }
                }
            }
            else if (crushDir == -Vector2.UnitY)
            {
                Vector2 value3 = new Vector2(2f, 0f);
                for (int k = 0; (float)k < Width / 8f; k++)
                {
                    Vector2 vector3 = new Vector2(Left + 4f + (float)(k * 8), Top - 1f);
                    if (!Scene.CollideCheck<Water>(vector3) && Scene.CollideCheck<Solid>(vector3))
                    {
                        //SceneAs<Level>().ParticlesFG.Emit(P_Impact, vector3 + value3, 1.57079637f);
                        //SceneAs<Level>().ParticlesFG.Emit(P_Impact, vector3 - value3, 1.57079637f);
                    }
                }
            }
            else if (crushDir == Vector2.UnitY)
            {
                Vector2 value4 = new Vector2(2f, 0f);
                for (int l = 0; (float)l < Width / 8f; l++)
                {
                    Vector2 vector4 = new Vector2(Left + 4f + (float)(l * 8), Bottom + 1f);
                    if (!Scene.CollideCheck<Water>(vector4) && Scene.CollideCheck<Solid>(vector4))
                    {
                        //SceneAs<Level>().ParticlesFG.Emit(P_Impact, vector4 + value4, -1.57079637f);
                        //SceneAs<Level>().ParticlesFG.Emit(P_Impact, vector4 - value4, -1.57079637f);
                    }
                }
            }
            Audio.Play("event:/game/06_reflection/crushblock_impact", Center);
            level.DirectionalShake(crushDir, 0.3f);
            Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
            StartShaking(0.4f);
            base.StopPlayerRunIntoAnimation = true;
            SoundSource sfx = currentMoveLoopSfx;
            currentMoveLoopSfx.Param("end", 1f);
            currentMoveLoopSfx = null;
            Alarm.Set(this, 0.5f, delegate
            {
                sfx.RemoveSelf();
            }, Alarm.AlarmMode.Oneshot);
            crushDir = Vector2.Zero;
            TurnOffImages();
            if (!chillOut)
            {
                face.Play("hurt", false, false);
                returnLoopSfx.Play("event:/game/06_reflection/crushblock_return_loop", null, 0f);
                yield return (object)0.4f;
                float speed = 0f;
                float waypointSfxDelay = 0f;
                while (returnStack.Count > 0)
                {
                    yield return (object)null;
                    speedIdx = 0;
                    base.StopPlayerRunIntoAnimation = false;
                    MoveState moveState = returnStack[returnStack.Count - 1];
                    speed = Calc.Approach(speed, 60f, 160f * Engine.DeltaTime);
                    waypointSfxDelay -= Engine.DeltaTime;
                    if (moveState.Direction.X != 0f)
                    {
                        MoveTowardsX(moveState.From.X, speed * Engine.DeltaTime);
                    }
                    if (moveState.Direction.Y != 0f)
                    {
                        MoveTowardsY(moveState.From.Y, speed * Engine.DeltaTime);
                    }
                    if ((moveState.Direction.X == 0f || ExactPosition.X == moveState.From.X) && (moveState.Direction.Y == 0f || ExactPosition.Y == moveState.From.Y))
                    {
                        speed = 0f;
                        returnStack.RemoveAt(returnStack.Count - 1);
                        base.StopPlayerRunIntoAnimation = true;
                        if (returnStack.Count <= 0)
                        {
                            face.Play("idle", false, false);
                            returnLoopSfx.Stop(true);
                            if (waypointSfxDelay <= 0f)
                            {
                                Audio.Play("event:/game/06_reflection/crushblock_rest", Center);
                            }
                        }
                        else if (waypointSfxDelay <= 0f)
                        {
                            Audio.Play("event:/game/06_reflection/crushblock_rest_waypoint", Center);
                        }
                        waypointSfxDelay = 0.1f;
                        StartShaking(0.2f);
                        yield return (object)0.2f;
                    }
                }
            }
        }

        private bool MoveHCheck(float amount)
        {
            if (base.MoveHCollideSolidsAndBounds(level, amount, true, null))
            {
                Rectangle bounds;
                if (amount < 0f)
                {
                    float left = base.Left;
                    bounds = level.Bounds;
                    if (left <= (float)bounds.Left)
                    {
                        return true;
                    }
                }
                if (amount > 0f)
                {
                    float right = base.Right;
                    bounds = level.Bounds;
                    if (right >= (float)bounds.Right)
                    {
                        return true;
                    }
                }
                for (int i = 1; i <= 4; i++)
                {
                    for (int num = 1; num >= -1; num -= 2)
                    {
                        Vector2 value = new Vector2((float)Math.Sign(amount), (float)(i * num));
                        if (!base.CollideCheck<Solid>(base.Position + value))
                        {
                            MoveVExact(i * num);
                            MoveHExact(Math.Sign(amount));
                            return false;
                        }
                    }
                }
                return true;
            }
            return false;
        }

        private bool MoveVCheck(float amount)
        {
            if (base.MoveVCollideSolidsAndBounds(level, amount, true, null))
            {
                Rectangle bounds;
                if (amount < 0f)
                {
                    float top = base.Top;
                    bounds = level.Bounds;
                    if (top <= (float)bounds.Top)
                    {
                        return true;
                    }
                }
                if (amount > 0f)
                {
                    float bottom = base.Bottom;
                    bounds = level.Bounds;
                    if (bottom >= (float)bounds.Bottom)
                    {
                        return true;
                    }
                }
                for (int i = 1; i <= 4; i++)
                {
                    for (int num = 1; num >= -1; num -= 2)
                    {
                        Vector2 value = new Vector2((float)(i * num), (float)Math.Sign(amount));
                        if (!base.CollideCheck<Solid>(base.Position + value))
                        {
                            MoveHExact(i * num);
                            MoveVExact(Math.Sign(amount));
                            return false;
                        }
                    }
                }
                return true;
            }
            return false;
        }
    }
}