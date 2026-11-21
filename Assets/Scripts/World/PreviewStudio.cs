using Liryne.Protocol.Messages.Characters;
using UnityEngine;
using UnityEngine.UI;

namespace Liryne.Game.World
{
    /// <summary>Aperçu 3D du personnage, caméra dédiée vers une RawImage.</summary>
    public sealed class PreviewStudio
    {
        const int PreviewLayer = 31;

        readonly Camera _camera;
        readonly GameObject _root;
        readonly RenderTexture _texture;
        GameObject _avatar;

        public RawImage Image { get; }

        public PreviewStudio(Transform parent, RawImage image)
        {
            Image = image;
            _root = new GameObject("PreviewStudio");
            _root.transform.SetParent(parent, false);
            _root.transform.position = new Vector3(0f, 2000f, 0f);

            _texture = new RenderTexture(512, 768, 16)
            {
                name = "LirynePreview",
                antiAliasing = 2,
            };
            image.texture = _texture;
            var aspect = image.GetComponent<AspectRatioFitter>() ?? image.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 512f / 768f;

            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(_root.transform, false);
            camGo.transform.localPosition = new Vector3(0.55f, 1.35f, -5.2f);
            WorldLook.Towards(camGo.transform, _root.transform.position + Vector3.up * 1.05f);
            _camera = camGo.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.09f, 0.08f, 0.16f, 1f);
            _camera.fieldOfView = 28f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 20f;
            _camera.cullingMask = 1 << PreviewLayer;
            _camera.targetTexture = _texture;
            _camera.depth = -10;
            SetLayer(_root, PreviewLayer);
        }

        public void Show(CharacterAppearance appearance)
        {
            Image.enabled = true;
            if (_avatar is not null)
            {
                Object.Destroy(_avatar);
            }

            _avatar = AvatarFactory.Create("PreviewAvatar", appearance, EntityKindVisual.Player);
            _avatar.transform.SetParent(_root.transform, false);
            _avatar.transform.localPosition = Vector3.zero;
            _avatar.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
            SetLayer(_avatar, PreviewLayer);
        }

        public void Hide()
        {
            Image.enabled = false;
            if (_avatar is not null)
            {
                Object.Destroy(_avatar);
                _avatar = null;
            }
        }

        public void Tick(float dt)
        {
            if (_avatar is not null)
            {
                _avatar.transform.Rotate(0f, 18f * dt, 0f);
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
}
