using UnityEngine;
using UnityEngine.UI;

namespace Liryne.Game.Ui
{
    public sealed class LoadingScreen
    {
        readonly GameObject _root;
        readonly Text _caption;

        public bool IsVisible => _root.activeSelf;

        public LoadingScreen(Transform canvas)
        {
            Image backdrop = UiFactory.Panel(canvas, "Loader", UiTheme.LogoWash);
            UiFactory.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            _root = backdrop.gameObject;

            Text title = UiFactory.Label(backdrop.transform, "Brand", 28, UiTheme.Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiFactory.Anchor(title.rectTransform, new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f), new Vector2(-280, -24), new Vector2(280, 24));
            title.text = "LIRYNE";

            _caption = UiFactory.Label(backdrop.transform, "Caption", 20, UiTheme.Text, FontStyle.Normal, TextAnchor.MiddleCenter);
            UiFactory.Anchor(_caption.rectTransform, new Vector2(0.5f, 0.46f), new Vector2(0.5f, 0.46f), new Vector2(-360, -20), new Vector2(360, 20));
            _caption.text = "Chargement…";

            _root.SetActive(false);
        }

        public void Show(string caption)
        {
            _caption.text = string.IsNullOrWhiteSpace(caption) ? "Chargement…" : caption;
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }

        public void Hide() => _root.SetActive(false);
    }
}
