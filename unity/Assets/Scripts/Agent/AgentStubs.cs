// Stubs so the agent bridge compiles on day one. Replace the bodies as the real systems
// land — keep the signatures, because the agent loop scripts depend on them.

#if DEBUG || UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace Mesruiyet.Agent
{
    /// <summary>Whatever the agent needs to know about the run, as JSON.</summary>
    public static class AgentState
    {
        /// <summary>False while a turn is resolving; TurnResolver owns this.</summary>
        public static bool TurnIdle = true;

        /// <summary>
        /// Dump the TRUE simulation state. Never route this through Reporting — the agent
        /// must be able to see reality in order to tell whether the lies are working.
        /// </summary>
        public static string DumpJson()
        {
            // TODO: replace with GameState.ToJson() once GameState exists.
            return JsonUtility.ToJson(new Snapshot
            {
                turn = 0,
                money = 0,
                food = 0,
                legitimacy = 60,
                axisOrder = 0,
                axisEconomy = 0,
                bufferTurns = 0,
                note = "AgentState.DumpJson henüz gerçek veriye bağlı değil"
            });
        }

        [System.Serializable]
        public struct Snapshot
        {
            public int turn;
            public int money;
            public int food;
            public int legitimacy;
            public int axisOrder;
            public int axisEconomy;
            public int bufferTurns;
            public string note;
        }
    }

    /// <summary>Synthetic input so the agent can actually play, not just observe.</summary>
    public static class AgentInput
    {
        /// <summary>Root of the UI Toolkit document; set this from Bootstrap.</summary>
        public static VisualElement UiRoot;

        public static void Press(string key)
        {
            // TODO: forward to the game's own input router rather than faking OS events.
            Debug.Log($"[AgentInput] press {key}");
        }

        /// <summary>Click a UI element by its `name` attribute. Returns false if not found.</summary>
        public static bool Click(string id)
        {
            if (UiRoot == null) { Debug.LogWarning("[AgentInput] UiRoot not set"); return false; }
            var el = UiRoot.Q<Button>(id) ?? UiRoot.Q(id) as VisualElement;
            if (el == null) return false;

            using (var e = new NavigationSubmitEvent { target = el })
                el.SendEvent(e);
            Debug.Log($"[AgentInput] click {id}");
            return true;
        }
    }
}
#endif
