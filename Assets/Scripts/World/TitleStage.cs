using System;
using System.Collections.Generic;
using Liryne.Game.Ui;
using Liryne.Protocol.Messages;
using Liryne.Protocol.Messages.Characters;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Liryne.Game.World
{
    /// <summary>Paysage titre 3D : collines ombrées, caméra toujours allumée, personnages en idle.</summary>
    public sealed class TitleStage
    {
        const int TitleLayer = 30;
        const int PreviewBit = 1 << 31;
        const int TitleBit = 1 << TitleLayer;
        static readonly Vector2[] Stands =
        {
            new Vector2(-3.4f, 1.15f),
            new Vector2(-1.2f, 2.05f),
            new Vector2(0.9f, 0.95f),
            new Vector2(2.8f, 1.85f),
            new Vector2(-4.6f, 2.55f),
            new Vector2(4.2f, 2.35f),
            new Vector2(-2.4f, 3.4f),
            new Vector2(1.6f, 3.2f),
            new Vector2(4.8f, 3.6f),
            new Vector2(-5.2f, 3.9f),
        };

        readonly Transform _root;
        readonly Transform _pawnsRoot;
        readonly List<TitlePawn> _pawns = new List<TitlePawn>();
        readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        readonly RaycastHit[] _groundHits = new RaycastHit[16];
        TitlePawn _draft;
        Camera _camera;
        readonly Vector3 _cameraHome = new Vector3(0.15f, 2.15f, -5.4f);
        readonly Vector3 _lookHome = new Vector3(0.2f, 1.05f, 1.8f);
        Vector3 _cameraLocal = new Vector3(0.15f, 2.15f, -5.4f);
        Vector3 _lookLocal = new Vector3(0.2f, 1.05f, 1.8f);
        Vector3 _cameraGoal = new Vector3(0.15f, 2.15f, -5.4f);
        Vector3 _lookGoal = new Vector3(0.2f, 1.05f, 1.8f);
        float _sway;
        Guid _selected;
        bool _picking;

        public event Action<Guid> Picked;

        public TitleStage(Transform parent)
        {
            var go = new GameObject("TitleStage");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 2400f, 0f);
            _root = go.transform;

            BuildSky();
            BuildHills();
            BuildLights();

            var pawns = new GameObject("Pawns");
            pawns.transform.SetParent(_root, false);
            _pawnsRoot = pawns.transform;
            SetLayer(go);

            Show();
        }

        public void Show()
        {
            _root.gameObject.SetActive(true);
            _picking = false;
            ApplyCamera();
        }

        public void Hide()
        {
            _picking = false;
            ClearPawns();
            _selected = Guid.Empty;
            _cameraLocal = _cameraHome;
            _lookLocal = _lookHome;
            _cameraGoal = _cameraHome;
            _lookGoal = _lookHome;
            _root.gameObject.SetActive(false);
        }

        public void Bind(IReadOnlyList<CharacterSummary> characters)
        {
            _picking = true;
            HideDraft();
            characters ??= Array.Empty<CharacterSummary>();

            var keep = new HashSet<Guid>();
            for (int i = 0; i < characters.Count; i++)
            {
                keep.Add(characters[i].Id);
            }

            for (int i = _pawns.Count - 1; i >= 0; i--)
            {
                if (!keep.Contains(_pawns[i].Id))
                {
                    if (_pawns[i].Root != null)
                    {
                        UnityEngine.Object.Destroy(_pawns[i].Root);
                    }

                    _pawns.RemoveAt(i);
                }
            }

            var next = new List<TitlePawn>(characters.Count);
            for (int i = 0; i < characters.Count; i++)
            {
                CharacterSummary summary = characters[i];
                TitlePawn existing = FindPawn(summary.Id);
                if (existing is null)
                {
                    float yaw = 180f + ((i % 5) - 2) * 7f;
                    next.Add(SpawnPawn(summary, StandAt(i), yaw));
                    continue;
                }

                existing.Apply(summary.Appearance, summary.ClassId, summary.Name, summary.Level);
                next.Add(existing);
            }

            _pawns.Clear();
            _pawns.AddRange(next);
            Select(_selected, frame: false);
        }

        public void Select(Guid id) => Select(id, frame: true);

        void Select(Guid id, bool frame)
        {
            bool same = _selected == id;
            _selected = id;
            TitlePawn focused = null;
            for (int i = 0; i < _pawns.Count; i++)
            {
                bool on = _pawns[i].Id == id;
                _pawns[i].SetSelected(on);
                if (on)
                {
                    focused = _pawns[i];
                }
            }

            if (frame && !same)
            {
                Frame(focused);
            }
        }

        public void ShowDraft(CharacterAppearance appearance, ushort classId)
        {
            Vector3 stand = StandAt(Mathf.Max(0, _pawns.Count));
            if (_draft is null)
            {
                var summary = new CharacterSummary(
                    Guid.Empty,
                    "Nouveau",
                    1,
                    classId,
                    Affinity.None,
                    appearance,
                    0,
                    0);
                _draft = SpawnPawn(summary, stand, 180f);
            }
            else
            {
                _draft.Apply(appearance, classId, "Nouveau", 1);
                _draft.Root.transform.localPosition = stand;
            }

            _draft.SetSelected(true);
            for (int i = 0; i < _pawns.Count; i++)
            {
                _pawns[i].SetSelected(false);
            }

            Frame(_draft);
        }

        public void HideDraft()
        {
            if (_draft is null)
            {
                return;
            }

            UnityEngine.Object.Destroy(_draft.Root);
            _draft = null;
        }

        public void Refresh(Guid id, CharacterAppearance appearance, ushort classId)
        {
            for (int i = 0; i < _pawns.Count; i++)
            {
                if (_pawns[i].Id == id)
                {
                    _pawns[i].Apply(appearance, classId, _pawns[i].DisplayName, _pawns[i].Level);
                    return;
                }
            }
        }

        public void Tick(float dt)
        {
            if (!_root.gameObject.activeSelf)
            {
                return;
            }

            float follow = 1f - Mathf.Exp(-5.5f * dt);
            _cameraLocal = Vector3.Lerp(_cameraLocal, _cameraGoal, follow);
            _lookLocal = Vector3.Lerp(_lookLocal, _lookGoal, follow);
            ApplyCamera();
            _sway += dt * 0.18f;
            if (_camera is not null)
            {
                Vector3 sway = new Vector3(Mathf.Sin(_sway) * 0.06f, Mathf.Cos(_sway * 0.73f) * 0.03f, 0f);
                _camera.transform.position = CameraWorld() + sway;
                WorldLook.Towards(_camera.transform, LookWorld());
            }

            if (_picking)
            {
                TryPick();
            }
        }

        public void Dispose()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
            }
        }

        void ClearPawns()
        {
            HideDraft();
            for (int i = 0; i < _pawns.Count; i++)
            {
                if (_pawns[i].Root != null)
                {
                    UnityEngine.Object.Destroy(_pawns[i].Root);
                }
            }

            _pawns.Clear();
        }

        TitlePawn SpawnPawn(CharacterSummary summary, Vector3 stand, float yaw)
        {
            var root = new GameObject(string.IsNullOrEmpty(summary.Name) ? "Pawn" : summary.Name);
            root.transform.SetParent(_pawnsRoot, false);
            root.transform.localPosition = stand;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            SetLayer(root);

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.92f, 0f);
            capsule.height = 1.9f;
            capsule.radius = 0.42f;

            var pawn = new TitlePawn(root, summary);
            pawn.Rebuild();
            return pawn;
        }

        Vector3 StandAt(int index)
        {
            Vector2 xz = Stands[index % Stands.Length];
            if (index >= Stands.Length)
            {
                xz += new Vector2((index / Stands.Length) * 1.4f, -1.1f);
            }

            Vector3 origin = _root.TransformPoint(new Vector3(xz.x, 18f, xz.y));
            int hits = Physics.RaycastNonAlloc(origin, Vector3.down, _groundHits, 40f, TitleBit);
            float best = float.MaxValue;
            Vector3 point = default;
            bool found = false;
            for (int i = 0; i < hits; i++)
            {
                RaycastHit hit = _groundHits[i];
                if (hit.collider is null || hit.collider.GetComponentInParent<TitleMarker>() is not null)
                {
                    continue;
                }

                if (hit.distance >= best)
                {
                    continue;
                }

                best = hit.distance;
                point = hit.point;
                found = true;
            }

            if (found)
            {
                return _root.InverseTransformPoint(point) + Vector3.up * 0.02f;
            }

            return new Vector3(xz.x, 0.05f, xz.y);
        }

        TitlePawn FindPawn(Guid id)
        {
            for (int i = 0; i < _pawns.Count; i++)
            {
                if (_pawns[i].Id == id)
                {
                    return _pawns[i];
                }
            }

            return null;
        }

        void TryPick()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse is null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            Vector2 screen = mouse.position.ReadValue();
#else
            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            Vector2 screen = Input.mousePosition;
#endif
            if (PointerOnUi(screen) || _camera is null)
            {
                return;
            }

            Ray ray = _camera.ScreenPointToRay(screen);
            if (!Physics.Raycast(ray, out RaycastHit hit, 80f, TitleBit))
            {
                return;
            }

            TitleMarker marker = hit.collider.GetComponentInParent<TitleMarker>();
            if (marker is null || marker.Id == Guid.Empty)
            {
                return;
            }

            Picked?.Invoke(marker.Id);
        }

        bool PointerOnUi(Vector2 screen)
        {
            EventSystem es = EventSystem.current;
            if (es is null)
            {
                return false;
            }

            var data = new PointerEventData(es) { position = screen };
            _uiHits.Clear();
            es.RaycastAll(data, _uiHits);
            return _uiHits.Count > 0;
        }

        void ApplyCamera()
        {
            _camera = Camera.main;
            if (_camera is null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                _camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            _camera.enabled = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = UiTheme.LogoWash;
            _camera.fieldOfView = 32f;
            _camera.nearClipPlane = 0.15f;
            _camera.farClipPlane = 140f;
            _camera.cullingMask = TitleBit;
            _camera.targetTexture = null;
            _camera.transform.position = CameraWorld();
            WorldLook.Towards(_camera.transform, LookWorld());
        }

        void Frame(TitlePawn pawn)
        {
            if (pawn is null || pawn.Root is null)
            {
                _cameraGoal = _cameraHome;
                _lookGoal = _lookHome;
                return;
            }

            Vector3 stand = pawn.Root.transform.localPosition;
            _lookGoal = stand + new Vector3(0f, 1.12f, 0f);
            _cameraGoal = stand + new Vector3(0f, 1.55f, -3.6f);
        }

        Vector3 CameraWorld() => _root.TransformPoint(_cameraLocal);

        Vector3 LookWorld() => _root.TransformPoint(_lookLocal);

        static void SetLayer(GameObject go)
        {
            foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = TitleLayer;
            }
        }

        static void LiftFromHillShadow(GameObject avatar)
        {
            MeshRenderer[] renderers = avatar.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].receiveShadows = false;
            }
        }

        void BuildSky()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Sky";
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3(0f, 7.4f, 36f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = new Vector3(68f, 38f, 1f);
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            var renderer = go.GetComponent<MeshRenderer>();
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Sprites/Default");
            if (renderer is not null && unlit is not null)
            {
                var material = new Material(unlit);
                Texture2D scene = LoginArt.Scene();
                material.mainTexture = scene;
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", scene);
                }

                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", Color.white);
                }

                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        void BuildHills()
        {
            Hill("Ground", PrimitiveType.Plane, new Vector3(0f, 0f, 8f), new Vector3(8f, 1f, 6f), Quaternion.identity, new Color(0.045f, 0.055f, 0.08f), SurfaceKind.Grass, new Vector2(8f, 6f));
            Hill("Back", PrimitiveType.Cube, new Vector3(0.5f, 1.1f, 22f), new Vector3(52f, 9.5f, 10f), Quaternion.Euler(0f, 8f, 0f), new Color(0.12f, 0.08f, 0.22f), SurfaceKind.Rock, new Vector2(4f, 1.4f));
            Hill("MidL", PrimitiveType.Cube, new Vector3(-11f, 0.4f, 14.5f), new Vector3(18f, 6.2f, 11f), Quaternion.Euler(0f, -16f, 0f), new Color(0.07f, 0.05f, 0.14f), SurfaceKind.Rock, new Vector2(2.4f, 1.2f));
            Hill("MidR", PrimitiveType.Cube, new Vector3(12.5f, 0.55f, 15.2f), new Vector3(20f, 6.8f, 10f), Quaternion.Euler(0f, 18f, 0f), new Color(0.075f, 0.055f, 0.15f), SurfaceKind.Rock, new Vector2(2.6f, 1.2f));
            Hill("MidC", PrimitiveType.Cube, new Vector3(0.8f, 0.2f, 16.5f), new Vector3(14f, 5.4f, 8f), Quaternion.Euler(0f, -4f, 0f), new Color(0.08f, 0.06f, 0.16f), SurfaceKind.Rock, new Vector2(2f, 1.1f));
            Hill("KnollL", PrimitiveType.Cube, new Vector3(-6.6f, -0.15f, 5.4f), new Vector3(9.2f, 2.4f, 7.4f), Quaternion.Euler(0f, 22f, 0f), new Color(0.05f, 0.07f, 0.1f), SurfaceKind.Grass, new Vector2(3.2f, 2.4f));
            Hill("KnollC", PrimitiveType.Cube, new Vector3(0.4f, -0.2f, 7.1f), new Vector3(8.4f, 2.6f, 6.6f), Quaternion.Euler(0f, -10f, 0f), new Color(0.055f, 0.075f, 0.11f), SurfaceKind.Grass, new Vector2(3f, 2.2f));
            Hill("KnollR", PrimitiveType.Cube, new Vector3(6.9f, -0.12f, 5.1f), new Vector3(10.4f, 2.2f, 7.2f), Quaternion.Euler(0f, -20f, 0f), new Color(0.048f, 0.068f, 0.1f), SurfaceKind.Grass, new Vector2(3.4f, 2.4f));
            Hill("FarL", PrimitiveType.Cube, new Vector3(-16f, 0.8f, 18f), new Vector3(12f, 7.2f, 8f), Quaternion.Euler(0f, 28f, 0f), new Color(0.1f, 0.07f, 0.2f), SurfaceKind.Rock, new Vector2(2f, 1.3f));
            Hill("FarR", PrimitiveType.Cube, new Vector3(17f, 0.9f, 19f), new Vector3(13f, 7.6f, 8f), Quaternion.Euler(0f, -24f, 0f), new Color(0.11f, 0.075f, 0.21f), SurfaceKind.Rock, new Vector2(2.1f, 1.3f));
        }

        void Hill(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rotation, Color color, SurfaceKind surface, Vector2 tiling)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(_root, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer is not null)
            {
                SurfaceArt.Apply(renderer, color, surface, 0.12f, false, tiling);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        void BuildLights()
        {
            var moon = new GameObject("MoonLight");
            moon.transform.SetParent(_root, false);
            moon.transform.rotation = Quaternion.Euler(38f, -48f, 0f);
            var moonLight = moon.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.color = new Color(0.92f, 0.86f, 0.72f);
            moonLight.intensity = 1.15f;
            moonLight.shadows = LightShadows.Soft;
            moonLight.shadowStrength = 0.42f;
            moonLight.shadowBias = 0.04f;
            moonLight.cullingMask = TitleBit;

            var key = new GameObject("KeyLight");
            key.transform.SetParent(_root, false);
            key.transform.rotation = Quaternion.Euler(18f, 176f, 0f);
            var keyLight = key.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(0.95f, 0.9f, 0.82f);
            keyLight.intensity = 1.05f;
            keyLight.shadows = LightShadows.None;
            keyLight.cullingMask = TitleBit;

            var fill = new GameObject("FillLight");
            fill.transform.SetParent(_root, false);
            fill.transform.rotation = Quaternion.Euler(12f, 40f, 0f);
            var fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(0.55f, 0.48f, 0.78f);
            fillLight.intensity = 0.55f;
            fillLight.shadows = LightShadows.None;
            fillLight.cullingMask = TitleBit;

            var bounce = new GameObject("BounceLight");
            bounce.transform.SetParent(_root, false);
            bounce.transform.localPosition = new Vector3(0.3f, 2.6f, 1.6f);
            var bounceLight = bounce.AddComponent<Light>();
            bounceLight.type = LightType.Point;
            bounceLight.color = new Color(0.85f, 0.78f, 0.95f);
            bounceLight.intensity = 6.5f;
            bounceLight.range = 16f;
            bounceLight.shadows = LightShadows.None;
            bounceLight.cullingMask = TitleBit;
        }

        sealed class TitlePawn
        {
            public readonly GameObject Root;
            public Guid Id { get; private set; }
            public string DisplayName { get; private set; }
            public ushort Level { get; private set; }

            GameObject _avatar;
            GameObject _ring;
            ushort _classId;
            CharacterAppearance _appearance;

            public TitlePawn(GameObject root, CharacterSummary summary)
            {
                Root = root;
                Id = summary.Id;
                DisplayName = summary.Name;
                Level = summary.Level;
                _classId = summary.ClassId;
                _appearance = summary.Appearance;
                var marker = root.AddComponent<TitleMarker>();
                marker.Id = summary.Id;
            }

            public void Rebuild()
            {
                if (_avatar is not null)
                {
                    UnityEngine.Object.Destroy(_avatar);
                }

                _avatar = AvatarFactory.Create("Visual", _appearance, EntityKindVisual.Player, _classId);
                _avatar.transform.SetParent(Root.transform, false);
                _avatar.transform.localPosition = Vector3.zero;
                _avatar.transform.localRotation = Quaternion.identity;
                SetLayer(Root);
                LiftFromHillShadow(_avatar);
                EnsureChrome();
            }

            public void Apply(CharacterAppearance appearance, ushort classId, string name, ushort level)
            {
                if (_appearance.Equals(appearance) && _classId == classId && DisplayName == name && Level == level)
                {
                    return;
                }

                _appearance = appearance;
                _classId = classId;
                DisplayName = name;
                Level = level;
                Rebuild();
            }

            public void SetSelected(bool selected)
            {
                if (_ring is not null)
                {
                    _ring.SetActive(selected);
                }
            }

            void EnsureChrome()
            {
                if (_ring is not null)
                {
                    return;
                }

                _ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _ring.name = "Select";
                _ring.transform.SetParent(Root.transform, false);
                _ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);
                _ring.transform.localScale = new Vector3(1.65f, 0.025f, 1.65f);
                UnityEngine.Object.Destroy(_ring.GetComponent<Collider>());
                var ringRenderer = _ring.GetComponent<MeshRenderer>();
                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (ringRenderer is not null && unlit is not null)
                {
                    var material = new Material(unlit);
                    var gold = new Color(0.95f, 0.78f, 0.28f, 0.92f);
                    if (material.HasProperty("_BaseColor"))
                    {
                        material.SetColor("_BaseColor", gold);
                    }

                    if (material.HasProperty("_Color"))
                    {
                        material.color = gold;
                    }

                    ringRenderer.sharedMaterial = material;
                    ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                _ring.SetActive(false);
            }
        }
    }

    sealed class TitleMarker : MonoBehaviour
    {
        [NonSerialized] public Guid Id;
    }
}
