using System;
using System.Collections.Generic;
using Liryne.Game.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Liryne.Game.Ui
{
    public sealed class ServerScreen
    {
        readonly GameObject _root;
        readonly Transform _list;
        readonly Text _error;
        readonly Action<ServerInfo> _onChoose;

        public ServerScreen(Transform canvas, Action onBack, Action<ServerInfo> onChoose)
        {
            _onChoose = onChoose;
            Image backdrop = UiFactory.Panel(canvas, "Servers", UiTheme.Backdrop);
            UiFactory.Stretch(backdrop.rectTransform);
            _root = backdrop.gameObject;

            Text title = UiFactory.Label(backdrop.transform, "Title", 36, UiTheme.Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-320, -110), new Vector2(320, -40));
            title.text = "Choisis un serveur";

            UiFactory.Scroll(backdrop.transform, "List", out _list);
            var scroll = _list.parent.GetComponent<RectTransform>();
            UiFactory.Anchor(scroll, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-360, -220), new Vector2(360, 220));

            Button back = UiFactory.Button(backdrop.transform, "Back", "Retour", UiTheme.PanelInner);
            UiFactory.Anchor(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-120, 40), new Vector2(120, 96));
            back.onClick.AddListener(() => onBack());

            _error = UiFactory.Label(backdrop.transform, "Error", 20, UiTheme.Danger, FontStyle.Normal, TextAnchor.MiddleCenter);
            UiFactory.Anchor(_error.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 110), new Vector2(-40, 160));
        }

        public void Bind(IReadOnlyList<ServerInfo> servers)
        {
            for (int i = _list.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(_list.GetChild(i).gameObject);
            }

            foreach (ServerInfo server in servers)
            {
                ServerInfo captured = server;
                Image row = UiFactory.Panel(_list, server.id, UiTheme.PanelInner);
                var le = row.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 84;
                le.minHeight = 84;

                Text name = UiFactory.Label(row.transform, "Name", 24, UiTheme.Text, FontStyle.Bold, TextAnchor.MiddleLeft);
                UiFactory.Anchor(name.rectTransform, new Vector2(0, 0), new Vector2(0.62f, 1), new Vector2(20, 8), new Vector2(-8, -8));
                name.text = server.name + "\n<size=16><color=#b8b0cc>" + server.region + " · " + server.worldHost + ":" + server.worldPort + "</color></size>";
                name.supportRichText = true;

                Button enter = UiFactory.Button(row.transform, "Enter", "Entrer", UiTheme.Accent);
                UiFactory.Anchor(enter.GetComponent<RectTransform>(), new Vector2(0.7f, 0.22f), new Vector2(0.96f, 0.78f), Vector2.zero, Vector2.zero);
                enter.onClick.AddListener(() => _onChoose(captured));
            }
        }

        public void SetVisible(bool visible) => _root.SetActive(visible);

        public void SetError(string message) => _error.text = message ?? string.Empty;
    }
}
