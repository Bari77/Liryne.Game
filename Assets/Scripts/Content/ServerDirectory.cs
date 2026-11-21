using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Liryne.Game.Content
{
    [Serializable]
    public sealed class ServerInfo
    {
        public string id = "local";
        public string name = "Clairière de la Lyre";
        public string region = "Local";
        public string authUrl = "http://127.0.0.1:8080";
        public string worldHost = "127.0.0.1";
        public int worldPort = 7777;
    }

    [Serializable]
    sealed class ServerListFile
    {
        public ServerInfo[] servers = Array.Empty<ServerInfo>();
    }

    /// <summary>Liste des royaumes. Un JSON dans StreamingAssets, plus une entrée locale par défaut.</summary>
    public static class ServerDirectory
    {
        public static IReadOnlyList<ServerInfo> Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "servers.json");
            if (File.Exists(path))
            {
                try
                {
                    var file = JsonUtility.FromJson<ServerListFile>(File.ReadAllText(path));
                    if (file?.servers is { Length: > 0 })
                    {
                        return file.servers;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Liste de serveurs illisible : " + ex.Message);
                }
            }

            return new[] { new ServerInfo() };
        }
    }
}
