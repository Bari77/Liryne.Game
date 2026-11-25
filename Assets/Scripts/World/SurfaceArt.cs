using System.Collections.Generic;
using UnityEngine;

namespace Liryne.Game.World
{
    public enum SurfaceKind : byte
    {
        Grain,
        Skin,
        Cloth,
        Hair,
        Leather,
        Metal,
        Wood,
        Grass,
        Rock,
        Plaster,
    }

    /// <summary>Albedos tuilés procéduraux. Les primitives n'ont pas d'UV Blender : le motif doit boucler.</summary>
    public static class SurfaceArt
    {
        const int MapSize = 96;

        static readonly Texture2D[] Maps = new Texture2D[10];
        static readonly Dictionary<long, Material> Materials = new Dictionary<long, Material>(64);
        static Shader _lit;
        static Shader _unlit;

        public static void Apply(
            Renderer renderer,
            Color color,
            SurfaceKind kind,
            float smoothness = 0.28f,
            bool unlit = false,
            Vector2 tiling = default)
        {
            if (renderer is null)
            {
                return;
            }

            if (tiling == default)
            {
                tiling = Tiling(kind);
            }

            renderer.sharedMaterial = Material(color, kind, smoothness, unlit, tiling);
        }

        public static Material Material(Color color, SurfaceKind kind, float smoothness, bool unlit, Vector2 tiling)
        {
            EnsureShaders();
            Shader shader = unlit && _unlit is not null ? _unlit : _lit;
            if (shader is null)
            {
                return null;
            }

            long key = Hash(color, kind, smoothness, unlit, tiling);
            if (Materials.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            var material = new Material(shader);
            Texture2D map = Map(kind);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (map is not null)
            {
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", map);
                    material.SetTextureScale("_BaseMap", tiling);
                }

                if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", map);
                    material.SetTextureScale("_MainTex", tiling);
                }
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", kind == SurfaceKind.Metal ? 0.55f : 0f);
            }

            Materials[key] = material;
            return material;
        }

        public static Vector2 Tiling(SurfaceKind kind) => kind switch
        {
            SurfaceKind.Cloth => new Vector2(2.4f, 2.4f),
            SurfaceKind.Hair => new Vector2(2.2f, 3.4f),
            SurfaceKind.Skin => new Vector2(1.4f, 1.4f),
            SurfaceKind.Leather => new Vector2(1.8f, 1.8f),
            SurfaceKind.Wood => new Vector2(1.6f, 3.2f),
            SurfaceKind.Grass => new Vector2(4f, 4f),
            SurfaceKind.Rock => new Vector2(2.6f, 2.2f),
            SurfaceKind.Plaster => new Vector2(2f, 2f),
            SurfaceKind.Metal => new Vector2(1.8f, 2.6f),
            _ => new Vector2(1.6f, 1.6f),
        };

        public static SurfaceKind Guess(Color color)
        {
            if (color.b > color.r + 0.12f && color.b > color.g)
            {
                return SurfaceKind.Metal;
            }

            if (color.g > color.r * 1.12f && color.g > color.b)
            {
                return SurfaceKind.Grass;
            }

            if (color.r > 0.72f && color.g > 0.62f && color.b > 0.48f)
            {
                return SurfaceKind.Plaster;
            }

            if (color.r > color.b + 0.08f && color.maxColorComponent < 0.55f)
            {
                return SurfaceKind.Wood;
            }

            return SurfaceKind.Rock;
        }

        static Texture2D Map(SurfaceKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= Maps.Length)
            {
                index = 0;
            }

            if (Maps[index] != null)
            {
                return Maps[index];
            }

            Maps[index] = Bake(kind);
            return Maps[index];
        }

        static Texture2D Bake(SurfaceKind kind)
        {
            var tex = new Texture2D(MapSize, MapSize, TextureFormat.RGBA32, false)
            {
                name = "Surface" + kind,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color[MapSize * MapSize];
            float inv = 1f / MapSize;
            for (int y = 0; y < MapSize; y++)
            {
                float v = (y + 0.5f) * inv;
                for (int x = 0; x < MapSize; x++)
                {
                    float u = (x + 0.5f) * inv;
                    pixels[y * MapSize + x] = Sample(kind, u, v);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }

        static Color Sample(SurfaceKind kind, float u, float v)
        {
            float n;
            switch (kind)
            {
                case SurfaceKind.Skin:
                    n = 0.9f + 0.08f * Mathf.Sin(u * 18f * Mathf.PI) * Mathf.Sin(v * 22f * Mathf.PI);
                    n += 0.04f * Mathf.Sin(u * 54f * Mathf.PI) * Mathf.Sin(v * 50f * Mathf.PI);
                    return Grey(n, 0.98f, 0.94f);
                case SurfaceKind.Cloth:
                    float warp = 0.82f + 0.18f * Mathf.Abs(Mathf.Sin(u * 36f * Mathf.PI));
                    float weft = 0.82f + 0.18f * Mathf.Abs(Mathf.Sin(v * 36f * Mathf.PI));
                    n = warp * weft;
                    n *= 0.92f + 0.08f * Mathf.Abs(Mathf.Sin((u + v) * 12f * Mathf.PI));
                    return Grey(n, 1f, 1f);
                case SurfaceKind.Hair:
                    n = 0.72f + 0.28f * Mathf.Abs(Mathf.Sin(u * 48f * Mathf.PI + Mathf.Sin(v * 8f * Mathf.PI)));
                    n *= 0.9f + 0.1f * Mathf.Sin(v * 20f * Mathf.PI);
                    return Grey(n, 0.96f, 0.9f);
                case SurfaceKind.Leather:
                    n = 0.78f + 0.16f * Mathf.Sin(u * 10f * Mathf.PI) * Mathf.Sin(v * 14f * Mathf.PI);
                    n += 0.06f * Mathf.Abs(Mathf.Sin(u * 40f * Mathf.PI + v * 28f * Mathf.PI));
                    return Grey(n, 0.92f, 0.82f);
                case SurfaceKind.Metal:
                    n = 0.78f + 0.18f * Mathf.Abs(Mathf.Sin(v * 64f * Mathf.PI + u * 6f * Mathf.PI));
                    n += 0.05f * Mathf.Sin(u * 8f * Mathf.PI);
                    return Grey(n, 1.02f, 1.05f);
                case SurfaceKind.Wood:
                    float ring = Mathf.Abs(Mathf.Sin((u * 1.2f + Mathf.Sin(v * 6f * Mathf.PI) * 0.08f) * 18f * Mathf.PI));
                    n = 0.62f + 0.28f * ring + 0.08f * Mathf.Sin(v * 40f * Mathf.PI);
                    return Grey(n, 0.9f, 0.72f);
                case SurfaceKind.Grass:
                    n = 0.7f + 0.2f * Mathf.Abs(Mathf.Sin(u * 22f * Mathf.PI + v * 4f));
                    n += 0.12f * Mathf.Abs(Mathf.Sin(v * 48f * Mathf.PI + u * 10f * Mathf.PI));
                    return Grey(n, 1.05f, 0.82f);
                case SurfaceKind.Rock:
                    n = 0.68f + 0.2f * Mathf.Sin(u * 8f * Mathf.PI) * Mathf.Sin(v * 10f * Mathf.PI);
                    float crack = Mathf.Min(
                        Mathf.Abs(Mathf.Sin(u * 16f * Mathf.PI + v * 3f)),
                        Mathf.Abs(Mathf.Sin(v * 14f * Mathf.PI - u * 2f)));
                    n *= 0.82f + 0.18f * crack;
                    return Grey(n, 0.96f, 1.02f);
                case SurfaceKind.Plaster:
                    n = 0.88f + 0.08f * Mathf.Sin(u * 28f * Mathf.PI) * Mathf.Sin(v * 26f * Mathf.PI);
                    n += 0.04f * Mathf.Abs(Mathf.Sin((u * 3f + v) * 20f * Mathf.PI));
                    return Grey(n, 1f, 0.96f);
                default:
                    n = 0.9f + 0.1f * Mathf.Sin(u * 16f * Mathf.PI) * Mathf.Sin(v * 16f * Mathf.PI);
                    return Grey(n, 1f, 1f);
            }
        }

        static Color Grey(float n, float gMul, float bMul)
        {
            n = Mathf.Clamp01(n);
            return new Color(n, Mathf.Clamp01(n * gMul), Mathf.Clamp01(n * bMul), 1f);
        }

        static void EnsureShaders()
        {
            if (_lit is null)
            {
                _lit = Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                    ?? Shader.Find("Sprites/Default");
            }

            if (_unlit is null)
            {
                _unlit = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Texture")
                    ?? Shader.Find("Sprites/Default");
            }
        }

        static long Hash(Color color, SurfaceKind kind, float smoothness, bool unlit, Vector2 tiling)
        {
            unchecked
            {
                int c = (Quant(color.r) << 24) | (Quant(color.g) << 16) | (Quant(color.b) << 8) | Quant(color.a);
                int extra = (Quant(smoothness) << 16) | (Quant(tiling.x) << 8) | Quant(tiling.y);
                return ((long)c << 32) ^ (uint)extra ^ ((uint)kind << 12) ^ (unlit ? 1u : 0u);
            }
        }

        static int Quant(float value) => Mathf.Clamp(Mathf.RoundToInt(value * 63f), 0, 63);
    }
}
