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
using Liryne.Protocol.Messages.Entities;
using Liryne.Protocol.Messages.Movement;
using Liryne.Protocol.Messages.Session;
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

        WorldStage _stage;
        PreviewStudio _preview;
        LoginScreen _login;
        ServerScreen _servers;
        CharacterScreen _charactersUi;
        HudView _hud;

        IReadOnlyList<ServerInfo> _serverList = Array.Empty<ServerInfo>();
        ServerInfo _server;
        string _token;
        IReadOnlyList<CharacterSummary> _characters = Array.Empty<CharacterSummary>();
        ushort _tickRate = 20;
        float _moveAccum;
        float _pingAccum;
        Vector2 _lastSentDir = new Vector2(float.NaN, float.NaN);
        bool _inWorld;
        bool _busy;

        public bool IsReady => _preview is not null && _hud is not null;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            _serverList = ServerDirectory.Load();
            _stage = new WorldStage(transform);
            Canvas canvas = UiFactory.CreateCanvas(transform);
            if (EventSystem.current is not null)
            {
                EventSystem.current.sendNavigationEvents = false;
            }

            _login = new LoginScreen(canvas.transform, () => _ = ConnectAccount(create: false), () => _ = ConnectAccount(create: true));
            _servers = new ServerScreen(canvas.transform, BackToLogin, server => _ = EnterServer(server));
            _charactersUi = new CharacterScreen(canvas.transform, DisconnectWorld, PlayCharacter, SaveAppearance, CreateCharacter);
            _hud = new HudView(canvas.transform, SendChat, Inspect, ShowSelfProfile, LogoutToCharacters);
            _preview = new PreviewStudio(transform, _charactersUi.Preview);
            _charactersUi.AppearanceChanged += appearance =>
            {
                if (!_inWorld)
                {
                    _preview.Show(appearance);
                }
            };
            _servers.Bind(_serverList);
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

            if (_inWorld)
            {
                TickCamera();
                TickTargeting();
                TickMovement();
                TickPing();
                _stage.FollowCamera();
                _hud.BindVitals(_stage.Local);
                _hud.BindTarget(_stage.Selected);
                _hud.Tick();
            }
            else
            {
                _stage.OrbitLobby(Time.deltaTime);
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
            _world.Dispose();
            _preview?.Dispose();
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
                _login.SetVisible(false);
                _servers.SetError(string.Empty);
                _servers.SetVisible(true);
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
                _server = server;
                WorldWelcome welcome = await _world.ConnectAsync(
                    server.worldHost,
                    server.worldPort,
                    _token,
                    destroyCancellationToken);
                OnWelcome(welcome);
            }
            catch (Exception ex)
            {
                _servers.SetError(ex.Message);
            }
            finally
            {
                _busy = false;
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
                    _charactersUi.Bind(_characters);
                    if (!_inWorld)
                    {
                        _servers.SetVisible(false);
                        _charactersUi.SetVisible(true);
                        _preview.Show(_charactersUi.CurrentAppearance);
                    }

                    break;
                case CreateCharacterResult created:
                    _charactersUi.SetError(ClientCatalog.CreateStatusText(created.Status));
                    break;
                case ChangeAppearanceResult appearance:
                    _charactersUi.SetError(ClientCatalog.AppearanceStatusText(appearance.Status));
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
                _servers.SetVisible(true);
                _charactersUi.SetVisible(false);
                _servers.SetError(ClientCatalog.WelcomeStatusText(welcome.Status));
                return;
            }

            _tickRate = welcome.TickRate == 0 ? (ushort)20 : welcome.TickRate;
            _stage.TickDelta = 1f / _tickRate;
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

            _inWorld = true;
            _lastSentDir = new Vector2(float.NaN, float.NaN);
            _charactersUi.SetVisible(false);
            _preview.Hide();
            _hud.SetVisible(true);
            _hud.HidePopups();
            _stage.Enter(enter.EntityId, enter.Position);
            _hud.AddSystem("Bienvenue dans la Clairière.");
        }

        void PlayCharacter(Guid id)
        {
            if (!_world.TrySend(new EnterWorldRequest(id), out _))
            {
                _charactersUi.SetError("Plus de connexion World.");
            }
        }

        void SaveAppearance(Guid id, CharacterAppearance appearance)
        {
            if (!_world.TrySend(new ChangeAppearance(id, appearance), out _))
            {
                _charactersUi.SetError("Plus de connexion World.");
            }
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
        }

        void TickCamera()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse is null || !mouse.rightButton.isPressed || _hud.ChatFocused)
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

            _stage.PredictLocal(dir.x, dir.y, Time.deltaTime);
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
            _servers.SetVisible(true);
            _charactersUi.SetVisible(false);
            _preview.Hide();
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
                WorldWelcome welcome = await _world.ConnectAsync(
                    _server.worldHost,
                    _server.worldPort,
                    _token,
                    destroyCancellationToken);
                OnWelcome(welcome);
                _servers.SetVisible(false);
                _charactersUi.SetVisible(true);
                _preview.Show(_charactersUi.CurrentAppearance);
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
            _preview.Hide();
            if (string.IsNullOrEmpty(_token))
            {
                ShowLogin();
                _login.SetError(reason);
                return;
            }

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

        void ShowLogin()
        {
            LeaveWorld();
            _world.Disconnect();
            _servers.SetVisible(false);
            _charactersUi.SetVisible(false);
            _preview.Hide();
            _login.SetVisible(true);
            _login.SetError(string.Empty);
        }
    }
}
