// The one transport surface both platforms implement: DesktopSocket over ClientWebSocket,
// WebGLSocket over the browser's WebSocket via WebSocket.jslib. NetManager speaks only this,
// so nothing above the socket knows which platform it is on. COOP.md §7.

using System;
using System.Threading.Tasks;

namespace Mesruiyet.Net
{
    public interface ISocket
    {
        bool Open { get; }
        string LastError { get; }
        Task<bool> Connect(string url);
        Task Send(string line);
        /// <summary>Main thread only: hand every queued message to the game.</summary>
        void Poll(Action<string> handle);
        void Close();
    }
}
