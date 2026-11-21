using System;
using System.Collections.Generic;
using Liryne.Game.Content;
using Liryne.Protocol;
using Liryne.Protocol.Messages.Characters;
using UnityEngine;
using UnityEngine.UI;

namespace Liryne.Game.Ui
{
    public sealed class CharacterScreen
    {
        readonly GameObject _root;
        readonly Transform _list;
        readonly Text _identity;
        readonly Text _error;
        readonly GameObject _createPanel;
        readonly InputField _nameField;
        readonly Text _classLabel;
        Text[] _appearanceLabels;
        readonly Action<Guid> _onPlay;
        readonly Action<Guid, CharacterAppearance> _onAppearance;
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
            Action<Guid, CharacterAppearance> onAppearance,
            Action<string, ushort, CharacterAppearance> onCreate)
        {
            _onPlay = onPlay;
            _onAppearance = onAppearance;
            _onCreate = onCreate;

            Image backdrop = UiFactory.Panel(canvas, "Characters", new Color(0.05f, 0.04f, 0.1f, 0.88f));
            UiFactory.Stretch(backdrop.rectTransform);
            _root = backdrop.gameObject;

            Text title = UiFactory.Label(backdrop.transform, "Title", 34, UiTheme.Gold, FontStyle.Bold, TextAnchor.MiddleLeft);
            UiFactory.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(0.4f, 1), new Vector2(36, -78), new Vector2(0, -24));
            title.text = "Tes personnages";

            Image listPanel = UiFactory.Panel(backdrop.transform, "ListPanel", UiTheme.Panel);
            UiFactory.Anchor(listPanel.rectTransform, new Vector2(0, 0), new Vector2(0.28f, 1), new Vector2(28, 110), new Vector2(-8, -90));
            UiFactory.Scroll(listPanel.transform, "List", out _list);
            UiFactory.Stretch(_list.parent.GetComponent<RectTransform>(), 8, 8, 8, 8);

            Image previewFrame = UiFactory.Panel(backdrop.transform, "PreviewFrame", UiTheme.Panel);
            UiFactory.Anchor(previewFrame.rectTransform, new Vector2(0.29f, 0.18f), new Vector2(0.62f, 0.92f), new Vector2(8, 8), new Vector2(-8, -8));
            var rawGo = new GameObject("Raw");
            rawGo.transform.SetParent(previewFrame.transform, false);
            Preview = rawGo.AddComponent<RawImage>();
            Preview.color = Color.white;
            UiFactory.Stretch(Preview.rectTransform, 12, 12, 12, 12);
            var previewAspect = rawGo.AddComponent<AspectRatioFitter>();
            previewAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            previewAspect.aspectRatio = 512f / 768f;

            Image info = UiFactory.Panel(backdrop.transform, "Info", UiTheme.Panel);
            UiFactory.Anchor(info.rectTransform, new Vector2(0.63f, 0.18f), new Vector2(1, 0.92f), new Vector2(8, 8), new Vector2(-28, -8));
            _identity = UiFactory.Label(info.transform, "Identity", 22, UiTheme.Text, FontStyle.Normal, TextAnchor.UpperLeft);
            UiFactory.Anchor(_identity.rectTransform, new Vector2(0, 0.72f), new Vector2(1, 1), new Vector2(20, 16), new Vector2(-20, -16));
            BuildAppearancePad(info.transform);

            Button play = UiFactory.Button(backdrop.transform, "Play", "Entrer dans le monde", UiTheme.Accent);
            UiFactory.Anchor(play.GetComponent<RectTransform>(), new Vector2(0.63f, 0), new Vector2(1, 0), new Vector2(8, 40), new Vector2(-28, 96));
            play.onClick.AddListener(PlaySelected);

            Button appear = UiFactory.Button(backdrop.transform, "Appear", "Enregistrer l'apparence", UiTheme.PanelInner);
            UiFactory.Anchor(appear.GetComponent<RectTransform>(), new Vector2(0.29f, 0), new Vector2(0.46f, 0), new Vector2(8, 40), new Vector2(-6, 96));
            appear.onClick.AddListener(SaveAppearance);

            Button create = UiFactory.Button(backdrop.transform, "New", "Nouveau personnage", UiTheme.PanelInner);
            UiFactory.Anchor(create.GetComponent<RectTransform>(), new Vector2(0.46f, 0), new Vector2(0.62f, 0), new Vector2(6, 40), new Vector2(-8, 96));
            create.onClick.AddListener(ToggleCreate);

            Button back = UiFactory.Button(backdrop.transform, "Back", "Déconnexion", UiTheme.Slot);
            UiFactory.Anchor(back.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0.28f, 0), new Vector2(28, 40), new Vector2(-8, 96));
            back.onClick.AddListener(() => onBack());

            _error = UiFactory.Label(backdrop.transform, "Error", 18, UiTheme.Danger, FontStyle.Normal, TextAnchor.MiddleLeft);
            UiFactory.Anchor(_error.rectTransform, new Vector2(0.29f, 0), new Vector2(0.62f, 0), new Vector2(8, 8), new Vector2(-8, 36));

            _createPanel = BuildCreate(backdrop.transform);
            _nameField = _createPanel.transform.Find("Name").GetComponent<InputField>();
            _classLabel = _createPanel.transform.Find("ClassLabel").GetComponent<Text>();
            _createPanel.SetActive(false);
        }

        public CharacterAppearance CurrentAppearance => _draft;

        public void Bind(IReadOnlyList<CharacterSummary> characters)
        {
            _characters = characters ?? Array.Empty<CharacterSummary>();
            for (int i = _list.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(_list.GetChild(i).gameObject);
            }

            if (_characters.Count == 0)
            {
                _selected = Guid.Empty;
                _creating = true;
                _createPanel.SetActive(true);
                RefreshCreateLabels();
                _identity.text = "Aucun personnage pour l'instant.\nChoisis un nom, une classe, puis crée-le.";
                AppearanceChanged?.Invoke(_draft);
                return;
            }

            if (_selected == Guid.Empty || IndexOf(_selected) < 0)
            {
                _selected = _characters[0].Id;
            }

            foreach (CharacterSummary summary in _characters)
            {
                CharacterSummary captured = summary;
                Image row = UiFactory.Panel(_list, summary.Id.ToString("N"), _selected == summary.Id ? UiTheme.Accent : UiTheme.PanelInner);
                var le = row.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 78;
                le.preferredHeight = 78;
                Text label = UiFactory.Label(row.transform, "Label", 20, UiTheme.Text, FontStyle.Bold, TextAnchor.MiddleLeft);
                UiFactory.Stretch(label.rectTransform, 18, 8, 12, 8);
                label.text = $"{summary.Name}\n<size=16><color=#c8bdd8>{ClientCatalog.ClassName(summary.ClassId)}  ·  niveau {summary.Level}</color></size>";
                label.supportRichText = true;
                var button = row.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => Select(captured, refreshList: true));
            }

            CharacterSummary current = Find(_selected) ?? _characters[0];
            Select(current, refreshList: false);
        }

        public void SetVisible(bool visible) => _root.SetActive(visible);

        public void SetError(string message) => _error.text = message ?? string.Empty;

        public event Action<CharacterAppearance> AppearanceChanged;

        void Select(CharacterSummary summary, bool refreshList)
        {
            _creating = false;
            _createPanel.SetActive(false);
            _selected = summary.Id;
            _draft = summary.Appearance;
            _identity.text =
                $"<b><size=28><color=#ddc06c>{summary.Name}</color></size></b>\n" +
                $"{ClientCatalog.ClassName(summary.ClassId)}  ·  niveau {summary.Level}\n" +
                $"{ClientCatalog.AffinityName(summary.Affinity)}\n" +
                $"{ClientCatalog.TitleName(summary.TitleId)}\n" +
                $"Ornement : {ClientCatalog.OrnamentName(summary.OrnamentId)}";
            _identity.supportRichText = true;
            AppearanceChanged?.Invoke(_draft);
            RefreshCreateLabels();
            if (refreshList)
            {
                Bind(_characters);
            }
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

        void SaveAppearance()
        {
            if (_creating || _selected == Guid.Empty)
            {
                SetError("Choisis un personnage.");
                return;
            }

            _onAppearance(_selected, _draft);
            SetError(string.Empty);
        }

        void ToggleCreate()
        {
            _creating = true;
            _createPanel.SetActive(true);
            _draft = CharacterAppearance.Default;
            RefreshCreateLabels();
            AppearanceChanged?.Invoke(_draft);
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
            Select(current, refreshList: true);
        }

        void SubmitCreate()
        {
            _onCreate(_nameField.text.Trim(), _createClass, _draft);
        }

        GameObject BuildCreate(Transform parent)
        {
            Image panel = UiFactory.Panel(parent, "Create", UiTheme.Panel);
            UiFactory.Anchor(panel.rectTransform, new Vector2(0, 0), new Vector2(0.28f, 1), new Vector2(28, 110), new Vector2(-8, -90));

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

        GameObject BuildAppearancePad(Transform parent)
        {
            Image pad = UiFactory.Panel(parent, "AppearancePad", new Color(0, 0, 0, 0));
            UiFactory.Anchor(pad.rectTransform, new Vector2(0, 0), new Vector2(1, 0.7f), new Vector2(12, 12), new Vector2(-12, 8));
            string[] rows = { "Silhouette", "Visage", "Coupe", "Cheveux", "Peau", "Yeux" };
            _appearanceLabels = new Text[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                int index = i;
                Image row = UiFactory.Panel(pad.transform, "Row" + i, UiTheme.Slot);
                float top = -8 - i * 48;
                UiFactory.Anchor(row.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(4, top - 42), new Vector2(-4, top));
                Text caption = UiFactory.Label(row.transform, "Caption", 15, UiTheme.Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
                UiFactory.Anchor(caption.rectTransform, new Vector2(0, 0), new Vector2(0.38f, 1), new Vector2(10, 0), Vector2.zero);
                caption.text = rows[i];
                Button minus = UiFactory.Button(row.transform, "Minus", "<", UiTheme.PanelInner);
                UiFactory.Anchor(minus.GetComponent<RectTransform>(), new Vector2(0.4f, 0.12f), new Vector2(0.52f, 0.88f), Vector2.zero, Vector2.zero);
                minus.onClick.AddListener(() => ShiftAppearance(index, -1));
                Text value = UiFactory.Label(row.transform, "Value", 16, UiTheme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
                UiFactory.Anchor(value.rectTransform, new Vector2(0.52f, 0), new Vector2(0.88f, 1), Vector2.zero, Vector2.zero);
                _appearanceLabels[i] = value;
                Button plus = UiFactory.Button(row.transform, "Plus", ">", UiTheme.PanelInner);
                UiFactory.Anchor(plus.GetComponent<RectTransform>(), new Vector2(0.88f, 0.12f), new Vector2(0.98f, 0.88f), Vector2.zero, Vector2.zero);
                plus.onClick.AddListener(() => ShiftAppearance(index, 1));
            }

            RefreshCreateLabels();
            return pad.gameObject;
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
        }

        void ShiftAppearance(int index, int delta)
        {
            byte Wrap(byte value, byte max)
            {
                int next = (value + delta) % max;
                if (next < 0)
                {
                    next += max;
                }

                return (byte)next;
            }

            _draft = index switch
            {
                0 => _draft with { BodyType = Wrap(_draft.BodyType, ClientCatalog.BodyTypes) },
                1 => _draft with { FaceId = Wrap(_draft.FaceId, ClientCatalog.Faces) },
                2 => _draft with { HairStyleId = Wrap(_draft.HairStyleId, ClientCatalog.HairStyles) },
                3 => _draft with { HairColorId = Wrap(_draft.HairColorId, ClientCatalog.HairColors) },
                4 => _draft with { SkinToneId = Wrap(_draft.SkinToneId, ClientCatalog.SkinTones) },
                _ => _draft with { EyeColorId = Wrap(_draft.EyeColorId, ClientCatalog.EyeColors) },
            };
            RefreshCreateLabels();
            AppearanceChanged?.Invoke(_draft);
        }

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

            if (_appearanceLabels is null)
            {
                return;
            }

            _appearanceLabels[0].text = ClientCatalog.BodyLabel(_draft.BodyType);
            _appearanceLabels[1].text = ClientCatalog.FaceLabel(_draft.FaceId);
            _appearanceLabels[2].text = ClientCatalog.HairStyleLabel(_draft.HairStyleId);
            _appearanceLabels[3].text = (_draft.HairColorId + 1).ToString();
            _appearanceLabels[4].text = (_draft.SkinToneId + 1).ToString();
            _appearanceLabels[5].text = (_draft.EyeColorId + 1).ToString();
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
    }
}
