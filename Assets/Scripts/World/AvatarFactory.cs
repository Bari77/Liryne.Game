using Liryne.Game.Content;
using Liryne.Protocol.Messages.Characters;
using UnityEngine;

namespace Liryne.Game.World
{
    /// <summary>Silhouettes manga en primitives : visage, yeux, coiffure, tenue de classe.</summary>
    public static class AvatarFactory
    {
        static SurfaceKind _surface = SurfaceKind.Grain;

        public static GameObject Create(string name, CharacterAppearance appearance, EntityKindVisual kind, ushort classOrTemplateId = 1)
        {
            var root = new GameObject(name);
            Build(root.transform, appearance, kind, classOrTemplateId);
            return root;
        }

        public static void Apply(GameObject root, CharacterAppearance appearance, EntityKindVisual kind, ushort classOrTemplateId = 1)
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            Build(root.transform, appearance, kind, classOrTemplateId);
        }

        static void Build(Transform root, CharacterAppearance appearance, EntityKindVisual kind, ushort id)
        {
            appearance = ClientCatalog.Clamp(appearance);
            if (kind == EntityKindVisual.Monster)
            {
                BuildMonster(root, id);
            }
            else
            {
                BuildPlayer(root, appearance, id);
            }

            BindWalk(root, kind);
        }

        static void BuildPlayer(Transform root, CharacterAppearance appearance, ushort id)
        {
            bool teen = appearance.BodyType == 0;
            float height = teen ? 0.94f : 1.02f;
            root.localScale = Vector3.one * height;

            Color skin = ClientCatalog.SkinColor(appearance.SkinToneId);
            Color hair = ClientCatalog.HairColor(appearance.HairColorId);
            Color iris = ClientCatalog.EyeColor(appearance.EyeColorId);
            Outfit outfit = Outfit.For(id);

            UsingSurface(SurfaceKind.Cloth, () =>
            {
                Part("Pelvis", root, PrimitiveType.Cube, new Vector3(0f, 0.92f, 0.01f), new Vector3(0.34f, 0.2f, 0.18f), outfit.Pants);
            });

            Transform spine = Joint("Spine", root, new Vector3(0f, 1.02f, 0f));
            UsingSurface(SurfaceKind.Cloth, () =>
            {
                Part("Torso", spine, PrimitiveType.Cube, new Vector3(0f, 0.2f, 0.02f), new Vector3(0.4f, 0.42f, 0.22f), outfit.Tunic);
                Part("Chest", spine, PrimitiveType.Sphere, new Vector3(0f, 0.32f, 0.05f), new Vector3(0.36f, 0.22f, 0.2f), outfit.Tunic);
                Part("Collar", spine, PrimitiveType.Cylinder, new Vector3(0f, 0.42f, 0.02f), new Vector3(0.18f, 0.04f, 0.18f), outfit.Trim);
            });
            UsingSurface(SurfaceKind.Skin, () =>
            {
                Part("Neck", spine, PrimitiveType.Cylinder, new Vector3(0f, 0.48f, 0.03f), new Vector3(0.1f, 0.07f, 0.1f), skin);
            });

            Limb(
                root,
                "LegL",
                new Vector3(-0.11f, 0.9f, 0.01f),
                "ThighL",
                outfit.Pants,
                new Vector3(0.14f, 0.28f, 0.14f),
                "ShinL",
                new Vector3(0.12f, 0.22f, 0.12f),
                "BootL",
                outfit.Boot);
            Limb(
                root,
                "LegR",
                new Vector3(0.11f, 0.9f, 0.01f),
                "ThighR",
                outfit.Pants,
                new Vector3(0.14f, 0.28f, 0.14f),
                "ShinR",
                new Vector3(0.12f, 0.22f, 0.12f),
                "BootR",
                outfit.Boot);

            Arm(spine, "ArmL", new Vector3(-0.24f, 0.36f, 0.02f), skin, outfit);
            Arm(spine, "ArmR", new Vector3(0.24f, 0.36f, 0.02f), skin, outfit);

            Transform head = null;
            UsingSurface(SurfaceKind.Skin, () =>
            {
                head = Part(
                    "Head",
                    spine,
                    PrimitiveType.Sphere,
                    new Vector3(0f, 0.66f, 0.04f),
                    Vector3.one * (teen ? 0.5f : 0.46f),
                    skin,
                    smoothness: 0.42f);
                Part("EarL", head, PrimitiveType.Sphere, new Vector3(-0.42f, 0f, 0f), new Vector3(0.18f, 0.28f, 0.16f), skin);
                Part("EarR", head, PrimitiveType.Sphere, new Vector3(0.42f, 0f, 0f), new Vector3(0.18f, 0.28f, 0.16f), skin);
            });

            Face(head, appearance.FaceId, iris, skin);
            Hair(head, appearance.HairStyleId, hair);
            Gear(root, spine, id, outfit);
        }

        static void Limb(
            Transform root,
            string hipName,
            Vector3 hipPos,
            string thighName,
            Color pants,
            Vector3 thighScale,
            string shinName,
            Vector3 shinScale,
            string bootName,
            Color boot)
        {
            Transform hip = Joint(hipName, root, hipPos);
            UsingSurface(SurfaceKind.Cloth, () =>
            {
                Part(thighName, hip, PrimitiveType.Capsule, new Vector3(0f, -0.28f, 0f), thighScale, pants);
            });
            Transform knee = Joint(hipName == "LegL" ? "KneeL" : "KneeR", hip, new Vector3(0f, -0.54f, 0f));
            UsingSurface(SurfaceKind.Cloth, () =>
            {
                Part(shinName, knee, PrimitiveType.Capsule, new Vector3(0f, -0.22f, 0f), shinScale, pants);
            });
            Transform ankle = Joint(hipName == "LegL" ? "AnkleL" : "AnkleR", knee, new Vector3(0f, -0.42f, 0f));
            UsingSurface(SurfaceKind.Leather, () =>
            {
                Part(bootName, ankle, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.04f), new Vector3(0.14f, 0.1f, 0.22f), boot);
            });
        }

        static void Arm(Transform spine, string name, Vector3 localPos, Color skin, Outfit outfit)
        {
            bool left = name == "ArmL";
            Transform arm = Joint(name, spine, localPos);
            arm.localRotation = Quaternion.Euler(0f, 0f, left ? -6f : 6f);
            UsingSurface(SurfaceKind.Cloth, () =>
            {
                Part(left ? "ShoulderL" : "ShoulderR", arm, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.14f, outfit.Tunic);
            });
            UsingSurface(SurfaceKind.Skin, () =>
            {
                Part(left ? "UpperArmL" : "UpperArmR", arm, PrimitiveType.Capsule, new Vector3(0f, -0.22f, 0.01f), new Vector3(0.11f, 0.19f, 0.11f), skin);
            });
            Transform elbow = Joint(left ? "ElbowL" : "ElbowR", arm, new Vector3(0f, -0.4f, 0.02f));
            elbow.localRotation = Quaternion.Euler(-12f, 0f, left ? -5f : 5f);
            UsingSurface(SurfaceKind.Cloth, () =>
            {
                Part(left ? "ForearmL" : "ForearmR", elbow, PrimitiveType.Capsule, new Vector3(0f, -0.18f, 0.02f), new Vector3(0.09f, 0.17f, 0.09f), outfit.Trim);
            });
            Transform wrist = Joint(left ? "WristL" : "WristR", elbow, new Vector3(0f, -0.34f, 0.03f));
            UsingSurface(SurfaceKind.Skin, () =>
            {
                Part(left ? "HandL" : "HandR", wrist, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.11f, skin);
            });
        }

        static void Face(Transform head, byte faceId, Color iris, Color skin)
        {
            int face = faceId % ClientCatalog.Faces;
            float spacing = 0.22f + (face % 3) * 0.02f;
            float eyeY = 0.04f - (face / 4) * 0.03f;
            float eyeW = 0.42f + (face % 2) * 0.06f;
            float eyeH = 0.5f - (face % 4) * 0.03f;
            float browTilt = (face % 4 - 1.5f) * 12f;
            Color lash = new Color(0.08f, 0.06f, 0.08f);
            Color white = new Color(0.97f, 0.97f, 0.98f);
            Color brow = Color.Lerp(skin, lash, 0.72f);

            Eye(head, "EyeL", new Vector3(-spacing, eyeY, 0.42f), eyeW, eyeH, iris, white, lash);
            Eye(head, "EyeR", new Vector3(spacing, eyeY, 0.42f), eyeW, eyeH, iris, white, lash);

            UsingSurface(SurfaceKind.Hair, () =>
            {
                Part("BrowL", head, PrimitiveType.Cube, new Vector3(-spacing, eyeY + 0.22f, 0.4f), new Vector3(0.28f, 0.05f, 0.08f), brow, Quaternion.Euler(0f, 0f, browTilt));
                Part("BrowR", head, PrimitiveType.Cube, new Vector3(spacing, eyeY + 0.22f, 0.4f), new Vector3(0.28f, 0.05f, 0.08f), brow, Quaternion.Euler(0f, 0f, -browTilt));
            });

            float mouthY = -0.18f - (face % 3) * 0.015f;
            float mouthW = 0.16f + (face % 5) * 0.02f;
            Color lip = Color.Lerp(skin, new Color(0.72f, 0.32f, 0.38f), 0.35f);
            UsingSurface(SurfaceKind.Skin, () =>
            {
                Part("Mouth", head, PrimitiveType.Cube, new Vector3(0f, mouthY, 0.44f), new Vector3(mouthW, 0.035f, 0.06f), lip, smoothness: 0.5f);
                Part("Nose", head, PrimitiveType.Sphere, new Vector3(0f, -0.02f, 0.48f), new Vector3(0.08f, 0.1f, 0.08f), skin);
            });
        }

        static void Eye(Transform head, string name, Vector3 pos, float width, float height, Color iris, Color white, Color lash)
        {
            Transform socket = Part(name, head, PrimitiveType.Sphere, pos, new Vector3(width, height, 0.18f), white, unlit: true);
            Part("Iris", socket, PrimitiveType.Sphere, new Vector3(0f, -0.04f, 0.35f), new Vector3(0.55f, 0.62f, 0.35f), iris, unlit: true);
            Part("Pupil", socket, PrimitiveType.Sphere, new Vector3(0f, -0.05f, 0.55f), new Vector3(0.22f, 0.28f, 0.2f), Color.black, unlit: true);
            Part("Shine", socket, PrimitiveType.Sphere, new Vector3(0.12f, 0.16f, 0.7f), new Vector3(0.16f, 0.2f, 0.12f), Color.white, unlit: true);
            Part("Shine2", socket, PrimitiveType.Sphere, new Vector3(-0.14f, -0.08f, 0.62f), new Vector3(0.08f, 0.1f, 0.08f), new Color(1f, 1f, 1f, 0.7f), unlit: true);
            Part("Lash", socket, PrimitiveType.Cube, new Vector3(0f, 0.42f, 0.2f), new Vector3(1.05f, 0.18f, 0.35f), lash, unlit: true);
        }

        static void Hair(Transform head, byte styleId, Color color)
        {
            SurfaceKind previous = _surface;
            _surface = SurfaceKind.Hair;
            int style = styleId % ClientCatalog.HairStyles;
            Part("HairCap", head, PrimitiveType.Sphere, new Vector3(0f, 0.22f, -0.06f), new Vector3(1.08f, 0.78f, 1.08f), color, smoothness: 0.45f);
            switch (style)
            {
                case 0:
                    Part("Bangs", head, PrimitiveType.Sphere, new Vector3(0f, 0.18f, 0.32f), new Vector3(0.85f, 0.28f, 0.35f), color, smoothness: 0.45f);
                    break;
                case 1:
                    Part("Sweep", head, PrimitiveType.Sphere, new Vector3(0.28f, 0.12f, 0.18f), new Vector3(0.7f, 0.4f, 0.55f), color, Quaternion.Euler(0f, -18f, 22f), 0.45f);
                    Part("Bangs", head, PrimitiveType.Sphere, new Vector3(-0.1f, 0.2f, 0.34f), new Vector3(0.7f, 0.22f, 0.28f), color, smoothness: 0.45f);
                    break;
                case 2:
                    for (int i = 0; i < 5; i++)
                    {
                        float x = -0.28f + i * 0.14f;
                        Part("Spike" + i, head, PrimitiveType.Capsule, new Vector3(x, 0.48f, -0.05f), new Vector3(0.16f, 0.38f, 0.16f), color, Quaternion.Euler(-18f + i * 4f, 0f, (i - 2) * 12f), 0.35f);
                    }

                    break;
                case 3:
                    Part("Tail", head, PrimitiveType.Capsule, new Vector3(0f, -0.15f, -0.7f), new Vector3(0.28f, 0.7f, 0.28f), color, Quaternion.Euler(55f, 0f, 0f), 0.45f);
                    Part("Tie", head, PrimitiveType.Sphere, new Vector3(0f, 0.05f, -0.48f), Vector3.one * 0.22f, Color.Lerp(color, Color.white, 0.15f));
                    break;
                case 4:
                    Part("TwinL", head, PrimitiveType.Capsule, new Vector3(-0.42f, -0.2f, -0.15f), new Vector3(0.22f, 0.62f, 0.22f), color, Quaternion.Euler(25f, 0f, 28f), 0.45f);
                    Part("TwinR", head, PrimitiveType.Capsule, new Vector3(0.42f, -0.2f, -0.15f), new Vector3(0.22f, 0.62f, 0.22f), color, Quaternion.Euler(25f, 0f, -28f), 0.45f);
                    Part("Bangs", head, PrimitiveType.Sphere, new Vector3(0f, 0.16f, 0.36f), new Vector3(0.8f, 0.24f, 0.3f), color, smoothness: 0.45f);
                    break;
                case 5:
                    Part("Long", head, PrimitiveType.Capsule, new Vector3(0f, -0.35f, -0.42f), new Vector3(0.55f, 0.85f, 0.35f), color, Quaternion.Euler(18f, 0f, 0f), 0.45f);
                    Part("Bangs", head, PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0.38f), new Vector3(0.9f, 0.26f, 0.32f), color, smoothness: 0.45f);
                    break;
                case 6:
                    Part("Bob", head, PrimitiveType.Sphere, new Vector3(0f, -0.08f, -0.05f), new Vector3(1.12f, 0.7f, 1.05f), color, smoothness: 0.45f);
                    Part("Bangs", head, PrimitiveType.Sphere, new Vector3(0f, 0.1f, 0.38f), new Vector3(0.92f, 0.22f, 0.28f), color, smoothness: 0.45f);
                    break;
                case 7:
                    Part("Crest", head, PrimitiveType.Capsule, new Vector3(0f, 0.55f, 0.05f), new Vector3(0.18f, 0.42f, 0.28f), color, Quaternion.Euler(-12f, 0f, 0f), 0.35f);
                    break;
                case 8:
                    Part("FringeL", head, PrimitiveType.Sphere, new Vector3(-0.22f, 0.1f, 0.38f), new Vector3(0.5f, 0.28f, 0.28f), color, Quaternion.Euler(0f, 12f, 18f), 0.45f);
                    Part("FringeR", head, PrimitiveType.Sphere, new Vector3(0.18f, 0.16f, 0.36f), new Vector3(0.42f, 0.22f, 0.24f), color, smoothness: 0.45f);
                    Part("Back", head, PrimitiveType.Sphere, new Vector3(0f, 0.05f, -0.32f), new Vector3(1f, 0.7f, 0.7f), color, smoothness: 0.45f);
                    break;
                case 9:
                    Part("TuftA", head, PrimitiveType.Sphere, new Vector3(-0.28f, 0.32f, 0.05f), new Vector3(0.45f, 0.4f, 0.4f), color, smoothness: 0.4f);
                    Part("TuftB", head, PrimitiveType.Sphere, new Vector3(0.26f, 0.38f, -0.08f), new Vector3(0.4f, 0.42f, 0.38f), color, smoothness: 0.4f);
                    Part("TuftC", head, PrimitiveType.Sphere, new Vector3(0.05f, 0.28f, 0.28f), new Vector3(0.38f, 0.28f, 0.3f), color, smoothness: 0.4f);
                    break;
                case 10:
                    Part("Volume", head, PrimitiveType.Sphere, new Vector3(0f, 0.28f, -0.08f), new Vector3(1.2f, 1.05f, 1.15f), color, smoothness: 0.5f);
                    break;
                default:
                    Part("Knot", head, PrimitiveType.Sphere, new Vector3(0f, 0.52f, -0.08f), Vector3.one * 0.42f, color, smoothness: 0.45f);
                    Part("Stick", head, PrimitiveType.Cylinder, new Vector3(0.12f, 0.52f, -0.08f), new Vector3(0.06f, 0.22f, 0.06f), new Color(0.55f, 0.38f, 0.22f), Quaternion.Euler(0f, 0f, 70f));
                    break;
            }

            _surface = previous;
        }

        static void Gear(Transform root, Transform spine, ushort classId, Outfit outfit)
        {
            Transform wristL = spine.Find("ArmL/ElbowL/WristL");
            Transform wristR = spine.Find("ArmR/ElbowR/WristR");
            Color wood = new Color(0.38f, 0.24f, 0.14f);
            UsingSurface(SurfaceKind.Metal, () => BuildGear(root, spine, classId, outfit, wood, wristL, wristR));
        }

        static void BuildGear(
            Transform root,
            Transform spine,
            ushort classId,
            Outfit outfit,
            Color wood,
            Transform wristL,
            Transform wristR)
        {
            switch (classId)
            {
                case 1:
                {
                    Transform holster = Joint("Holster", root, new Vector3(0.26f, 0.9f, 0.06f));
                    Transform sword = Grip(
                        "SwordGrip",
                        holster,
                        wristR,
                        new Vector3(0.04f, -0.06f, 0.03f),
                        Quaternion.Euler(-12f, 8f, 18f));
                    UsingSurface(SurfaceKind.Wood, () =>
                    {
                        Part("Hilt", sword, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.07f, 0.055f, 0.07f), wood);
                    });
                    Part("Guard", sword, PrimitiveType.Cube, new Vector3(0f, -0.06f, 0f), new Vector3(0.18f, 0.04f, 0.06f), outfit.Metal, smoothness: 0.55f);
                    Part("Sword", sword, PrimitiveType.Cube, new Vector3(0f, -0.36f, 0f), new Vector3(0.05f, 0.42f, 0.07f), outfit.Metal, smoothness: 0.55f);
                    Transform back = Joint("ShieldHolster", root, new Vector3(0.02f, 1.18f, -0.24f));
                    Transform shield = Grip(
                        "ShieldGrip",
                        back,
                        wristL,
                        new Vector3(0.1f, -0.14f, 0.1f),
                        Quaternion.Euler(78f, 12f, -18f));
                    Part("Shield", shield, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.42f, 0.04f, 0.42f), outfit.Metal, Quaternion.Euler(90f, 0f, 0f), 0.55f);
                    break;
                }

                case 2:
                {
                    Transform hipL = Joint("HolsterL", root, new Vector3(-0.22f, 0.88f, 0.06f));
                    Transform hipR = Joint("HolsterR", root, new Vector3(0.22f, 0.88f, 0.06f));
                    Transform bladeL = Grip("BladeLGrip", hipL, wristL, new Vector3(0.02f, -0.06f, 0.02f), Quaternion.identity);
                    Transform bladeR = Grip("BladeRGrip", hipR, wristR, new Vector3(-0.02f, -0.06f, 0.02f), Quaternion.identity);
                    Part("BladeL", bladeL, PrimitiveType.Cube, new Vector3(0f, -0.28f, 0f), new Vector3(0.04f, 0.34f, 0.07f), outfit.Metal, smoothness: 0.6f);
                    Part("BladeR", bladeR, PrimitiveType.Cube, new Vector3(0f, -0.28f, 0f), new Vector3(0.04f, 0.34f, 0.07f), outfit.Metal, smoothness: 0.6f);
                    break;
                }

                case 3:
                {
                    Transform back = Joint("BowHolster", root, new Vector3(-0.02f, 1.18f, -0.24f));
                    Transform bow = Grip("BowGrip", back, wristL, new Vector3(0.02f, -0.1f, 0.08f), Quaternion.Euler(8f, 0f, 12f));
                    UsingSurface(SurfaceKind.Wood, () =>
                    {
                        Part("Bow", bow, PrimitiveType.Cube, Vector3.zero, new Vector3(0.06f, 0.7f, 0.06f), wood);
                        Part("BowArc", bow, PrimitiveType.Cube, new Vector3(0f, 0f, -0.04f), new Vector3(0.04f, 0.55f, 0.22f), new Color(0.4f, 0.24f, 0.12f));
                    });
                    break;
                }

                case 4:
                    UsingSurface(SurfaceKind.Cloth, () =>
                    {
                        Part("Robe", root, PrimitiveType.Cube, new Vector3(0f, 0.85f, 0.02f), new Vector3(0.48f, 0.7f, 0.28f), Color.Lerp(outfit.Tunic, Color.black, 0.15f));
                    });
                    Transform staffHolster = Joint("StaffHolster", root, new Vector3(0.28f, 0.55f, 0.04f));
                    Transform staff = Grip("StaffGrip", staffHolster, wristR, new Vector3(0.03f, -0.1f, 0.02f), Quaternion.identity);
                    UsingSurface(SurfaceKind.Wood, () =>
                    {
                        Part("Staff", staff, PrimitiveType.Cylinder, new Vector3(0f, -0.35f, 0f), new Vector3(0.05f, 0.72f, 0.05f), wood);
                    });
                    Part("Orb", staff, PrimitiveType.Sphere, new Vector3(0f, 0.42f, 0f), Vector3.one * 0.14f, outfit.Trim, unlit: true);
                    break;
                case 5:
                    UsingSurface(SurfaceKind.Cloth, () =>
                    {
                        Part("Sash", root, PrimitiveType.Cube, new Vector3(0f, 1.05f, 0.12f), new Vector3(0.42f, 0.08f, 0.08f), outfit.Trim);
                    });
                    Transform maceHolster = Joint("MaceHolster", root, new Vector3(0.26f, 0.9f, 0.06f));
                    Transform mace = Grip("MaceGrip", maceHolster, wristR, new Vector3(0.03f, -0.08f, 0.02f), Quaternion.identity);
                    UsingSurface(SurfaceKind.Wood, () =>
                    {
                        Part("Mace", mace, PrimitiveType.Cylinder, new Vector3(0f, -0.22f, 0f), new Vector3(0.05f, 0.32f, 0.05f), wood);
                    });
                    Part("MaceHead", mace, PrimitiveType.Sphere, new Vector3(0f, -0.52f, 0f), Vector3.one * 0.16f, outfit.Metal, smoothness: 0.55f);
                    break;
                default:
                    Part("Seal", root, PrimitiveType.Cylinder, new Vector3(0f, 1.28f, 0.16f), new Vector3(0.16f, 0.03f, 0.16f), outfit.Trim, Quaternion.Euler(90f, 0f, 0f), unlit: true);
                    Transform floatL = Joint("OrbHolsterL", root, new Vector3(-0.42f, 1.55f, 0.12f));
                    Transform floatR = Joint("OrbHolsterR", root, new Vector3(0.42f, 1.48f, 0.1f));
                    Transform orbL = Grip("OrbLGrip", floatL, wristL, new Vector3(0f, -0.16f, 0.06f), Quaternion.identity);
                    Transform orbR = Grip("OrbRGrip", floatR, wristR, new Vector3(0f, -0.16f, 0.06f), Quaternion.identity);
                    Part("OrbL", orbL, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.1f, outfit.Trim, unlit: true);
                    Part("OrbR", orbR, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.08f, outfit.Metal, unlit: true);
                    break;
            }
        }

        static Transform Grip(string name, Transform sheath, Transform hand, Vector3 handPos, Quaternion handRot)
        {
            Transform grip = Joint(name, sheath, Vector3.zero);
            var hold = grip.gameObject.AddComponent<WeaponGrip>();
            hold.Sheath = sheath;
            hold.Hand = hand;
            hold.SheathPos = Vector3.zero;
            hold.SheathRot = Quaternion.identity;
            hold.HandPos = handPos;
            hold.HandRot = handRot;
            return grip;
        }

        static void BuildMonster(Transform root, ushort templateId)
        {
            root.localScale = Vector3.one;
            if (templateId == 502)
            {
                Color glow = new Color(0.95f, 0.92f, 0.45f);
                Color body = new Color(0.35f, 0.42f, 0.22f);
                Part("Body", root, PrimitiveType.Sphere, new Vector3(0f, 0.55f, 0f), new Vector3(0.38f, 0.32f, 0.5f), body, smoothness: 0.5f);
                Part("Glow", root, PrimitiveType.Sphere, new Vector3(0f, 0.48f, -0.05f), new Vector3(0.28f, 0.28f, 0.32f), glow, unlit: true);
                Part("Head", root, PrimitiveType.Sphere, new Vector3(0f, 0.72f, 0.12f), Vector3.one * 0.28f, body);
                Eye(root, "EyeL", new Vector3(-0.08f, 0.76f, 0.24f), 0.12f, 0.14f, new Color(0.2f, 0.55f, 0.2f), Color.white, Color.black);
                Eye(root, "EyeR", new Vector3(0.08f, 0.76f, 0.24f), 0.12f, 0.14f, new Color(0.2f, 0.55f, 0.2f), Color.white, Color.black);
                Transform wingL = Joint("WingL", root, new Vector3(-0.28f, 0.7f, -0.02f));
                Part("Wing", wingL, PrimitiveType.Cube, Vector3.zero, new Vector3(0.32f, 0.04f, 0.18f), new Color(0.85f, 0.9f, 0.7f), Quaternion.Euler(8f, 18f, 22f), unlit: true);
                Transform wingR = Joint("WingR", root, new Vector3(0.28f, 0.7f, -0.02f));
                Part("Wing", wingR, PrimitiveType.Cube, Vector3.zero, new Vector3(0.32f, 0.04f, 0.18f), new Color(0.85f, 0.9f, 0.7f), Quaternion.Euler(8f, -18f, -22f), unlit: true);
                return;
            }

            Color amber = new Color(0.92f, 0.62f, 0.22f);
            Color dark = new Color(0.55f, 0.28f, 0.08f);
            Part("Blob", root, PrimitiveType.Sphere, new Vector3(0f, 0.55f, 0f), new Vector3(0.95f, 0.85f, 0.95f), amber, smoothness: 0.72f);
            Part("BlobTop", root, PrimitiveType.Sphere, new Vector3(0f, 1.05f, 0.05f), new Vector3(0.7f, 0.55f, 0.7f), Color.Lerp(amber, Color.white, 0.12f), smoothness: 0.72f);
            Part("Core", root, PrimitiveType.Sphere, new Vector3(0f, 0.58f, 0.08f), Vector3.one * 0.32f, dark, smoothness: 0.5f);
            Eye(root, "EyeL", new Vector3(-0.22f, 0.78f, 0.38f), 0.28f, 0.34f, new Color(0.25f, 0.12f, 0.05f), new Color(1f, 0.95f, 0.85f), dark);
            Eye(root, "EyeR", new Vector3(0.22f, 0.78f, 0.38f), 0.28f, 0.34f, new Color(0.25f, 0.12f, 0.05f), new Color(1f, 0.95f, 0.85f), dark);
            Part("Mouth", root, PrimitiveType.Cube, new Vector3(0f, 0.48f, 0.46f), new Vector3(0.22f, 0.06f, 0.08f), dark);
        }

        static void BindWalk(Transform root, EntityKindVisual kind)
        {
            var walk = root.GetComponent<WalkCycle>() ?? root.gameObject.AddComponent<WalkCycle>();
            walk.Bind(kind);
            var weapons = root.GetComponent<WeaponHold>() ?? root.gameObject.AddComponent<WeaponHold>();
            weapons.Bind();
        }

        static Transform Joint(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        static Transform Part(
            string name,
            Transform parent,
            PrimitiveType type,
            Vector3 pos,
            Vector3 scale,
            Color color,
            Quaternion rotation = default,
            float smoothness = 0.28f,
            bool unlit = false)
        {
            if (rotation.Equals(default))
            {
                rotation = Quaternion.identity;
            }

            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            DropCollider(go);
            Paint(go, color, smoothness, unlit);
            return go.transform;
        }

        static void Paint(GameObject go, Color color, float smoothness, bool unlit)
        {
            SurfaceArt.Apply(go.GetComponent<MeshRenderer>(), color, _surface, smoothness, unlit);
        }

        static void UsingSurface(SurfaceKind kind, System.Action build)
        {
            SurfaceKind previous = _surface;
            _surface = kind;
            try
            {
                build();
            }
            finally
            {
                _surface = previous;
            }
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

        readonly struct Outfit
        {
            public Outfit(Color tunic, Color pants, Color boot, Color trim, Color metal)
            {
                Tunic = tunic;
                Pants = pants;
                Boot = boot;
                Trim = trim;
                Metal = metal;
            }

            public Color Tunic { get; }
            public Color Pants { get; }
            public Color Boot { get; }
            public Color Trim { get; }
            public Color Metal { get; }

            public static Outfit For(ushort classId) => classId switch
            {
                1 => new Outfit(Hex("3a5a8a"), Hex("2a3a52"), Hex("1e2430"), Hex("c8b06a"), Hex("c5cdd8")),
                2 => new Outfit(Hex("5a2438"), Hex("2a1822"), Hex("1a1216"), Hex("d45a6a"), Hex("d8dce8")),
                3 => new Outfit(Hex("3a5a38"), Hex("2a3a28"), Hex("3a2818"), Hex("6a8a48"), Hex("b8a078")),
                4 => new Outfit(Hex("3a2a6a"), Hex("241848"), Hex("1a1228"), Hex("d4b45a"), Hex("c8b06a")),
                5 => new Outfit(Hex("efe6d4"), Hex("d4c8b0"), Hex("6a4a28"), Hex("c4a45a"), Hex("e8d48a")),
                _ => new Outfit(Hex("2a1a3a"), Hex("1a1028"), Hex("120c18"), Hex("8a5ad4"), Hex("c8a0f0")),
            };

            static Color Hex(string hex)
            {
                ColorUtility.TryParseHtmlString("#" + hex, out Color color);
                return color;
            }
        }
    }

    public enum EntityKindVisual : byte
    {
        Player = 1,
        Monster = 2,
        Other = 3,
    }
}
