using System;
using System.Collections.Generic;
using Liryne.Game.Content;
using Liryne.Game.Net;
using Liryne.Game.Ui;
using Liryne.Game.World;
using Liryne.Protocol.Messages;
using Liryne.Protocol.Messages.Auth;
using Liryne.Protocol.Messages.Characters;
using Liryne.Protocol.Messages.Chat;
using Liryne.Protocol.Messages.Combat;
using Liryne.Protocol.Messages.Entities;
using Liryne.Protocol.Messages.Movement;
using Liryne.Protocol.Messages.Session;
using Liryne.Protocol.Wire;
using UnityEngine;
using UnityEngine.EventSystems;
using SessionPing = Liryne.Protocol.Messages.Session.Ping;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Liryne.Game.App
{
    public sealed class LiryneApp : MonoBehaviour
    {
        readonly AuthClient _auth = new AuthClient();
        readonly WorldConnection _world = new WorldConnection();
        readonly List<string> _playerNames = new List<string>();

        readonly SceneFlow _scenes = new SceneFlow();
        readonly Queue<IMessage> _heldWhileLoading = new Queue<IMessage>();

        WorldStage _stage;
        TitleStage _title;
        PreviewStudio _preview;
        LoginScreen _login;
        ServerScreen _servers;
        CharacterScreen _charactersUi;
        WardrobeScreen _wardrobe;
        HudView _hud;
        LoadingScreen _loader;

        IReadOnlyList<ServerInfo> _serverList = Array.Empty<ServerInfo>();
        ServerInfo _server;
        string _token;
        IReadOnlyList<CharacterSummary> _characters = Array.Empty<CharacterSummary>();
        ushort _tickRate = 20;
        bool _autoEngaged;
        Guid _engagedId;
        float _moveAccum;
        float _pingAccum;
        Vector2 _lastSentDir = new Vector2(float.NaN, float.NaN);
        bool _inWorld;
        bool _busy;
        bool _loading;

#if ENABLE_INPUT_SYSTEM
        static readonly Key[] SpellHotkeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0,
        };

        static readonly Key[] SpellNumpads =
        {
            Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5,
            Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9, Key.Numpad0,
        };
#endif

        public bool IsReady => _preview is not null && _hud is not null;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            _serverList = ServerDirectory.Load();
            _stage = new WorldStage(transform);
            _title = new TitleStage(transform);
            Canvas canvas = UiFactory.CreateCanvas(transform);
            if (EventSystem.current is not null)
            {
                EventSystem.current.sendNavigationEvents = false;
            }

            _login = new LoginScreen(canvas.transform, () => _ = ConnectAccount(create: false), () => _ = ConnectAccount(create: true));
            _servers = new ServerScreen(canvas.transform, BackToLogin, server => _ = EnterServer(server));
            _charactersUi = new CharacterScreen(canvas.transform, DisconnectWorld, PlayCharacter, OpenWardrobe, CreateCharacter);
            _wardrobe = new WardrobeScreen(canvas.transform, CloseWardrobe, SaveAppearance);
            _hud = new HudView(canvas.transform, SendChat, Inspect, ShowSelfProfile, LogoutToCharacters, CastSpell);
            _loader = new LoadingScreen(canvas.transform);
            _preview = new PreviewStudio(transform);
            _preview.Bind(_hud.ProfilePreview);
            _preview.Bind(_wardrobe.Preview);
            _preview.Show(CharacterAppearance.Default, 1, _wardrobe.Preview);
            _preview.Hide();
            _wardrobe.DraftChanged += (appearance, classId) =>
            {
                if (!_inWorld)
                {
                    _preview.Show(appearance, classId, _wardrobe.Preview);
                }
            };
            _charactersUi.AppearanceChanged += OnTitleAppearance;
            _charactersUi.SelectionChanged += id =>
            {
                if (!_inWorld)
                {
                    _title.Select(id);
                    if (id != Guid.Empty)
                    {
                        _title.HideDraft();
                    }
                }
            };
            _title.Picked += id =>
            {
                if (!_inWorld)
                {
                    _charactersUi.Select(id);
                }
            };
            _servers.Bind(_serverList);
            _login.SetVisible(false);
            _servers.SetVisible(false);
            _charactersUi.SetVisible(false);
            _wardrobe.SetVisible(false);
            _hud.SetVisible(false);
            _title.Hide();
        }

        async void Start()
        {
            await OpenTitle("Liryne");
            ShowLogin();
        }

        void Update()
        {
            if (!IsReady)
            {
                return;
            }

            _world.Pump(message =>
            {
                try
                {
                    if (_loading)
                    {
                        _heldWhileLoading.Enqueue(message);
                        return;
                    }

                    HandleMessage(message);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            });
            if (!_world.Handshaking && _world.TryConsumeRemoteClose(out string lost))
            {
                HandleDisconnect(lost);
            }

            _preview.Tick(Time.deltaTime);

            if (_loading)
            {
                return;
            }

            if (_inWorld)
            {
                TickCamera();
                TickTargeting();
                TickMovement();
                TickSpells();
                TickAutoAttack();
                TickPing();
                _stage.FollowCamera();
                _hud.BindVitals(_stage.Local);
                _hud.BindTarget(_stage.Selected);
                _hud.Tick();
                if (!_hud.ProfileVisible)
                {
                    _preview.Hide();
                }
            }
            else if (!_wardrobe.IsVisible)
            {
                _title.Tick(Time.deltaTime);
            }
        }

        void OnApplicationPause(bool paused)
        {
            Application.runInBackground = true;
            if (!paused && _world.IsConnected)
            {
                _world.TrySend(new SessionPing((uint)(Time.realtimeSinceStartup * 1000f)), out _);
            }
        }

        void OnApplicationFocus(bool focused)
        {
            Application.runInBackground = true;
            if (focused && _world.IsConnected)
            {
                _world.TrySend(new SessionPing((uint)(Time.realtimeSinceStartup * 1000f)), out _);
            }
        }

        void OnDestroy()
        {
            _hud?.FlushLayout();
            _world.Dispose();
            _preview?.Dispose();
            _title?.Dispose();
        }

        async System.Threading.Tasks.Task ConnectAccount(bool create)
        {
            if (_busy)
            {
                return;
            }

            string login = _login.Login;
            string password = _login.Password;
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                _login.SetError("Identifiant et mot de passe requis.");
                return;
            }

            ServerInfo authHost = _serverList.Count > 0 ? _serverList[0] : new ServerInfo();
            _busy = true;
            _login.SetBusy(true);
            _login.SetError(create ? "Création du compte…" : "Connexion…");
            try
            {
                if (create)
                {
                    CreateAccountResult created = await _auth.CreateAccountAsync(authHost.authUrl, login, password);
                    if (created.Status != AuthStatus.Ok)
                    {
                        _login.SetError(ClientCatalog.AuthStatusText(created.Status));
                        return;
                    }
                }

                LoginResult result = await _auth.LoginAsync(authHost.authUrl, login, password);
                if (result.Status != AuthStatus.Ok)
                {
                    string text = ClientCatalog.AuthStatusText(result.Status);
                    if (result.Status == AuthStatus.Banned && result.BanEndsAtUnixSeconds > 0)
                    {
                        DateTime end = DateTimeOffset.FromUnixTimeSeconds(result.BanEndsAtUnixSeconds).LocalDateTime;
                        text += " Fin : " + end.ToString("g");
                    }

                    _login.SetError(text);
                    return;
                }

                _token = result.SessionToken;
                _login.SetError(string.Empty);
                if (_serverList.Count == 1)
                {
                    await ConnectWorld(_serverList[0], fromLogin: true);
                    return;
                }

                _login.SetVisible(false);
                _servers.SetError(string.Empty);
                _servers.SetVisible(true);
                _title.Show();
            }
            catch (Exception ex)
            {
                _login.SetError(ex.Message);
            }
            finally
            {
                _busy = false;
                _login.SetBusy(false);
            }
        }

        async System.Threading.Tasks.Task EnterServer(ServerInfo server)
        {
            if (_busy || string.IsNullOrEmpty(_token))
            {
                return;
            }

            _busy = true;
            _servers.SetError("Connexion à " + server.name + "…");
            try
            {
                await ConnectWorld(server, fromLogin: false);
            }
            finally
            {
                _busy = false;
            }
        }

        async System.Threading.Tasks.Task ConnectWorld(ServerInfo server, bool fromLogin)
        {
            const string unavailable = "Le serveur n'est pas disponible.";
            try
            {
                _server = server;
                WorldWelcome welcome = await _world.ConnectAsync(
                    server.worldHost,
                    server.worldPort,
                    _token,
                    destroyCancellationToken);
                OnWelcome(welcome);
                if (welcome.Status == WorldStatus.Ok)
                {
                    _login.SetVisible(false);
                    _servers.SetVisible(false);
                    _title.Show();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Liryne] World injoignable : " + ex.Message);
                if (fromLogin)
                {
                    _login.SetVisible(true);
                    _login.SetError(unavailable);
                    _servers.SetVisible(false);
                }
                else
                {
                    _servers.SetVisible(true);
                    _servers.SetError(unavailable);
                }
            }
        }

        void HandleMessage(IMessage message)
        {
            switch (message)
            {
                case WorldWelcome welcome:
                    OnWelcome(welcome);
                    break;
                case CharacterList list:
                    _characters = list.Characters;
                    if (!_inWorld && !_wardrobe.IsVisible)
                    {
                        _servers.SetVisible(false);
                        _login.SetVisible(false);
                        _charactersUi.SetVisible(true);
                        _title.Show();
                        _title.Bind(_characters);
                    }

                    _charactersUi.Bind(_characters);
                    if (!_inWorld && _charactersUi.IsCreating)
                    {
                        _title.ShowDraft(_charactersUi.CurrentAppearance, _charactersUi.CurrentClassId);
                    }

                    break;
                case CreateCharacterResult created:
                    _charactersUi.SetError(ClientCatalog.CreateStatusText(created.Status));
                    break;
                case ChangeAppearanceResult appearance:
                    OnAppearanceSaved(appearance);
                    break;
                case EnterWorldResult enter:
                    OnEnterWorld(enter);
                    break;
                case EntitySpawn spawn:
                    _stage.Spawn(spawn);
                    RefreshPlayerNames();
                    break;
                case EntityDespawn despawn:
                    _stage.Despawn(despawn.EntityId);
                    RefreshPlayerNames();
                    break;
                case EntitySnapshot snapshot:
                    _stage.ApplySnapshot(snapshot);
                    _hud.SetServerTick(snapshot.ServerTick);
                    break;
                case SpellBook book:
                    _hud.BindSpellBook(book);
                    break;
                case SpellCastResult cast:
                    _hud.ApplyCastResult(cast);
                    if (cast.Outcome == SpellOutcome.Cast)
                    {
                        _stage.PlayStrike(cast.CasterEntityId);
                    }

                    break;
                case CombatEvents combat:
                    _hud.SetServerTick(combat.ServerTick);
                    _stage.PlayCombat(combat);
                    break;
                case EntityPresentation presentation:
                    _stage.ApplyPresentation(presentation);
                    break;
                case ChatMessage chat:
                    _hud.AddChat(chat);
                    break;
                case ChatRejected rejected:
                    _hud.AddSystem(ClientCatalog.ChatRejectText(rejected.Reason));
                    break;
                case CommandCatalog catalog:
                    _hud.SetCommands(catalog.Commands);
                    break;
                case PlayerProfile profile:
                    ShowProfile(profile);
                    break;
                case Disconnected disconnected:
                    HandleDisconnect(ClientCatalog.DisconnectText(disconnected.Reason));
                    break;
            }
        }

        void OnWelcome(WorldWelcome welcome)
        {
            if (welcome.Status != WorldStatus.Ok)
            {
                _world.Disconnect();
                _charactersUi.SetVisible(false);
                string text = ClientCatalog.WelcomeStatusText(welcome.Status);
                if (_serverList.Count <= 1)
                {
                    _login.SetVisible(true);
                    _login.SetError(text);
                    _servers.SetVisible(false);
                }
                else
                {
                    _servers.SetVisible(true);
                    _servers.SetError(text);
                }

                return;
            }

            _tickRate = welcome.TickRate == 0 ? (ushort)20 : welcome.TickRate;
            _stage.TickDelta = 1f / _tickRate;
            _hud.SetTickRate(_tickRate);
        }

        void OnEnterWorld(EnterWorldResult enter)
        {
            if (enter.Status != EnterWorldStatus.Ok)
            {
                _charactersUi.SetError(enter.Status switch
                {
                    EnterWorldStatus.CharacterNotFound => "Personnage introuvable.",
                    EnterWorldStatus.AlreadyInWorld => "Déjà en jeu.",
                    _ => "Impossible d'entrer en jeu.",
                });
                return;
            }

            _loading = true;
            _ = EnterMap(enter);
        }

        async System.Threading.Tasks.Task EnterMap(EnterWorldResult enter)
        {
            string mapName = MapCatalog.Name(enter.MapId);
            _loader.Show("Chargement de " + mapName);
            try
            {
                await _scenes.LoadMapAsync(enter.MapId);
                _inWorld = true;
                _lastSentDir = new Vector2(float.NaN, float.NaN);
                _autoEngaged = false;
                _engagedId = Guid.Empty;
                _charactersUi.SetVisible(false);
                _wardrobe.SetVisible(false);
                _title.Hide();
                _preview.Hide();
                _hud.SetVisible(true);
                _hud.HidePopups();
                _stage.Enter(enter.EntityId, enter.Position);
                _hud.AddSystem("Bienvenue dans " + mapName + ".");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Liryne] Chargement map : " + ex.Message);
                _heldWhileLoading.Clear();
                _charactersUi.SetVisible(true);
                _charactersUi.SetError("Impossible de charger " + mapName + ".");
                await OpenTitle();
            }
            finally
            {
                _loading = false;
                _loader.Hide();
                FlushHeld();
            }
        }

        void PlayCharacter(Guid id)
        {
            if (!_world.TrySend(new EnterWorldRequest(id), out _))
            {
                _charactersUi.SetError("Plus de connexion World.");
            }
        }

        void OpenWardrobe()
        {
            if (_inWorld)
            {
                return;
            }

            _charactersUi.SetVisible(false);
            _wardrobe.Open(_charactersUi.SelectedId, _charactersUi.CurrentAppearance, _charactersUi.CurrentClassId);
        }

        void CloseWardrobe()
        {
            _wardrobe.SetVisible(false);
            _preview.Hide();
            if (_inWorld)
            {
                return;
            }

            _charactersUi.SetVisible(true);
            _charactersUi.Bind(_characters);
        }

        void SaveAppearance(Guid id, CharacterAppearance appearance)
        {
            if (!_world.TrySend(new ChangeAppearance(id, appearance), out _))
            {
                if (_wardrobe.IsVisible)
                {
                    _wardrobe.SetError("Plus de connexion World.");
                    return;
                }

                _charactersUi.SetError("Plus de connexion World.");
            }
        }

        void OnAppearanceSaved(ChangeAppearanceResult result)
        {
            string text = ClientCatalog.AppearanceStatusText(result.Status);
            if (result.Status != ChangeAppearanceStatus.Ok)
            {
                if (_wardrobe.IsVisible)
                {
                    _wardrobe.SetError(text);
                    return;
                }

                _charactersUi.SetError(text);
                return;
            }

            CloseWardrobe();
        }

        void CreateCharacter(string name, ushort classId, CharacterAppearance appearance)
        {
            if (!_world.TrySend(new CreateCharacter(name, classId, appearance), out _))
            {
                _charactersUi.SetError("Plus de connexion World.");
            }
        }

        void SendChat(ChatChannel channel, string text)
        {
            _world.TrySend(new ChatSendIntent(channel, text), out _);
        }

        void Inspect(Guid entityId)
        {
            if (entityId == Guid.Empty)
            {
                return;
            }

            if (_stage.Avatars.TryGetValue(entityId, out EntityAvatar avatar))
            {
                ShowAvatarProfile(avatar);
                return;
            }

            _world.TrySend(new InspectPlayerIntent(entityId), out _);
        }

        void ShowSelfProfile()
        {
            if (_stage.Local is not null)
            {
                ShowAvatarProfile(_stage.Local);
                return;
            }

            if (_stage.LocalEntityId != Guid.Empty)
            {
                _world.TrySend(new InspectPlayerIntent(_stage.LocalEntityId), out _);
            }
        }

        void ShowAvatarProfile(EntityAvatar avatar)
        {
            EntityState state = avatar.LastState;
            string vitals = state is null
                ? string.Empty
                : $"{ClientCatalog.AffinityName(state.Affinity)}\nPV {state.Hp}/{state.MaxHp}   PM {state.Mp}/{state.MaxMp}\n\n";
            _hud.ShowProfile(
                avatar.DisplayName,
                $"{ClientCatalog.ClassName(avatar.ClassId)} · niveau {avatar.Level}\n" +
                $"{ClientCatalog.TitleName(avatar.TitleId)}\n" +
                $"Ornement : {ClientCatalog.OrnamentName(avatar.OrnamentId)}\n" +
                vitals +
                "Équipement : emplacements vides jusqu'à l'inventaire.");
            _preview.Show(avatar.Appearance, avatar.ClassId, _hud.ProfilePreview);
        }

        void ShowProfile(PlayerProfile profile)
        {
            if (profile.Status != InspectStatus.Ok)
            {
                _hud.AddSystem("Profil introuvable.");
                return;
            }

            _hud.ShowProfile(
                profile.Name,
                $"{ClientCatalog.ClassName(profile.ClassId)} · niveau {profile.Level}\n" +
                $"{ClientCatalog.TitleName(profile.TitleId)}\n" +
                $"Ornement : {ClientCatalog.OrnamentName(profile.OrnamentId)}\n\n" +
                "Équipement : emplacements vides jusqu'à l'inventaire.");
            _preview.Show(profile.Appearance, profile.ClassId, _hud.ProfilePreview);
        }

        void TickCamera()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse is null || _hud.ChatFocused)
            {
                return;
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float step = Mathf.Abs(scroll) > 5f ? scroll * 0.012f : scroll * 1.15f;
                _stage.ZoomFollow(-step);
            }

            if (!mouse.rightButton.isPressed)
            {
                return;
            }

            Vector2 delta = mouse.delta.ReadValue();
            _stage.OrbitFollow(delta.x * 0.18f, delta.y * 0.18f);
#endif
        }

        void TickTargeting()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard is null || _hud.ChatFocused || !keyboard.tabKey.wasPressedThisFrame)
            {
                return;
            }

            _stage.CycleTarget();
#endif
        }

        void TickMovement()
        {
            Vector2 axes = ReadMove();
            Vector2 dir = _hud.ChatFocused ? Vector2.zero : _stage.ToCameraMove(axes.x, axes.y);
            EntityAvatar marked = _stage.Selected;
            bool attacking = marked is not null && !marked.IsDead && marked.Kind == EntityKind.Monster;
            bool jump = !_hud.ChatFocused && JumpPressed() && !attacking;

            _stage.PredictLocal(dir.x, dir.y, jump, Time.deltaTime);
            if (jump)
            {
                _world.TrySend(new JumpIntent(), out _);
            }
            _moveAccum += Time.deltaTime;
            float interval = 1f / Mathf.Max(1, _tickRate);
            bool changed = !Approximately(dir, _lastSentDir);
            bool moving = dir.sqrMagnitude > 0.0001f;
            if (!changed && !moving && !float.IsNaN(_lastSentDir.x))
            {
                return;
            }

            if (changed || (moving && _moveAccum >= interval))
            {
                if (_world.TrySend(new MoveIntent(dir.x, dir.y), out uint sequence))
                {
                    _stage.RecordMove(sequence, dir.x, dir.y);
                    _lastSentDir = dir;
                    _moveAccum = 0f;
                }
            }
        }

        void TickPing()
        {
            _pingAccum += Time.unscaledDeltaTime;
            if (_pingAccum < 2f)
            {
                return;
            }

            _pingAccum = 0f;
            _world.TrySend(new SessionPing((uint)(Time.realtimeSinceStartup * 1000f)), out _);
        }

        void TickSpells()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard is null || _hud.ChatFocused)
            {
                return;
            }

            for (int i = 0; i < SpellHotkeys.Length; i++)
            {
                if (!keyboard[SpellHotkeys[i]].wasPressedThisFrame && !keyboard[SpellNumpads[i]].wasPressedThisFrame)
                {
                    continue;
                }

                if (_hud.TrySpellAt(i, out ushort spellId))
                {
                    CastSpell(spellId);
                }
            }
#endif
        }

        void CastSpell(ushort spellId)
        {
            EntityAvatar target = _stage.Selected;
            Guid targetId = target is null ? Guid.Empty : target.EntityId;
            Transform source = target is not null ? target.transform : _stage.Local?.transform;
            Vec3 point = Vec3.Zero;
            if (source is not null)
            {
                Vector3 pos = source.position;
                point = new Vec3(pos.x, pos.y, pos.z);
            }

            _world.TrySend(new CastSpellIntent(spellId, targetId, point), out _);
            if (target is not null && target.Kind == EntityKind.Monster && !target.IsDead)
            {
                _autoEngaged = true;
                _engagedId = target.EntityId;
            }
        }

        void TickAutoAttack()
        {
            EntityAvatar local = _stage.Local;
            EntityAvatar target = _stage.Selected;
            if (local is null || local.IsDead)
            {
                DisengageAuto();
                return;
            }

            bool hostile = target is not null && !target.IsDead && target.Kind == EntityKind.Monster;
            if (!hostile)
            {
                DisengageAuto();
                return;
            }

            bool engage = !_hud.ChatFocused && JumpPressed();
            if (engage)
            {
                _autoEngaged = true;
            }

            if (!_autoEngaged)
            {
                _stage.SetSelectedAttacking(false);
                return;
            }

            if (engage || _engagedId != target.EntityId)
            {
                _engagedId = target.EntityId;
                CastSpell(ClientCatalog.AutoSpellId(local.ClassId));
            }

            _stage.SetSelectedAttacking(true);
        }

        void DisengageAuto()
        {
            _autoEngaged = false;
            _engagedId = Guid.Empty;
            _stage.SetSelectedAttacking(false);
        }

        static bool JumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard is not null && keyboard.spaceKey.wasPressedThisFrame;
#else
            return false;
#endif
        }

        static Vector2 ReadMove()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard is null)
            {
                return Vector2.zero;
            }

            float x = 0f;
            float z = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                x -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                x += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                z -= 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                z += 1f;
            }

            var dir = new Vector2(x, z);
            return dir.sqrMagnitude > 1f ? dir.normalized : dir;
#else
            return Vector2.zero;
#endif
        }

        static bool Approximately(Vector2 a, Vector2 b) =>
            Mathf.Abs(a.x - b.x) < 0.01f && Mathf.Abs(a.y - b.y) < 0.01f;

        void RefreshPlayerNames()
        {
            _playerNames.Clear();
            foreach (EntityAvatar avatar in _stage.Avatars.Values)
            {
                if (!string.IsNullOrEmpty(avatar.DisplayName))
                {
                    _playerNames.Add(avatar.DisplayName);
                }
            }

            _hud.SetPlayerNames(_playerNames);
        }

        void BackToLogin()
        {
            _token = null;
            ShowLogin();
        }

        void DisconnectWorld()
        {
            LeaveWorld();
            _world.Disconnect();
            _charactersUi.SetVisible(false);
            _wardrobe.SetVisible(false);
            _preview.Hide();
            if (_serverList.Count <= 1)
            {
                _token = null;
                ShowLogin();
                return;
            }

            _ = OpenTitle();
            _servers.SetVisible(true);
        }

        void LogoutToCharacters()
        {
            if (_busy)
            {
                return;
            }

            LeaveWorld();
            _ = ReturnToCharacters();
        }

        async System.Threading.Tasks.Task ReturnToCharacters()
        {
            if (string.IsNullOrEmpty(_token) || _server is null || string.IsNullOrEmpty(_server.worldHost))
            {
                DisconnectWorld();
                return;
            }

            _busy = true;
            _world.Disconnect();
            try
            {
                await OpenTitle();
                WorldWelcome welcome = await _world.ConnectAsync(
                    _server.worldHost,
                    _server.worldPort,
                    _token,
                    destroyCancellationToken);
                OnWelcome(welcome);
                _servers.SetVisible(false);
                _charactersUi.SetVisible(true);
                _title.Show();
                _title.Bind(_characters);
                _charactersUi.Bind(_characters);
            }
            catch (Exception ex)
            {
                HandleDisconnect(ex.Message);
            }
            finally
            {
                _busy = false;
            }
        }

        void HandleDisconnect(string reason)
        {
            LeaveWorld();
            _world.Disconnect();
            _charactersUi.SetVisible(false);
            _wardrobe.SetVisible(false);
            _preview.Hide();
            if (string.IsNullOrEmpty(_token) || _serverList.Count <= 1)
            {
                ShowLogin(string.IsNullOrEmpty(_token) ? reason : "Le serveur n'est pas disponible.");
                return;
            }

            _ = OpenTitle();
            _servers.SetVisible(true);
            _servers.SetError(reason);
        }

        void LeaveWorld()
        {
            _inWorld = false;
            _hud.SetVisible(false);
            _hud.HidePopups();
            _stage.Clear();
            _stage.SetVisible(false);
        }

        void ShowLogin(string error = null)
        {
            LeaveWorld();
            _world.Disconnect();
            _servers.SetVisible(false);
            _charactersUi.SetVisible(false);
            _wardrobe.SetVisible(false);
            _preview.Hide();
            _login.SetVisible(true);
            _login.SetError(error ?? string.Empty);
            _ = RevealLogin(error);
        }

        async System.Threading.Tasks.Task RevealLogin(string error)
        {
            if (!_scenes.TitleIsReady)
            {
                await OpenTitle();
            }
            else
            {
                _title.Show();
            }

            _title.Bind(Array.Empty<CharacterSummary>());
            _login.SetVisible(true);
            if (string.IsNullOrEmpty(error))
            {
                await WarnIfOnlyServerDown();
            }
        }

        async System.Threading.Tasks.Task OpenTitle(string caption = "Chargement…")
        {
            _loader.Show(caption);
            _loading = true;
            try
            {
                await _scenes.LoadTitleAsync();
                _title.Show();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Liryne] Scène titre : " + ex.Message);
                _title.Show();
            }
            finally
            {
                _loading = false;
                _loader.Hide();
                FlushHeld();
            }
        }

        void FlushHeld()
        {
            while (_heldWhileLoading.Count > 0 && !_loading)
            {
                HandleMessage(_heldWhileLoading.Dequeue());
            }
        }

        async System.Threading.Tasks.Task WarnIfOnlyServerDown()
        {
            if (_serverList.Count != 1)
            {
                return;
            }

            bool reachable = await ServerDirectory.IsWorldReachable(_serverList[0]);
            if (!reachable && _login.IsVisible && !_inWorld)
            {
                _login.SetError("Le serveur n'est pas disponible.");
            }
        }

        void OnTitleAppearance(CharacterAppearance appearance, ushort classId)
        {
            if (_inWorld)
            {
                return;
            }

            if (_charactersUi.IsCreating)
            {
                _title.ShowDraft(appearance, classId);
                return;
            }

            _title.HideDraft();
            _title.Refresh(_charactersUi.SelectedId, appearance, classId);
        }
    }
}
