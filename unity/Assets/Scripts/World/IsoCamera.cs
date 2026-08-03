// The orthographic isometric camera: pitched 40°, yawed in 90° steps, WASD to pan, wheel to zoom.
//
// Orthographic rather than perspective because the whole read of the city — which district is
// which, where the crowds are — depends on distances staying comparable across the map.

using UnityEngine;
using UnityEngine.InputSystem;
using Mesruiyet.Core;

namespace Mesruiyet.World
{
    public sealed class IsoCamera : MonoBehaviour
    {
        public static IsoCamera Instance;

        const float Pitch = 40f;
        const float PanSpeed = 46f;
        // 18 stopped the wheel about four tiles short of a doorstep, which made "does this wall
        // meet the ground?" impossible to answer by looking. There is nothing to hide down there.
        const float MinSize = 8f;
        const float MaxSize = 120f;
        /// <summary>Frames the whole 48×32 delta with the rail and dock clear of the map.</summary>
        const float DefaultSize = 68f;

        Camera _cam;
        Vector3 _focus;
        float _yaw = 45f;
        float _targetYaw = 45f;
        float _size = DefaultSize;
        float _targetSize = DefaultSize;

        public Camera Cam => _cam;

        /// <summary>Where the player is looking, for anything that budgets detail by distance.</summary>
        public Vector3 Focus => _focus;

        public void Init(Camera cam)
        {
            Instance = this;
            _cam = cam;
            _cam.orthographic = true;
            _cam.orthographicSize = _size;
            _cam.nearClipPlane = 1f;
            _cam.farClipPlane = 600f;
            _focus = Vector3.zero;
            Apply();
        }

        void Update()
        {
            if (_cam == null) return;

            // At the title the camera drifts on its own, slowly, and takes no input. A still
            // frame behind a menu is a poster; a city you are watching breathe is an invitation
            // — and the drift also shows off the far side of the map before you have to govern it.
            var hud = UI.Hud.Instance;
            if (hud != null && hud.AtTitle)
            {
                _yaw += Time.deltaTime * 2.2f;
                _targetYaw = _yaw;
                _size = Mathf.Lerp(_size, DefaultSize * 1.12f, Time.deltaTime * 0.7f);
                Apply();
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                Vector2 move = Vector2.zero;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1;

                if (move.sqrMagnitude > 0.01f) Pan(move);

                if (keyboard.qKey.wasPressedThisFrame) Rotate(-90f);
                if (keyboard.eKey.wasPressedThisFrame) Rotate(90f);
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) Zoom(-scroll * 0.04f);
            }

            _yaw = Mathf.LerpAngle(_yaw, _targetYaw, Time.deltaTime * 9f);
            _size = Mathf.Lerp(_size, _targetSize, Time.deltaTime * 11f);
            Apply();
        }

        /// <summary>
        /// Snap back to the framing the game is designed to be read at. The title screen lets the
        /// yaw wander, which looks good and would be unplayable: the whole city is drawn to be
        /// legible from a 45° multiple, and half a degree off makes every road read as a stair.
        /// </summary>
        public void ResetFraming()
        {
            _targetYaw = 45f;
            _focus = Vector3.zero;
            _targetSize = DefaultSize;
        }

        /// <summary>Pan in screen space, so W always means "up the screen" whatever the yaw is.</summary>
        public void Pan(Vector2 screenDirection)
        {
            Quaternion flat = Quaternion.Euler(0, _yaw, 0);
            Vector3 delta = flat * new Vector3(screenDirection.x, 0, screenDirection.y);
            float scale = PanSpeed * Time.deltaTime * (_size / DefaultSize);
            _focus += delta.normalized * scale;

            float halfW = CityGrid.Width * CityGrid.TileSize * 0.62f;
            float halfH = CityGrid.Height * CityGrid.TileSize * 0.62f;
            _focus.x = Mathf.Clamp(_focus.x, -halfW, halfW);
            _focus.z = Mathf.Clamp(_focus.z, -halfH, halfH);
        }

        public void Zoom(float delta) => _targetSize = Mathf.Clamp(_targetSize + delta * 40f, MinSize, MaxSize);

        public void Rotate(float degrees) => _targetYaw += degrees;

        public void FocusOn(Vector3 world) => _focus = new Vector3(world.x, 0, world.z);

        void Apply()
        {
            var rot = Quaternion.Euler(Pitch, _yaw, 0);
            _cam.transform.rotation = rot;
            _cam.transform.position = _focus - rot * Vector3.forward * 260f;
            _cam.orthographicSize = _size;
        }
    }
}
