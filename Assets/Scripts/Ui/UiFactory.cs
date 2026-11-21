using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Liryne.Game.Ui
{
    public static class UiFactory
    {
        public static Canvas CreateCanvas(Transform parent)
        {
            UiTheme.Ensure();
            var go = new GameObject("LiryneUi");
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            if (Object.FindAnyObjectByType<EventSystem>() is null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(parent, false);
                es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            return canvas;
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = UiTheme.White;
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        public static Text Label(Transform parent, string name, int size, Color color, FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = UiTheme.Font;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button Button(Transform parent, string name, string caption, Color background)
        {
            Image image = Panel(parent, name, background);
            var button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = background * 1.15f;
            colors.pressedColor = background * 0.85f;
            colors.selectedColor = background;
            button.colors = colors;
            Text text = Label(image.transform, "Label", 22, UiTheme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            text.text = caption;
            text.raycastTarget = false;
            return button;
        }

        public static InputField Field(Transform parent, string name, string placeholder, bool password)
        {
            Image image = Panel(parent, name, UiTheme.Slot);
            var field = image.gameObject.AddComponent<InputField>();
            Text text = Label(image.transform, "Text", 22, UiTheme.Text, FontStyle.Normal, TextAnchor.MiddleLeft);
            Stretch(text.rectTransform, 12, 4, 12, 4);
            text.supportRichText = false;
            Text hint = Label(image.transform, "Placeholder", 22, UiTheme.Muted, FontStyle.Italic, TextAnchor.MiddleLeft);
            Stretch(hint.rectTransform, 12, 4, 12, 4);
            hint.text = placeholder;
            field.textComponent = text;
            field.placeholder = hint;
            field.characterLimit = 64;
            if (password)
            {
                field.contentType = InputField.ContentType.Password;
            }

            return field;
        }

        public static ScrollRect Scroll(Transform parent, string name, out Transform content)
        {
            Image viewportImg = Panel(parent, name, new Color(0, 0, 0, 0.12f));
            var scroll = viewportImg.gameObject.AddComponent<ScrollRect>();
            var mask = viewportImg.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportImg.transform, false);
            var contentRt = contentGo.AddComponent<RectTransform>();
            Stretch(contentRt);
            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 6;
            layout.padding = new RectOffset(8, 8, 8, 8);
            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = contentRt;
            scroll.viewport = viewportImg.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            content = contentGo.transform;
            return scroll;
        }

        public static void Stretch(RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        public static Image Bar(Transform parent, string name, Color fillColor, out Image fill)
        {
            Image back = Panel(parent, name, UiTheme.Slot);
            fill = Panel(back.transform, "Fill", fillColor);
            Stretch(fill.rectTransform);
            fill.rectTransform.anchorMax = new Vector2(1f, 1f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 1f;
            return back;
        }
    }
}
