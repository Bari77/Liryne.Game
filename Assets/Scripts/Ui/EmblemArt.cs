using System.Collections.Generic;
using Liryne.Protocol.Messages;
using UnityEngine;

namespace Liryne.Game.Ui
{
    /// <summary>Pictogrammes de classe et d'affinité, dessinés en code.</summary>
    static class EmblemArt
    {
        const int Size = 64;
        static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>();

        public static Sprite ClassMark(ushort classId) => Get(1000 + classId, ink => DrawClass(ink, classId));

        public static Sprite AffinityMark(Affinity kind) => Get(2000 + (int)kind, ink => DrawAffinity(ink, kind));

        static Sprite Get(int key, System.Action<Color[]> paint)
        {
            if (Cache.TryGetValue(key, out Sprite existing) && existing is not null && existing.texture is not null)
            {
                return existing;
            }

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var ink = new Color[Size * Size];
            paint(ink);
            tex.SetPixels(ink);
            tex.Apply(false, true);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            Cache[key] = sprite;
            return sprite;
        }

        static void DrawClass(Color[] ink, ushort classId)
        {
            Color gold = new Color(0.9f, 0.78f, 0.42f, 1f);
            Color steel = new Color(0.72f, 0.76f, 0.86f, 1f);
            switch (classId)
            {
                case 1:
                    Shield(ink, gold);
                    break;
                case 2:
                    TwinBlades(ink, steel);
                    break;
                case 3:
                    Bow(ink, new Color(0.72f, 0.86f, 0.55f));
                    break;
                case 4:
                    Spark(ink, new Color(0.72f, 0.55f, 0.95f));
                    break;
                case 5:
                    Sigil(ink, new Color(0.95f, 0.88f, 0.55f));
                    break;
                default:
                    Seal(ink, new Color(0.55f, 0.72f, 0.95f));
                    break;
            }
        }

        static void DrawAffinity(Color[] ink, Affinity affinity)
        {
            switch (affinity)
            {
                case Affinity.Fire:
                    Flame(ink, new Color(0.95f, 0.42f, 0.22f));
                    break;
                case Affinity.Water:
                    Drop(ink, new Color(0.32f, 0.62f, 0.95f));
                    break;
                case Affinity.Light:
                    Star(ink, new Color(0.98f, 0.92f, 0.62f));
                    break;
                case Affinity.Darkness:
                    Crescent(ink, new Color(0.55f, 0.38f, 0.85f));
                    break;
                default:
                    Ring(ink, new Color(0.55f, 0.52f, 0.62f), 18f, 3.2f);
                    break;
            }
        }

        static void Shield(Color[] ink, Color color)
        {
            for (int y = 8; y < 56; y++)
            {
                float t = (y - 8) / 48f;
                float half = t < 0.62f ? 16f : 16f * (1f - (t - 0.62f) / 0.38f);
                for (int x = 0; x < Size; x++)
                {
                    if (Mathf.Abs(x - 32) <= half)
                    {
                        Plot(ink, x, y, color);
                    }
                }
            }
        }

        static void TwinBlades(Color[] ink, Color color)
        {
            Stroke(ink, 20, 10, 28, 54, 3.2f, color);
            Stroke(ink, 44, 10, 36, 54, 3.2f, color);
            Stroke(ink, 18, 14, 22, 18, 2.2f, color);
            Stroke(ink, 46, 14, 42, 18, 2.2f, color);
        }

        static void Bow(Color[] ink, Color color)
        {
            Ring(ink, color, 16f, 2.6f);
            for (int x = 14; x < 50; x++)
            {
                Plot(ink, x, 32, color);
            }

            Stroke(ink, 32, 32, 52, 32, 2f, color);
            Plot(ink, 54, 32, color);
            Plot(ink, 55, 32, color);
        }

        static void Spark(Color[] ink, Color color)
        {
            Star(ink, color);
            Ring(ink, color, 22f, 2f);
        }

        static void Sigil(Color[] ink, Color color)
        {
            Stroke(ink, 32, 8, 32, 56, 3f, color);
            Ring(ink, color, 12f, 2.6f);
        }

        static void Seal(Color[] ink, Color color)
        {
            Ring(ink, color, 20f, 2.8f);
            Stroke(ink, 32, 18, 32, 46, 2.4f, color);
            Stroke(ink, 18, 32, 46, 32, 2.4f, color);
        }

        static void Flame(Color[] ink, Color color)
        {
            for (int y = 10; y < 54; y++)
            {
                float t = (y - 10) / 44f;
                float half = 4f + 10f * Mathf.Sin(t * Mathf.PI);
                for (int x = 0; x < Size; x++)
                {
                    float wobble = 2f * Mathf.Sin((y + x) * 0.18f);
                    if (Mathf.Abs(x - 32 - wobble) <= half)
                    {
                        Plot(ink, x, y, color);
                    }
                }
            }
        }

        static void Drop(Color[] ink, Color color)
        {
            for (int y = 8; y < 56; y++)
            {
                float t = (y - 8) / 48f;
                float half = t < 0.35f ? t * 28f : 10f * Mathf.Sqrt(Mathf.Max(0f, 1f - (t - 0.35f) / 0.65f));
                for (int x = 0; x < Size; x++)
                {
                    if (Mathf.Abs(x - 32) <= half)
                    {
                        Plot(ink, x, y, color);
                    }
                }
            }
        }

        static void Star(Color[] ink, Color color)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Stroke(ink, 32, 32, 32 + Mathf.Cos(a) * 22f, 32 + Mathf.Sin(a) * 22f, 2.4f, color);
            }

            FillDisc(ink, 32, 32, 5f, color);
        }

        static void Crescent(Color[] ink, Color color)
        {
            FillDisc(ink, 34, 32, 18f, color);
            FillDisc(ink, 42, 36, 14f, Color.clear);
        }

        static void Ring(Color[] ink, Color color, float radius, float thickness)
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
                    if (Mathf.Abs(d - radius) <= thickness)
                    {
                        Plot(ink, x, y, color);
                    }
                }
            }
        }

        static void FillDisc(Color[] ink, float cx, float cy, float radius, Color color)
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) <= radius)
                    {
                        Plot(ink, x, y, color);
                    }
                }
            }
        }

        static void Stroke(Color[] ink, float x0, float y0, float x1, float y1, float width, Color color)
        {
            float steps = Mathf.Max(2f, Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / steps;
                FillDisc(ink, Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), width, color);
            }
        }

        static void Plot(Color[] ink, int x, int y, Color color)
        {
            if ((uint)x >= Size || (uint)y >= Size)
            {
                return;
            }

            if (color.a <= 0.01f)
            {
                ink[y * Size + x] = Color.clear;
                return;
            }

            ink[y * Size + x] = color;
        }
    }
}
