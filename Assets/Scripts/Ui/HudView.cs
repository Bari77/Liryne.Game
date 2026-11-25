using System;
using System.Collections.Generic;
using Liryne.Game.Content;
using Liryne.Game.World;
using Liryne.Protocol.Messages.Characters;
using Liryne.Protocol.Messages.Chat;
using Liryne.Protocol.Messages.Combat;
using Liryne.Protocol.Messages.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
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
        readonly RawImage _profilePreview;
        readonly GameObject _inventoryRoot;
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
        readonly Image[] _spellSlots = new Image[SpellSlots];
        readonly Text[] _spellLabels = new Text[SpellSlots];
        readonly Text[] _spellCool = new Text[SpellSlots];
        readonly Action<ushort> _onCast;
        readonly HudLayoutStore _layout = new HudLayoutStore();
        readonly RectTransform[] _movable = new RectTransform[5];
        readonly int[] _bar = new int[SpellSlots];
        readonly Dictionary<ushort, uint> _cooldownEnds = new Dictionary<ushort, uint>();

        ChatChannel _channel = ChatChannel.Say;
        uint _serverTick;
        ushort _tickRate = 20;
        IReadOnlyList<CommandInfo> _commands = Array.Empty<CommandInfo>();
        IReadOnlyList<string> _playerNames = Array.Empty<string>();
        RectTransform _ghost;
        Text _ghostLabel;
        Canvas _canvas;

        public RawImage ProfilePreview => _profilePreview;

        public bool ProfileVisible => _profileRoot.activeSelf;

        public const int SpellSlots = 30;
        const int SpellCols = 10;
        const int SpellRows = 3;

        public HudView(
            Transform canvas,
            Action<ChatChannel, string> onSend,
            Action<Guid> onInspect,
            Action onSelfProfile,
            Action onLogout,
            Action<ushort> onCast)
        {
            _onSend = onSend;
            _onInspect = onInspect;
            _onSelfProfile = onSelfProfile;
            _onLogout = onLogout;
            _onCast = onCast;

            Image root = UiFactory.Panel(canvas, "Hud", new Color(0, 0, 0, 0));
            UiFactory.Stretch(root.rectTransform);
            root.raycastTarget = false;
            _root = root.gameObject;
            _canvas = canvas.GetComponent<Canvas>();

            Image vitals = UiFactory.Panel(root.transform, "Vitals", UiTheme.Panel);
            UiFactory.Anchor(vitals.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -134), new Vector2(360, -16));
            MakeGrip(vitals.rectTransform);
            _name = UiFactory.Label(vitals.transform, "Name", 20, UiTheme.Gold, FontStyle.Bold, TextAnchor.MiddleLeft);
            UiFactory.Anchor(_name.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -48), new Vector2(-12, -22));
            UiFactory.Bar(vitals.transform, "Hp", UiTheme.Hp, out _hpFill);
            UiFactory.Anchor(_hpFill.transform.parent.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -74), new Vector2(-12, -52));
            _hpText = UiFactory.Label(_hpFill.transform.parent, "HpText", 14, UiTheme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiFactory.Stretch(_hpText.rectTransform);
            UiFactory.Bar(vitals.transform, "Mp", UiTheme.Mp, out _mpFill);
            UiFactory.Anchor(_mpFill.transform.parent.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -104), new Vector2(-12, -82));
            _mpText = UiFactory.Label(_mpFill.transform.parent, "MpText", 14, UiTheme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiFactory.Stretch(_mpText.rectTransform);

            Image menu = UiFactory.Panel(root.transform, "Menu", UiTheme.Panel);
            UiFactory.Anchor(menu.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-200, -196), new Vector2(-16, -16));
            MakeGrip(menu.rectTransform);
            Button profile = UiFactory.Button(menu.transform, "ProfileBtn", "Profil (C)", UiTheme.PanelInner);
            UiFactory.Anchor(profile.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -64), new Vector2(-8, -24));
            profile.onClick.AddListener(() => ToggleProfile());
            Button inventory = UiFactory.Button(menu.transform, "InvBtn", "Sac (I)", UiTheme.PanelInner);
            UiFactory.Anchor(inventory.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -112), new Vector2(-8, -72));
            inventory.onClick.AddListener(ToggleInventory);
            Button logout = UiFactory.Button(menu.transform, "LogoutBtn", "Déconnexion", UiTheme.Slot);
            UiFactory.Anchor(logout.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -160), new Vector2(-8, -120));
            logout.onClick.AddListener(() => _onLogout());

            _targetRoot = BuildTarget(root.transform, out _targetName, out _targetMeta, out _targetHpFill, out _targetHpText, out _targetMpFill, out _targetMpText);

            Image chat = UiFactory.Panel(root.transform, "Chat", UiTheme.Panel);
            UiFactory.Anchor(chat.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 16), new Vector2(520, 296));
            MakeGrip(chat.rectTransform);
            _chatScroll = UiFactory.Scroll(chat.transform, "Log", out _chatContent);
            UiFactory.Anchor(_chatScroll.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 1), new Vector2(8, 78), new Vector2(-8, -56));
            PinChatToBottom();

            _channelButtons = new Button[PlayerChannels.Length];
            for (int i = 0; i < PlayerChannels.Length; i++)
            {
                ChatChannel channel = PlayerChannels[i];
                Button btn = UiFactory.Button(chat.transform, channel.ToString(), ClientCatalog.ChannelLabel(channel), UiTheme.Slot);
                float x = 8 + i * 100;
                UiFactory.Anchor(btn.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -50), new Vector2(x + 96, -22));
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

            Image bar = UiFactory.Panel(root.transform, "Spells", UiTheme.Panel);
            UiFactory.Anchor(bar.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-296, 16), new Vector2(296, 220));
            MakeGrip(bar.rectTransform);
            const float slot = 52f;
            const float gap = 4f;
            const float pad = 8f;
            for (int i = 0; i < SpellSlots; i++)
            {
                int col = i % SpellCols;
                int row = i / SpellCols;
                float x = pad + col * (slot + gap);
                float y = pad + row * (slot + gap);
                Image cell = UiFactory.Panel(bar.transform, "Spell" + i, UiTheme.Slot);
                UiFactory.Anchor(cell.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, y), new Vector2(x + slot, y + slot));
                Text label = UiFactory.Label(cell.transform, "Label", 12, UiTheme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
                UiFactory.Stretch(label.rectTransform, 2, 4, 2, 10);
                Text cool = UiFactory.Label(cell.transform, "Cd", 12, UiTheme.Gold, FontStyle.Bold, TextAnchor.LowerCenter);
                UiFactory.Anchor(cool.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 2), new Vector2(-1, 16));
                var hook = cell.gameObject.AddComponent<SpellBarSlot>();
                hook.Index = i;
                hook.Clicked = CastSlot;
                hook.DragBegan = BeginSpellDrag;
                hook.Dragged = MoveSpellGhost;
                hook.Ended = EndSpellDrag;
                hook.Dropped = DropSpell;
                _spellSlots[i] = cell;
                _spellLabels[i] = label;
                _spellCool[i] = cool;
            }

            _movable[0] = vitals.rectTransform;
            _movable[1] = menu.rectTransform;
            _movable[2] = _targetRoot.GetComponent<RectTransform>();
            _movable[3] = chat.rectTransform;
            _movable[4] = bar.rectTransform;
            ApplySavedLayout();

            _profileRoot = BuildProfile(root.transform);
            _profileBody = _profileRoot.transform.Find("Body").GetComponent<Text>();
            _profilePreview = _profileRoot.transform.Find("PreviewFrame/Preview").GetComponent<RawImage>();
            _inventoryRoot = BuildInventory(root.transform);
            SetChannel(ChatChannel.Say);
            _root.SetActive(false);
        }

        const int ChatFontSize = 18;
        const int ChatLineHeight = 22;

        public bool ChatFocused => _chatInput.isFocused;

        public void SetVisible(bool visible)
        {
            _root.SetActive(visible);
            if (!visible)
            {
                _layout.Flush();
            }
        }

        public void FlushLayout() => _layout.Flush();

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

        public void SetTickRate(ushort tickRate) => _tickRate = tickRate == 0 ? (ushort)20 : tickRate;

        public void SetServerTick(uint tick)
        {
            _serverTick = tick;
            RefreshSpells();
        }

        public void BindSpellBook(SpellBook book)
        {
            var known = new HashSet<ushort>();
            if (book?.Spells is not null)
            {
                for (int i = 0; i < book.Spells.Count; i++)
                {
                    SpellSlot slot = book.Spells[i];
                    if (slot.SpellId == 0)
                    {
                        continue;
                    }

                    known.Add(slot.SpellId);
                    _cooldownEnds[slot.SpellId] = slot.CooldownEndTick;
                }
            }

            bool empty = true;
            for (int i = 0; i < SpellSlots; i++)
            {
                if (_bar[i] == 0)
                {
                    continue;
                }

                if (!known.Contains((ushort)_bar[i]))
                {
                    _bar[i] = 0;
                }
                else
                {
                    empty = false;
                }
            }

            if (book?.Spells is null)
            {
                RefreshSpells();
                return;
            }

            if (empty)
            {
                int write = 0;
                for (int i = 0; i < book.Spells.Count && write < SpellSlots; i++)
                {
                    if (book.Spells[i].SpellId != 0)
                    {
                        _bar[write++] = book.Spells[i].SpellId;
                    }
                }
            }
            else
            {
                for (int i = 0; i < book.Spells.Count; i++)
                {
                    ushort id = book.Spells[i].SpellId;
                    if (id == 0 || IndexOfSpell(id) >= 0)
                    {
                        continue;
                    }

                    int hole = FirstEmptySlot();
                    if (hole >= 0)
                    {
                        _bar[hole] = id;
                    }
                }
            }

            PersistBar();
            RefreshSpells();
        }

        public void ApplyCastResult(SpellCastResult result)
        {
            if (result.SpellId == 0)
            {
                return;
            }

            if (result.CooldownEndTick != 0)
            {
                _cooldownEnds[result.SpellId] = result.CooldownEndTick;
            }

            RefreshSpells();
        }

        public bool TrySpellAt(int slotIndex, out ushort spellId)
        {
            spellId = 0;
            if (slotIndex < 0 || slotIndex >= SpellSlots)
            {
                return false;
            }

            spellId = (ushort)_bar[slotIndex];
            return spellId != 0;
        }

        void CastSlot(int slot)
        {
            if (TrySpellAt(slot, out ushort spellId))
            {
                _onCast?.Invoke(spellId);
            }
        }

        void RefreshSpells()
        {
            float rate = Mathf.Max(1, _tickRate);
            for (int i = 0; i < SpellSlots; i++)
            {
                ushort id = (ushort)_bar[i];
                if (id == 0)
                {
                    _spellLabels[i].text = i < SpellCols ? HotkeyLabel(i) : string.Empty;
                    _spellLabels[i].color = UiTheme.Muted;
                    _spellCool[i].text = string.Empty;
                    continue;
                }

                _spellLabels[i].color = UiTheme.Text;
                string key = i < SpellCols ? HotkeyLabel(i) + "\n" : string.Empty;
                _spellLabels[i].text = key + ClientCatalog.SpellName(id);
                uint end = _cooldownEnds.TryGetValue(id, out uint tick) ? tick : 0;
                uint remain = end > _serverTick ? end - _serverTick : 0;
                _spellCool[i].text = remain == 0 ? string.Empty : (remain / rate).ToString("0.0");
            }
        }

        static string HotkeyLabel(int slot) => slot == 9 ? "0" : (slot + 1).ToString();

        int IndexOfSpell(ushort spellId)
        {
            for (int i = 0; i < SpellSlots; i++)
            {
                if (_bar[i] == spellId)
                {
                    return i;
                }
            }

            return -1;
        }

        int FirstEmptySlot()
        {
            for (int i = 0; i < SpellSlots; i++)
            {
                if (_bar[i] == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        void MakeGrip(RectTransform panel)
        {
            Image grip = UiFactory.Panel(panel, "Move", new Color(1f, 1f, 1f, 0.1f));
            UiFactory.Anchor(grip.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(4, -16), new Vector2(-4, -4));
            grip.raycastTarget = true;
            var drag = grip.gameObject.AddComponent<HudDrag>();
            drag.Target = panel;
            drag.Moved = OnPanelMoved;
        }

        void OnPanelMoved()
        {
            CapturePanelPoses();
            _layout.MarkDirty();
        }

        void ApplySavedLayout()
        {
            if (_layout.Data.Bar is not null && _layout.Data.Bar.Length == SpellSlots)
            {
                Array.Copy(_layout.Data.Bar, _bar, SpellSlots);
            }

            if (_layout.Data.Panels is null)
            {
                return;
            }

            for (int i = 0; i < _layout.Data.Panels.Length; i++)
            {
                HudPanelPose pose = _layout.Data.Panels[i];
                if (pose is null || string.IsNullOrEmpty(pose.Id))
                {
                    continue;
                }

                for (int p = 0; p < _movable.Length; p++)
                {
                    if (_movable[p] is not null && _movable[p].name == pose.Id)
                    {
                        _movable[p].anchoredPosition = new Vector2(pose.X, pose.Y);
                        break;
                    }
                }
            }
        }

        void CapturePanelPoses()
        {
            var poses = new HudPanelPose[_movable.Length];
            for (int i = 0; i < _movable.Length; i++)
            {
                RectTransform rt = _movable[i];
                Vector2 pos = rt.anchoredPosition;
                poses[i] = new HudPanelPose { Id = rt.name, X = pos.x, Y = pos.y };
            }

            _layout.Data.Panels = poses;
        }

        void PersistBar()
        {
            Array.Copy(_bar, _layout.Data.Bar, SpellSlots);
            _layout.MarkDirty();
        }

        void BeginSpellDrag(int index, PointerEventData eventData)
        {
            if (_bar[index] == 0)
            {
                return;
            }

            EnsureGhost();
            _ghost.gameObject.SetActive(true);
            _ghost.SetAsLastSibling();
            _ghostLabel.text = ClientCatalog.SpellName((ushort)_bar[index]);
            MoveSpellGhost(eventData);
        }

        void MoveSpellGhost(PointerEventData eventData)
        {
            if (_ghost is null || !_ghost.gameObject.activeSelf)
            {
                return;
            }

            RectTransform canvasRt = _canvas.transform as RectTransform;
            if (canvasRt is null)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRt, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                _ghost.anchoredPosition = local;
            }
        }

        void EndSpellDrag()
        {
            if (_ghost is not null)
            {
                _ghost.gameObject.SetActive(false);
            }
        }

        void DropSpell(int from, int to)
        {
            if (from < 0 || to < 0 || from >= SpellSlots || to >= SpellSlots || from == to)
            {
                return;
            }

            if (_bar[from] == 0)
            {
                return;
            }

            int held = _bar[from];
            _bar[from] = _bar[to];
            _bar[to] = held;
            PersistBar();
            RefreshSpells();
        }

        void EnsureGhost()
        {
            if (_ghost is not null)
            {
                return;
            }

            Image ghost = UiFactory.Panel(_canvas.transform, "SpellGhost", new Color(UiTheme.Slot.r, UiTheme.Slot.g, UiTheme.Slot.b, 0.88f));
            ghost.raycastTarget = false;
            ghost.rectTransform.sizeDelta = new Vector2(56f, 56f);
            ghost.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            ghost.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _ghostLabel = UiFactory.Label(ghost.transform, "Label", 12, UiTheme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiFactory.Stretch(_ghostLabel.rectTransform, 2, 2, 2, 2);
            _ghostLabel.raycastTarget = false;
            _ghost = ghost.rectTransform;
            _ghost.gameObject.SetActive(false);
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
            _layout.Tick();
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
            UiFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-190, -166), new Vector2(190, -16));
            MakeGrip(panel.rectTransform);
            name = UiFactory.Label(panel.transform, "Name", 20, UiTheme.Gold, FontStyle.Bold, TextAnchor.MiddleLeft);
            UiFactory.Anchor(name.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -48), new Vector2(-12, -22));
            meta = UiFactory.Label(panel.transform, "Meta", 15, UiTheme.Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
            UiFactory.Anchor(meta.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -72), new Vector2(-12, -48));
            UiFactory.Bar(panel.transform, "Hp", UiTheme.Hp, out hpFill);
            UiFactory.Anchor(hpFill.transform.parent.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -102), new Vector2(-12, -80));
            hpText = UiFactory.Label(hpFill.transform.parent, "HpText", 14, UiTheme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiFactory.Stretch(hpText.rectTransform);
            UiFactory.Bar(panel.transform, "Mp", UiTheme.Mp, out mpFill);
            UiFactory.Anchor(mpFill.transform.parent.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -132), new Vector2(-12, -110));
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
            UiFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-420, -250), new Vector2(420, 250));
            Text title = UiFactory.Label(panel.transform, "Title", 26, UiTheme.Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiFactory.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -52), new Vector2(-16, -12));
            title.text = "Profil";

            Image frame = UiFactory.Panel(panel.transform, "PreviewFrame", UiTheme.PanelInner);
            UiFactory.Anchor(frame.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(20, 56), new Vector2(250, -60));
            var rawGo = new GameObject("Preview");
            rawGo.transform.SetParent(frame.transform, false);
            RawImage preview = rawGo.AddComponent<RawImage>();
            preview.color = Color.white;
            preview.raycastTarget = true;
            UiFactory.Stretch(preview.rectTransform, 8, 8, 8, 8);

            Text body = UiFactory.Label(panel.transform, "Body", 20, UiTheme.Text, FontStyle.Normal, TextAnchor.UpperLeft);
            UiFactory.Anchor(body.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(270, 56), new Vector2(-24, -60));
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
