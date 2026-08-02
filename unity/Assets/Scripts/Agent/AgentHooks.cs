// The one call Bootstrap makes into the agent layer. Always compiles; does nothing in a
// release build, where the bridge itself is compiled out.

using UnityEngine.UIElements;
using Mesruiyet.Core;
using Mesruiyet.World;

namespace Mesruiyet.Agent
{
    public static class AgentHooks
    {
        public static void Bind(GameState state, CityGrid grid, VisualElement uiRoot)
        {
#if DEBUG || UNITY_EDITOR
            AgentState.Bind(state, grid);
            AgentInput.UiRoot = uiRoot;
#endif
        }
    }
}
