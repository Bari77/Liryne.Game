using System;
using System.Collections.Generic;
using Liryne.Game.Content;
using Liryne.Protocol;
using Liryne.Protocol.Messages;
using Liryne.Protocol.Messages.Characters;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Liryne.Game.Ui
{
    public sealed class CharacterScreen
    {
        readonly GameObject _root;
        readonly GameObject _picker;
        readonly Text _name;
        readonly Text _identity;
        readonly Image _classMark;
        readonly Image _affinityMark;
        readonly Button _prev;
        readonly Button _next;
        readonly Text _error;
        readonly GameObject _createPanel;
        readonly InputField _nameField;
        readonly Text _classLabel;
        readonly Action<Guid> _onPlay;
        readonly Action _onWardrobe;
        readonly Action<string, ushort, CharacterAppearance> _onCreate;

        IReadOnlyList<CharacterSummary> _characters = Array.Empty<CharacterSummary>();
        Guid _selected;
        ushort _createClass = 1;
        CharacterAppearance _draft = CharacterAppearance.Default;
        bool _creating;

        public RawImage Preview { get; }

        public CharacterScreen(
            Transform canvas,
            Action onBack,
            Action<Guid> onPlay,
            Action onWardrobe,
            Action<string, ushort, CharacterAppearance> onCreate)
        {
            _onPlay = onPlay;
            _onWardrobe = onWardrobe;
            _onCreate = onCreate;

            Image backdrop = UiFactory.Panel(canvas, "Characters", Color.clear);
            UiFactory.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = false;
            _root = backdrop.gameObject;

            TitleChrome.Attach(backdrop.transform, TitleChrome.LogoPlace.Compact);

            var rawGo = new GameObject("Raw");
            rawGo.transform.SetParent(backdrop.transform, false);
            rawGo.SetActive(false);
            Preview = rawGo.AddComponent<RawImage>();
            Preview.color = Color.white;

            Image info = UiFactory.Panel(backdrop.transform, "Info", UiTheme.Panel);
            UiFactory.Anchor(info.rectTransform, new Vector2(0.25f, 0f), new Vector2(0.75f, 0f), new Vector2(6, 112), new Vector2(-6, 348));

            _picker = new GameObject("Picker");
            _picker.transform.SetParent(info.transform, false);
            var pickerRt = _picker.AddComponent<RectTransform>();
            UiFactory.Anchor(pickerRt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -56), new Vector2(-12, -8));

            _prev = UiFactory.Button(_picker.transform, "Prev", "<", UiTheme.PanelInner);
            UiFactory.Anchor(_prev.GetComponent<RectTransform>(), new Vector2(0, 0.1f), new Vector2(0, 0.9f), new Vector2(0, 0), new Vector2(52, 0));
            _prev.onClick.AddListener(() => ShiftSelected(-1));

            _name = UiFactory.Label(_picker.transform, "Name", 28, UiTheme.Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiFactory.Anchor(_name.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(58, 0), new Vector2(-58, 0));

            _next = UiFactory.Button(_picker.transform, "Next", ">", UiTheme.PanelInner);
            UiFactory.Anchor(_next.GetComponent<RectTransform>(), new Vector2(1, 0.1f), new Vector2(1, 0.9f), new Vector2(-52, 0), new Vector2(0, 0));
            _next.onClick.AddListener(() => ShiftSelected(1));

            _classMark = MakeEmblem(info.transform, "ClassMark", new Vector2(0.5f, 1f), new Vector2(-78, -128), new Vector2(-18, -68));
            _affinityMark = MakeEmblem(info.transform, "AffinityMark", new Vector2(0.5f, 1f), new Vector2(18, -128), new Vector2(78, -68));

            _identity = UiFactory.Label(info.transform, "Identity", 18, UiTheme.Text, FontStyle.Normal, TextAnchor.UpperCenter);
            UiFactory.Anchor(_identity.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(16, 16), new Vector2(-16, -132));

            Button back = UiFactory.Button(backdrop.transform, "Back", "Déconnexion", UiTheme.Slot);
            UiFactory.Anchor(back.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.25f, 0f), new Vector2(28, 40), new Vector2(-6, 96));
            back.onClick.AddListener(() => onBack());

            Button appear = UiFactory.Button(backdrop.transform, "Appear", "Garde-robe", UiTheme.PanelInner);
            UiFactory.Anchor(appear.GetComponent<RectTransform>(), new Vector2(0.25f, 0f), new Vector2(0.5f, 0f), new Vector2(6, 40), new Vector2(-6, 96));
            appear.onClick.AddListener(OpenWardrobe);

            Button create = UiFactory.Button(backdrop.transform, "New", "Nouveau personnage", UiTheme.PanelInner);
            UiFactory.Anchor(create.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.75f, 0f), new Vector2(6, 40), new Vector2(-6, 96));
            create.onClick.AddListener(ToggleCreate);

            Button play = UiFactory.Button(backdrop.transform, "Play", "Entrer dans le monde", UiTheme.Accent);
            UiFactory.Anchor(play.GetComponent<RectTransform>(), new Vector2(0.75f, 0f), new Vector2(1f, 0f), new Vector2(6, 40), new Vector2(-28, 96));
            play.onClick.AddListener(PlaySelected);

            _error = UiFactory.Label(backdrop.transform, "Error", 18, UiTheme.Danger, FontStyle.Normal, TextAnchor.MiddleCenter);
            UiFactory.Anchor(_error.rectTransform, new Vector2(0.25f, 0f), new Vector2(0.75f, 0f), new Vector2(6, 8), new Vector2(-6, 36));

            _createPanel = BuildCreate(backdrop.transform);
            _nameField = _createPanel.transform.Find("Name").GetComponent<InputField>();
            _classLabel = _createPanel.transform.Find("ClassLabel").GetComponent<Text>();
            _createPanel.SetActive(false);

            var nav = _root.AddComponent<CharacterSelectNav>();
            nav.Bind(ShiftSelected);
        }

        public CharacterAppearance CurrentAppearance => _draft;
        public Guid SelectedId => _selected;
        public bool IsCreating => _creating;

        public ushort CurrentClassId =>
            !_creating && IndexOf(_selected) >= 0 ? _characters[IndexOf(_selected)].ClassId : _createClass;

        public event Action<Guid> SelectionChanged;

        public void Bind(IReadOnlyList<CharacterSummary> characters)
        {
            _characters = characters ?? Array.Empty<CharacterSummary>();
            if (_characters.Count == 0)
            {
                _selected = Guid.Empty;
                _creating = true;
                _createPanel.SetActive(true);
                RefreshCreateLabels();
                _name.text = "Nouveau";
                _identity.text = "Aucun personnage pour l'instant.\nChoisis un nom et une classe.";
                _picker.SetActive(false);
                _classMark.enabled = false;
                _affinityMark.enabled = false;
                NotifyPreview();
                SelectionChanged?.Invoke(Guid.Empty);
                return;
            }

            if (_selected == Guid.Empty || IndexOf(_selected) < 0)
            {
                _selected = _characters[0].Id;
            }

            CharacterSummary current = Find(_selected) ?? _characters[0];
            Select(current);
        }

        public void Select(Guid id)
        {
            CharacterSummary summary = Find(id);
            if (summary is null)
            {
                return;
            }

            Select(summary);
        }

        public void SetVisible(bool visible) => _root.SetActive(visible);

        public void SetError(string message) => _error.text = message ?? string.Empty;

        public event Action<CharacterAppearance, ushort> AppearanceChanged;

        public void ShiftSelected(int delta)
        {
            if (_creating || _characters.Count == 0)
            {
                return;
            }

            int index = IndexOf(_selected);
            if (index < 0)
            {
                index = 0;
            }

            index = (index + delta + _characters.Count) % _characters.Count;
            Select(_characters[index]);
        }

        void Select(CharacterSummary summary)
        {
            _creating = false;
            _createPanel.SetActive(false);
            _picker.SetActive(true);
            _selected = summary.Id;
            _draft = summary.Appearance;
            _name.text = summary.Name;
            _identity.text =
                $"{ClientCatalog.ClassName(summary.ClassId)}  ·  Niv. {summary.Level}\n" +
                $"{ClientCatalog.AffinityName(summary.Affinity)}  ·  {ClientCatalog.TitleName(summary.TitleId)}\n" +
                $"{ClientCatalog.StartRealm}";
            _identity.supportRichText = true;
            _classMark.sprite = EmblemArt.ClassMark(summary.ClassId);
            _classMark.color = Color.white;
            _classMark.enabled = true;
            _affinityMark.sprite = EmblemArt.AffinityMark(summary.Affinity);
            _affinityMark.color = Color.white;
            _affinityMark.enabled = true;
            bool many = _characters.Count > 1;
            _prev.gameObject.SetActive(many);
            _next.gameObject.SetActive(many);
            NotifyPreview();
            RefreshCreateLabels();
            SelectionChanged?.Invoke(_selected);
        }

        void OpenWardrobe()
        {
            if (_creating || _selected == Guid.Empty)
            {
                SetError("Choisis un personnage.");
                return;
            }

            _onWardrobe();
        }

        void PlaySelected()
        {
            if (_creating || _selected == Guid.Empty)
            {
                SetError("Choisis un personnage, ou crée-en un.");
                return;
            }

            _onPlay(_selected);
        }

        void ToggleCreate()
        {
            _creating = true;
            _createPanel.SetActive(true);
            _picker.SetActive(false);
            _draft = CharacterAppearance.Default;
            RefreshCreateLabels();
            NotifyPreview();
        }

        void CancelCreate()
        {
            if (_characters.Count == 0)
            {
                return;
            }

            _creating = false;
            _createPanel.SetActive(false);
            CharacterSummary current = Find(_selected) ?? _characters[0];
            Select(current);
        }

        void SubmitCreate()
        {
            _onCreate(_nameField.text.Trim(), _createClass, _draft);
        }

        GameObject BuildCreate(Transform parent)
        {
            Image panel = UiFactory.Panel(parent, "Create", UiTheme.Panel);
            UiFactory.Anchor(panel.rectTransform, new Vector2(0, 0), new Vector2(0.24f, 1), new Vector2(28, 110), new Vector2(-8, -90));

            Text heading = UiFactory.Label(panel.transform, "Heading", 24, UiTheme.Gold, FontStyle.Bold, TextAnchor.MiddleLeft);
            UiFactory.Anchor(heading.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -52), new Vector2(-16, -12));
            heading.text = "Nouveau";

            InputField name = UiFactory.Field(panel.transform, "Name", "Nom (3 à 16 lettres)", false);
            UiFactory.Anchor(name.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -112), new Vector2(-16, -60));
            name.characterLimit = Limits.CharacterNameMaxChars;

            Button prevClass = UiFactory.Button(panel.transform, "PrevClass", "<", UiTheme.PanelInner);
            UiFactory.Anchor(prevClass.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -176), new Vector2(64, -120));
            prevClass.onClick.AddListener(() => ShiftClass(-1));

            Text classLabel = UiFactory.Label(panel.transform, "ClassLabel", 18, UiTheme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiFactory.Anchor(classLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(72, -176), new Vector2(-72, -120));

            Button nextClass = UiFactory.Button(panel.transform, "NextClass", ">", UiTheme.PanelInner);
            UiFactory.Anchor(nextClass.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-64, -176), new Vector2(-16, -120));
            nextClass.onClick.AddListener(() => ShiftClass(1));

            Button submit = UiFactory.Button(panel.transform, "Submit", "Créer", UiTheme.Accent);
            UiFactory.Anchor(submit.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 80), new Vector2(-16, 136));
            submit.onClick.AddListener(SubmitCreate);

            Button cancel = UiFactory.Button(panel.transform, "Cancel", "Annuler", UiTheme.Slot);
            UiFactory.Anchor(cancel.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 16), new Vector2(-16, 72));
            cancel.onClick.AddListener(CancelCreate);

            classLabel.text = ClientCatalog.ClassName(_createClass);
            return panel.gameObject;
        }

        void ShiftClass(int delta)
        {
            int index = 0;
            for (int i = 0; i < ClientCatalog.Classes.Length; i++)
            {
                if (ClientCatalog.Classes[i].Id == _createClass)
                {
                    index = i;
                    break;
                }
            }

            index = (index + delta + ClientCatalog.Classes.Length) % ClientCatalog.Classes.Length;
            _createClass = ClientCatalog.Classes[index].Id;
            RefreshCreateLabels();
            NotifyPreview();
        }

        void NotifyPreview() => AppearanceChanged?.Invoke(_draft, CurrentClassId);

        void RefreshCreateLabels()
        {
            if (_classLabel is not null)
            {
                var info = ClientCatalog.Classes[0];
                for (int i = 0; i < ClientCatalog.Classes.Length; i++)
                {
                    if (ClientCatalog.Classes[i].Id == _createClass)
                    {
                        info = ClientCatalog.Classes[i];
                        break;
                    }
                }

                _classLabel.text = info.Name + "\n<size=14>" + info.Blurb + "</size>";
                _classLabel.supportRichText = true;
            }
        }

        int IndexOf(Guid id)
        {
            for (int i = 0; i < _characters.Count; i++)
            {
                if (_characters[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        CharacterSummary Find(Guid id)
        {
            int index = IndexOf(id);
            return index < 0 ? null : _characters[index];
        }

        static Image MakeEmblem(Transform parent, string name, Vector2 anchor, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = UiTheme.White;
            image.preserveAspect = true;
            image.raycastTarget = false;
            UiFactory.Anchor(image.rectTransform, anchor, anchor, offsetMin, offsetMax);
            return image;
        }

        sealed class CharacterSelectNav : MonoBehaviour
        {
            Action<int> _shift;

            public void Bind(Action<int> shift) => _shift = shift;

            void Update()
            {
                if (!isActiveAndEnabled || _shift is null)
                {
                    return;
                }

#if ENABLE_INPUT_SYSTEM
                Keyboard keyboard = Keyboard.current;
                if (keyboard is null)
                {
                    return;
                }

                if (keyboard.leftArrowKey.wasPressedThisFrame)
                {
                    _shift(-1);
                }
                else if (keyboard.rightArrowKey.wasPressedThisFrame)
                {
                    _shift(1);
                }
#endif
            }
        }
    }
}
