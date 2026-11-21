using System;
using System.Collections.Generic;
using Liryne.Game.Content;
using Liryne.Game.World;
using Liryne.Protocol.Messages.Characters;
using Liryne.Protocol.Messages.Chat;
using Liryne.Protocol.Messages.Entities;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Liryne.Game.Ui
{
    public sealed class HudView
    {
        static readonly ChatChannel[] PlayerChannels =
        {
            ChatChannel.Say, ChatChannel.Map, ChatChannel.Trade, ChatChannel.Recruitment, ChatChannel.Staff,
        };

        readonly GameObject _root;
        readonly Text _name;
        readonly Text _hpText;
        readonly Text _mpText;
        readonly Image _hpFill;
        readonly Image _mpFill;
        readonly Transform _chatContent;
        readonly ScrollRect _chatScroll;
        readonly InputField _chatInput;
        readonly Transform _suggestContent;
        readonly GameObject _suggestRoot;
        readonly GameObject _profileRoot;
        readonly Text _profileBody;
        readonly GameObject _inventoryRoot;
        readonly Text _status;
        readonly Action<ChatChannel, string> _onSend;
        readonly Action<Guid> _onInspect;
        readonly Action _onSelfProfile;
        readonly Action _onLogout;
        readonly Button[] _channelButtons;
        readonly GameObject _targetRoot;
        readonly Text _targetName;
        readonly Text _targetMeta;
        readonly Text _targetHpText;
        readonly Text _targetMpText;
        readonly Image _targetHpFill;
        readonly Image _targetMpFill;

        ChatChannel _channel = ChatChannel.Say;
        IReadOnlyList<CommandInfo> _commands = Array.Empty<CommandInfo>();
        IReadOnlyList<string> _playerNames = Array.Empty<string>();

        public HudView(
            Transform canvas,
            Action<ChatChannel, string> onSend,
            Action<Guid> onInspect,
            Action onSelfProfile,
            Action onLogout)
        {
            _onSend = onSend;
            _onInspect = onInspect;
            _onSelfProfile = onSelfProfile;
            _onLogout = onLogout;

            Image root = UiFactory.Panel(canvas, "Hud", new Color(0, 0, 0, 0));
            UiFactory.Stretch(root.rectTransform);
            root.raycastTarget = false;
            _root = root.gameObject;

            Image vitals = UiFactory.Panel(root.transform, "Vitals", UiTheme.Panel);
            UiFactory.Anchor(vitals.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -118), new Vector2(360, -16));
            _name = UiFactory.Label(vitals.transform, "Name", 20, UiTheme.Gold, FontStyle.Bold, TextAnchor.MiddleLeft);
            UiFactory.Anchor(_name.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -32), new Vector2(-12, -6));
            UiFactory.Bar(vitals.transform, "Hp", UiTheme.Hp, out _hpFill);
            UiFactory.Anchor(_hpFill.transform.parent.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -58), new Vector2(-12, -36));
            _hpText = UiFactory.Label(_hpFill.transform.parent, "HpText", 14, UiTheme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiFactory.Stretch(_hpText.rectTransform);
            UiFactory.Bar(vitals.transform, "Mp", UiTheme.Mp, out _mpFill);
            UiFactory.Anchor(_mpFill.transform.parent.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -88), new Vector2(-12, -66));
            _mpText = UiFactory.Label(_mpFill.transform.parent, "MpText", 14, UiTheme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiFactory.Stretch(_mpText.rectTransform);

            Button profile = UiFactory.Button(root.transform, "ProfileBtn", "Profil (C)", UiTheme.Panel);
            UiFactory.Anchor(profile.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-200, -60), new Vector2(-16, -16));
            profile.onClick.AddListener(() => ToggleProfile());

            Button inventory = UiFactory.Button(root.transform, "InvBtn", "Sac (I)", UiTheme.Panel);
            UiFactory.Anchor(inventory.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-200, -112), new Vector2(-16, -68));
            inventory.onClick.AddListener(ToggleInventory);

            Button logout = UiFactory.Button(root.transform, "LogoutBtn", "Déconnexion", UiTheme.Slot);
            UiFactory.Anchor(logout.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-200, -164), new Vector2(-16, -120));
            logout.onClick.AddListener(() => _onLogout());

            _targetRoot = BuildTarget(root.transform, out _targetName, out _targetMeta, out _targetHpFill, out _targetHpText, out _targetMpFill, out _targetMpText);

            Image chat = UiFactory.Panel(root.transform, "Chat", UiTheme.Panel);
            UiFactory.Anchor(chat.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 16), new Vector2(520, 280));
            _chatScroll = UiFactory.Scroll(chat.transform, "Log", out _chatContent);
            UiFactory.Anchor(_chatScroll.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 1), new Vector2(8, 78), new Vector2(-8, -40));
            PinChatToBottom();

            _channelButtons = new Button[PlayerChannels.Length];
            for (int i = 0; i < PlayerChannels.Length; i++)
            {
                ChatChannel channel = PlayerChannels[i];
                Button btn = UiFactory.Button(chat.transform, channel.ToString(), ClientCatalog.ChannelLabel(channel), UiTheme.Slot);
                float x = 8 + i * 100;
                UiFactory.Anchor(btn.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -34), new Vector2(x + 96, -6));
                btn.onClick.AddListener(() => SetChannel(channel));
                _channelButtons[i] = btn;
            }

            _chatInput = UiFactory.Field(chat.transform, "Input", "Écrire…  Entrée pour envoyer", false);
            UiFactory.Anchor(_chatInput.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0), new Vector2(8, 8), new Vector2(-8, 48));
            _chatInput.characterLimit = 200;
            _chatInput.textComponent.fontSize = ChatFontSize;
            if (_chatInput.placeholder is Text placeholder)
            {
                placeholder.fontSize = ChatFontSize;
            }
            _chatInput.onSubmit.AddListener(_ => SendChat());
            _chatInput.onValueChanged.AddListener(OnChatChanged);

            Image suggest = UiFactory.Panel(chat.transform, "Suggest", UiTheme.PanelInner);
            UiFactory.Anchor(suggest.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(8, 52), new Vector2(-8, 160));
            _suggestRoot = suggest.gameObject;
            UiFactory.Scroll(suggest.transform, "SuggestList", out _suggestContent);
            UiFactory.Stretch(_suggestContent.parent.GetComponent<RectTransform>(), 4, 4, 4, 4);
            _suggestRoot.SetActive(false);

            _status = UiFactory.Label(root.transform, "Status", 16, UiTheme.Muted, FontStyle.Normal, TextAnchor.LowerRight);
            UiFactory.Anchor(_status.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-420, 16), new Vector2(-16, 48));
            _status.text = "WASD · clic droit caméra · Tab cible · Entrée chat · C profil · I sac";

            _profileRoot = BuildProfile(root.transform);
            _profileBody = _profileRoot.transform.Find("Body").GetComponent<Text>();
            _inventoryRoot = BuildInventory(root.transform);
            SetChannel(ChatChannel.Say);
            _root.SetActive(false);
        }

        const int ChatFontSize = 18;
        const int ChatLineHeight = 22;

        public bool ChatFocused => _chatInput.isFocused;

        public void SetVisible(bool visible) => _root.SetActive(visible);

        public void SetCommands(IReadOnlyList<CommandInfo> commands) =>
            _commands = commands ?? Array.Empty<CommandInfo>();

        public void SetPlayerNames(IReadOnlyList<string> names) =>
            _playerNames = names ?? Array.Empty<string>();

        public void BindVitals(EntityAvatar avatar)
        {
            if (avatar?.LastState is null)
            {
                return;
            }

            EntityState state = avatar.LastState;
            _name.text = $"{avatar.DisplayName}  ·  {ClientCatalog.ClassName(avatar.ClassId)}  ·  {state.Hp}/{state.MaxHp}";
            _hpFill.fillAmount = state.MaxHp == 0 ? 0 : (float)state.Hp / state.MaxHp;
            _mpFill.fillAmount = state.MaxMp == 0 ? 0 : (float)state.Mp / state.MaxMp;
            _hpText.text = $"{state.Hp} / {state.MaxHp}";
            _mpText.text = $"{state.Mp} / {state.MaxMp}";
        }

        public void BindTarget(EntityAvatar avatar)
        {
            if (avatar is null)
            {
                _targetRoot.SetActive(false);
                return;
            }

            _targetRoot.SetActive(true);
            _targetName.text = string.IsNullOrEmpty(avatar.DisplayName) ? "Cible" : avatar.DisplayName;
            _targetMeta.text = TargetMeta(avatar);
            EntityState state = avatar.LastState;
            if (state is null)
            {
                _targetHpFill.fillAmount = 0f;
                _targetMpFill.fillAmount = 0f;
                _targetHpText.text = "—";
                _targetMpText.text = "—";
                return;
            }

            _targetHpFill.fillAmount = state.MaxHp == 0 ? 0f : (float)state.Hp / state.MaxHp;
            _targetMpFill.fillAmount = state.MaxMp == 0 ? 0f : (float)state.Mp / state.MaxMp;
            _targetHpText.text = $"{state.Hp} / {state.MaxHp}";
            _targetMpText.text = $"{state.Mp} / {state.MaxMp}";
        }

        public void AddChat(ChatMessage message)
        {
            Text line = UiFactory.Label(_chatContent, "Line", ChatFontSize, UiTheme.Text, FontStyle.Normal, TextAnchor.MiddleLeft);
            line.horizontalOverflow = HorizontalWrapMode.Overflow;
            line.verticalOverflow = VerticalWrapMode.Truncate;
            line.supportRichText = true;
            var le = line.gameObject.AddComponent<LayoutElement>();
            le.minHeight = ChatLineHeight;
            le.preferredHeight = ChatLineHeight;
            le.flexibleHeight = 0;
            string channel = $"<color=#{ColorUtility.ToHtmlStringRGB(ClientCatalog.ChannelColor(message.Channel))}>[{ClientCatalog.ChannelLabel(message.Channel)}]</color>";
            if (string.IsNullOrEmpty(message.SenderName))
            {
                line.text = channel + "  " + message.Text;
            }
            else
            {
                string who = $"<color=#{ColorUtility.ToHtmlStringRGB(StaffColor(message.SenderFlags))}>{message.SenderName}</color>";
                line.text = channel + "  " + who + "  " + message.Text;
                var click = line.gameObject.AddComponent<Button>();
                ColorBlock colors = click.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = Color.white;
                click.colors = colors;
                Guid id = message.SenderEntityId;
                click.onClick.AddListener(() => _onInspect(id));
                line.raycastTarget = true;
            }

            Canvas.ForceUpdateCanvases();
            _chatScroll.verticalNormalizedPosition = 0f;
        }

        public void AddSystem(string text)
        {
            AddChat(new ChatMessage(ChatChannel.System, Guid.Empty, string.Empty, ChatSenderFlags.None, false, text, 0));
        }

        public void ShowProfile(string title, string body)
        {
            _profileRoot.SetActive(true);
            _profileRoot.transform.Find("Title").GetComponent<Text>().text = title;
            _profileBody.text = body;
        }

        public void HidePopups()
        {
            _profileRoot.SetActive(false);
            _inventoryRoot.SetActive(false);
            _suggestRoot.SetActive(false);
        }

        public void Tick()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard is null)
            {
                return;
            }

            if (_chatInput.isFocused)
            {
                if (keyboard.tabKey.wasPressedThisFrame)
                {
                    CompleteCommand();
                }

                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    _chatInput.DeactivateInputField();
                    _suggestRoot.SetActive(false);
                }

                return;
            }

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                _chatInput.ActivateInputField();
            }
            else if (keyboard.cKey.wasPressedThisFrame)
            {
                ToggleProfile();
            }
            else if (keyboard.iKey.wasPressedThisFrame)
            {
                ToggleInventory();
            }
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                HidePopups();
            }
#endif
        }

        void ToggleProfile()
        {
            if (_profileRoot.activeSelf)
            {
                _profileRoot.SetActive(false);
                return;
            }

            _inventoryRoot.SetActive(false);
            _onSelfProfile();
        }

        void ToggleInventory()
        {
            bool next = !_inventoryRoot.activeSelf;
            _inventoryRoot.SetActive(next);
            if (next)
            {
                _profileRoot.SetActive(false);
            }
        }

        void SetChannel(ChatChannel channel)
        {
            _channel = channel;
            for (int i = 0; i < PlayerChannels.Length; i++)
            {
                Image image = _channelButtons[i].GetComponent<Image>();
                image.color = PlayerChannels[i] == channel ? UiTheme.Accent : UiTheme.Slot;
            }
        }

        void SendChat()
        {
            string text = _chatInput.text.Trim();
            _chatInput.text = string.Empty;
            _suggestRoot.SetActive(false);
            _chatInput.ActivateInputField();
            if (text.Length == 0)
            {
                return;
            }

            _onSend(_channel, text);
        }

        void OnChatChanged(string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] != '/')
            {
                _suggestRoot.SetActive(false);
                return;
            }

            string typed = value.TrimStart('/');
            string[] parts = typed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string token = parts.Length == 0 ? string.Empty : parts[0];
            for (int i = _suggestContent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(_suggestContent.GetChild(i).gameObject);
            }

            int shown = 0;
            foreach (CommandInfo command in _commands)
            {
                if (!Matches(command, token))
                {
                    continue;
                }

                CommandInfo captured = command;
                Button row = UiFactory.Button(_suggestContent, command.Name, command.Usage + "  —  " + command.Description, UiTheme.Slot);
                var le = row.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 28;
                le.preferredHeight = 28;
                row.GetComponentInChildren<Text>().fontSize = 14;
                row.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                row.onClick.AddListener(() =>
                {
                    _chatInput.text = "/" + captured.Name + " ";
                    _chatInput.caretPosition = _chatInput.text.Length;
                    _chatInput.ActivateInputField();
                });
                shown++;
                if (shown >= 8)
                {
                    break;
                }
            }

            _suggestRoot.SetActive(shown > 0);
        }

        void CompleteCommand()
        {
            if (_suggestContent.childCount == 0)
            {
                return;
            }

            _suggestContent.GetChild(0).GetComponent<Button>()?.onClick.Invoke();
        }

        static bool Matches(CommandInfo command, string token)
        {
            if (command.Name.StartsWith(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (string alias in command.Aliases)
            {
                if (alias.StartsWith(token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return token.Length == 0;
        }

        static Color StaffColor(ChatSenderFlags flags)
        {
            if ((flags & ChatSenderFlags.Administrator) != 0)
            {
                return new Color(1f, 0.55f, 0.45f);
            }

            if ((flags & ChatSenderFlags.Moderator) != 0)
            {
                return new Color(0.55f, 0.85f, 1f);
            }

            return UiTheme.Gold;
        }

        void PinChatToBottom()
        {
            RectTransform content = _chatContent.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(1f, 0f);
            content.pivot = new Vector2(0.5f, 0f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);
            var layout = _chatContent.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.spacing = 2;
            layout.padding = new RectOffset(6, 4, 4, 4);
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
        }

        GameObject BuildTarget(
            Transform parent,
            out Text name,
            out Text meta,
            out Image hpFill,
            out Text hpText,
            out Image mpFill,
            out Text mpText)
        {
            Image panel = UiFactory.Panel(parent, "Target", UiTheme.Panel);
            UiFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-190, -150), new Vector2(190, -16));
            name = UiFactory.Label(panel.transform, "Name", 20, UiTheme.Gold, FontStyle.Bold, TextAnchor.MiddleLeft);
            UiFactory.Anchor(name.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -32), new Vector2(-12, -6));
            meta = UiFactory.Label(panel.transform, "Meta", 15, UiTheme.Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
            UiFactory.Anchor(meta.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -56), new Vector2(-12, -32));
            UiFactory.Bar(panel.transform, "Hp", UiTheme.Hp, out hpFill);
            UiFactory.Anchor(hpFill.transform.parent.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -86), new Vector2(-12, -64));
            hpText = UiFactory.Label(hpFill.transform.parent, "HpText", 14, UiTheme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiFactory.Stretch(hpText.rectTransform);
            UiFactory.Bar(panel.transform, "Mp", UiTheme.Mp, out mpFill);
            UiFactory.Anchor(mpFill.transform.parent.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -116), new Vector2(-12, -94));
            mpText = UiFactory.Label(mpFill.transform.parent, "MpText", 14, UiTheme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiFactory.Stretch(mpText.rectTransform);
            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        static string TargetMeta(EntityAvatar avatar)
        {
            string kind = avatar.Kind == EntityKind.Monster ? "Monstre" : ClientCatalog.ClassName(avatar.ClassId);
            var parts = new List<string>
            {
                kind,
                "niveau " + avatar.Level,
            };
            if (avatar.LastState is not null)
            {
                parts.Add(ClientCatalog.AffinityName(avatar.LastState.Affinity));
            }

            string title = ClientCatalog.TitleName(avatar.TitleId);
            if (avatar.TitleId != 0 && title != "—")
            {
                parts.Add(title);
            }

            string ornament = ClientCatalog.OrnamentName(avatar.OrnamentId);
            if (avatar.OrnamentId != 0 && ornament != "—")
            {
                parts.Add(ornament);
            }

            return string.Join("  ·  ", parts);
        }

        GameObject BuildProfile(Transform parent)
        {
            Image panel = UiFactory.Panel(parent, "Profile", UiTheme.Panel);
            UiFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-260, -220), new Vector2(260, 220));
            Text title = UiFactory.Label(panel.transform, "Title", 26, UiTheme.Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiFactory.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -52), new Vector2(-16, -12));
            title.text = "Profil";
            Text body = UiFactory.Label(panel.transform, "Body", 20, UiTheme.Text, FontStyle.Normal, TextAnchor.UpperLeft);
            UiFactory.Anchor(body.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(24, 56), new Vector2(-24, -60));
            Button close = UiFactory.Button(panel.transform, "Close", "Fermer", UiTheme.Slot);
            UiFactory.Anchor(close.GetComponent<RectTransform>(), new Vector2(0.3f, 0), new Vector2(0.7f, 0), new Vector2(0, 12), new Vector2(0, 48));
            close.onClick.AddListener(() => panel.gameObject.SetActive(false));
            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        GameObject BuildInventory(Transform parent)
        {
            Image panel = UiFactory.Panel(parent, "Inventory", UiTheme.Panel);
            UiFactory.Anchor(panel.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-420, -280), new Vector2(-16, 280));
            Text title = UiFactory.Label(panel.transform, "Title", 24, UiTheme.Gold, FontStyle.Bold, TextAnchor.MiddleLeft);
            UiFactory.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -48), new Vector2(-16, -12));
            title.text = "Inventaire";
            Text hint = UiFactory.Label(panel.transform, "Hint", 14, UiTheme.Muted, FontStyle.Italic, TextAnchor.UpperLeft);
            UiFactory.Anchor(hint.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -78), new Vector2(-16, -48));
            hint.text = "Les objets arriveront avec le système d'inventaire. Les emplacements sont prêts.";

            string[] slots = { "Tête", "Torse", "Arme", "Mains", "Pieds", "Accessoire" };
            for (int i = 0; i < slots.Length; i++)
            {
                Image slot = UiFactory.Panel(panel.transform, "Eq" + i, UiTheme.Slot);
                float x = 16 + (i % 3) * 128;
                float y = -92 - (i / 3) * 72;
                UiFactory.Anchor(slot.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y - 64), new Vector2(x + 120, y));
                Text label = UiFactory.Label(slot.transform, "L", 14, UiTheme.Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
                UiFactory.Stretch(label.rectTransform);
                label.text = slots[i];
            }

            for (int i = 0; i < 24; i++)
            {
                Image slot = UiFactory.Panel(panel.transform, "Bag" + i, UiTheme.Slot);
                float x = 16 + (i % 6) * 64;
                float y = -250 - (i / 6) * 64;
                UiFactory.Anchor(slot.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y - 56), new Vector2(x + 56, y));
            }

            Button close = UiFactory.Button(panel.transform, "Close", "Fermer", UiTheme.Slot);
            UiFactory.Anchor(close.GetComponent<RectTransform>(), new Vector2(0.25f, 0), new Vector2(0.75f, 0), new Vector2(0, 12), new Vector2(0, 48));
            close.onClick.AddListener(() => panel.gameObject.SetActive(false));
            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }
    }
}
