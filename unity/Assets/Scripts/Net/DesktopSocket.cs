// DesktopSocket — the desktop transport: System.Net.WebSockets over line-JSON.
//
// COOP.md §7: ClientWebSocket on desktop; WebGL cannot use it (no raw sockets in a browser)
// and gets its own .jslib wrapper behind the same surface in a later slice. Everything
// network-threaded stays in here: the receive loop fills a concurrent queue, and the game
// only ever touches messages from the main thread via Poll. Nothing in this file knows what
// the messages mean.

#if !UNITY_WEBGL
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mesruiyet.Net
{
    public sealed class DesktopSocket
    {
        ClientWebSocket _ws;
        CancellationTokenSource _cts;
        readonly ConcurrentQueue<string> _inbox = new ConcurrentQueue<string>();
        readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        public bool Open => _ws != null && _ws.State == WebSocketState.Open;
        public string LastError { get; private set; } = "";

        public async Task<bool> Connect(string url)
        {
            Close();
            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();
            try
            {
                await _ws.ConnectAsync(new Uri(url), _cts.Token);
            }
            catch (Exception e)
            {
                LastError = e.Message;
                return false;
            }
            _ = ReceiveLoop();
            return true;
        }

        async Task ReceiveLoop()
        {
            var buffer = new byte[16 * 1024];
            var message = new System.IO.MemoryStream();
            try
            {
                while (_ws != null && _ws.State == WebSocketState.Open)
                {
                    var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    message.Write(buffer, 0, result.Count);
                    if (result.EndOfMessage)
                    {
                        _inbox.Enqueue(Encoding.UTF8.GetString(message.ToArray()));
                        message.SetLength(0);
                    }
                }
            }
            catch (Exception e)
            {
                LastError = e.Message;
            }
        }

        public async Task Send(string line)
        {
            if (!Open) return;
            var bytes = Encoding.UTF8.GetBytes(line);
            await _sendLock.WaitAsync();
            try
            {
                await _ws.SendAsync(new ArraySegment<byte>(bytes),
                                    WebSocketMessageType.Text, true, _cts.Token);
            }
            catch (Exception e)
            {
                LastError = e.Message;
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <summary>Main thread only: hand every queued message to the game.</summary>
        public void Poll(Action<string> handle)
        {
            while (_inbox.TryDequeue(out var line)) handle(line);
        }

        public void Close()
        {
            try { _cts?.Cancel(); } catch { }
            try { _ws?.Dispose(); } catch { }
            _ws = null;
        }
    }
}
#endif
