// AgentBridge — the door the AI agent uses to play the game.
//
// Opens a line-delimited JSON command server on 127.0.0.1:8787 and executes each command
// on Unity's main thread. Debug and editor builds only; compiled out of release builds so
// it can never ship.
//
// Protocol: one JSON object per line in, one JSON object per line out.
//   -> {"cmd":"state"}
//   <- {"ok":true,"data":{...}}

#if DEBUG || UNITY_EDITOR
#define AGENT_BRIDGE
#endif

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Mesruiyet.Agent
{
#if AGENT_BRIDGE
    /// <summary>Commands the agent can send. Extend by adding a case in Execute().</summary>
    public sealed class AgentBridge : MonoBehaviour
    {
        public const int Port = 8787;

        static AgentBridge _instance;
        TcpListener _listener;
        Thread _accept;
        volatile bool _running;

        readonly ConcurrentQueue<Job> _inbox = new ConcurrentQueue<Job>();

        sealed class Job
        {
            public string Raw;
            public string Response;
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (_instance != null) return;
            var go = new GameObject("~AgentBridge");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AgentBridge>();
        }

        void OnEnable()
        {
            _running = true;
            try
            {
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                _accept = new Thread(AcceptLoop) { IsBackground = true, Name = "AgentBridge" };
                _accept.Start();
                Debug.Log($"[AgentBridge] listening on 127.0.0.1:{Port}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AgentBridge] could not start: {e.Message}");
            }
        }

        void OnDisable()
        {
            _running = false;
            try { _listener?.Stop(); } catch { /* shutting down */ }
        }

        void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client = null;
                try { client = _listener.AcceptTcpClient(); }
                catch { break; }                       // listener stopped

                try
                {
                    using (client)
                    using (var stream = client.GetStream())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true })
                    {
                        string line;
                        while (_running && (line = reader.ReadLine()) != null)
                        {
                            if (line.Length == 0) continue;
                            var job = new Job { Raw = line };
                            _inbox.Enqueue(job);
                            // Unity APIs are main-thread only, so wait for Update() to run it.
                            if (!job.Done.Wait(TimeSpan.FromSeconds(30)))
                                job.Response = "{\"ok\":false,\"error\":\"timeout\"}";
                            writer.WriteLine(job.Response);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AgentBridge] client error: {e.Message}");
                }
            }
        }

        void Update()
        {
            while (_inbox.TryDequeue(out var job))
            {
                try { job.Response = Execute(job.Raw); }
                catch (Exception e) { job.Response = Err(e.Message); }
                job.Done.Set();
            }
        }

        // ---------------------------------------------------------------- commands

        static string Ok(string dataJson = "null") => "{\"ok\":true,\"data\":" + dataJson + "}";
        static string Err(string msg) => "{\"ok\":false,\"error\":" + JsonUtility.ToJson(new Wrap { v = msg }) + "}";
        [Serializable] struct Wrap { public string v; }

        [Serializable]
        struct Cmd
        {
            public string cmd;
            public string key;
            public string id;
            public string path;
            public int n;
        }

        string Execute(string raw)
        {
            var c = JsonUtility.FromJson<Cmd>(raw);
            switch (c.cmd)
            {
                case "ping":
                    return Ok("\"pong\"");

                case "state":
                    // GameState is the single source of TRUE values. The agent always reads
                    // the truth here; what the player sees goes through Reporting instead.
                    return Ok(AgentState.DumpJson());

                case "press":
                    AgentInput.Press(c.key);
                    return Ok();

                case "click":
                    return AgentInput.Click(c.id) ? Ok() : Err($"no ui element named '{c.id}'");

                case "endturn":
                    StartCoroutine(EndTurns(Mathf.Max(1, c.n)));
                    return Ok();

                case "shot":
                    StartCoroutine(Shot(string.IsNullOrEmpty(c.path) ? "agent/shots/shot.png" : c.path));
                    return Ok();

                case "quit":
                    Application.Quit();
                    return Ok();

                default:
                    return Err($"unknown cmd '{c.cmd}'");
            }
        }

        IEnumerator EndTurns(int n)
        {
            for (int i = 0; i < n; i++)
            {
                AgentInput.Click("btn_end_turn");
                // Let the turn resolve; TurnResolver raises this when the tick finishes.
                yield return new WaitUntil(() => AgentState.TurnIdle);
            }
        }

        IEnumerator Shot(string path)
        {
            var full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(full);
            Debug.Log($"[AgentBridge] shot -> {full}");
        }
    }
#endif
}
