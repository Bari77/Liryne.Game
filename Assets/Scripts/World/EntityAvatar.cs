using System;
using Liryne.Game.Content;
using Liryne.Protocol.Messages.Characters;
using Liryne.Protocol.Messages.Combat;
using Liryne.Protocol.Messages.Entities;
using Liryne.Protocol.Wire;
using UnityEngine;

namespace Liryne.Game.World
{
    public sealed class EntityAvatar : MonoBehaviour
    {
        public Guid EntityId { get; private set; }
        public string DisplayName { get; private set; }
        public ushort ClassId { get; private set; }
        public ushort Level { get; private set; }
        public ushort TitleId { get; private set; }
        public ushort OrnamentId { get; private set; }
        public CharacterAppearance Appearance { get; private set; }
        public EntityState LastState { get; private set; }
        public EntityKind Kind { get; private set; }
        public bool IsLocal { get; set; }
        public bool IsDead => LastState is not null && (LastState.Flags & EntityFlags.Dead) != 0;
        public bool InCombat => LastState is not null && (LastState.Flags & EntityFlags.InCombat) != 0;
        public bool IsCasting => LastState is not null && (LastState.Flags & EntityFlags.Casting) != 0;

        Vector3 _from;
        Vector3 _to;
        float _fromFacing;
        float _toFacing;
        float _t;
        float _duration = 0.05f;
        Transform _visual;
        Transform _chrome;
        TextMesh _nameplate;
        Transform _hpFill;
        Transform _mpFill;
        GameObject _selectRing;
        Material _ringMaterial;
        Camera _billboard;
        bool _selected;

        public static EntityAvatar Spawn(EntitySpawn spawn, Transform parent, Camera billboard, bool local)
        {
            var root = new GameObject(string.IsNullOrEmpty(spawn.Name) ? spawn.State.EntityId.ToString("N").Substring(0, 8) : spawn.Name);
            root.transform.SetParent(parent, false);
            var avatar = root.AddComponent<EntityAvatar>();
            avatar.EntityId = spawn.State.EntityId;
            avatar.DisplayName = spawn.Name;
            avatar.ClassId = spawn.TemplateId;
            avatar.Level = spawn.Level;
            avatar.TitleId = spawn.TitleId;
            avatar.OrnamentId = spawn.OrnamentId;
            avatar.Appearance = spawn.Appearance;
            avatar.LastState = spawn.State;
            avatar.Kind = spawn.Kind;
            avatar.IsLocal = local;
            avatar._billboard = billboard;

            EntityKindVisual kind = spawn.Kind == EntityKind.Monster ? EntityKindVisual.Monster : EntityKindVisual.Player;
            avatar._visual = AvatarFactory.Create("Visual", spawn.Appearance, kind, spawn.TemplateId).transform;
            avatar._visual.SetParent(root.transform, false);

            avatar.BuildChrome(spawn);
            Vector3 pos = ToUnity(spawn.State.Position);
            avatar._from = avatar._to = pos;
            avatar._fromFacing = avatar._toFacing = spawn.State.Facing;
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, spawn.State.Facing, 0f);
            avatar.RefreshBars();
            return avatar;
        }

        public void ApplyPresentation(EntityPresentation presentation)
        {
            Appearance = presentation.Appearance;
            TitleId = presentation.TitleId;
            OrnamentId = presentation.OrnamentId;
            if (_visual is not null)
            {
                AvatarFactory.Apply(
                    _visual.gameObject,
                    presentation.Appearance,
                    Kind == EntityKind.Monster ? EntityKindVisual.Monster : EntityKindVisual.Player,
                    ClassId);
            }
        }

        public void ApplyState(EntityState state, float interval, bool snap)
        {
            LastState = state;
            RefreshBars();
            Vector3 pos = ToUnity(state.Position);
            if (snap || (state.Flags & EntityFlags.Teleported) != 0)
            {
                _from = _to = pos;
                _fromFacing = _toFacing = state.Facing;
                _t = 1f;
                transform.position = pos;
                transform.rotation = Quaternion.Euler(0f, state.Facing, 0f);
                return;
            }

            _from = transform.position;
            _to = pos;
            _fromFacing = transform.eulerAngles.y;
            _toFacing = state.Facing;
            _duration = Mathf.Max(0.03f, interval);
            _t = 0f;
        }

        public void SnapTo(Vector3 position, float facing)
        {
            _from = _to = position;
            _fromFacing = _toFacing = facing;
            _t = 1f;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, facing, 0f);
        }

        public void PlayStrike()
        {
            if (_visual is not null)
            {
                var walk = _visual.GetComponent<WalkCycle>();
                if (walk is not null)
                {
                    walk.PlayStrike();
                }
            }
        }

        public void ShowCombatFloat(CombatEvent ev)
        {
            string caption = ClientCatalog.CombatFloatText(ev);
            if (string.IsNullOrEmpty(caption))
            {
                return;
            }

            var go = new GameObject("CombatFloat");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(UnityEngine.Random.Range(-0.28f, 0.28f), 2.35f, 0f);
            var text = go.AddComponent<TextMesh>();
            text.alignment = TextAlignment.Center;
            text.anchor = TextAnchor.MiddleCenter;
            text.fontSize = 42;
            text.characterSize = 0.045f;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font is not null)
            {
                text.font = font;
            }

            var floater = go.AddComponent<CombatFloat>();
            floater.Bind(caption, ClientCatalog.CombatFloatColor(ev));
        }

        public void SetSelected(bool selected, bool attacking = false)
        {
            _selected = selected;
            if (_selectRing is not null)
            {
                _selectRing.SetActive(selected);
            }

            PaintRing(attacking
                ? new Color(0.92f, 0.16f, 0.14f, 0.95f)
                : new Color(0.95f, 0.78f, 0.28f, 0.9f));
        }

        void Update()
        {
            if (!IsLocal && _t < 1f)
            {
                _t = Mathf.Min(1f, _t + Time.deltaTime / _duration);
                transform.position = Vector3.Lerp(_from, _to, _t);
                float facing = Mathf.LerpAngle(_fromFacing, _toFacing, _t);
                transform.rotation = Quaternion.Euler(0f, facing, 0f);
            }

            if (_chrome is not null && _billboard is not null)
            {
                WorldLook.AwayFrom(_chrome, _billboard.transform.position);
            }
        }

        void BuildChrome(EntitySpawn spawn)
        {
            var chrome = new GameObject("Chrome");
            chrome.transform.SetParent(transform, false);
            chrome.transform.localPosition = new Vector3(0f, 2.12f, 0f);
            _chrome = chrome.transform;

            var plate = new GameObject("Name");
            plate.transform.SetParent(_chrome, false);
            plate.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            var text = plate.AddComponent<TextMesh>();
            text.alignment = TextAlignment.Center;
            text.anchor = TextAnchor.LowerCenter;
            text.fontSize = 32;
            text.characterSize = 0.055f;
            text.color = spawn.Kind == EntityKind.Monster
                ? new Color(0.95f, 0.55f, 0.55f)
                : new Color(0.93f, 0.9f, 0.98f);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font is not null)
            {
                text.font = font;
            }

            text.text = spawn.Name;
            _nameplate = text;

            _hpFill = MakeBar(_chrome, "Hp", new Vector3(0f, 0.08f, 0f), new Color(0.78f, 0.28f, 0.38f));
            _mpFill = MakeBar(_chrome, "Mp", new Vector3(0f, -0.02f, 0f), new Color(0.32f, 0.52f, 0.9f));

            _selectRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _selectRing.name = "Select";
            _selectRing.transform.SetParent(transform, false);
            _selectRing.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            _selectRing.transform.localScale = new Vector3(1.7f, 0.03f, 1.7f);
            UnityEngine.Object.Destroy(_selectRing.GetComponent<Collider>());
            var ringRenderer = _selectRing.GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (ringRenderer is not null && shader is not null)
            {
                _ringMaterial = new Material(shader);
                ringRenderer.sharedMaterial = _ringMaterial;
            }

            PaintRing(new Color(0.95f, 0.78f, 0.28f, 0.9f));
            _selectRing.SetActive(false);
        }

        void PaintRing(Color color)
        {
            if (_ringMaterial is null)
            {
                return;
            }

            if (_ringMaterial.HasProperty("_BaseColor"))
            {
                _ringMaterial.SetColor("_BaseColor", color);
            }

            if (_ringMaterial.HasProperty("_Color"))
            {
                _ringMaterial.color = color;
            }
        }

        static Transform MakeBar(Transform parent, string name, Vector3 offset, Color fill)
        {
            var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = name + "Back";
            back.transform.SetParent(parent, false);
            back.transform.localPosition = offset;
            back.transform.localScale = new Vector3(1.05f, 0.07f, 0.07f);
            UnityEngine.Object.Destroy(back.GetComponent<Collider>());
            PaintUnlit(back, new Color(0.08f, 0.07f, 0.12f, 0.9f));

            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name + "Fill";
            bar.transform.SetParent(back.transform, false);
            bar.transform.localPosition = Vector3.zero;
            bar.transform.localScale = Vector3.one;
            UnityEngine.Object.Destroy(bar.GetComponent<Collider>());
            PaintUnlit(bar, fill);
            return bar.transform;
        }

        static void PaintUnlit(GameObject go, Color color)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (renderer is null || shader is null)
            {
                return;
            }

            var material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.color = color;
            }

            renderer.sharedMaterial = material;
        }

        void RefreshBars()
        {
            if (LastState is null)
            {
                return;
            }

            SetBar(_hpFill, LastState.MaxHp == 0 ? 0f : (float)LastState.Hp / LastState.MaxHp);
            SetBar(_mpFill, LastState.MaxMp == 0 ? 0f : (float)LastState.Mp / LastState.MaxMp);
        }

        static void SetBar(Transform fill, float amount)
        {
            if (fill is null)
            {
                return;
            }

            amount = Mathf.Clamp01(amount);
            fill.localScale = new Vector3(amount, 1f, 1f);
            fill.localPosition = new Vector3((amount - 1f) * 0.5f, 0f, -0.2f);
        }

        public static Vector3 ToUnity(Vec3 position) => new Vector3(position.X, position.Y, position.Z);
    }

    static class WorldLook
    {
        public static void AwayFrom(Transform transform, Vector3 worldPoint)
        {
            Apply(transform, transform.position - worldPoint);
        }

        public static void Towards(Transform transform, Vector3 worldPoint)
        {
            Apply(transform, worldPoint - transform.position);
        }

        static void Apply(Transform transform, Vector3 forward)
        {
            if (transform is null || forward.sqrMagnitude < 1e-8f)
            {
                return;
            }

            forward.Normalize();
            Vector3 up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.999f)
            {
                up = Vector3.right;
            }

            transform.rotation = Quaternion.LookRotation(forward, up);
        }
    }
}
