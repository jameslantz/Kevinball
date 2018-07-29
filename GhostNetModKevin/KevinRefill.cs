// Celeste.Refill
using Celeste;
using Microsoft.Xna.Framework;
using Monocle;
using System.Collections;

namespace Celeste.Mod.GhostKevinball.Net
{
    public class KevinRefill : Entity
    {
        public static ParticleType P_Shatter;

        public static ParticleType P_Regen;

        public static ParticleType P_Glow;

        private Sprite sprite;

        private Sprite flash;

        private Image outline;

        private Wiggler wiggler;

        private BloomPoint bloom;

        private VertexLight light;

        private Level level;

        private SineWave sine;

        private float respawnTimer;

        public KevinRefill(Vector2 position)
            : base(position)
        {
            base.Collider = new Hitbox(16f, 16f, -8f, -8f);
            base.Add(new PlayerCollider(OnPlayer, null, null));
            base.Add(outline = new Image(GFX.Game["objects/refill/outline"]));
            outline.CenterOrigin();
            outline.Visible = false;
            base.Add(sprite = new Sprite(GFX.Game, "objects/kevinRefill/idle"));
            sprite.AddLoop("idle", "", 0.1f);
            sprite.Play("idle", false, false);
            sprite.CenterOrigin();
            base.Add(flash = new Sprite(GFX.Game, "objects/refill/flash"));
            flash.Add("flash", "", 0.05f);
            flash.OnFinish = delegate
            {
                flash.Visible = false;
            };
            flash.CenterOrigin();
            base.Add(wiggler = Wiggler.Create(1f, 4f, delegate (float v)
            {
                sprite.Scale = (flash.Scale = Vector2.One * (1f + v * 0.2f));
            }, false, false));
            base.Add(new MirrorReflection());
            base.Add(bloom = new BloomPoint(0.8f, 16f));
            base.Add(light = new VertexLight(Color.White, 1f, 16, 48));
            base.Add(sine = new SineWave(0.6f));
            sine.Randomize();
            UpdateY();
            base.Depth = -100;
        }

        public KevinRefill(EntityData data, Vector2 offset)
            : this(data.Position + offset)
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
            if (respawnTimer > 0f)
            {
                respawnTimer -= Engine.DeltaTime;
                if (respawnTimer <= 0f)
                {
                    Respawn();
                }
            }
            else if (base.Scene.OnInterval(0.1f))
            {
                //level.ParticlesFG.Emit(P_Glow, 1, base.Position, Vector2.One * 5f);
            }
            UpdateY();
            light.Alpha = Calc.Approach(light.Alpha, sprite.Visible ? 1f : 0f, 4f * Engine.DeltaTime);
            bloom.Alpha = light.Alpha * 0.8f;
            if (base.Scene.OnInterval(2f) && sprite.Visible)
            {
                flash.Play("flash", true, false);
                flash.Visible = true;
            }
        }

        private void Respawn()
        {
            if (!base.Collidable)
            {
                base.Collidable = true;
                sprite.Visible = true;
                outline.Visible = false;
                base.Depth = -100;
                wiggler.Start();
                Audio.Play("event:/game/general/diamond_return", base.Position);
                //level.ParticlesFG.Emit(P_Regen, 16, base.Position, Vector2.One * 2f);
            }
        }

        private void UpdateY()
        {
            Sprite obj = flash;
            Sprite obj2 = sprite;
            BloomPoint bloomPoint = bloom;
            float num2 = bloomPoint.Y = sine.Value * 2f;
            float num5 = obj.Y = (obj2.Y = num2);
        }

        public override void Render()
        {
            if (sprite.Visible)
            {
                sprite.DrawOutline(1);
            }
            base.Render();
        }

        private void OnPlayer(Player player)
        {
            player.Dashes = player.MaxDashes;
            player.RefillStamina();
            if (GhostNetModule.Instance != null && GhostNetModule.Instance.Client != null && GhostNetModule.Instance.Client.Connection != null)
            {
                GhostNetModule.Instance.Client.OnPickupKevinRefill(); 
            }
            Audio.Play("event:/game/general/diamond_touch", base.Position);
            Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
            base.Collidable = false;
            base.Add(new Coroutine(RefillRoutine(player), true));
            respawnTimer = 5.0f;
        }

        public void OnOtherPlayer(Player player)
        {
            Audio.Play("event:/game/general/diamond_touch", base.Position);
            Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
            base.Collidable = false;
            base.Add(new Coroutine(RefillRoutine(player), true));
            respawnTimer = 5.0f;
        }

        private IEnumerator RefillRoutine(Player player)
        {
            //Celeste.Celeste.Freeze(0.05f);
            yield return (object)null;
            level.Shake(0.3f);
            sprite.Visible = (flash.Visible = false);
            outline.Visible = true;
            Depth = 8999;
            yield return (object)0.05f;
            float num = player.Speed.Angle();
            //level.ParticlesFG.Emit(P_Shatter, 5, base.Position, Vector2.One * 4f, num - 1.57079637f);
            //level.ParticlesFG.Emit(P_Shatter, 5, base.Position, Vector2.One * 4f, num + 1.57079637f);
            SlashFx.Burst(base.Position, num);
        }
    }
}