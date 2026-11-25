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
        public static readonly Color LogoWash = new Color(0.043f, 0.035f, 0.145f, 1f);

        public static Sprite White { get; private set; }
        public static Sprite Wordmark { get; private set; }
        public static Sprite Lyre { get; private set; }
        public static Font Font { get; private set; }

        public static void Ensure()
        {
            if (!Alive(White))
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                tex.Apply();
                White = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            }

            Font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!Alive(Wordmark))
            {
                Wordmark = LoadSprite("logo.png", knockoutBackdrop: true);
            }

            if (!Alive(Lyre))
            {
                Lyre = LoadSprite("liryne-lyre.png", knockoutBackdrop: false);
            }
        }

        static bool Alive(Sprite sprite) =>
            sprite is not null && sprite.texture is not null;

        static Sprite LoadSprite(string fileName, bool knockoutBackdrop)
        {
            foreach (string path in CandidatePaths(fileName))
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                byte[] bytes;
                try
                {
                    bytes = File.ReadAllBytes(path);
                }
                catch
                {
                    continue;
                }

                if (bytes is null || bytes.Length < 32)
                {
                    continue;
                }

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes) || texture.width < 8 || texture.height < 8)
                {
                    Object.Destroy(texture);
                    continue;
                }

                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                if (knockoutBackdrop)
                {
                    KnockoutBackdrop(texture);
                }

                return Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }

            return null;
        }

        static string[] CandidatePaths(string fileName) =>
            new[]
            {
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", fileName)),
                Path.Combine(Application.streamingAssetsPath, fileName),
                Path.Combine(Application.dataPath, fileName),
                Path.Combine(Application.persistentDataPath, fileName),
            };

        static void KnockoutBackdrop(Texture2D texture)
        {
            Color[] pixels = texture.GetPixels();
            if (pixels.Length == 0)
            {
                return;
            }

            Color key = pixels[pixels.Length - 1];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color p = pixels[i];
                float dr = p.r - key.r;
                float dg = p.g - key.g;
                float db = p.b - key.b;
                float dist = Mathf.Sqrt((dr * dr) + (dg * dg) + (db * db));
                if (dist < 0.09f && p.maxColorComponent < 0.28f)
                {
                    p.a = 0f;
                }
                else if (dist < 0.16f && p.maxColorComponent < 0.35f)
                {
                    p.a *= Mathf.InverseLerp(0.09f, 0.16f, dist);
                }

                pixels[i] = p;
            }

            texture.SetPixels(pixels);
            texture.Apply();
        }
    }
}
