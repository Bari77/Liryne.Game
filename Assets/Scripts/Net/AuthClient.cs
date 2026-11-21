using System;
using System.Threading.Tasks;
using Liryne.Protocol.Codec;
using Liryne.Protocol.Messages;
using Liryne.Protocol.Messages.Auth;
using UnityEngine;
using UnityEngine.Networking;

namespace Liryne.Game.Net
{
    /// <summary>HTTP Auth : une trame Protocol par requête, comme le serveur.</summary>
    public sealed class AuthClient
    {
        const string ContentType = "application/x-liryne";

        public Task<CreateAccountResult> CreateAccountAsync(string authUrl, string login, string password) =>
            PostAsync<CreateAccountResult>(Join(authUrl, "/auth/accounts"), new CreateAccountRequest(login, password));

        public Task<LoginResult> LoginAsync(string authUrl, string login, string password) =>
            PostAsync<LoginResult>(Join(authUrl, "/auth/sessions"), new LoginRequest(login, password));

        static async Task<T> PostAsync<T>(string url, IMessage body)
            where T : class, IMessage
        {
            byte[] frame = MessageCodec.Encode(body, sequence: 0);
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(frame),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 12,
            };
            request.SetRequestHeader("Content-Type", ContentType);

            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            while (!op.isDone)
            {
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(
                    string.IsNullOrEmpty(request.error)
                        ? $"Auth injoignable ({request.responseCode})."
                        : request.error);
            }

            byte[] data = request.downloadHandler.data;
            if (data is null || data.Length < MessageCodec.LengthPrefixSize + MessageCodec.HeaderSize)
            {
                throw new InvalidOperationException("Réponse Auth vide.");
            }

            DecodedMessage decoded = MessageCodec.Decode(
                data.AsMemory(MessageCodec.LengthPrefixSize),
                MessageDirection.ServerToClient);

            if (decoded.Message is not T typed)
            {
                throw new InvalidOperationException($"Réponse Auth inattendue : {decoded.Message.GetType().Name}.");
            }

            return typed;
        }

        static string Join(string authUrl, string path)
        {
            string root = (authUrl ?? string.Empty).Trim().TrimEnd('/');
            if (root.Length == 0)
            {
                throw new InvalidOperationException("Adresse Auth manquante.");
            }

            return root + path;
        }
    }
}
