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
using Mesruiyet.Core;

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
            public int x;
            public int y;
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

                case "focus":
                    AgentInput.Focus(c.x, c.y);
                    return Ok();

                case "build":
                    // Placement is the whole first slice, so the agent has to be able to do it.
                    return AgentInput.Build(c.id, c.x, c.y, out string why) ? Ok() : Err(why);

                case "event":
                    // {"cmd":"event","n":0} — answer the card by option index.
                    return AgentInput.Event(c.n, out string eventWhy)
                        ? Ok(JsonUtility.ToJson(new Wrap { v = eventWhy })) : Err(eventWhy);

                case "loan":
                    // n != 0 signs, n == 0 settles.
                    return AgentInput.Loan(c.id, c.n != 0, out string loanWhy)
                        ? Ok(JsonUtility.ToJson(new Wrap { v = loanWhy })) : Err(loanWhy);

                case "card":
                    // Test affordance: deal a named card instead of waiting for the draw.
                    return AgentInput.Card(c.id, out string cardWhy) ? Ok() : Err(cardWhy);

                case "threat":
                    // Test affordance: drive Mersa to a level so a scenario can reach the cards
                    // that only appear under pressure, without playing twenty turns first.
                    return AgentInput.SetThreat(c.n, out string threatWhy) ? Ok() : Err(threatWhy);

                case "tax":
                    return AgentInput.Tax(c.n, out string taxWhy) ? Ok() : Err(taxWhy);

                case "key":
                    // Only the keys that open or close something. Synthesising real key events
                    // through the Input System is far more machinery than calling the one
                    // handler they reach, and the handler is what actually needs testing.
                    if (c.id == "escape")
                    {
                        if (UI.Hud.Instance == null) return Err("hud yok");
                        UI.Hud.Instance.OnEscape();
                        return Ok();
                    }
                    if (c.id == "m")
                    {
                        if (AudioBus.Instance == null) return Err("ses yok");
                        AudioBus.Instance.ToggleMute();
                        return Ok();
                    }
                    return Err($"bilinmeyen tuş '{c.id}'");

                case "probe":
                    // {"cmd":"probe","id":"bina|araba|yaya"} — measurements, not opinions.
                    return Ok(AgentInput.Probe(c.id));

                case "overlay":
                    // Diagnostic: which visible elements are big enough to be covering the map.
                    // Wrapped, not pasted raw — the report is multi-line text, not JSON.
                    return Ok(JsonUtility.ToJson(new Wrap { v = AgentInput.UiOverlay() }));

                case "ui":
                    // Diagnostic: {"cmd":"ui","n":0} takes the whole interface off screen.
                    return AgentInput.ShowUi(c.n != 0, out string uiWhy) ? Ok() : Err(uiWhy);

                case "shadows":
                    // Diagnostic: {"cmd":"shadows","n":0} kills the sun's shadows. If an
                    // artefact survives that, it is geometry, not shadowing.
                    return AgentInput.Shadows(c.n != 0, out string shadowWhy) ? Ok() : Err(shadowWhy);

                case "mute":
                    // {"cmd":"mute","n":1} silences, n == 0 restores. Same switch the M key throws.
                    if (AudioBus.Instance == null) return Err("ses yok");
                    AudioBus.Instance.SetMuted(c.n != 0);
                    return Ok();

                case "grant":
                    // Test affordance: fills the treasury and the depot so a scenario can ask
                    // "is this parcel legal?" without also asking "can the city pay today?".
                    return AgentInput.Grant(c.n, out string grantWhy) ? Ok() : Err(grantWhy);

                case "clause":
                    return AgentInput.Clause(c.id, out string clauseWhy) ? Ok() : Err(clauseWhy);

                case "decree":
                    return AgentInput.Decree(c.id, out string decreeWhy) ? Ok() : Err(decreeWhy);

                case "law":
                    // n != 0 adopts, n == 0 repeals.
                    return AgentInput.Law(c.id, c.n != 0, out string lawWhy) ? Ok() : Err(lawWhy);

                case "election":
                    // {"cmd":"election","id":"yap"} — also "ertele" and "hile".
                    return AgentInput.Election(c.id, out string electionWhy)
                        ? Ok(JsonUtility.ToJson(new Wrap { v = electionWhy })) : Err(electionWhy);

                case "council":
                    return AgentInput.Council(c.n != 0, out string councilWhy) ? Ok() : Err(councilWhy);

                case "appoint":
                    // {"cmd":"appoint","id":"tarim","n":1}  — n != 0 picks the loyalist.
                    return AgentInput.Appoint(c.id, c.n != 0, out string appointWhy)
                        ? Ok() : Err(appointWhy);

                case "block":
                    // Stop (n != 0) or restart (n == 0) every building of a type, so a supply
                    // blockage can be reproduced on demand instead of waited for.
                    int hit = AgentInput.Block(c.id, c.n != 0, out string blockWhy);
                    return hit > 0 ? Ok(hit.ToString()) : Err(blockWhy);

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
                // Wait for the previous turn to settle, then give the HUD one frame to
                // re-enable the button before pressing it again — otherwise the click lands
                // on a disabled control and the turn is silently skipped.
                yield return new WaitUntil(() => AgentState.TurnIdle);

                // Script execution order between the HUD and this coroutine is undefined, so
                // retry for a few frames rather than assuming the button is enabled yet.
                bool pressed = false;
                for (int attempt = 0; attempt < 30 && !pressed; attempt++)
                {
                    yield return null;
                    pressed = AgentInput.Click("btn_end_turn");
                }

                if (!pressed)
                {
                    Debug.LogWarning($"[AgentBridge] endturn {i + 1}/{n}: btn_end_turn basılamadı");
                    yield break;
                }

                // BeginTurn clears TurnIdle synchronously, so this waits for the real tick.
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


