using UnityEngine;

namespace Liryne.Game.World
{
    /// <summary>
    /// Village de la Clairière, en placeholders. Spawn serveur = origin, place laissée libre.
    /// </summary>
    public static class VillageBuilder
    {
        public static void Build(Transform parent)
        {
            var root = new GameObject("VillageClairiere");
            root.transform.SetParent(parent, false);

            Plaza(root.transform);
            Paths(root.transform);
            Well(root.transform, new Vector3(4.2f, 0f, -4.8f));
            Lyre(root.transform, new Vector3(-3.6f, 0f, 3.8f));
            Inn(root.transform, new Vector3(-21f, 0f, 1f), 90f);
            House(root.transform, "MaisonNord", new Vector3(6f, 0f, 20f), 180f, Hex("f0d8c0"), Hex("7a3a48"), new Vector3(5.2f, 2.6f, 4.6f));
            House(root.transform, "MaisonNordEst", new Vector3(20f, 0f, 15f), -130f, Hex("ead4b8"), Hex("4a5a8a"), new Vector3(4.6f, 2.4f, 4.2f));
            House(root.transform, "MaisonSudEst", new Vector3(19f, 0f, -16f), -40f, Hex("e8c8b0"), Hex("3a6a5a"), new Vector3(5f, 2.5f, 4.4f));
            House(root.transform, "MaisonSud", new Vector3(-4f, 0f, -21f), 0f, Hex("f2e0cc"), Hex("8a4a2a"), new Vector3(4.8f, 2.3f, 4.2f));
            House(root.transform, "MaisonSudOuest", new Vector3(-19f, 0f, -17f), 40f, Hex("e6d0c4"), Hex("5a3a6a"), new Vector3(4.4f, 2.4f, 4f));
            House(root.transform, "MaisonNordOuest", new Vector3(-17f, 0f, 18f), 140f, Hex("efe0d0"), Hex("3a4a6a"), new Vector3(4.6f, 2.5f, 4.2f));
            Stall(root.transform, new Vector3(13.5f, 0f, 1.5f), -90f);
            Notice(root.transform, new Vector3(-1.2f, 0f, 6.5f));
            Crate(root.transform, new Vector3(3.4f, 0f, -1.1f), 18f, new Vector3(0.9f, 0.72f, 0.9f), Hex("b48a58"));
            Crate(root.transform, new Vector3(4.4f, 0f, -1.4f), -12f, new Vector3(0.8f, 0.58f, 0.8f), Hex("c49a62"));
            FenceRing(root.transform);
            Trees(root.transform);
            Flowers(root.transform);
            Sign(root.transform, "Clairière de la Lyre", new Vector3(0f, 3.4f, 0f), 28, Hex("efe4c8"));
        }

        static void Plaza(Transform parent)
        {
            GameObject plaza = Cylinder("Place", parent, new Vector3(0f, 0.03f, 0f), Quaternion.identity, new Vector3(9.5f, 0.04f, 9.5f), Hex("8a8074"));
            DropCollider(plaza);
            GameObject ring = Cylinder("Dallage", parent, new Vector3(0f, 0.04f, 0f), Quaternion.identity, new Vector3(5.2f, 0.04f, 5.2f), Hex("9a9084"));
            DropCollider(ring);
        }

        static void Paths(Transform parent)
        {
            Strip("AlléeNS", parent, Vector3.zero, new Vector3(3.2f, 0.025f, 48f), Hex("6e5840"));
            Strip("AlléeEO", parent, Vector3.zero, new Vector3(48f, 0.025f, 3.2f), Hex("6e5840"));
        }

        static void Strip(string name, Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            GameObject cube = Cube(name, parent, pos + Vector3.up * 0.02f, Quaternion.identity, scale, color);
            DropCollider(cube);
        }

        static void Well(Transform parent, Vector3 pos)
        {
            Cube("PuitsSocle", parent, pos + new Vector3(0f, 0.2f, 0f), Quaternion.identity, new Vector3(2.1f, 0.4f, 2.1f), Hex("6a645c"));
            Cylinder("PuitsEau", parent, pos + new Vector3(0f, 0.42f, 0f), Quaternion.identity, new Vector3(1.5f, 0.12f, 1.5f), Hex("3a6a8a"));
            Cube("PuitsPilierL", parent, pos + new Vector3(-0.85f, 1.1f, 0f), Quaternion.identity, new Vector3(0.22f, 1.6f, 0.22f), Hex("5a3a22"));
            Cube("PuitsPilierR", parent, pos + new Vector3(0.85f, 1.1f, 0f), Quaternion.identity, new Vector3(0.22f, 1.6f, 0.22f), Hex("5a3a22"));
            Cube("PuitsToit", parent, pos + new Vector3(0f, 1.95f, 0f), Quaternion.Euler(0f, 45f, 0f), new Vector3(2.4f, 0.16f, 2.4f), Hex("7a3a2a"));
            Sign(parent, "Puits de l'Aube", pos + new Vector3(0f, 2.35f, 0f), 18, Hex("efe8dc"));
        }

        static void Lyre(Transform parent, Vector3 pos)
        {
            Cube("SocleLyre", parent, pos + new Vector3(0f, 0.25f, 0f), Quaternion.identity, new Vector3(1.6f, 0.5f, 1.6f), Hex("c4b48a"));
            Cube("ColLyre", parent, pos + new Vector3(0f, 1.2f, 0f), Quaternion.identity, new Vector3(0.22f, 1.5f, 0.18f), Hex("d4c49a"));
            Cylinder("Crosse", parent, pos + new Vector3(0.35f, 2.05f, 0f), Quaternion.Euler(0f, 0f, -55f), new Vector3(0.9f, 0.12f, 0.9f), Hex("e8d8a8"));
            Color[] strings = { Hex("d45a3a"), Hex("3a8ad4"), Hex("efe6c0"), Hex("4a3a6a") };
            for (int i = 0; i < strings.Length; i++)
            {
                float x = -0.18f + i * 0.14f;
                Cube("Corde" + i, parent, pos + new Vector3(x, 1.45f, 0.12f), Quaternion.identity, new Vector3(0.04f, 1.1f, 0.04f), strings[i]);
            }
        }

        static void Inn(Transform parent, Vector3 pos, float yaw)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            House(parent, "Auberge", pos, yaw, Hex("e8c8a8"), Hex("6a2438"), new Vector3(8.5f, 3.4f, 6.2f));
            Cube("Auvent", parent, pos + rot * new Vector3(0f, 1.6f, 3.4f), rot, new Vector3(7.2f, 0.16f, 2.2f), Hex("5a1e2c"));
            Sign(parent, "Auberge de la Lyre", pos + rot * new Vector3(0f, 4.6f, 0.2f), 22, Hex("ffe8c8"));
        }

        static void House(Transform parent, string name, Vector3 pos, float yaw, Color wall, Color roof, Vector3 size)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            var house = new GameObject(name);
            house.transform.SetParent(parent, false);
            house.transform.SetPositionAndRotation(pos, rot);
            Cube("Murs", house.transform, new Vector3(0f, size.y * 0.5f, 0f), Quaternion.identity, size, wall);
            Cube("Toit", house.transform, new Vector3(0f, size.y + 0.55f, 0f), Quaternion.Euler(0f, 45f, 0f), new Vector3(size.x * 1.18f, 0.7f, size.z * 1.18f), roof);
            Cube("Porte", house.transform, new Vector3(0f, 0.95f, size.z * 0.51f), Quaternion.identity, new Vector3(0.9f, 1.8f, 0.12f), Hex("4a2e1c"));
            Cube("FenetreL", house.transform, new Vector3(-size.x * 0.28f, 1.6f, size.z * 0.51f), Quaternion.identity, new Vector3(0.7f, 0.7f, 0.08f), Hex("8ab4d4"));
            Cube("FenetreR", house.transform, new Vector3(size.x * 0.28f, 1.6f, size.z * 0.51f), Quaternion.identity, new Vector3(0.7f, 0.7f, 0.08f), Hex("8ab4d4"));
            Cube("Cheminee", house.transform, new Vector3(size.x * 0.28f, size.y + 1.1f, -size.z * 0.15f), Quaternion.identity, new Vector3(0.55f, 1.1f, 0.55f), Hex("6a4a42"));
        }

        static void Stall(Transform parent, Vector3 pos, float yaw)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Cube("Etal", parent, pos + new Vector3(0f, 0.7f, 0f), rot, new Vector3(3.4f, 0.16f, 1.6f), Hex("c4a06a"));
            Cube("PiedL", parent, pos + rot * new Vector3(-1.4f, 0.35f, 0f), rot, new Vector3(0.16f, 0.7f, 0.16f), Hex("5a3a22"));
            Cube("PiedR", parent, pos + rot * new Vector3(1.4f, 0.35f, 0f), rot, new Vector3(0.16f, 0.7f, 0.16f), Hex("5a3a22"));
            Cube("Toile", parent, pos + new Vector3(0f, 1.7f, 0f), rot * Quaternion.Euler(12f, 0f, 0f), new Vector3(3.6f, 0.08f, 1.8f), Hex("c45a4a"));
            Sign(parent, "Marché", pos + new Vector3(0f, 2.15f, 0f), 16, Hex("fff0e0"));
        }

        static void Crate(Transform parent, Vector3 pos, float yaw, Vector3 size, Color color)
        {
            Cube("Caisse", parent, pos + new Vector3(0f, size.y * 0.5f, 0f), Quaternion.Euler(0f, yaw, 0f), size, color);
        }

        static void Notice(Transform parent, Vector3 pos)
        {
            Cube("PanneauPied", parent, pos + new Vector3(0f, 0.9f, 0f), Quaternion.identity, new Vector3(0.14f, 1.8f, 0.14f), Hex("5a3a22"));
            Cube("Panneau", parent, pos + new Vector3(0f, 1.7f, 0f), Quaternion.identity, new Vector3(1.6f, 1.1f, 0.08f), Hex("d4b07a"));
            Sign(parent, "Avis du village", pos + new Vector3(0f, 2.4f, 0f), 14, Hex("4a3018"));
        }

        static void FenceRing(Transform parent)
        {
            const float r = 27f;
            for (int i = 0; i < 36; i++)
            {
                float a = i * 10f * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                if (Mathf.Abs(pos.x) < 3.5f || Mathf.Abs(pos.z) < 3.5f)
                {
                    continue;
                }

                Cube("Poteau", parent, pos + Vector3.up * 0.55f, Quaternion.identity, new Vector3(0.16f, 1.1f, 0.16f), Hex("5a3e28"));
            }
        }

        static void Trees(Transform parent)
        {
            Vector3[] spots =
            {
                new Vector3(28f, 0f, 10f), new Vector3(26f, 0f, -12f), new Vector3(24f, 0f, 24f),
                new Vector3(-27f, 0f, 8f), new Vector3(-25f, 0f, -10f), new Vector3(-24f, 0f, 26f),
                new Vector3(10f, 0f, 28f), new Vector3(-8f, 0f, 27f), new Vector3(12f, 0f, -28f),
                new Vector3(-12f, 0f, -27f), new Vector3(32f, 0f, 2f), new Vector3(-32f, 0f, -4f),
                new Vector3(8f, 0f, 32f), new Vector3(-30f, 0f, 18f), new Vector3(29f, 0f, -22f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                Tree(parent, spots[i], 1f + (i % 4) * 0.12f);
            }
        }

        static void Tree(Transform parent, Vector3 pos, float scale)
        {
            var tree = new GameObject("Arbre");
            tree.transform.SetParent(parent, false);
            tree.transform.position = pos;
            tree.transform.localScale = Vector3.one * scale;
            Cylinder("Tronc", tree.transform, new Vector3(0f, 1.1f, 0f), Quaternion.identity, new Vector3(0.55f, 2.2f, 0.55f), Hex("4a2e1c"));
            Sphere("Feuille1", tree.transform, new Vector3(0f, 3.1f, 0f), 2.4f, Hex("3a7a48"));
            Sphere("Feuille2", tree.transform, new Vector3(0.7f, 2.6f, 0.3f), 1.6f, Hex("2e6a3c"));
            Sphere("Feuille3", tree.transform, new Vector3(-0.5f, 2.7f, -0.4f), 1.5f, Hex("4a8a50"));
        }

        static void Flowers(Transform parent)
        {
            Color[] petals = { Hex("d45a7a"), Hex("e8d46a"), Hex("7a5ad4"), Hex("f0e8f8") };
            Vector3[] spots =
            {
                new Vector3(2.8f, 0f, 7.2f), new Vector3(-6.5f, 0f, 5f), new Vector3(7.4f, 0f, -2.2f),
                new Vector3(-7.2f, 0f, -6f), new Vector3(5.5f, 0f, 6.2f), new Vector3(-5f, 0f, 7.4f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                Cube("Jardiniere", parent, spots[i] + new Vector3(0f, 0.18f, 0f), Quaternion.identity, new Vector3(1.1f, 0.35f, 0.7f), Hex("6a4a32"));
                Sphere("Fleur", parent, spots[i] + new Vector3(0f, 0.55f, 0f), 0.45f, petals[i % petals.Length]);
            }
        }

        static void Sign(Transform parent, string text, Vector3 pos, int size, Color color)
        {
            var go = new GameObject("Enseigne");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.fontSize = size;
            mesh.characterSize = 0.08f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font is not null)
            {
                mesh.font = font;
            }

            go.AddComponent<SignBillboard>();
        }

        static GameObject Cube(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Place(go, name, parent, pos, rot, scale, color);
            return go;
        }

        static GameObject Cylinder(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Place(go, name, parent, pos, rot, scale, color);
            return go;
        }

        static GameObject Sphere(string name, Transform parent, Vector3 pos, float diameter, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Place(go, name, parent, pos, Quaternion.identity, Vector3.one * diameter, color);
            return go;
        }

        static void Place(GameObject go, string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Color color)
        {
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            Paint(go, color);
            DropCollider(go);
        }

        static void Paint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer is null)
            {
                return;
            }

            SurfaceKind kind = SurfaceArt.Guess(color);
            Vector3 scale = go.transform.localScale;
            var tiling = new Vector2(Mathf.Max(1.2f, Mathf.Abs(scale.x) * 0.35f), Mathf.Max(1.2f, Mathf.Abs(scale.y) * 0.45f));
            SurfaceArt.Apply(renderer, color, kind, 0.22f, false, tiling);
        }

        static void DropCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider is null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(collider);
            }
            else
            {
                Object.DestroyImmediate(collider);
            }
        }

        static Color Hex(string hex)
        {
            if (!ColorUtility.TryParseHtmlString("#" + hex, out Color color))
            {
                return Color.magenta;
            }

            return color;
        }
    }

    sealed class SignBillboard : MonoBehaviour
    {
        void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera is null)
            {
                return;
            }

            WorldLook.AwayFrom(transform, camera.transform.position);
        }
    }
}
