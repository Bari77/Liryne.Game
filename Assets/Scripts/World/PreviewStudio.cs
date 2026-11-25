using System.Collections.Generic;
using Liryne.Protocol.Messages.Characters;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Liryne.Game.World
{
    /// <summary>Aperçu 3D de face, idle, rotation à la souris.</summary>
    public sealed class PreviewStudio
    {
        const int PreviewLayer = 31;
        const float DragYaw = 0.42f;
        const float FrontYaw = 0f;

        readonly Camera _camera;
        readonly GameObject _root;
        readonly RenderTexture _texture;
        readonly List<RawImage> _views = new List<RawImage>();
        GameObject _avatar;
        CharacterAppearance _shown;
        ushort _shownClass;
        bool _hasShown;
        float _yaw = FrontYaw;

        public PreviewStudio(Transform parent)
        {
            _root = new GameObject("PreviewStudio");
            _root.transform.SetParent(parent, false);
            _root.transform.position = new Vector3(0f, 2000f, 0f);

            _texture = new RenderTexture(512, 768, 16)
            {
                name = "LirynePreview",
                antiAliasing = 2,
            };

            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(_root.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.15f, 3.9f);
            WorldLook.Towards(camGo.transform, _root.transform.position + Vector3.up * 1.05f);
            _camera = camGo.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.09f, 0.08f, 0.16f, 1f);
            _camera.fieldOfView = 32f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 20f;
            _camera.cullingMask = 1 << PreviewLayer;
            _camera.targetTexture = _texture;
            _camera.depth = -10;

            var key = new GameObject("KeyLight");
            key.transform.SetParent(_root.transform, false);
            key.transform.localPosition = new Vector3(-1.2f, 2.4f, 2.2f);
            var keyLight = key.AddComponent<Light>();
            keyLight.type = LightType.Point;
            keyLight.range = 14f;
            keyLight.intensity = 40f;
            keyLight.color = new Color(1f, 0.95f, 0.9f);
            keyLight.cullingMask = 1 << PreviewLayer;

            var fill = new GameObject("FillLight");
            fill.transform.SetParent(_root.transform, false);
            fill.transform.localPosition = new Vector3(1.5f, 1.2f, 1.4f);
            var fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.range = 12f;
            fillLight.intensity = 8f;
            fillLight.color = new Color(0.55f, 0.5f, 0.85f);
            fillLight.cullingMask = 1 << PreviewLayer;

            SetLayer(_root, PreviewLayer);
        }

        public void Bind(RawImage image)
        {
            if (image is null || _views.Contains(image))
            {
                return;
            }

            image.texture = _texture;
            image.color = Color.white;
            image.raycastTarget = true;
            var orbit = image.GetComponent<PreviewOrbit>() ?? image.gameObject.AddComponent<PreviewOrbit>();
            orbit.Dragged = delta =>
            {
                _yaw += delta.x * DragYaw;
                ApplyYaw();
            };

            var aspect = image.GetComponent<AspectRatioFitter>() ?? image.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 512f / 768f;
            _views.Add(image);
        }

        public void Show(CharacterAppearance appearance, ushort classId, RawImage view)
        {
            Bind(view);
            if (_avatar is not null && _hasShown && _shown.Equals(appearance) && _shownClass == classId)
            {
                _avatar.SetActive(true);
                Present(view);
                return;
            }

            if (_avatar is null)
            {
                _yaw = FrontYaw;
                _avatar = AvatarFactory.Create("PreviewAvatar", appearance, EntityKindVisual.Player, classId);
                _avatar.transform.SetParent(_root.transform, false);
                _avatar.transform.localPosition = Vector3.zero;
            }
            else
            {
                _avatar.SetActive(true);
                AvatarFactory.Apply(_avatar, appearance, EntityKindVisual.Player, classId);
            }

            ApplyYaw();
            SetLayer(_avatar, PreviewLayer);
            _shown = appearance;
            _shownClass = classId;
            _hasShown = true;
            Present(view);
        }

        public void Present(RawImage view)
        {
            for (int i = 0; i < _views.Count; i++)
            {
                if (_views[i] is not null)
                {
                    _views[i].enabled = _views[i] == view;
                }
            }
        }

        public void Hide()
        {
            Present(null);
            if (_avatar is not null)
            {
                _avatar.SetActive(false);
            }
        }

        public void Tick(float dt) => ApplyYaw();

        void ApplyYaw()
        {
            if (_avatar is not null)
            {
                _avatar.transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            }
        }

        public void Dispose()
        {
            if (_texture is not null)
            {
                _texture.Release();
                Object.Destroy(_texture);
            }

            if (_root is not null)
            {
                Object.Destroy(_root);
            }
        }

        static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }
    }

    sealed class PreviewOrbit : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public System.Action<Vector2> Dragged;

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnDrag(PointerEventData eventData) =>
            Dragged?.Invoke(eventData.delta);
    }
}
