using UnityEngine;
using UnityEngine.UI;

namespace Liryne.Game.Ui
{
    /// <summary>Voile indigo et logo partagés par les écrans titre.</summary>
    static class TitleChrome
    {
        public enum LogoPlace
        {
            Hero,
            Compact,
        }

        public static void Attach(Transform parent, LogoPlace logo, bool paintedScene = false)
        {
            if (paintedScene)
            {
                RawImage scene = new GameObject("Scene").AddComponent<RawImage>();
                scene.transform.SetParent(parent, false);
                UiFactory.Stretch(scene.rectTransform);
                scene.texture = LoginArt.Scene();
                scene.color = Color.white;
                scene.raycastTarget = false;
            }

            RawImage veil = new GameObject("Veil").AddComponent<RawImage>();
            veil.transform.SetParent(parent, false);
            UiFactory.Stretch(veil.rectTransform);
            veil.texture = LoginArt.Veil(UiTheme.LogoWash);
            veil.color = Color.white;
            veil.raycastTarget = false;

            PlaceLogo(parent, logo);
        }

        static void PlaceLogo(Transform parent, LogoPlace place)
        {
            Sprite mark = UiTheme.Wordmark;
            Vector2 min = place == LogoPlace.Hero ? new Vector2(0.5f, 0.78f) : new Vector2(0.5f, 0.92f);
            Vector2 size = place == LogoPlace.Hero ? new Vector2(200f, 200f) : new Vector2(110f, 90f);
            if (mark is null || mark.texture is null)
            {
                Text fallback = UiFactory.Label(parent, "LogoText", place == LogoPlace.Hero ? 42 : 28, UiTheme.Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
                UiFactory.Anchor(fallback.rectTransform, min, min, new Vector2(-size.x, -size.y * 0.28f), new Vector2(size.x, size.y * 0.28f));
                fallback.text = "LIRYNE";
                fallback.raycastTarget = false;
                return;
            }

            var go = new GameObject("Logo");
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = mark;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.type = Image.Type.Simple;
            UiFactory.Anchor(image.rectTransform, min, min, new Vector2(-size.x, -size.y), new Vector2(size.x, size.y));
        }
    }
}
