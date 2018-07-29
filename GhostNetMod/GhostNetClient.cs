using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Monocle;
using Celeste; 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Open.Nat;

namespace Celeste.Mod.GhostKevinball.Net {
    public class GhostNetClient : DrawableGameComponent {

        public readonly static Vector2 FontScale = new Vector2(0.4f, 0.4f);

        public GhostNetConnection Connection;

        protected float time;

        protected Queue<Action> MainThreadQueue = new Queue<Action>();

        public int UpdateIndex;

        public const int KevinballWin = 0;
        public const int CoinWin = 1;
        public const int DeathWin = 2; 

        public Player Player;
        public Session Session;
        public GhostRecorder GhostRecorder;
        public GhostName PlayerName;
        public GhostNetEmoteWheel EmoteWheel;

        public uint PlayerID = uint.MaxValue;
        public ChunkMPlayer PlayerInfo;
        public ChunkMServerInfo ServerInfo;

        public Dictionary<uint, ChunkMPlayer> PlayerMap = new Dictionary<uint, ChunkMPlayer>();
        public Dictionary<uint, GhostNetFrame> UpdateMap = new Dictionary<uint, GhostNetFrame>();

        public List<Ghost> Ghosts = new List<Ghost>();
        public Dictionary<uint, Ghost> GhostMap = new Dictionary<uint, Ghost>();
        public Dictionary<uint, GhostNetEmote> IdleMap = new Dictionary<uint, GhostNetEmote>();
        public Dictionary<Ghost, uint> GhostPlayerIDs = new Dictionary<Ghost, uint>();
        public Dictionary<uint, uint> GhostUpdateIndices = new Dictionary<uint, uint>();
        public Dictionary<int, float> GhostDashTimes = new Dictionary<int, float>();

        public List<GhostTouchSwitch> GhostTouchSwitches = new List<GhostTouchSwitch>();
        public bool TouchedGhostSwitch = false;

        public List<MultiplayerControlSwitch> ControlSwitches = new List<MultiplayerControlSwitch>();
        public List<MultiplayerTriggerSpikes> mTriggerSpikes = new List<MultiplayerTriggerSpikes>();

        public Vector2 p1Spawn = new Vector2(80, 140);
        public Vector2 p2Spawn = new Vector2(240, 140);
        public bool firstSpawns = true; 

        public bool PlayingKevinball = false;
        public bool InStartRoutine = false;
        public float StartRoutineTimer = 3f;
        public float StartRoutineState = 4; 

        public uint P1_id = 0;
        public uint P2_id = 1;

        public MultiplayerVariableSpeedCrushBlock crushBlock = null; 

        private bool WasPaused = false;
        public bool FirstGhostSwitch = true;
        public bool FirstControlSwitch = true;
        public bool FirstTriggerSpike = true; 

        public List<ChatLine> ChatLog = new List<ChatLine>();
        public string ChatInput = "";

        public List<string> ChatRepeat = new List<string>() {
            ""
        };
        protected int _ChatRepeatIndex;
        public int ChatRepeatIndex {
            get {
                return _ChatRepeatIndex;
            }
            set {
                if (_ChatRepeatIndex == value)
                    return;

                value = (value + ChatRepeat.Count) % ChatRepeat.Count;

                if (_ChatRepeatIndex == 0 && value != 0) {
                    ChatRepeat[0] = ChatInput;
                }
                ChatInput = ChatRepeat[value];
                _ChatRepeatIndex = value;
            }
        }

        protected bool _ChatVisible;
        protected bool _ChatWasPaused;
        protected Overlay _ChatLevelOverlay;
        protected int _ChatConsumeInput = 0;
        public bool VirtualPause = false; 
        public bool ChatVisible {
            get {
                return _ChatVisible;
            }
            set {
                if (_ChatVisible == value)
                    return;

                if (value) {
                    _ChatWasPaused = VirtualPause;
                    if(!PlayingKevinball)
                    {
                        Engine.Scene.Paused = true;
                        VirtualPause = true;
                    }
                    //MInput.Disabled = true; 
                    // If we're in a level, add a dummy overlay to prevent the pause menu from handling input.
                    //if (Engine.Scene is Level)
                    //    ((Level) Engine.Scene).Overlay = _ChatLevelOverlay = new Overlay();

                    _ChatRepeatIndex = 0;

                } else {
                    ChatInput = "";
                    if(!PlayingKevinball)
                    {
                        Engine.Scene.Paused = _ChatWasPaused;
                        VirtualPause = _ChatWasPaused;
                    }
                    //MInput.Disabled = false;
                    _ChatConsumeInput = 2;
                    //if (_ChatLevelOverlay != null && (Engine.Scene as Level)?.Overlay == _ChatLevelOverlay)
                    //    ((Level) Engine.Scene).Overlay = null;
                }

                _ChatVisible = value;
            }
        }

        protected string PlayerListText;
        public bool PlayerListVisible;

        public static event Action<GhostNetClient> OnCreate;
        public event GhostNetFrameHandler OnHandle;
        // TODO: More events.

        public GhostNetClient(Game game)
            : base(game) {
            OnCreate?.Invoke(this);
        }

        public bool KevinHittable = false;
        public KevinRefill refill = null; 
        public void OnPickupKevinRefill()
        {
            KevinHittable = true;
            SendUPickupKevinRefill(PlayerID); 
        }

        public virtual void SendUPickupKevinRefill(uint id)
        {
            if (Connection == null)
                return;

            Connection.SendUpdate(new GhostNetFrame {
                new ChunkUPickupKevinRefill {
                    With = id
                }
            }, true);
        }

        public Color KevinColor = Calc.HexToColor("ff8a23");

        public override void Update(GameTime gameTime) {
            SendUUpdate();

            lastPlayerDeath += Engine.DeltaTime;

            Level level = Engine.Scene as Level;

            time += Engine.RawDeltaTime;

            bool inputDisabled = MInput.Disabled;
            MInput.Disabled = false;

            if(PlayerShouldDie)
            {
                if(Player != null)
                {
                    Player.Die(new Vector2(0, 0));
                    PlayerShouldDie = false;
                }
            }

            if(KevinHittable && Player != null && Player.Dashes > 0)
            {
                Player.Hair.Color = KevinColor; 
            }

            if(PlayingKevinball)
            {
                if (crushBlock != null)
                {
                    if (crushBlock.CollideCheck<KevinballEndzoneTrigger>())
                    {
                        if (crushBlock.Position.X < 200f)
                        {
                            SendUKevinballWin(P1_id, P2_id, KevinballWin);
                        }
                        else
                        {
                            SendUKevinballWin(P2_id, P1_id, KevinballWin);
                        }
                    }
                }

                int p1Count = 0;
                int p2Count = 0; 
                foreach(MultiplayerControlSwitch mSwitch in ControlSwitches)
                {
                    if (mSwitch.currentController == MultiplayerControlSwitch.Controller.P1)
                        p1Count++;
                    else if (mSwitch.currentController == MultiplayerControlSwitch.Controller.P2)
                        p2Count++;
                }

                if(p1Count >= ControlSwitches.Count)
                {
                    SendUKevinballWin(P1_id, P2_id, CoinWin);
                }
                else if(p2Count >= ControlSwitches.Count)
                {
                    SendUKevinballWin(P2_id, P1_id, CoinWin);
                }
            }

            if(InStartRoutine)
            {
                StartRoutineTimer -= Engine.DeltaTime; 
                if(StartRoutineTimer <= 0)
                {
                    InStartRoutine = false;
                    Player.StateMachine.ForceState(Player.StNormal);
                }

                if(StartRoutineState == 4 && StartRoutineTimer <= 3f)
                {
                    //string text = "Starting in 3...";
                    //ChatLog.Insert(0, new ChatLine(uint.MaxValue, PlayerID, "", "KEVINBALL", text));
                    //ChatRepeat.Insert(1, text);
                    StartRoutineState = 3; 
                }

                if (StartRoutineState == 3 && StartRoutineTimer <= 2f)
                {
                    //string text = "Starting in 2...";
                    //ChatLog.Insert(0, new ChatLine(uint.MaxValue, PlayerID, "", "KEVINBALL", text));
                    //ChatRepeat.Insert(1, text);
                    StartRoutineState = 2;
                }

                if (StartRoutineState == 2 && StartRoutineTimer <= 1f)
                {
                    //string text = "Starting in 1...";
                    //ChatLog.Insert(0, new ChatLine(uint.MaxValue, PlayerID, "", "KEVINBALL", text));
                    //ChatRepeat.Insert(1, text);
                    StartRoutineState = 1;
                }

                if (StartRoutineState == 1 && StartRoutineTimer <= 0f)
                {
                    //string text = "KEVINBALL!!";
                    //ChatLog.Insert(0, new ChatLine(uint.MaxValue, PlayerID, "", "KEVINBALL", text));
                    //ChatRepeat.Insert(1, text);
                    StartRoutineState = 0;
                }

            }

            if (PlayerID != uint.MaxValue && (Player?.Scene?.Paused ?? false) != WasPaused) {
                SendMPlayer();
                if (Player != null)
                    OnIdle(PlayerID, Player, Player?.Scene?.Paused ?? false);
                WasPaused = Player?.Scene?.Paused ?? false;
            }

            if (!(Player?.Scene?.Paused ?? true)) {
                EmoteWheel.Shown = GhostNetModule.Instance.JoystickEmoteWheel.Value.LengthSquared() >= 0.36f;
                if (EmoteWheel.Shown && EmoteWheel.Selected != -1 && GhostNetModule.Instance.ButtonEmoteSend.Pressed) {
                    SendMEmote(EmoteWheel.Selected);
                }
            } else if (EmoteWheel != null) {
                EmoteWheel.Shown = false;
                EmoteWheel.Selected = -1;
            }

            if (!(VirtualPause == true)) {
                string input = ChatInput;
                ChatVisible = false;
                ChatInput = input;
            }

            if (!ChatVisible && GhostNetModule.Instance.ButtonChat.Pressed) {
                ChatVisible = true;

            } else if (ChatVisible) {
                Engine.Commands.Open = false;
                //Engine.Commands.Enabled = false;

                if (MInput.Keyboard.Pressed(Keys.Enter)) {
                    SendMChat(ChatInput);
                    ChatVisible = false;

                } else if (MInput.Keyboard.Pressed(Keys.Down) && ChatRepeatIndex > 0) {
                    ChatRepeatIndex--;
                } else if (MInput.Keyboard.Pressed(Keys.Up) && ChatRepeatIndex < ChatRepeat.Count - 1) {
                    ChatRepeatIndex++;

                } else if (Input.ESC.Pressed || Input.Pause.Pressed) {
                    ChatVisible = false;
                }
            }

            if (!ChatVisible) {
                //Engine.Commands.Enabled = true; 
                if (MInput.Keyboard.Pressed(Keys.D1))
                    SendMEmote(0);
                else if (MInput.Keyboard.Pressed(Keys.D2))
                    SendMEmote(1);
                else if (MInput.Keyboard.Pressed(Keys.D3))
                    SendMEmote(2);
                else if (MInput.Keyboard.Pressed(Keys.D4))
                    SendMEmote(3);
                else if (MInput.Keyboard.Pressed(Keys.D5))
                    SendMEmote(4);
                else if (MInput.Keyboard.Pressed(Keys.D6))
                    SendMEmote(5);
                else if (MInput.Keyboard.Pressed(Keys.D7))
                    SendMEmote(6);
                else if (MInput.Keyboard.Pressed(Keys.D8))
                    SendMEmote(7);
                else if (MInput.Keyboard.Pressed(Keys.D9))
                    SendMEmote(8);
                else if (MInput.Keyboard.Pressed(Keys.D0))
                    SendMEmote(9);
            }

            // Prevent the menus from reacting to player input after exiting the chat.
            if (_ChatConsumeInput > 0) {
                Input.MenuConfirm.ConsumeBuffer();
                Input.MenuConfirm.ConsumePress();
                Input.ESC.ConsumeBuffer();
                Input.ESC.ConsumePress();
                Input.Pause.ConsumeBuffer();
                Input.Pause.ConsumePress();
                _ChatConsumeInput--;
            }

            if (!(Player?.Scene?.Paused ?? false) && GhostNetModule.Instance.ButtonPlayerList.Pressed)
                PlayerListVisible = !PlayerListVisible;

            MInput.Disabled = inputDisabled;

            // Update ghosts even if the game is paused.

            if (level != null && level.FrozenOrPaused) {
                level.Particles.Update();
                level.ParticlesFG.Update();
                level.ParticlesBG.Update();
                TrailManager trailManager = Engine.Scene.Tracker.GetEntity<TrailManager>();
                if (trailManager != null) {
                    TrailManager.Snapshot[] snapshots = trailManager.GetSnapshots();
                    for (int i = 0; i < snapshots.Length; i++) {
                        TrailManager.Snapshot snapshot = snapshots[i];
                        if (snapshot == null)
                            continue;
                        snapshot.Update();
                    }
                }
            }

            for (int i = 0; i < Ghosts.Count; i++) {
                Ghost ghost = Ghosts[i];
                if (ghost == null)
                    continue;

                ghost.Update();

                if (level?.FrozenOrPaused ?? true)
                    ghost.Hair?.AfterUpdate();

                if (ghost.Frame.Data.DashColor != null) {
                    float dashTime;
                    if (!GhostDashTimes.TryGetValue(i, out dashTime)) {
                        CreateTrail(ghost);
                        GhostDashTimes[i] = 0.08f;
                    } else {
                        dashTime -= Engine.RawDeltaTime;
                        if (dashTime <= 0f) {
                            CreateTrail(ghost);
                            dashTime += 0.08f;
                        }
                        GhostDashTimes[i] = dashTime;
                    }

                    if (level != null && ghost.Frame.Data.Speed != Vector2.Zero && level.OnInterval(0.02f)) {
                        level.ParticlesFG.Emit(ghost.Frame.Data.DashWasB ? Player.P_DashB : Player.P_DashA, ghost.Center + Calc.Random.Range(Vector2.One * -2f, Vector2.One * 2f), ghost.Frame.Data.DashDir.Angle());
                    }
                } else if (GhostDashTimes.ContainsKey(i)) {
                    GhostDashTimes.Remove(i);
                }
            }

            while (MainThreadQueue.Count > 0)
                MainThreadQueue.Dequeue()();

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime) {
            base.Draw(gameTime);

            int viewWidth = Engine.ViewWidth;
            int viewHeight = Engine.ViewHeight;

            Monocle.Draw.SpriteBatch.Begin();

            if (ChatVisible) {
                Monocle.Draw.Rect(10f, viewHeight - 50f, viewWidth - 20f, 40f, Color.Black * 0.8f);

                string text = ">" + ChatInput;
                if (Calc.BetweenInterval(time, 0.5f))
                    text += "_";
                ActiveFont.Draw(
                    text,
                    new Vector2(20f, viewHeight - 42f),
                    Vector2.Zero,
                    FontScale,
                    Color.White
                );
            }

            lock (ChatLog) {
                if (ChatLog.Count > 0) {
                    DateTime now = DateTime.UtcNow;
                    float y = viewHeight - 20f;
                    if (ChatVisible)
                        y -= 40f + 2f;
                    for (int i = 0; i < ChatLog.Count && i < GhostNetModule.Settings.ChatLogLength; i++) {
                        ChatLine line = ChatLog[i];

                        float alpha = 1f;

                        float delta = (float) (now - line.Date).TotalSeconds;
                        if (!ChatVisible && delta > 3f) {
                            alpha = 1f - Ease.CubeIn(delta - 3f);
                        }

                        if (alpha <= 0f)
                            continue;

                        string text = line.ToString();
                        Vector2 size = ActiveFont.Measure(text) * FontScale;
                        float height = 20f + size.Y;

                        y -= height;

                        Monocle.Draw.Rect(10f, y, size.X + 20f, height, Color.Black * 0.8f * alpha);
                        ActiveFont.Draw(
                            text,
                            new Vector2(20f, y + 10f),
                            Vector2.Zero,
                            FontScale,
                            line.Color * alpha * (line.Unconfirmed ? 0.6f : 1f)
                        );
                    }
                }
            }

            if (PlayerListVisible) {
                if (PlayerListText == null)
                    RebuildPlayerList();
                float y = 0f;
                if ((Settings.Instance?.SpeedrunClock ?? SpeedrunType.Off) != SpeedrunType.Off) {
                    y += 192f * (viewHeight / 1920f);
                }
                Vector2 size = Monocle.Draw.DefaultFont.MeasureString(PlayerListText);
                Monocle.Draw.Rect(10f, 10f + y, size.X + 20f, size.Y + 20f, Color.Black * 0.8f);
                Monocle.Draw.SpriteBatch.DrawString(
                    Monocle.Draw.DefaultFont,
                    PlayerListText,
                    new Vector2(20f, 20f + y),
                    Color.White
                );
            }

            Monocle.Draw.SpriteBatch.End();
        }

        protected void RunOnMainThread(Action action, bool wait = false) {
            bool finished = true;
            if (wait) {
                finished = false;
                action = new Func<Action, Action>(_ => () => {
                    try {
                        _();
                    } finally {
                        finished = true;
                    }
                })(action);
            }

            MainThreadQueue.Enqueue(action);

            while (!finished)
                Thread.Sleep(0);
        }

        protected virtual void RebuildPlayerList() {
            if (Monocle.Draw.DefaultFont == null)
                return;

            StringBuilder builder = new StringBuilder();
            lock (PlayerMap) {
                foreach (KeyValuePair<uint, ChunkMPlayer> player in PlayerMap) {
                    if (string.IsNullOrWhiteSpace(player.Value.Name))
                        continue;
                    builder
                        .Append(Escape(player.Value.Name, Monocle.Draw.DefaultFont))
                        .Append("#")
                        .Append(player.Key)
                    ;
                    if (!string.IsNullOrWhiteSpace(player.Value.SID)) {
                        builder
                            .Append(" @ ")
                            .Append(Escape(AreaDataExt.Get(player.Value.SID)?.Name?.DialogCleanOrNull(Dialog.Languages["english"]) ?? player.Value.SID, Monocle.Draw.DefaultFont))
                            .Append(" ")
                            .Append((char) ('A' + (int) player.Value.Mode))
                            .Append(" ")
                            .Append(Escape(player.Value.Level, Monocle.Draw.DefaultFont))
                        ;
                    }
                    builder.AppendLine();
                }
            }
            PlayerListText = builder.ToString().Trim();
        }

        public static string Escape(string text, SpriteFont font) {
            StringBuilder escaped = new StringBuilder();
            for (int i = 0; i < text.Length; i++) {
                char c = text[i];
                if (!font.Characters.Contains(c) && c != '\n')
                    c = ' ';
                escaped.Append(c);
            }
            return escaped.ToString();
        }

        public virtual Ghost AddGhost(GhostNetFrame frame) {
            Ghost ghost = new Ghost(Player);
            ghost.AddTag(Tags.Persistent | Tags.TransitionUpdate | Tags.FrozenUpdate);
            ghost.Name.AddTag(Tags.Persistent | Tags.TransitionUpdate | Tags.FrozenUpdate);

            ghost.Collidable = true;

            ghost.Collider = new Hitbox(8f, 11f, -4f, -11f);
            ghost.Add(new PlayerCollider(OnPlayerTouchGhost(ghost)));

            Engine.Scene.Add(ghost);
            lock (GhostMap) {
                GhostMap[frame.HHead.PlayerID] = ghost;
            }
            lock (GhostPlayerIDs) {
                GhostPlayerIDs[ghost] = frame.HHead.PlayerID;
            }
            Ghosts.Add(ghost);
            return ghost;
        }

        public bool InKevinballMap = false;

        public virtual void LoadedKevinballLevel(Level level)
        {
            PlayingKevinball = false;
            KevinHittable = false;
            //level.PauseLock = true; 
            InKevinballMap = true;
            SendULoadedKevinball(PlayerID);
        }

        public virtual void StartKevinballRoutine(uint player1, uint player2)
        {
            P1_id = player1;
            P2_id = player2; 

            if (PlayerID != player1 && PlayerID != player2)
                return; 

            PlayingKevinball = true;
            InStartRoutine = true;
            StartRoutineTimer = 2f;
            StartRoutineState = 3;

            if (player1 == PlayerID)
            {
                Player.Center = p1Spawn;
                Player.StateMachine.ForceState(Player.StFrozen);
            }

            else if (player2 == PlayerID)
            {
                Player.Center = p2Spawn;
                Player.StateMachine.ForceState(Player.StFrozen);
            }

            string text = "KEVINBALL!!";
            ChatLog.Insert(0, new ChatLine(uint.MaxValue, PlayerID, "", "KEVINBALL", text));
            ChatRepeat.Insert(1, text);
        }

        public virtual void AddGhostTouch(GhostTouchSwitch touch)
        {
            if(FirstGhostSwitch)
            {
                FirstGhostSwitch = false;
                GhostTouchSwitches = new List<GhostTouchSwitch>(); 
            }

            touch.Add(new PlayerCollider(OnPlayerTouchSwitch(touch), null, new Hitbox(30f, 30f, -15f, -15f)));
            touch.tIdx = GhostTouchSwitches.Count;
            GhostTouchSwitches.Add(touch);
        }

        public virtual void AddControlSwitch(MultiplayerControlSwitch control)
        {
            if (FirstControlSwitch)
            {
                FirstControlSwitch = false;
                ControlSwitches = new List<MultiplayerControlSwitch>();
            }

            control.tIdx = ControlSwitches.Count;
            ControlSwitches.Add(control);
        }

        public virtual void AddTriggerSpikes(MultiplayerTriggerSpikes spikes)
        {
            if (FirstTriggerSpike)
            {
                FirstTriggerSpike = false;
                mTriggerSpikes = new List<MultiplayerTriggerSpikes>();
            }

            spikes.tIdx = mTriggerSpikes.Count;
            mTriggerSpikes.Add(spikes);
        }

        public virtual Action<Player> OnPlayerTouchSwitch(GhostTouchSwitch touch)
            => player =>
        {
            TouchedGhostSwitch = true; 
            SendUTouchPress((uint)touch.tIdx, PlayerID); 
        };

        public virtual void OnHitMultiplayerCrush(Vector2 dir, MultiplayerVariableSpeedCrushBlock block)
        {
            KevinHittable = false; 
            SendUCrushHit(dir, PlayerID);
        }

        public virtual void OnTriggerMultiplayerSpikes(MultiplayerTriggerSpikes spikes, int infoIdx)
        {
            SendUSpikesTrigger((uint)spikes.tIdx, PlayerID, (uint)infoIdx);
        }

        public virtual void OnTriggerMultiplayerControlSwitch(MultiplayerControlSwitch cSwitch)
        {
            SendUControlPress((uint)cSwitch.tIdx, PlayerID);
        }

        public virtual Action<Player> OnPlayerTouchGhost(Ghost ghost)
            => player => {
                if (!GhostNetModule.Settings.Collision)
                    return;

                bool head = false;

                if (player.StateMachine.State == Player.StNormal &&
                    player.Speed.Y > 0f && player.Bottom <= ghost.Top + 3f) {
                    int dashes = player.Dashes;
                    float stamina = player.Stamina;

                    OnJumpedOnHead(player, true, false);

                    player.Bounce(ghost.Top + 2f);

                    // If we ever want to _not_ refil dashes and stamina (again)...
                    // player.Dashes = dashes;
                    // player.Stamina = stamina;
                    head = true;
                }

                // In a perfect world, the server would see and handle the collision, and we would receive the following.
                // Right now, though, GhostNetMod doesn't have a dedicated server handling each player's state.
                uint otherID;
                if (GhostPlayerIDs.TryGetValue(ghost, out otherID)) {
                    SendUActionCollision(otherID, head);
                }
            };

        public void OnJumpedOnHead(Actor who, bool isPlayer, bool withPlayer) {
            Audio.Play("event:/game/general/thing_booped", who.Center).setVolume(0.7f);

            Level level = Engine.Scene as Level;

            Dust.Burst(who.BottomCenter, -1.57079637f, 8);

            if (isPlayer || withPlayer) {
                level?.DirectionalShake(Vector2.UnitY, 0.05f);

                Input.Rumble(RumbleStrength.Light, RumbleLength.Medium);
            }
        }

        protected virtual void OnIdle(uint id, Actor who, bool idle) {
            GhostNetEmote emote;
            if (idle) {
                if (IdleMap.TryGetValue(id, out emote)) {
                    emote.PopOut = true;
                    emote.AnimationTime = 1f;
                }

                IdleMap[id] = emote = new GhostNetEmote(who, "i:hover/idle") {
                    PopIn = true,
                    Float = true
                };
                emote.AddTag(Tags.Persistent | Tags.TransitionUpdate | Tags.FrozenUpdate);
                Engine.Scene.Add(emote);

            } else if (IdleMap.TryGetValue(id, out emote)) {
                emote.PopOut = true;
                emote.AnimationTime = 1f;
            }
        }

        public virtual void CreateTrail(Ghost ghost) {
            TrailManager.Add(ghost, ghost.Frame.Data.DashColor ?? Color.Transparent, 1f);
        }

        #region Frame Senders

        public virtual void SendMPlayer(LevelExit.Mode? levelExit = null, bool levelCompleted = false) {
            if (Connection == null)
                return;
            Connection.SendManagement(new GhostNetFrame {
                MPlayer = new ChunkMPlayer {
                    Name = GhostModule.Settings.Name,
                    SID = Session?.Area.GetSID() ?? "",
                    Mode = Session?.Area.Mode ?? AreaMode.Normal,
                    Level = Session?.Level ?? "",
                    LevelCompleted = levelCompleted,
                    LevelExit = levelExit,
                    Idle = Player?.Scene?.Paused ?? false
                }
            }, true);
        }

        public virtual void SendMEmote(int index) {
            string[] emotes = GhostNetModule.Settings.EmoteFavs;
            if (index < 0 || emotes.Length <= index)
                return;
            SendMEmote(emotes[index]);
        }

        public virtual void SendMEmote(string value) {
            if (string.IsNullOrWhiteSpace(value))
                return;
            Connection?.SendManagement(new GhostNetFrame {
                new ChunkMEmote {
                    Value = value.Trim()
                }
            }, true);
        }

        public virtual void SendMChat(string text) {
            text = text?.TrimEnd();
            if (string.IsNullOrWhiteSpace(text))
                return;
            ChatLog.Insert(0, new ChatLine(uint.MaxValue, PlayerID, "", PlayerInfo?.Name ?? GhostModule.Settings.Name, text));
            ChatRepeat.Insert(1, text);
            Connection?.SendManagement(new GhostNetFrame {
                new ChunkMChat {
                    Text = text
                }
            }, true);
        }

        public virtual void SendUUpdate() {
            if (Connection == null || GhostRecorder == null)
                return;

            if ((UpdateIndex % (GhostNetModule.Settings.SendFrameSkip + 1)) != 0)
                return;

            GhostNetFrame frame = new GhostNetFrame {
                UUpdate = new ChunkUUpdate {
                    UpdateIndex = (uint) UpdateIndex,
                    Data = GhostRecorder.LastFrameData.Data
                }
            };

            ChunkUUpdate update = frame.UUpdate;
            if (Player != null && Player.Sprite != null && Player.Hair != null) {
                int hairCount = Player.Sprite.HairCount;
                update.HairColors = new Color[hairCount];
                for (int i = 0; i < hairCount; i++)
                    update.HairColors[i] = Player.Hair.GetHairColor(i);
                update.HairTextures = new string[hairCount];
                for (int i = 0; i < hairCount; i++)
                    update.HairTextures[i] = Player.Hair.GetHairTexture(i).AtlasPath;
            }

            Connection.SendUpdate(frame, true);

            UpdateIndex++;
        }

        public virtual void SendUPlayerDeath(uint with)
        {
            if (Connection == null)
                return;

            if(PlayingKevinball)
            {
                uint winner = P1_id;
                uint loser = P2_id;
                uint wintype = DeathWin;

                if(with == P1_id)
                {
                    winner = P2_id;
                    loser = P1_id; 
                }

                Connection.SendUpdate(new GhostNetFrame {
                    new ChunkUKevinballWin {
                        Winner = winner, 
                        Loser = loser,
                        WinType = wintype
                    }
                }, true);
            }
            else
            {
                Connection.SendUpdate(new GhostNetFrame {
                    new ChunkUPlayerDeath {
                        With = with
                    }
                }, true);
            }
        }

        public virtual void SendUKevinballWin(uint winner, uint loser, uint winType)
        {
            if (Connection == null)
                return;

            Connection.SendUpdate(new GhostNetFrame {
                new ChunkUKevinballWin {
                    Winner = winner,
                    Loser = loser,
                    WinType = winType
                }
            }, true);
        }

        public virtual void SendUActionCollision(uint with, bool head) {
            if (Connection == null)
                return;

            Connection.SendUpdate(new GhostNetFrame {
                new ChunkUActionCollision {
                    With = with,
                    Head = head
                }
            }, true);
        }

        public virtual void SendUTouchPress(uint tIdx, uint with)
        {
            if (Connection == null)
                return;



            GhostNetFrame frame = new GhostNetFrame {
                new ChunkUTouchPress {
                    TIdx = tIdx,
                    With = with,
                }
            };

            Connection.SendUpdate(frame, true, true);
        }

        public virtual void SendUCrushHit(Vector2 dir, uint with)
        {
            if (Connection == null)
                return;

            uint bDir = 100;
            if (dir.X <= 0)
                bDir = 0;
            else
                bDir = 1;

            GhostNetFrame frame = new GhostNetFrame {
                new ChunkUCrushHit {
                    With = with,
                    Dir = bDir
                }
            };

            Connection.SendUpdate(frame, true, true);
        }

        public virtual void SendUSpikesTrigger(uint tIdx, uint with, uint infoIdx)
        {
            if (Connection == null)
                return;



            GhostNetFrame frame = new GhostNetFrame {
                new ChunkUSpikeTrigger {
                    TIdx = tIdx,
                    With = with,
                    SInfo = infoIdx
                }
            };

            Connection.SendUpdate(frame, true, true);
        }

        public virtual void SendUControlPress(uint tIdx, uint with)
        {
            if (Connection == null)
                return;



            GhostNetFrame frame = new GhostNetFrame {
                new ChunkUControlPress {
                    TIdx = tIdx,
                    With = with,
                }
            };

            Connection.SendUpdate(frame, true, true);
        }

        public virtual void SendUAudio(Player player, string sound, string param = null, float value = 0f) {
            if (Connection == null)
                return;

            Connection.SendUpdate(new GhostNetFrame {
                new ChunkUAudioPlay {
                    Sound = sound,
                    Param = param,
                    Value = value,

                    Position = player.Center
                }
            }, true);
        }

        public virtual void SendMSession() {
            if (Connection == null)
                return;
            if (Session == null) {
                Connection.SendManagement(new GhostNetFrame {
                    new ChunkMSession {
                        InSession = false
                    }
                }, true);
                return;
            }
            Connection.SendManagement(new GhostNetFrame {
                new ChunkMSession {
                    InSession = true,

                    Audio = Session.Audio?.Clone(),
                    RespawnPoint = Session.RespawnPoint,
                    Inventory = Session.Inventory,
                    Flags = new HashSet<string>(Session.Flags ?? new HashSet<string>()),
                    LevelFlags = new HashSet<string>(Session.LevelFlags ?? new HashSet<string>()),
                    Strawberries = new HashSet<EntityID>(Session.Strawberries ?? new HashSet<EntityID>()),
                    DoNotLoad = new HashSet<EntityID>(Session.DoNotLoad ?? new HashSet<EntityID>()),
                    Keys = new HashSet<EntityID>(Session.Keys ?? new HashSet<EntityID>()),
                    Counters = new List<Session.Counter>(Session.Counters ?? new List<Session.Counter>()),
                    FurthestSeenLevel = Session.FurthestSeenLevel,
                    StartCheckpoint = Session.StartCheckpoint,
                    ColorGrade = Session.ColorGrade,
                    SummitGems = Session.SummitGems,
                    FirstLevel = Session.FirstLevel,
                    Cassette = Session.Cassette,
                    HeartGem = Session.HeartGem,
                    Dreaming = Session.Dreaming,
                    GrabbedGolden = Session.GrabbedGolden,
                    HitCheckpoint = Session.HitCheckpoint,
                    LightingAlphaAdd = Session.LightingAlphaAdd,
                    BloomBaseAdd = Session.BloomBaseAdd,
                    DarkRoomAlpha = Session.DarkRoomAlpha,
                    Time = Session.Time,
                    CoreMode = Session.CoreMode
                }
            }, true);
        }

        public virtual void SendRListAreas() {
            if (Connection == null)
                return;
            Connection.SendManagement(new GhostNetFrame {
                new ChunkRListAreas {
                    Entries = AreaData.Areas.Select(area => area.GetSID()).ToArray()
                }
            } , true);
        }

        public virtual void SendRListMods() {
            if (Connection == null)
                return;
            Connection.SendManagement(new GhostNetFrame {
                new ChunkRListMods {
                    Entries = Everest.Modules.Select(module => new ChunkRListMods.Entry {
                        Name = module.Metadata.Name,
                        Version = module.Metadata.Version
                    }).ToArray()
                }
            } , true);
        }

        #endregion

        #region Frame Handlers

        public virtual void Handle(GhostNetConnection con, GhostNetFrame frame) {


            if (frame.HHead == null)
            return;

            if (frame.Has<ChunkMServerInfo>()) {
                // The client can receive this more than once.
                frame.Get(out ServerInfo);
                Logger.Log("ghostnet-c", $"Received MServerInfo: #{frame.HHead.PlayerID} in {ServerInfo.Name}");
                PlayerID = frame.HHead.PlayerID;
            }

            if (frame.MPlayer != null)
                HandleMPlayer(con, frame);

            if (frame.MPlayer == null) {
                ChunkMPlayer player;
                if (!PlayerMap.TryGetValue(frame.HHead.PlayerID, out player) || player == null) {
                    // Ghost not managed, possibly the server.
                }
                // Temporarily attach the MPlayer chunk to make player identification easier.
                if (frame.MPlayer == null && player != null) {
                    frame.MPlayer = player;
                    frame.MPlayer.IsCached = true;
                }
            }

            if (frame.Has<ChunkMRequest>())
                HandleMRequest(con, frame);

            if (frame.Has<ChunkMEmote>())
                HandleMEmote(con, frame);

            if (frame.Has<ChunkMChat>())
                HandleMChat(con, frame);

            if (frame.Has<ChunkMKevinballStart>())
                HandleMKevinballStart(con, frame);

            if (frame.Has<ChunkMKevinballEnd>())
                HandleMKevinballEnd(con, frame);

            if (frame.Has<ChunkMKevinballShuffle>())
                HandleMKevinballShuffle(con, frame); 

            if (frame.Has<ChunkUPickupKevinRefill>())
                HandleUPickupKevinRefill(con, frame);

            if (frame.UUpdate != null)
            {
                HandleUUpdate(con, frame);
            }

            if (frame.Has<ChunkUActionCollision>())
                HandleUActionCollision(con, frame);

            if (frame.Has<ChunkUTouchPress>())
            {
                HandleUTouchPress(con, frame);
            }

            if (frame.Has<ChunkUControlPress>())
            {
                HandleUControlPress(con, frame);
            }

            if (frame.Has<ChunkUSpikeTrigger>())
            {
                HandleUSpikeTrigger(con, frame);
            }

            if (frame.Has<ChunkUCrushHit>())
            {
                HandleUCrushHit(con, frame);
            }

            if (frame.Has<ChunkUPlayerDeath>())
            {
                HandleUPlayerDeath(con, frame);
            }

            if (frame.Has<ChunkUAudioPlay>())
                HandleUAudioPlay(con, frame);

            if (frame.Has<ChunkUParticles>())
                HandleUParticles(con, frame);

            OnHandle?.Invoke(con, frame);
        }

        public virtual void HandleMPlayer(GhostNetConnection con, GhostNetFrame frame) {
            // Logger.Log(LogLevel.Verbose, "ghostnet-c", $"Received nM0 from #{frame.PlayerID} ({con.EndPoint})");
            Logger.Log(LogLevel.Info, "ghostnet-c", $"#{frame.HHead.PlayerID} {frame.MPlayer.Name} in {frame.MPlayer.SID} {(char) ('A' + frame.MPlayer.Mode)} {frame.MPlayer.Level}");

            lock (PlayerMap) {
                PlayerMap[frame.HHead.PlayerID] = frame.MPlayer;
            }
            RebuildPlayerList();

            if (Engine.Instance == null)
                return;

            if (frame.HHead.PlayerID == PlayerID) {
                // Server told us to move... or just told us about our proper name.
                PlayerInfo = frame.MPlayer;
                if (PlayerName != null)
                    PlayerName.Name = frame.MPlayer.Name;

                if (frame.MPlayer.IsEcho) {
                    // If we're receiving a MPlayer after having sent one, ignore it.
                    // This fixes the client being thrown back if the player moves too quickly between rooms.
                    return;
                }

                ChunkMSession sessionToApply = frame;
                if (sessionToApply != null && !sessionToApply.InSession)
                    sessionToApply = null;

                Logger.Log(LogLevel.Info, "ghostnet-c", $"Server told us to move to {frame.MPlayer.SID} {(char) ('A' + frame.MPlayer.Mode)} {frame.MPlayer.Level}");
                if (frame.MPlayer.SID != (Session?.Area.GetSID() ?? "") ||
                    frame.MPlayer.Mode != (Session?.Area.Mode ?? AreaMode.Normal) ||
                    frame.MPlayer.Level != (Session?.Level ?? "") ||
                    sessionToApply != null) {
                    // Server told us to move.

                    RunOnMainThread(() => {
                        if (SaveData.Instance == null) {
                            SaveData.InitializeDebugMode();
                        }
                    }, true);

                    AreaData area = AreaDataExt.Get(frame.MPlayer.SID);
                    if (area != null) {
                        RunOnMainThread(() => {
                            if (Session != null) {
                                Session.RespawnPoint = null;
                            }

                            if (frame.MPlayer.SID != (Session?.Area.GetSID() ?? "") ||
                                frame.MPlayer.Mode != (Session?.Area.Mode ?? AreaMode.Normal) ||
                                sessionToApply != null) {
                                // Different SID or mode - create new session.
                                Session = new Session(SaveData.Instance.LastArea = area.ToKey(frame.MPlayer.Mode), null, SaveData.Instance.Areas[area.ID]);
                                if (sessionToApply != null) {
                                    HandleMSession(con, frame);
                                    sessionToApply = null;
                                }
                                SaveData.Instance.CurrentSession = Session;
                            }

                            if (!string.IsNullOrEmpty(frame.MPlayer.Level) && Session.MapData.Get(frame.MPlayer.Level) != null) {
                                Session.Level = frame.MPlayer.Level;
                                Session.FirstLevel = false;
                            }

                            LevelEnterExt.ErrorMessage = null;
                            Cleanup();
                            Engine.Scene = new LevelLoader(Session, frame.UUpdate?.Data.Position);
                        }, true);

                    } else {
                        RunOnMainThread(() => {
                            OnExitLevel(null, null, LevelExit.Mode.SaveAndQuit, null, null);

                            string message = Dialog.Get("postcard_levelgone");
                            if (string.IsNullOrEmpty(frame.MPlayer.SID)) {
                                message = Dialog.Has("postcard_ghostnetmodule_backtomenu") ? Dialog.Get("postcard_ghostnetmodule_backtomenu") :
@"The server has sent you back to the main menu.";
                            }

                            message = message.Replace("((player))", SaveData.Instance.Name);
                            message = message.Replace("((sid))", frame.MPlayer.SID);

                            LevelEnterExt.ErrorMessage = message;
                            LevelEnter.Go(new Session(new AreaKey(1).SetSID("")), false);
                        });
                        return;
                    }
                }

                if (sessionToApply != null) {
                    // We received additional session data from the server.
                    HandleMSession(con, frame);
                    sessionToApply = null;
                }

                return;
            }

            Level level = Engine.Scene as Level;
            if (level == null || Session == null)
                return;

            Ghost ghost;

            if (frame.MPlayer.SID != Session.Area.GetSID() ||
                frame.MPlayer.Mode != Session.Area.Mode) {
                // Ghost not in the same level.
                // Find the ghost and remove it if it exists.
                if (GhostMap.TryGetValue(frame.HHead.PlayerID, out ghost) && ghost != null && ghost.Scene == Engine.Scene) {
                    ghost.RemoveSelf();
                    GhostMap[frame.HHead.PlayerID] = null;
                    int index = Ghosts.IndexOf(ghost);
                    if (index != -1) {
                        Ghosts[index] = null;
                        lock (GhostPlayerIDs) {
                            GhostPlayerIDs.Remove(ghost);
                        }
                    }
                }
                return;
            }

            if (!GhostMap.TryGetValue(frame.HHead.PlayerID, out ghost) || ghost == null || ghost.Scene != Engine.Scene) {
                // No ghost for the player existing.
                // Create a new ghost for the player.
                ghost = AddGhost(frame);
            }

            if (ghost != null) {
                if (ghost.Name != null)
                    ghost.Name.Name = frame.MPlayer.Name;

                OnIdle(frame.HHead.PlayerID, ghost, frame.MPlayer.Idle);
            }
        }

        public virtual void HandleMRequest(GhostNetConnection con, GhostNetFrame frame) {
            // TODO: Event for request by server in client.
            switch (frame.Get<ChunkMRequest>().ID) {
                case ChunkMPlayer.ChunkID:
                    SendMPlayer();
                    break;

                case ChunkUUpdate.ChunkID:
                    SendUUpdate();
                    break;

                case ChunkMSession.ChunkID:
                    SendMSession();
                    break;
                
                case ChunkRListAreas.ChunkID:
                    SendRListAreas();
                    break;
                case ChunkRListMods.ChunkID:
                    SendRListMods();
                    break;

                default:
                    break;
            }
        }

        public virtual void HandleMEmote(GhostNetConnection con, GhostNetFrame frame) {
            if (Player?.Scene == null)
                return;

            // Logger.Log(LogLevel.Info, "ghostnet-c", $"#{frame.HHead.PlayerID} emote: {frame.MEmote.Value}");

            Ghost ghost = null;
            if (frame.HHead.PlayerID != PlayerID) {
                // We received an icon from somebody else.
                if (!GhostMap.TryGetValue(frame.HHead.PlayerID, out ghost) || ghost == null || ghost.Scene != Engine.Scene) {
                    // No ghost for the player existing.
                    return;
                }
            }

            GhostNetEmote emote = new GhostNetEmote(ghost ?? (Entity) Player, frame.Get<ChunkMEmote>().Value) {
                PopIn = true,
                FadeOut = true
            };
            emote.AddTag(Tags.Persistent | Tags.TransitionUpdate | Tags.FrozenUpdate);
            Engine.Scene.Add(emote);
        }

        public uint NextKevinballLevel = 0; 

        public virtual void HandleMKevinballStart(GhostNetConnection con, GhostNetFrame frame)
        {
            ChunkMKevinballStart start = frame;
            StartKevinballRoutine(start.Player1, start.Player2);
        }

        public bool PlayerShouldDie = false; 
        public virtual void HandleMKevinballShuffle(GhostNetConnection con, GhostNetFrame frame)
        {
            ChunkMKevinballShuffle shuffle = frame;
            NextKevinballLevel = shuffle.NextLevel;
            PlayerShouldDie = true; 
        }

        public virtual void HandleMChat(GhostNetConnection con, GhostNetFrame frame) {
            // Logger.Log(LogLevel.Info, "ghostnet-c", $"#{frame.HHead.PlayerID} chat: {frame.MChat.Text}");

            if (frame.HHead.PlayerID != uint.MaxValue &&
                frame.MPlayer == null) {
                // We've received a message from a dead ghost.
                return;
            }

            ChatLine line = new ChatLine(frame);

            // If there's already a chat line with the same message ID, replace it.
            // Also remove any "unconfirmed" messages.
            bool locked = false;
            try {
                for (int i = ChatLog.Count - 1; i > -1; --i) {
                    ChatLine existing = ChatLog[i];
                    if (existing.MessageID == line.MessageID) {
                        ChatLog[i] = line;
                        return;
                    }
                    if (existing.PlayerID == line.PlayerID && existing.Unconfirmed) {
                        if (!locked) {
                            // Enter the lock only if we remove items.
                            Monitor.Enter(ChatLog, ref locked);
                        }
                        ChatLog.RemoveAt(i);
                    }
                }
            } finally {
                if (locked)
                    Monitor.Exit(ChatLog);
            }

            ChatLog.Insert(0, line);
        }

        public virtual void HandleUUpdate(GhostNetConnection con, GhostNetFrame frame) {
            if (Player == null)
                return;

            if (frame.HHead.PlayerID == PlayerID) {
                // TODO: Server told us to move.
                return;
            }

            Ghost ghost;
            if (!GhostMap.TryGetValue(frame.HHead.PlayerID, out ghost) || ghost == null || ghost.Scene != Engine.Scene)
                return;

            uint lastIndex;
            if (GhostUpdateIndices.TryGetValue(frame.HHead.PlayerID, out lastIndex) && frame.UUpdate.UpdateIndex < lastIndex) {
                // Logger.Log(LogLevel.Verbose, "ghostnet-c", $"Out of order update from #{frame.H0.PlayerID} - got {frame.U0.UpdateIndex}, newest is {lastIndex]}");
                return;
            }
            GhostUpdateIndices[frame.HHead.PlayerID] = frame.UUpdate.UpdateIndex;

            lock (UpdateMap) {
                UpdateMap[frame.HHead.PlayerID] = frame;
            }

            // Logger.Log(LogLevel.Verbose, "ghostnet-c", $"Received nU0 from #{frame.PlayerID} ({remote}), HasData: {frame.Frame.HasData}");

            ghost.ForcedFrame = new GhostFrame {
                Data = frame.UUpdate.Data
            };
        }

        public virtual void HandleUActionCollision(GhostNetConnection con, GhostNetFrame frame) {
            if (Player == null)
                return;

            if (frame.HHead.PlayerID == PlayerID) {
                // We collided with ourselves..?!
                return;
            }

            ChunkUActionCollision collision = frame;

            bool withPlayer = collision.With == PlayerID;

            Ghost ghost;
            if (!GhostMap.TryGetValue(frame.HHead.PlayerID, out ghost) || ghost == null || ghost.Scene != Engine.Scene)
                return;

            if (collision.Head) {
                OnJumpedOnHead(ghost, false, withPlayer);
                if (withPlayer) {
                    Player.Speed.Y = Math.Max(Player.Speed.Y, 16f);
                }
            }
        }

        public virtual void HandleUTouchPress(GhostNetConnection con, GhostNetFrame frame)
        {
            ChunkUTouchPress touchPress = frame;
            foreach (GhostTouchSwitch touch in GhostTouchSwitches)
            {
                if(touch.tIdx == touchPress.TIdx)
                {
                    touch.TurnOn(); 
                }
            }
        }

        public virtual void HandleUControlPress(GhostNetConnection con, GhostNetFrame frame)
        {
            ChunkUControlPress controlPress = frame;
            foreach (MultiplayerControlSwitch control in ControlSwitches)
            {
                if (control.tIdx == controlPress.TIdx)
                {
                    control.TurnOn(controlPress.With);
                }
            }
        }

        public virtual void HandleUSpikeTrigger(GhostNetConnection con, GhostNetFrame frame)
        {
            ChunkUSpikeTrigger trigger = frame;
            foreach (MultiplayerTriggerSpikes spikes in mTriggerSpikes)
            {
                if (spikes.tIdx == trigger.TIdx)
                {
                    spikes.ActivateSpikesMP((int)trigger.SInfo);
                }
            }
        }

        public virtual void HandleUCrushHit(GhostNetConnection con, GhostNetFrame frame)
        {
            ChunkUCrushHit crushHit = frame;
            Vector2 dir = new Vector2(0, 0);
            if (crushHit.Dir == 0)
                dir.X = -1;
            else
                dir.X = 1;

            if (crushBlock.CanActivate(dir, true))
                crushBlock.Attack(dir);
        }

        public virtual void HandleUPickupKevinRefill(GhostNetConnection con, GhostNetFrame frame)
        {
            ChunkUPickupKevinRefill kevinFrame = frame;
            if(kevinFrame.With != PlayerID)
            {
                refill.OnOtherPlayer(Player);
            }
        }

        public float lastPlayerDeath = 0f; 
        public virtual void HandleUPlayerDeath(GhostNetConnection con, GhostNetFrame frame)
        {
            lastPlayerDeath = 0f; 
            ChunkUTouchPress touchPress = frame;
            if (Player.CollideCheck<MultiplayerDeathTrigger>())
            {
                if(!Player.Dead)
                    Player.Die(new Vector2(0, 0)); 
            }
        }

        public virtual void HandleMKevinballEnd(GhostNetConnection con, GhostNetFrame frame)
        {
            ChunkMKevinballEnd end = frame;
            bool nextLevelDiff = false; 
            if(NextKevinballLevel != end.NextLevel)
            {
                NextKevinballLevel = end.NextLevel;
                nextLevelDiff = true; 
            }

            if (!Player.Dead && lastPlayerDeath > 3f)
                Player.Die(new Vector2(0, 0));
            else if(nextLevelDiff)
                Player.Die(new Vector2(0, 0));

            if (end.Wintype == KevinballWin)
            {
                Audio.Play("event:/kevinball_kevingoal");
            }
            else if(end.Wintype == DeathWin)
            {
                int num = Calc.Random.Range(1, 4);
                string str = "event:/kevinball_ouch_" + num.ToString();
                Audio.Play(str);
            }
        }

        public virtual void HandleUAudioPlay(GhostNetConnection con, GhostNetFrame frame) {
            if (Player == null)
                return;

            if (frame.HHead.PlayerID == PlayerID) {
                // We've received our own audio... which we already played.
                return;
            }

            Level level = Engine.Scene as Level;
            if (level == null)
                return;

            if (frame.MPlayer.SID != (Session?.Area.GetSID() ?? "") ||
                frame.MPlayer.Mode != (Session?.Area.Mode ?? AreaMode.Normal) ||
                frame.MPlayer.Level != (Session?.Level ?? "")) {
                // Not the same level - skip.
                return;
            }

            if (!GhostNetModule.Settings.Sounds)
                return;

            ChunkUAudioPlay audio = frame;
            Audio.Play(audio.Sound, audio.Position ?? Player.Center, audio.Param, audio.Value);
        }

        public virtual void HandleUParticles(GhostNetConnection con, GhostNetFrame frame) {
            if (Player == null)
                return;

            if (frame.HHead.PlayerID == PlayerID) {
                // We've received our own particles... which we already spawned.
                return;
            }

            Level level = Engine.Scene as Level;
            if (level == null)
                return;

            if (frame.MPlayer.SID != (Session?.Area.GetSID() ?? "") ||
                frame.MPlayer.Mode != (Session?.Area.Mode ?? AreaMode.Normal) ||
                frame.MPlayer.Level != (Session?.Level ?? "")) {
                // Not the same level - skip.
                return;
            }

            ChunkUParticles particles = frame;

            ParticleSystem system;
            switch (particles.System) {
                case ChunkUParticles.Systems.Particles:
                    system = level.Particles;
                    break;
                case ChunkUParticles.Systems.ParticlesBG:
                    system = level.ParticlesBG;
                    break;
                case ChunkUParticles.Systems.ParticlesFG:
                    system = level.ParticlesFG;
                    break;
                default:
                    return;
            }

            system.Emit(
                particles.Type,
                particles.Amount,
                particles.Position,
                particles.PositionRange,
                particles.Color,
                particles.Direction
            );
        }

        public virtual void HandleMSession(GhostNetConnection con, GhostNetFrame frame) {
            if (Session == null)
                return;

            ChunkMSession received = frame;
            if (!received.InSession)
                return;

            Session.Area = new AreaKey(-1, frame.MPlayer.Mode).SetSID(frame.MPlayer.SID);
            Session.Level = frame.MPlayer.Level;

            Session.Audio = received.Audio;
            Session.RespawnPoint = received.RespawnPoint;
            Session.Inventory = received.Inventory;
            Session.Flags = received.Flags;
            Session.LevelFlags = received.LevelFlags;
            Session.Strawberries = received.Strawberries;
            Session.DoNotLoad = received.DoNotLoad;
            Session.Keys = received.Keys;
            Session.Counters = received.Counters;
            Session.FurthestSeenLevel = received.FurthestSeenLevel;
            Session.StartCheckpoint = received.StartCheckpoint;
            Session.ColorGrade = received.ColorGrade;
            Session.SummitGems = received.SummitGems;
            Session.FirstLevel = received.FirstLevel;
            Session.Cassette = received.Cassette;
            Session.HeartGem = received.HeartGem;
            Session.Dreaming = received.Dreaming;
            Session.GrabbedGolden = received.GrabbedGolden;
            Session.HitCheckpoint = received.HitCheckpoint;
            Session.LightingAlphaAdd = received.LightingAlphaAdd;
            Session.BloomBaseAdd = received.BloomBaseAdd;
            Session.DarkRoomAlpha = received.DarkRoomAlpha;
            Session.Time = received.Time;
            Session.CoreMode = received.CoreMode;
        }

        #endregion

        #region Connection Handlers

        protected virtual void HandleM(GhostNetConnection con, IPEndPoint remote, GhostNetFrame frame) {
            Handle(con, frame);
        }

        protected virtual void HandleU(GhostNetConnection con, IPEndPoint remote, GhostNetFrame frame, bool log) {
            if(log)
                Logger.Log(LogLevel.Info, "ghostnet-c", "Logged frame at HandleU in Client");
            Handle(con, frame);
        }

        protected virtual void HandleDisconnect(GhostNetConnection con) {
            Logger.Log(LogLevel.Info, "ghostnet-c", "Client disconnected");

            Connection = null;

            Cleanup();

            if (GhostNetModule.Settings.EnabledEntry != null) {
                GhostNetModule.Settings.EnabledEntry.LeftPressed();
            }

            On.Celeste.Level.LoadLevel -= OnLoadLevel;
            Everest.Events.Level.OnExit -= OnExitLevel;
            Everest.Events.Level.OnComplete -= OnCompleteLevel;
            Everest.Events.Level.OnTransitionTo -= OnTransitionTo; 
            Everest.Events.Player.OnDie -= OnDie;
            TextInput.OnInput -= OnTextInput;

            OnExitLevel(null, null, LevelExit.Mode.SaveAndQuit, null, null);

            Celeste.Instance.Components.Remove(this);
        }

        #endregion

        #region Celeste Events

        public void OnLoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level level, Player.IntroTypes playerIntro, bool isFromLoader = false) {
            orig?.Invoke(level, playerIntro, isFromLoader);

            Session = level.Session;

            if (Connection == null)
                return;

            FirstGhostSwitch = true;
            FirstControlSwitch = true;
            FirstTriggerSpike = true;

            Logger.Log(LogLevel.Info, "ghost-c", $"Stepping into {Session.Area.GetSID()} {(char) ('A' + Session.Area.Mode)} {Session.Level}");

            Player = level.Tracker.GetEntity<Player>();

            for (int i = 0; i < Ghosts.Count; i++) {
                Ghost ghost = Ghosts[i];
                if (ghost == null)
                    continue;
                if (ghost.Scene != Engine.Scene) {
                    Ghosts[i] = ghost = null;
                    continue;
                }
                ghost.Player = Player;
            }

            level.Add(GhostRecorder = new GhostRecorder(Player));
            GhostRecorder.AddTag(Tags.Persistent | Tags.TransitionUpdate | Tags.FrozenUpdate);

            PlayerName?.RemoveSelf();
            level.Add(PlayerName = new GhostName(Player, PlayerInfo?.Name ?? ""));

            EmoteWheel?.RemoveSelf();
            level.Add(EmoteWheel = new GhostNetEmoteWheel(Player));
            
            SendMPlayer();
        }

        public void OnExitLevel(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
            Session = null;
            InKevinballMap = false;
            firstSpawns = true; 

            Cleanup();

            if (Connection == null)
                return;

            Logger.Log(LogLevel.Info, "ghost-c", "Leaving level");

            SendMPlayer(levelExit: mode);
        }

        public void OnDie(Player player)
        {
            if (Connection == null)
                return;

            if(PlayingKevinball)
                SendUPlayerDeath(PlayerID);

            if(InKevinballMap)
            {
                int targetLevel = (int)NextKevinballLevel % Session.MapData.Levels.Count;
                LevelData target = Session.MapData.Levels[targetLevel];
                Session.Level = target.Name;
                Session.RespawnPoint = target.Spawns[0];
            }


            if (!player.CollideCheck<MultiplayerDeathTrigger>())
                return; 

            if(TouchedGhostSwitch)
            {
                TouchedGhostSwitch = false; 
                SendUPlayerDeath(PlayerID);
            }
        }

        public virtual void SendULoadedKevinball(uint with)
        {
            if (Connection == null)
                return;

            Connection.SendUpdate(new GhostNetFrame {
                new ChunkULoadedKevinball {
                    With = with
                }
            }, true);
        }

        public void OnCompleteLevel(Level level) {
            if (Connection == null)
                return;

            Logger.Log(LogLevel.Info, "ghost-c", "Completed level");

            SendMPlayer(levelCompleted: true);
        }

        public void OnTextInput(char c) {
            if (!ChatVisible)
                return;
            if (c == (char) 13) {
                // Enter - send.
                // Handled in Update.

            } else if (c == (char) 8) {
                // Backspace - trim.
                if (ChatInput.Length > 0)
                    ChatInput = ChatInput.Substring(0, ChatInput.Length - 1);
                _ChatRepeatIndex = 0;

            } else if (c == (char) 127) {
                // Delete - currenly not handled.

            } else if (!char.IsControl(c)) {
                // Any other character - append.
                ChatInput += c;
                _ChatRepeatIndex = 0;
            }
        }

        #endregion

        public void StartHostPostUPnP()
        {
            Connection = GhostNetModule.Instance.Server.LocalConnectionToServer;
            GhostNetModule.Instance.Server.Accept(new GhostNetLocalConnection
            {
                OnReceiveManagement = HandleM,
                OnReceiveUpdate = HandleU,
                OnDisconnect = HandleDisconnect
            });
        }

        public async Task PerformUPnP()
        {
            var discoverer = new NatDiscoverer();
            var cts = new CancellationTokenSource(10000);
            var device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);

            await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 2782, 2782, "Celeste-Kevinball"));
            await device.CreatePortMapAsync(new Mapping(Protocol.Udp, 2782, 2782, "Celeste-Kevinball"));
            StartHostPostUPnP();
        }

        public void Start() {
            if (Connection != null) {
                Logger.Log(LogLevel.Warn, "ghostnet-c", "Client already running, restarting");
                Stop();
            }

            Logger.Log(LogLevel.Info, "ghostnet-c", "Starting client");

            

            if (GhostNetModule.Instance.Server != null) {
                // We're hosting - let's just set up pseudo connections.
                PerformUPnP(); 
            } else {
                // Set up a remote connection.
                Connection = new GhostNetRemoteConnection(
                    GhostNetModule.Settings.Host,
                    GhostNetModule.Settings.Port
                ) {
                    OnReceiveManagement = HandleM,
                    OnReceiveUpdate = HandleU,
                    OnDisconnect = HandleDisconnect
                };
            }

            On.Celeste.Level.LoadLevel += OnLoadLevel;
            Everest.Events.Level.OnExit += OnExitLevel;
            Everest.Events.Level.OnComplete += OnCompleteLevel;
            Everest.Events.Player.OnDie += OnDie;
            Everest.Events.Level.OnTransitionTo += OnTransitionTo;
            TextInput.OnInput += OnTextInput;

            if (Engine.Instance != null && Engine.Scene is Level)
                OnLoadLevel(null, (Level) Engine.Scene, Player.IntroTypes.Transition, true);

        }

        private void OnTransitionTo(Level level, LevelData next, Vector2 direction)
        {
            TouchedGhostSwitch = false;
        }

        public void Stop() {
            Logger.Log(LogLevel.Info, "ghostnet-c", "Stopping client");

            ChatVisible = false;

            Celeste.Instance.Components.Remove(this);

            Connection?.Dispose();
            Connection = null;
        }

        public void Cleanup() {
            if (Player != null) {
                OnIdle(PlayerID, Player, false);
            }

            Player = null;

            for (int i = 0; i < Ghosts.Count; i++)
                Ghosts[i]?.RemoveSelf();
            GhostMap.Clear();
            IdleMap.Clear();
            Ghosts.Clear();

            GhostRecorder?.RemoveSelf();
            GhostRecorder = null;

            PlayerName?.RemoveSelf();
            PlayerName = null;

            EmoteWheel?.RemoveSelf();
            EmoteWheel = null;
        }

        private bool disposed = false;

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);

            if (disposed)
                return;

            if (disposing) {
                Stop();
            }

            disposed = true;
        }

        public struct ChatLine {

            public uint MessageID;
            public uint PlayerID;
            public string PlayerName;
            public string Tag;
            public string Text;
            public Color Color;
            public DateTime Date;
            public bool Unconfirmed => MessageID == uint.MaxValue;

            public ChatLine(uint messageID, uint playerID, string playerName, string tag, string text)
                : this(messageID, playerID, playerName, tag, text, Color.White) {
            }
            public ChatLine(GhostNetFrame frame)
                : this(frame, frame) {
            }
            private ChatLine(GhostNetFrame frame, ChunkMChat chat)
                : this(chat.ID, frame.HHead.PlayerID, frame.MPlayer?.Name ?? "**SERVER**", chat.Tag, chat.Text, chat.Color) {
            }
            public ChatLine(uint messageID, uint playerID, string playerName, string tag, string text, Color color) {
                MessageID = messageID;
                PlayerID = playerID;
                PlayerName = playerName;
                Tag = tag;
                Text = text;
                Color = color;
                Date = DateTime.UtcNow;
            }

            public override string ToString()
                => $"[{Date.ToLocalTime().ToLongTimeString()}]{(string.IsNullOrEmpty(Tag) ? "" : $"[{Tag}]")} {PlayerName}{(PlayerID == uint.MaxValue ? "" : $"#{PlayerID}")}:{(Text.Contains('\n') ? "\n" : " ")}{Text}";

        }

    }
}
