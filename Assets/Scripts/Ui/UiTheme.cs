using System.IO;
using UnityEngine;

namespace Liryne.Game.Ui
{
    public static class UiTheme
    {
        public static readonly Color Backdrop = new Color(0.07f, 0.06f, 0.14f, 0.72f);
        public static readonly Color Panel = new Color(0.12f, 0.10f, 0.22f, 0.94f);
        public static readonly Color PanelInner = new Color(0.16f, 0.13f, 0.28f, 0.96f);
        public static readonly Color Accent = new Color(0.62f, 0.48f, 0.95f, 1f);
        public static readonly Color Gold = new Color(0.86f, 0.72f, 0.42f, 1f);
        public static readonly Color Text = new Color(0.94f, 0.91f, 0.97f, 1f);
        public static readonly Color Muted = new Color(0.72f, 0.68f, 0.8f, 1f);
        public static readonly Color Danger = new Color(0.9f, 0.38f, 0.42f, 1f);
        public static readonly Color Hp = new Color(0.78f, 0.28f, 0.38f, 1f);
        public static readonly Color Mp = new Color(0.32f, 0.52f, 0.9f, 1f);
        public static readonly Color Slot = new Color(0.1f, 0.09f, 0.18f, 0.92f);

        public static Sprite White { get; private set; }
        public static Sprite Wordmark { get; private set; }
        public static Sprite Lyre { get; private set; }
        public static Font Font { get; private set; }

        public static void Ensure()
        {
            if (White is not null)
            {
                return;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            White = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Wordmark = LoadSprite("logo.png");
            Lyre = LoadSprite("liryne-lyre.png");
        }

        static Sprite LoadSprite(string fileName)
        {
            string[] candidates =
            {
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", fileName)),
                Path.Combine(Application.streamingAssetsPath, fileName),
            };

            foreach (string path in candidates)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    continue;
                }

                texture.filterMode = FilterMode.Bilinear;
                return Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));
            }

            return null;
        }
    }
}
