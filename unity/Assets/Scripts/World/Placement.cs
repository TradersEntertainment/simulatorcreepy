// Placement — picking a tile and putting a building on it.
//
// Design pillar one: placement is a political act. So this class does not just spend money and
// materials; it hands the finished building to TurnResolver.ApplyPlacement, which is where the
// district-dependent axis and faction shifts actually land. The player sees the political cost
// on the selection card *before* they commit, and feels it the instant they do.

using UnityEngine;
using UnityEngine.InputSystem;
using Mesruiyet.Core;
using Mesruiyet.Sim;

namespace Mesruiyet.World
{
    public sealed class Placement : MonoBehaviour
    {
        public static Placement Instance;

        CityGrid _grid;
        GameState _state;
        Camera _cam;

        /// <summary>The building the hotbar has armed, or null when the cursor just inspects.</summary>
        public BuildingDef Selected { get; private set; }
        public Vector2Int Hover { get; private set; } = new Vector2Int(-1, -1);
        public bool HoverValid { get; private set; }
        public PlacedBuilding Inspected { get; private set; }

        /// <summary>Last refusal, in Turkish, for the HUD to print.</summary>
        public string LastRefusal { get; private set; } = "";

        GameObject _cursor;
        Material _cursorMat;

        static readonly Color Ok = new Color(0.35f, 0.66f, 0.96f, 0.55f);
        static readonly Color No = new Color(0.95f, 0.34f, 0.29f, 0.5f);

        public System.Action Changed;

        public void Init(CityGrid grid, GameState state, Camera cam)
        {
            Instance = this;
            _grid = grid;
            _state = state;
            _cam = cam;
            BuildCursor();
        }

        void BuildCursor()
        {
            _cursor = new GameObject("PlacementCursor");
            _cursor.transform.SetParent(transform, false);

            var b = new MeshBuilder();
            b.AddTile(Vector3.zero, CityGrid.TileSize * 0.96f, Color.white);
            _cursor.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Cursor");

            // Unlit and always on top of the ground, so the cursor never fights the shading.
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            _cursorMat = new Material(shader) { name = "Cursor" };
            _cursorMat.SetFloat("_Surface", 1f);                                  // transparent
            _cursorMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _cursorMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _cursorMat.SetFloat("_ZWrite", 0f);
            _cursorMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _cursorMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            var r = _cursor.AddComponent<MeshRenderer>();
            r.sharedMaterial = _cursorMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _cursor.SetActive(false);
        }

        public void Arm(BuildingDef def)
        {
            Selected = def;
            LastRefusal = "";
            Changed?.Invoke();
        }

        public void Disarm()
        {
            Selected = null;
            Changed?.Invoke();
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || _cam == null) return;

            UpdateHover(mouse.position.ReadValue());

            if (mouse.leftButton.wasPressedThisFrame && !UI.Hud.PointerOverUi)
            {
                if (Selected != null) TryBuild(Hover.x, Hover.y);
                else Inspect(Hover.x, Hover.y);
            }

            if (mouse.rightButton.wasPressedThisFrame) Disarm();

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Disarm();
        }

        void UpdateHover(Vector2 screen)
        {
            var ray = _cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, Vector3.zero);

            if (!plane.Raycast(ray, out float dist))
            {
                _cursor.SetActive(false);
                return;
            }

            if (!CityGrid.Tile(ray.GetPoint(dist), out var tile))
            {
                Hover = new Vector2Int(-1, -1);
                _cursor.SetActive(false);
                return;
            }

            Hover = tile;
            HoverValid = Selected == null || CanBuild(Selected, tile.x, tile.y, out _);

            _cursor.SetActive(true);
            _cursor.transform.position = CityGrid.World(tile.x, tile.y, 0.06f);
            _cursorMat.color = HoverValid ? Ok : No;
        }

        public int MoneyCost(BuildingDef def)
            => Mathf.RoundToInt(def.CostMoney * _state.Modifiers.BuildCost * _state.Consent);

        public int MaterialCost(BuildingDef def)
            => Mathf.RoundToInt(def.CostMaterial * _state.Modifiers.BuildCost * _state.Consent);

        /// <summary>Every reason a build can be refused, in one place, phrased for the player.</summary>
        public bool CanBuild(BuildingDef def, int x, int y, out string reason)
        {
            reason = "";

            if (!CityGrid.InBounds(x, y)) { reason = "Harita dışı."; return false; }

            var kind = _grid.At(x, y);
            if (kind == TileKind.Su) { reason = "Nehre inşa edilemez."; return false; }
            if (kind == TileKind.Bataklik) { reason = "Bataklık önce kurutulmalı."; return false; }
            if (kind == TileKind.Yol) { reason = "Yolun üstüne inşa edilemez."; return false; }
            if (_grid.Occupant[CityGrid.Index(x, y)] >= 0) { reason = "Bu parsel dolu."; return false; }

            if (def.Requires.HasValue && kind != def.Requires.Value)
            {
                reason = def.Requires.Value == TileKind.Verimli
                    ? "Verimli toprak gerekiyor (nehir kıyısı)."
                    : "Tepelik arazi gerekiyor (kuzeydoğu).";
                return false;
            }

            var district = Districts.At(x, y);
            if (district == null) { reason = "Bu parsel hiçbir mahalleye ait değil."; return false; }
            if (_state.District(district.Id).Lost)
            {
                reason = $"{district.Name} artık valiliğe bağlı değil.";
                return false;
            }

            // Laws move what building costs, so quote the price the player will actually pay.
            int money = MoneyCost(def);
            int material = MaterialCost(def);

            if (_state.Stock[(int)Res.Para] < money)
            {
                reason = $"Para yetmiyor: {money} ₺ gerek.";
                return false;
            }
            // Construction draws from the depot, never from the ledger total. A quarry running
            // flat out into a stopped İşlik shows a healthy Malzeme figure and still cannot put
            // up a shed, and the refusal has to say so or the player learns nothing.
            if (_state.DepotStock < material)
            {
                var chain = _state.MaterialChain;
                float raw = chain.Total - chain.Final.Stock;
                reason = raw > material
                    ? $"Depoda {chain.Final.Stock:0} işlenmiş malzeme var, {material} gerek. " +
                      $"Ham taş bekliyor ({raw:0}) — {chain.Diagnosis()}."
                    : $"Malzeme yetmiyor: depoda {chain.Final.Stock:0}, {material} gerek.";
                return false;
            }

            return true;
        }

        /// <summary>Place the armed building. Returns false and sets LastRefusal on any refusal.</summary>
        public bool TryBuild(int x, int y) => TryBuild(Selected, x, y);

        public bool TryBuild(BuildingDef def, int x, int y)
        {
            if (def == null) { LastRefusal = "Önce bir yapı seçin."; return false; }

            if (!CanBuild(def, x, y, out string reason))
            {
                LastRefusal = reason;
                Changed?.Invoke();
                return false;
            }

            var district = Districts.At(x, y);
            var placed = new PlacedBuilding
            {
                Def = def,
                Tile = new Vector2Int(x, y),
                District = district.Id,
                BuiltOnTurn = _state.Turn,
            };

            _state.Stock[(int)Res.Para] -= MoneyCost(def);
            _state.MaterialChain.Draw(MaterialCost(def));

            _grid.Occupant[CityGrid.Index(x, y)] = _state.Buildings.Count;
            _state.Buildings.Add(placed);

            // A road is terrain, not a box on a plot. Laying one has to change the tile itself
            // or the road graph never grows, the traffic never clears, and the cheapest thing
            // in the hotbar does nothing at all.
            bool road = def.Id == "yol";
            if (road)
            {
                _grid.Tiles[CityGrid.Index(x, y)] = TileKind.Yol;
                CityRenderer.Instance.RebuildGround();
            }

            TurnResolver.Instance.ApplyPlacement(placed);
            CityRenderer.Instance.Rebuild();

            Inspected = placed;
            LastRefusal = "";
            Changed?.Invoke();
            return true;
        }

        void Inspect(int x, int y)
        {
            if (!CityGrid.InBounds(x, y)) { Inspected = null; Changed?.Invoke(); return; }

            int idx = _grid.Occupant[CityGrid.Index(x, y)];
            Inspected = idx >= 0 && idx < _state.Buildings.Count ? _state.Buildings[idx] : null;
            Changed?.Invoke();
        }
    }
}





