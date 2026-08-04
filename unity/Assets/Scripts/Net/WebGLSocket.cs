// WebGLSocket — the browser transport behind the same ISocket surface as DesktopSocket.
//
// COOP.md §7: System.Net.WebSockets does not exist in a browser build; the raw socket lives
// in JavaScript (Plugins/WebGL/WebSocket.jslib) and this class only marshals strings across.
// There are no threads on WebGL — the receive queue fills in JS event handlers and drains on
// the main thread in Poll, so the shape stays identical to the desktop transport.

#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Mesruiyet.Net
{
    public sealed class WebGLSocket : ISocket
    {
        [DllImport("__Internal")] static extern int MesNetConnect(string url);
        [DllImport("__Internal")] static extern int MesNetState(int id);
        [DllImport("__Internal")] static extern void MesNetSend(int id, string line);
        [DllImport("__Internal")] static extern IntPtr MesNetPoll(int id);
        [DllImport("__Internal")] static extern void MesNetFree(IntPtr ptr);
        [DllImport("__Internal")] static extern IntPtr MesNetError(int id);
        [DllImport("__Internal")] static extern void MesNetClose(int id);

        int _id = -1;

        public bool Open => _id >= 0 && MesNetState(_id) == 1;
        public string LastError { get; private set; } = "";

        public async Task<bool> Connect(string url)
        {
            Close();
            _id = MesNetConnect(url);

            // No threads to block: yield frames until the handshake settles. The 15 s cap is
            // the same order as the desktop ClientWebSocket's own timeout.
            float deadline = UnityEngine.Time.realtimeSinceStartup + 15f;
            while (MesNetState(_id) == 0 && UnityEngine.Time.realtimeSinceStartup < deadline)
                await Task.Yield();

            if (MesNetState(_id) == 1) return true;
            var p = MesNetError(_id);
            LastError = Marshal.PtrToStringUTF8(p) ?? "bağlanamadı";
            MesNetFree(p);
            return false;
        }

        public Task Send(string line)
        {
            if (Open) MesNetSend(_id, line);
            return Task.CompletedTask;
        }

        public void Poll(Action<string> handle)
        {
            if (_id < 0) return;
            IntPtr p;
            while ((p = MesNetPoll(_id)) != IntPtr.Zero)
            {
                string line = Marshal.PtrToStringUTF8(p);
                MesNetFree(p);
                if (line != null) handle(line);
            }
        }

        public void Close()
        {
            if (_id >= 0) MesNetClose(_id);
            _id = -1;
        }
    }
}
#endif
