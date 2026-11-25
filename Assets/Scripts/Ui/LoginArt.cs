using UnityEngine;

namespace Liryne.Game.Ui
{
    /// <summary>Fond d'écran titre : ciel nocturne, relief en couches, voile indigo du logo.</summary>
    static class LoginArt
    {
        static Texture2D _scene;
        static Texture2D _veil;
        static Color _veilSolid;

        public static Texture2D Scene()
        {
            if (_scene != null)
            {
                return _scene;
            }

            const int w = 1280;
            const int h = 720;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "LoginScene",
            };

            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (h - 1f);
                Color sky = Color.Lerp(
                    new Color(0.015f, 0.015f, 0.05f),
                    new Color(0.18f, 0.1f, 0.32f),
                    Smooth(v * 1.15f));
                sky = Color.Lerp(sky, new Color(0.42f, 0.28f, 0.18f), Smooth((v - 0.42f) * 2.4f) * 0.45f);
                for (int x = 0; x < w; x++)
                {
                    float u = x / (w - 1f);
                    Color c = sky;
                    float n = Fbm(u * 3.2f, v * 2.4f, 4) * 0.08f;
                    c.r += n * 0.15f;
                    c.b += n * 0.22f;

                    float star = Hash(x * 19.17f, y * 13.91f);
                    if (v > 0.38f && star > 0.9974f)
                    {
                        float spark = (star - 0.9974f) / 0.0026f;
                        c = Color.Lerp(c, new Color(0.95f, 0.92f, 1f), spark);
                    }

                    float moon = Vector2.Distance(new Vector2(u, v), new Vector2(0.78f, 0.78f));
                    if (moon < 0.22f)
                    {
                        float glow = Smooth(1f - moon / 0.22f);
                        c += new Color(0.35f, 0.28f, 0.55f) * glow * 0.35f;
                    }

                    if (moon < 0.046f)
                    {
                        c = Color.Lerp(c, new Color(0.93f, 0.9f, 0.82f), Smooth(1f - moon / 0.046f));
                    }

                    c = OverlayHills(c, u, v);
                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            _scene = tex;
            return tex;
        }

        public static Texture2D Veil(Color solid)
        {
            if (_veil != null && _veilSolid == solid)
            {
                return _veil;
            }

            const int h = 256;
            var tex = new Texture2D(8, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "LoginVeil",
            };

            var pixels = new Color[8 * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (h - 1f);
                float a = v > 0.62f ? 0.92f : Smooth((v - 0.18f) / 0.44f) * 0.92f;
                var c = new Color(solid.r, solid.g, solid.b, a);
                for (int x = 0; x < 8; x++)
                {
                    pixels[y * 8 + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            _veil = tex;
            _veilSolid = solid;
            return tex;
        }

        static Color OverlayHills(Color sky, float u, float v)
        {
            float ridge3 = 0.34f + 0.05f * Mathf.Sin(u * 6.2f + 0.4f) + 0.03f * Fbm(u * 2.1f, 1.4f, 3);
            float ridge2 = 0.26f + 0.07f * Mathf.Sin(u * 4.1f + 1.7f) + 0.04f * Fbm(u * 3.3f + 2f, 2.2f, 3);
            float ridge1 = 0.16f + 0.08f * Mathf.Sin(u * 3.2f + 0.2f) + 0.05f * Fbm(u * 4.4f, 0.8f, 3);

            if (v < ridge3)
            {
                sky = ShadeRidge(sky, new Color(0.16f, 0.1f, 0.28f), 0.72f, u, v, ridge3, 6.2f, 0.4f, 2.1f, 1.4f, 0.05f, 0.03f);
            }

            if (v < ridge2)
            {
                sky = ShadeRidge(sky, new Color(0.08f, 0.06f, 0.16f), 0.86f, u, v, ridge2, 4.1f, 1.7f, 3.3f, 2.2f, 0.07f, 0.04f);
            }

            if (v < ridge1)
            {
                float grass = 0.04f + 0.03f * Fbm(u * 8f, v * 6f, 2);
                var ground = new Color(0.05f + grass, 0.07f + grass * 0.6f, 0.1f);
                sky = ShadeRidge(sky, ground, 0.94f, u, v, ridge1, 3.2f, 0.2f, 4.4f, 0.8f, 0.08f, 0.05f);
            }

            float mist = Smooth((0.38f - Mathf.Abs(v - 0.3f)) / 0.18f) * 0.14f;
            sky = Color.Lerp(sky, new Color(0.22f, 0.14f, 0.3f), mist);
            return sky;
        }

        static Color ShadeRidge(
            Color sky,
            Color pigment,
            float cover,
            float u,
            float v,
            float ridge,
            float sineFreq,
            float sinePhase,
            float fbmX,
            float fbmY,
            float sineAmp,
            float fbmAmp)
        {
            sky = Color.Lerp(sky, pigment, cover);

            float depth = Mathf.Max(0f, ridge - v);
            float ao = Smooth(depth * 7.5f) * 0.42f;
            float crest = Smooth(1f - depth * 28f) * 0.18f;

            const float du = 0.012f;
            float left = RidgeAt(u - du, sineFreq, sinePhase, fbmX, fbmY, sineAmp, fbmAmp);
            float slope = (ridge - left) / du;
            float lee = Smooth(slope * 3.4f);

            Color shadow = Color.Lerp(sky, new Color(0.012f, 0.01f, 0.03f), 0.72f);
            sky = Color.Lerp(sky, shadow, Mathf.Clamp01(ao * 0.7f + lee * 0.55f));

            Color lit = sky + new Color(0.1f, 0.08f, 0.05f) * (1f - lee) * crest;
            sky = Color.Lerp(sky, lit, 0.55f);

            float contact = Smooth(1f - Mathf.Abs(ridge - v) * 36f);
            sky = Color.Lerp(sky, new Color(0.02f, 0.015f, 0.04f), contact * 0.35f);
            return sky;
        }

        static float RidgeAt(float u, float sineFreq, float sinePhase, float fbmX, float fbmY, float sineAmp, float fbmAmp)
        {
            return sineAmp * Mathf.Sin(u * sineFreq + sinePhase) + fbmAmp * Fbm(u * fbmX, fbmY, 3);
        }

        static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f;
            float amp = 0.5f;
            float freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Mathf.PerlinNoise(x * freq, y * freq);
                freq *= 2.03f;
                amp *= 0.5f;
            }

            return sum;
        }

        static float Hash(float x, float y)
        {
            return Mathf.Abs(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f) % 1f;
        }

        static float Smooth(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
    }
}
