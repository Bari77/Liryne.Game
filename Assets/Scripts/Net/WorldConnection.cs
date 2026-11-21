using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Liryne.Protocol;
using Liryne.Protocol.Codec;
using Liryne.Protocol.Messages;
using Liryne.Protocol.Messages.Session;
using UnityEngine;
using SessionPing = Liryne.Protocol.Messages.Session.Ping;

namespace Liryne.Game.Net
{
    /// <summary>TCP World : une trame Protocol, lue hors du fil Unity, pompée dans Update.</summary>
    public sealed class WorldConnection : IDisposable
    {
        readonly ConcurrentQueue<IMessage> _incoming = new ConcurrentQueue<IMessage>();
        readonly object _sendLock = new object();
        TcpClient _tcp;
        NetworkStream _stream;
        CancellationTokenSource _cts;
        CancellationTokenSource _keepAlive;
        uint _sequence = 1;
        volatile bool _closing;
        volatile string _remoteClose;

        public bool IsConnected => _tcp is { Connected: true } && !_closing;

        public bool Handshaking { get; private set; }

        public event Action<string> Closed;

        public async Task<WorldWelcome> ConnectAsync(string host, int port, string sessionToken, CancellationToken cancellationToken)
        {
            Handshaking = true;
            Exception last = new InvalidOperationException($"World injoignable ({host}:{port}).");
            try
            {
                foreach (IPAddress ip in await Candidates(host).ConfigureAwait(true))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Disconnect();
                    _closing = false;
                    _remoteClose = null;
                    try
                    {
                        TcpClient tcp = await OpenSocket(ip, port, cancellationToken).ConfigureAwait(true);
                        tcp.NoDelay = true;
                        _tcp = tcp;
                        _stream = tcp.GetStream();
                        _sequence = 1;
                        _cts = new CancellationTokenSource();
                        CancellationToken readToken = _cts.Token;
                        _ = Task.Run(() => ReadLoop(readToken), CancellationToken.None);
                        Send(new WorldHello(ProtocolInfo.WireVersion, sessionToken));
                        WorldWelcome welcome = await WaitForWelcome(cancellationToken).ConfigureAwait(true);
                        StartKeepAlive();
                        return welcome;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        last = ex;
                        Debug.LogWarning($"[Liryne] World {ip}:{port} : {ex.Message}");
                    }
                }

                throw last;
            }
            catch
            {
                Disconnect();
                throw;
            }
            finally
            {
                Handshaking = false;
            }
        }

        public uint Send(IMessage message)
        {
            NetworkStream stream = _stream;
            if (stream is null || !IsConnected)
            {
                throw new InvalidOperationException("Pas de connexion World.");
            }

            lock (_sendLock)
            {
                uint sequence = _sequence++;
                byte[] frame = MessageCodec.Encode(message, sequence);
                stream.Write(frame, 0, frame.Length);
                stream.Flush();
                return sequence;
            }
        }

        public bool TrySend(IMessage message, out uint sequence)
        {
            sequence = 0;
            try
            {
                sequence = Send(message);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Pump(Action<IMessage> handler)
        {
            if (Handshaking)
            {
                return;
            }

            while (_incoming.TryDequeue(out IMessage message))
            {
                handler(message);
            }
        }

        public bool TryConsumeRemoteClose(out string reason)
        {
            reason = _remoteClose;
            _remoteClose = null;
            return !string.IsNullOrEmpty(reason);
        }

        public void Disconnect()
        {
            _closing = true;
            StopKeepAlive();
            try
            {
                _cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                _stream?.Close();
            }
            catch
            {
            }

            _stream = null;
            try
            {
                _tcp?.Close();
            }
            catch
            {
            }

            _tcp?.Dispose();
            _tcp = null;
            while (_incoming.TryDequeue(out _))
            {
            }
        }

        public void Dispose() => Disconnect();

        async Task<WorldWelcome> WaitForWelcome(CancellationToken cancellationToken)
        {
            var held = new List<IMessage>();
            float deadline = Time.realtimeSinceStartup + 8f;
            try
            {
                while (Time.realtimeSinceStartup < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    while (_incoming.TryDequeue(out IMessage message))
                    {
                        if (message is WorldWelcome welcome)
                        {
                            return welcome;
                        }

                        held.Add(message);
                    }

                    if (_remoteClose is not null)
                    {
                        throw new InvalidOperationException(_remoteClose);
                    }

                    await Task.Yield();
                }

                throw new InvalidOperationException("World ne répond pas. Le serveur tourne-t-il sur le port TCP 7777 ?");
            }
            finally
            {
                for (int i = 0; i < held.Count; i++)
                {
                    _incoming.Enqueue(held[i]);
                }
            }
        }

        static async Task<TcpClient> OpenSocket(IPAddress ip, int port, CancellationToken cancellationToken)
        {
            var tcp = new TcpClient(ip.AddressFamily);
            try
            {
                Task connect = tcp.ConnectAsync(ip, port);
                Task finished = await Task.WhenAny(connect, Task.Delay(4000, cancellationToken)).ConfigureAwait(true);
                if (finished != connect)
                {
                    throw new TimeoutException();
                }

                await connect.ConfigureAwait(true);
                if (!tcp.Connected)
                {
                    throw new InvalidOperationException("Socket World non connectée.");
                }

                tcp.NoDelay = true;
                tcp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                return tcp;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                tcp.Dispose();
                throw new InvalidOperationException($"World injoignable ({ip}:{port}). {ex.Message}");
            }
        }

        static async Task<List<IPAddress>> Candidates(string host)
        {
            var list = new List<IPAddress>();
            void Add(IPAddress ip)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Equals(ip))
                    {
                        return;
                    }
                }

                list.Add(ip);
            }

            if (IPAddress.TryParse(host, out IPAddress parsed))
            {
                Add(parsed);
                if (IPAddress.IsLoopback(parsed))
                {
                    Add(IPAddress.Loopback);
                    Add(IPAddress.IPv6Loopback);
                }

                return list;
            }

            foreach (IPAddress ip in await Dns.GetHostAddressesAsync(host).ConfigureAwait(true))
            {
                Add(ip);
            }

            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                Add(IPAddress.Loopback);
                Add(IPAddress.IPv6Loopback);
            }

            if (list.Count == 0)
            {
                throw new InvalidOperationException("Hôte World introuvable : " + host);
            }

            return list;
        }

        void ReadLoop(CancellationToken cancellationToken)
        {
            var accumulator = new FrameAccumulator();
            var buffer = new byte[8192];
            NetworkStream stream = _stream;
            try
            {
                while (!cancellationToken.IsCancellationRequested && stream is not null && !_closing)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                    {
                        SignalClose("Connexion World fermée par le serveur.");
                        return;
                    }

                    accumulator.Append(buffer.AsSpan(0, read));
                    while (accumulator.TryReadFrame(out byte[] body))
                    {
                        DecodedMessage decoded = MessageCodec.Decode(body, MessageDirection.ServerToClient);
                        _incoming.Enqueue(decoded.Message);
                    }
                }
            }
            catch (ProtocolException ex)
            {
                SignalClose(ex.Message);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                if (!_closing)
                {
                    SignalClose("Connexion World perdue (" + ex.GetType().Name + ").");
                }
            }
            catch (Exception ex)
            {
                if (!_closing)
                {
                    SignalClose(ex.Message);
                }
            }
        }

        void StartKeepAlive()
        {
            StopKeepAlive();
            _keepAlive = new CancellationTokenSource();
            CancellationToken token = _keepAlive.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested && !_closing)
                    {
                        await Task.Delay(2000, token).ConfigureAwait(false);
                        if (_closing || token.IsCancellationRequested)
                        {
                            return;
                        }

                        TrySend(new SessionPing((uint)Environment.TickCount), out _);
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }, CancellationToken.None);
        }

        void StopKeepAlive()
        {
            try
            {
                _keepAlive?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                _keepAlive?.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }

            _keepAlive = null;
        }

        void SignalClose(string reason)
        {
            if (_closing)
            {
                return;
            }

            _remoteClose = reason;
            Debug.LogWarning("[Liryne] " + reason);
            Closed?.Invoke(reason);
        }
    }
}
