using System;
using UnityEngine;
using UnityEngine.UI;

namespace Liryne.Game.Ui
{
    public sealed class LoginScreen
    {
        readonly GameObject _root;
        readonly Text _error;
        readonly InputField _login;
        readonly InputField _password;

        public LoginScreen(Transform canvas, Action onConnect, Action onCreate)
        {
            Image backdrop = UiFactory.Panel(canvas, "Login", UiTheme.Backdrop);
            UiFactory.Stretch(backdrop.rectTransform);
            _root = backdrop.gameObject;

            if (UiTheme.Wordmark is not null)
            {
                Image logo = UiFactory.Panel(backdrop.transform, "Logo", Color.white);
                logo.sprite = UiTheme.Wordmark;
                logo.preserveAspect = true;
                logo.raycastTarget = false;
                UiFactory.Anchor(logo.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), new Vector2(-280, -90), new Vector2(280, 90));
            }

            Image card = UiFactory.Panel(backdrop.transform, "Card", UiTheme.Panel);
            UiFactory.Anchor(card.rectTransform, new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f), new Vector2(-240, -210), new Vector2(240, 210));

            Text title = UiFactory.Label(card.transform, "Title", 28, UiTheme.Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiFactory.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -64), new Vector2(-16, -16));
            title.text = "Connexion";

            _login = UiFactory.Field(card.transform, "Login", "Identifiant", false);
            UiFactory.Anchor(_login.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(28, -128), new Vector2(-28, -76));
            _login.characterLimit = 32;

            _password = UiFactory.Field(card.transform, "Password", "Mot de passe", true);
            UiFactory.Anchor(_password.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(28, -192), new Vector2(-28, -140));
            _password.characterLimit = 64;

            Button connect = UiFactory.Button(card.transform, "Connect", "Entrer", UiTheme.Accent);
            UiFactory.Anchor(connect.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(28, 72), new Vector2(-8, 124));
            connect.onClick.AddListener(() => onConnect());

            Button create = UiFactory.Button(card.transform, "Create", "Créer un compte", UiTheme.PanelInner);
            UiFactory.Anchor(create.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(8, 72), new Vector2(-28, 124));
            create.onClick.AddListener(() => onCreate());

            _error = UiFactory.Label(card.transform, "Error", 18, UiTheme.Danger, FontStyle.Normal, TextAnchor.MiddleCenter);
            UiFactory.Anchor(_error.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 16), new Vector2(-16, 64));

            _password.onSubmit.AddListener(_ => onConnect());
        }

        public string Login => _login.text.Trim();
        public string Password => _password.text;

        public void SetVisible(bool visible)
        {
            _root.SetActive(visible);
            if (visible)
            {
                _login.ActivateInputField();
            }
        }

        public void SetBusy(bool busy)
        {
            _login.interactable = !busy;
            _password.interactable = !busy;
        }

        public void SetError(string message) => _error.text = message ?? string.Empty;
    }
}
