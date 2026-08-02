// Minister portraits, drawn from primitives at runtime.
//
// Zero external assets is a hard constraint, so a face is a rounded plate, a skin circle, a
// hair cap, two eyes and a collar in the domain's accent colour — the same construction as the
// reference mockup's SVG avatars, expressed with Painter2D. The seed comes from the minister
// definition, so a given minister has the same face for the whole run and you learn to
// recognise the man who keeps telling you the granary is fine.

using UnityEngine;
using UnityEngine.UIElements;

namespace Mesruiyet.UI
{
    public sealed class Portrait : VisualElement
    {
        int _seed;
        Color _accent;
        bool _loyalist;

        public Portrait(int seed, Color accent, bool loyalist, float size)
        {
            _seed = seed;
            _accent = accent;
            _loyalist = loyalist;

            style.width = size;
            style.height = size;
            style.backgroundColor = UiKit.Hex("#1A212C");
            this.Radius(11).Border(1, UiKit.Hairline);
            style.overflow = Overflow.Hidden;
            pickingMode = PickingMode.Ignore;

            generateVisualContent += Draw;
        }

        float Rnd(int salt)
        {
            int h = _seed * 73856093 ^ salt * 19349663;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
        }

        static readonly Color[] Skins =
        {
            new Color(0.847f, 0.659f, 0.486f),
            new Color(0.788f, 0.576f, 0.408f),
            new Color(0.878f, 0.710f, 0.537f),
            new Color(0.796f, 0.604f, 0.431f),
            new Color(0.831f, 0.631f, 0.459f),
        };

        static readonly Color[] Hairs =
        {
            new Color(0.231f, 0.165f, 0.106f),
            new Color(0.141f, 0.102f, 0.071f),
            new Color(0.290f, 0.227f, 0.149f),
            new Color(0.357f, 0.290f, 0.200f),
            new Color(0.482f, 0.443f, 0.412f),
        };

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float w = contentRect.width, h = contentRect.height;
            if (w <= 1 || h <= 1) return;

            float u = w / 40f;                       // the mockup draws on a 40×40 viewBox

            Color skin = Skins[Mathf.FloorToInt(Rnd(1) * Skins.Length) % Skins.Length];
            Color hair = Hairs[Mathf.FloorToInt(Rnd(2) * Hairs.Length) % Hairs.Length];

            // Collar: a wedge of the domain's colour rising from the bottom corners.
            p.fillColor = new Color(_accent.r, _accent.g, _accent.b, 0.85f);
            p.BeginPath();
            p.MoveTo(new Vector2(6 * u, h));
            p.BezierCurveTo(new Vector2(20 * u, 25.5f * u), new Vector2(20 * u, 25.5f * u),
                            new Vector2(34 * u, h));
            p.ClosePath();
            p.Fill();

            // Neck.
            p.fillColor = new Color(skin.r * 0.94f, skin.g * 0.94f, skin.b * 0.94f);
            p.BeginPath();
            p.MoveTo(new Vector2(17 * u, 26 * u));
            p.LineTo(new Vector2(23 * u, 26 * u));
            p.LineTo(new Vector2(23 * u, 33 * u));
            p.LineTo(new Vector2(17 * u, 33 * u));
            p.ClosePath();
            p.Fill();

            // Head.
            float headY = (16f + Rnd(3) * 1.5f) * u;
            float headR = (9.2f + Rnd(4) * 0.8f) * u;
            p.fillColor = skin;
            p.BeginPath();
            p.Arc(new Vector2(20 * u, headY), headR, 0, 360);
            p.ClosePath();
            p.Fill();

            // Hair, sitting on the crown. Its height is the main thing that separates two faces.
            float cap = (9.5f + Rnd(5) * 3.5f) * u;
            p.fillColor = hair;
            p.BeginPath();
            p.MoveTo(new Vector2(20 * u - headR, headY - 1 * u));
            p.BezierCurveTo(new Vector2(20 * u - headR, headY - cap),
                            new Vector2(20 * u + headR, headY - cap),
                            new Vector2(20 * u + headR, headY - 1 * u));
            p.BezierCurveTo(new Vector2(20 * u + headR * 0.5f, headY - headR * 0.55f),
                            new Vector2(20 * u - headR * 0.5f, headY - headR * 0.55f),
                            new Vector2(20 * u - headR, headY - 1 * u));
            p.ClosePath();
            p.Fill();

            // Eyes. A loyalist's sit a touch higher and closer — a small, cheap tell that the
            // player will read as a personality long before they can name it.
            float eyeY = headY + (_loyalist ? -0.4f : 0.2f) * u;
            float eyeGap = (_loyalist ? 2.3f : 2.7f) * u;
            p.fillColor = UiKit.Hex("#20262F");
            foreach (float dx in new[] { -eyeGap, eyeGap })
            {
                p.BeginPath();
                p.Arc(new Vector2(20 * u + dx, eyeY), 1.15f * u, 0, 360);
                p.ClosePath();
                p.Fill();
            }

            // Moustache or none, purely so the five faces differ at a glance.
            if (Rnd(6) > 0.45f)
            {
                p.fillColor = hair;
                p.BeginPath();
                p.MoveTo(new Vector2(20 * u - 3.2f * u, headY + 4.2f * u));
                p.LineTo(new Vector2(20 * u + 3.2f * u, headY + 4.2f * u));
                p.LineTo(new Vector2(20 * u + 2.4f * u, headY + 5.4f * u));
                p.LineTo(new Vector2(20 * u - 2.4f * u, headY + 5.4f * u));
                p.ClosePath();
                p.Fill();
            }
        }
    }
}
