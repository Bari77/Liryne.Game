using System;
using Liryne.Game.Content;
using Liryne.Protocol.Messages.Characters;
using UnityEngine;
using UnityEngine.UI;

namespace Liryne.Game.Ui
{
    /// <summary>Garde-robe : aperçu 3D et réglages d'apparence, hors de la sélection.</summary>
    public sealed class WardrobeScreen
    {
        readonly GameObject _root;
        readonly Text[] _labels;
        readonly Text _error;
        readonly Action _onBack;
        readonly Action<Guid, CharacterAppearance> _onSave;

        Guid _characterId;
        ushort _classId = 1;
        CharacterAppearance _draft = CharacterAppearance.Default;

        public RawImage Preview { get; }
        public bool IsVisible => _root.activeSelf;

        public event Action<CharacterAppearance, ushort> DraftChanged;

        public WardrobeScreen(Transform canvas, Action onBack, Action<Guid, CharacterAppearance> onSave)
        {
            _onBack = onBack;
            _onSave = onSave;

            Image backdrop = UiFactory.Panel(canvas, "Wardrobe", Color.black);
            UiFactory.Stretch(backdrop.rectTransform);
            _root = backdrop.gameObject;

            TitleChrome.Attach(backdrop.transform, TitleChrome.LogoPlace.Compact);

            Image previewFrame = UiFactory.Panel(backdrop.transform, "PreviewFrame", UiTheme.Panel);
            UiFactory.Anchor(previewFrame.rectTransform, new Vector2(0.32f, 0.14f), new Vector2(0.68f, 0.9f), new Vector2(8, 8), new Vector2(-8, -8));
            var rawGo = new GameObject("Raw");
            rawGo.transform.SetParent(previewFrame.transform, false);
            Preview = rawGo.AddComponent<RawImage>();
            Preview.color = Color.white;
            UiFactory.Stretch(Preview.rectTransform, 16, 16, 16, 40);

            Text hint = UiFactory.Label(previewFrame.transform, "Hint", 16, UiTheme.Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            UiFactory.Anchor(hint.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 10), new Vector2(-16, 36));
            hint.text = "Glisser pour tourner";

            Image pad = UiFactory.Panel(backdrop.transform, "Pad", UiTheme.Panel);
            UiFactory.Anchor(pad.rectTransform, new Vector2(0, 0.14f), new Vector2(0.3f, 0.9f), new Vector2(28, 8), new Vector2(-8, -8));
            Text title = UiFactory.Label(pad.transform, "PadTitle", 22, UiTheme.Gold, FontStyle.Bold, TextAnchor.MiddleLeft);
            UiFactory.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -48), new Vector2(-16, -12));
            title.text = "Apparence";
            _labels = BuildRows(pad.transform);

            Button back = UiFactory.Button(backdrop.transform, "Back", "Retour", UiTheme.Slot);
            UiFactory.Anchor(back.GetComponent<RectTransform>(), new Vector2(0.32f, 0), new Vector2(0.5f, 0), new Vector2(8, 40), new Vector2(-6, 96));
            back.onClick.AddListener(() => _onBack());

            Button save = UiFactory.Button(backdrop.transform, "Save", "Enregistrer", UiTheme.Accent);
            UiFactory.Anchor(save.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.68f, 0), new Vector2(6, 40), new Vector2(-8, 96));
            save.onClick.AddListener(Save);

            _error = UiFactory.Label(backdrop.transform, "Error", 18, UiTheme.Danger, FontStyle.Normal, TextAnchor.MiddleCenter);
            UiFactory.Anchor(_error.rectTransform, new Vector2(0.32f, 0), new Vector2(0.78f, 0), new Vector2(8, 8), new Vector2(-8, 36));

            _root.SetActive(false);
        }

        public void Open(Guid characterId, CharacterAppearance appearance, ushort classId)
        {
            _characterId = characterId;
            _draft = appearance;
            _classId = classId;
            SetError(string.Empty);
            RefreshLabels();
            _root.SetActive(true);
            DraftChanged?.Invoke(_draft, _classId);
        }

        public void SetVisible(bool visible) => _root.SetActive(visible);

        public void SetError(string message) => _error.text = message ?? string.Empty;

        Text[] BuildRows(Transform parent)
        {
            string[] rows = { "Silhouette", "Visage", "Coupe", "Cheveux", "Peau", "Yeux" };
            var labels = new Text[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                int index = i;
                Image row = UiFactory.Panel(parent, "Row" + i, UiTheme.Slot);
                float top = -56 - i * 52;
                UiFactory.Anchor(row.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, top - 44), new Vector2(-16, top));
                Text caption = UiFactory.Label(row.transform, "Caption", 16, UiTheme.Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
                UiFactory.Anchor(caption.rectTransform, new Vector2(0, 0), new Vector2(0.38f, 1), new Vector2(12, 0), Vector2.zero);
                caption.text = rows[i];
                Button minus = UiFactory.Button(row.transform, "Minus", "<", UiTheme.PanelInner);
                UiFactory.Anchor(minus.GetComponent<RectTransform>(), new Vector2(0.4f, 0.12f), new Vector2(0.52f, 0.88f), Vector2.zero, Vector2.zero);
                minus.onClick.AddListener(() => Shift(index, -1));
                Text value = UiFactory.Label(row.transform, "Value", 16, UiTheme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
                UiFactory.Anchor(value.rectTransform, new Vector2(0.52f, 0), new Vector2(0.86f, 1), Vector2.zero, Vector2.zero);
                labels[i] = value;
                Button plus = UiFactory.Button(row.transform, "Plus", ">", UiTheme.PanelInner);
                UiFactory.Anchor(plus.GetComponent<RectTransform>(), new Vector2(0.86f, 0.12f), new Vector2(0.98f, 0.88f), Vector2.zero, Vector2.zero);
                plus.onClick.AddListener(() => Shift(index, 1));
            }

            return labels;
        }

        void Shift(int index, int delta)
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
            RefreshLabels();
            DraftChanged?.Invoke(_draft, _classId);
        }

        void RefreshLabels()
        {
            if (_labels is null || _labels.Length < 6)
            {
                return;
            }

            _labels[0].text = ClientCatalog.BodyLabel(_draft.BodyType);
            _labels[1].text = ClientCatalog.FaceLabel(_draft.FaceId);
            _labels[2].text = ClientCatalog.HairStyleLabel(_draft.HairStyleId);
            _labels[3].text = (_draft.HairColorId + 1).ToString();
            _labels[4].text = (_draft.SkinToneId + 1).ToString();
            _labels[5].text = (_draft.EyeColorId + 1).ToString();
        }

        void Save()
        {
            if (_characterId == Guid.Empty)
            {
                SetError("Choisis un personnage.");
                return;
            }

            _onSave(_characterId, _draft);
        }
    }
}
