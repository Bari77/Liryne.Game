using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Liryne.Game.Ui
{
    public sealed class LoginScreen
    {
        readonly GameObject _root;
        readonly Text _error;
        readonly InputField _login;
        readonly InputField _password;

        public bool IsVisible => _root.activeSelf;

        public LoginScreen(Transform canvas, Action onConnect, Action onCreate)
        {
            Image backdrop = UiFactory.Panel(canvas, "Login", Color.black);
            UiFactory.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            _root = backdrop.gameObject;

            TitleChrome.Attach(backdrop.transform, TitleChrome.LogoPlace.Hero, paintedScene: true);

            Image card = UiFactory.Panel(backdrop.transform, "Card", UiTheme.Panel);
            UiFactory.Anchor(card.rectTransform, new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.38f), new Vector2(-240, -210), new Vector2(240, 210));

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

            _login.onSubmit.AddListener(_ => onConnect());
            _password.onSubmit.AddListener(_ => onConnect());

            var nav = _root.AddComponent<LoginNavigation>();
            nav.Bind(_login, _password);
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

        sealed class LoginNavigation : MonoBehaviour
        {
            InputField _login;
            InputField _password;

            public void Bind(InputField login, InputField password)
            {
                _login = login;
                _password = password;
            }

            void Update()
            {
                if (!isActiveAndEnabled)
                {
                    return;
                }

#if ENABLE_INPUT_SYSTEM
                Keyboard keyboard = Keyboard.current;
                if (keyboard is null)
                {
                    return;
                }

                if (keyboard.tabKey.wasPressedThisFrame)
                {
                    if (_login is not null && _login.isFocused)
                    {
                        _password.ActivateInputField();
                        EventSystem.current?.SetSelectedGameObject(_password.gameObject);
                    }
                    else if (_password is not null && _password.isFocused)
                    {
                        _login.ActivateInputField();
                        EventSystem.current?.SetSelectedGameObject(_login.gameObject);
                    }
                }
#endif
            }
        }
    }
}
