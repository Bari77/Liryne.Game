using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
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

        public static async Task<bool> IsWorldReachable(ServerInfo server, int timeoutMs = 2000)
        {
            if (server is null || string.IsNullOrWhiteSpace(server.worldHost) || server.worldPort <= 0)
            {
                return false;
            }

            try
            {
                using var tcp = new TcpClient();
                Task connect = tcp.ConnectAsync(server.worldHost, server.worldPort);
                Task finished = await Task.WhenAny(connect, Task.Delay(timeoutMs)).ConfigureAwait(true);
                if (finished != connect)
                {
                    return false;
                }

                await connect.ConfigureAwait(true);
                return tcp.Connected;
            }
            catch
            {
                return false;
            }
        }
    }
}
