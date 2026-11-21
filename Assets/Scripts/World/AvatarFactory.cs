using Liryne.Protocol.Messages.Characters;
using Liryne.Game.Content;
using UnityEngine;

namespace Liryne.Game.World
{
    /// <summary>Placeholders manga : capsules, pas de mesh de production.</summary>
    public static class AvatarFactory
    {
        static Shader _lit;

        public static GameObject Create(string name, CharacterAppearance appearance, EntityKindVisual kind)
        {
            var root = new GameObject(name);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            Object.Destroy(body.GetComponent<Collider>());

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            Object.Destroy(head.GetComponent<Collider>());

            var hair = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hair.name = "Hair";
            hair.transform.SetParent(root.transform, false);
            Object.Destroy(hair.GetComponent<Collider>());

            var eyeL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeL.name = "EyeL";
            eyeL.transform.SetParent(root.transform, false);
            Object.Destroy(eyeL.GetComponent<Collider>());

            var eyeR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeR.name = "EyeR";
            eyeR.transform.SetParent(root.transform, false);
            Object.Destroy(eyeR.GetComponent<Collider>());

            Apply(root, appearance, kind);
            return root;
        }

        public static void Apply(GameObject root, CharacterAppearance appearance, EntityKindVisual kind)
        {
            appearance = ClientCatalog.Clamp(appearance);
            Transform body = root.transform.Find("Body");
            Transform head = root.transform.Find("Head");
            Transform hair = root.transform.Find("Hair");
            Transform eyeL = root.transform.Find("EyeL");
            Transform eyeR = root.transform.Find("EyeR");
            if (body is null)
            {
                return;
            }

            float height = appearance.BodyType == 0 ? 0.92f : 1.04f;
            if (kind == EntityKindVisual.Monster)
            {
                height *= 0.88f;
            }

            root.transform.localScale = Vector3.one * height;
            body.localPosition = new Vector3(0f, 1f, 0f);
            body.localScale = Vector3.one;
            head.localPosition = new Vector3(0f, 1.85f, 0.05f);
            head.localScale = Vector3.one * 0.72f;

            float hairY = 2.12f + (appearance.HairStyleId % 4) * 0.04f;
            float hairScale = 0.55f + (appearance.HairStyleId % 5) * 0.05f;
            hair.localPosition = new Vector3(0f, hairY, -0.02f + (appearance.HairStyleId % 3) * 0.02f);
            hair.localScale = new Vector3(hairScale + 0.08f, hairScale, hairScale + 0.04f);

            Vector3 eye = new Vector3(0.14f, 1.88f, 0.28f);
            eyeL.localPosition = new Vector3(-eye.x, eye.y, eye.z);
            eyeR.localPosition = new Vector3(eye.x, eye.y, eye.z);
            eyeL.localScale = eyeR.localScale = Vector3.one * 0.12f;

            Color skin = kind == EntityKindVisual.Monster
                ? new Color(0.45f, 0.38f, 0.55f)
                : ClientCatalog.SkinColor(appearance.SkinToneId);
            Color hairColor = kind == EntityKindVisual.Monster
                ? new Color(0.2f, 0.16f, 0.28f)
                : ClientCatalog.HairColor(appearance.HairColorId);
            Color eyeColor = ClientCatalog.EyeColor(appearance.EyeColorId);

            Paint(body, skin);
            Paint(head, skin);
            Paint(hair, hairColor);
            Paint(eyeL, eyeColor);
            Paint(eyeR, eyeColor);
        }

        static void Paint(Transform part, Color color)
        {
            var renderer = part.GetComponent<MeshRenderer>();
            if (renderer is null)
            {
                return;
            }

            if (_lit is null)
            {
                _lit = Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                    ?? Shader.Find("Sprites/Default");
            }

            var material = new Material(_lit);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            renderer.sharedMaterial = material;
        }
    }

    public enum EntityKindVisual : byte
    {
        Player = 1,
        Monster = 2,
        Other = 3,
    }
}
