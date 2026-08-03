// UiKit — the glass panel system from reference/src-sehir-3d.html, expressed as UI Toolkit.
//
// The mockup's CSS is the spec: rgba(14,18,25,.82) panels, a 1 px hairline at 9% white, 14 px
// radius, amber for action, red for dangerous or untrustworthy, green for honest. Every helper
// here exists so the HUD code below reads as layout rather than as a wall of style assignments.

using UnityEngine;
using UnityEngine.UIElements;

namespace Mesruiyet.UI
{
    public static class UiKit
    {
        public static readonly Color Bg = Hex("#0B0E13");
        public static readonly Color Panel = new Color32(14, 18, 25, 214);
        public static readonly Color Hairline = new Color(1f, 1f, 1f, 0.09f);
        public static readonly Color Ink = Hex("#E9EEF5");
        public static readonly Color Muted = Hex("#7E8CA0");
        public static readonly Color Dim = Hex("#5A6678");
        public static readonly Color Amber = Hex("#F5B33C");
        public static readonly Color Red = Hex("#F2564B");
        public static readonly Color Green = Hex("#3FCF77");
        public static readonly Color Blue = Hex("#5AA9F5");
        public static readonly Color Track = new Color(1f, 1f, 1f, 0.10f);

        public static Color Hex(string s)
        {
            ColorUtility.TryParseHtmlString(s, out var c);
            return c;
        }

        public static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>Add and hand the child back, so a tree can be built in one expression.</summary>
        public static T Attach<T>(this VisualElement parent, T child) where T : VisualElement
        {
            parent.Add(child);
            return child;
        }

        public static T Radius<T>(this T e, float r) where T : VisualElement
        {
            e.style.borderTopLeftRadius = r;
            e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r;
            e.style.borderBottomRightRadius = r;
            return e;
        }

        public static T Border<T>(this T e, float w, Color c) where T : VisualElement
        {
            e.style.borderTopWidth = w;
            e.style.borderBottomWidth = w;
            e.style.borderLeftWidth = w;
            e.style.borderRightWidth = w;
            e.style.borderTopColor = c;
            e.style.borderBottomColor = c;
            e.style.borderLeftColor = c;
            e.style.borderRightColor = c;
            return e;
        }

        public static T Pad<T>(this T e, float v, float h) where T : VisualElement
        {
            e.style.paddingTop = v;
            e.style.paddingBottom = v;
            e.style.paddingLeft = h;
            e.style.paddingRight = h;
            return e;
        }

        public static T Margin<T>(this T e, float top = 0, float right = 0, float bottom = 0, float left = 0)
            where T : VisualElement
        {
            e.style.marginTop = top;
            e.style.marginRight = right;
            e.style.marginBottom = bottom;
            e.style.marginLeft = left;
            return e;
        }

        /// <summary>
        /// The city's seal: a double ring, the monogram, a diamond. Drawn from elements rather
        /// than from an image, like everything else, so it scales to any size and both themes.
        /// It appears large on the title and small beside the ledger, and being the same mark
        /// in both places is what makes it read as an institution rather than a decoration.
        /// </summary>
        public static VisualElement Emblem(float size)
        {
            var ring = new VisualElement();
            ring.style.width = size; ring.style.height = size;
            ring.Radius(size / 2).Border(Mathf.Max(1.5f, size * 0.026f), Amber);
            ring.style.backgroundColor = Hex("#141A24");
            ring.style.alignItems = Align.Center;
            ring.style.justifyContent = Justify.Center;
            ring.style.flexShrink = 0;

            var inner = new VisualElement();
            float ins = size * 0.82f;
            inner.style.width = ins; inner.style.height = ins;
            inner.Radius(ins / 2).Border(1, Alpha(Amber, 0.4f));
            inner.style.alignItems = Align.Center;
            inner.style.justifyContent = Justify.Center;
            ring.Add(inner);

            var m = Text("M", size * 0.4f, Amber, FontStyle.Bold);
            m.style.unityTextAlign = TextAnchor.MiddleCenter;
            inner.Add(m);

            var mark = Text("◆", size * 0.12f, Alpha(Amber, 0.75f));
            mark.style.marginTop = -size * 0.06f;
            inner.Add(mark);

            return ring;
        }

        /// <summary>A floating translucent card — the `.g` class from the mockup.</summary>
        public static VisualElement Glass()
        {
            var v = new VisualElement();
            v.style.backgroundColor = Panel;
            v.Radius(14).Border(1, Hairline);
            return v;
        }

        public static VisualElement Row(float gap = 0)
        {
            var v = new VisualElement();
            v.style.flexDirection = FlexDirection.Row;
            v.style.alignItems = Align.Center;
            if (gap > 0) v.style.marginRight = gap;
            return v;
        }

        public static VisualElement Column()
        {
            var v = new VisualElement();
            v.style.flexDirection = FlexDirection.Column;
            return v;
        }

        public static VisualElement Spacer()
        {
            var v = new VisualElement();
            v.style.flexGrow = 1;
            return v;
        }

        public static Label Text(string text, float size, Color colour, FontStyle style = FontStyle.Normal)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = colour;
            l.style.unityFontStyleAndWeight = style;
            l.style.paddingTop = 0;
            l.style.paddingBottom = 0;
            l.style.marginTop = 0;
            l.style.marginBottom = 0;
            return l;
        }

        /// <summary>The small wide-tracked uppercase caption — `.lbl` in the mockup.</summary>
        public static Label Caption(string text, Color? colour = null)
        {
            var l = Text(text.ToUpperInvariant(), 10.5f, colour ?? Muted, FontStyle.Bold);
            l.style.letterSpacing = 1.4f;
            return l;
        }

        /// <summary>A rounded status pill: ok / warn / bad.</summary>
        public static Label Pill(string text, Color colour)
        {
            var l = Text(text.ToUpperInvariant(), 9.5f, colour, FontStyle.Bold);
            l.style.letterSpacing = 1.1f;
            l.style.backgroundColor = Alpha(colour, 0.17f);
            l.Radius(20).Pad(4, 9);
            return l;
        }

        /// <summary>The red `?` badge that marks a figure the ministers made unreliable.</summary>
        public static Label WarnBadge()
        {
            var l = Text("?", 10f, Bg, FontStyle.Bold);
            l.style.backgroundColor = Red;
            l.style.width = 15;
            l.style.height = 15;
            l.style.marginLeft = 6;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.Radius(8);
            return l;
        }

        /// <summary>A thin meter track with a filled portion.</summary>
        public static VisualElement Bar(float fill01, Color colour, float width, float height = 5)
        {
            var track = new VisualElement();
            track.style.width = width;
            track.style.height = height;
            track.style.backgroundColor = Track;
            track.Radius(height * 0.5f);

            var f = new VisualElement();
            f.style.width = Length.Percent(Mathf.Clamp01(fill01) * 100f);
            f.style.height = Length.Percent(100);
            f.style.backgroundColor = colour;
            f.Radius(height * 0.5f);
            track.Add(f);
            return track;
        }

        public static VisualElement Divider()
        {
            var v = new VisualElement();
            v.style.height = 1;
            v.style.backgroundColor = new Color(1, 1, 1, 0.07f);
            v.style.marginTop = 6;
            v.style.marginBottom = 6;
            return v;
        }

        /// <summary>Section heading: caption on the left, a quiet note on the right.</summary>
        public static VisualElement Heading(string left, string right = null, Color? rightColour = null)
        {
            var row = Row();
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 11;
            row.Add(Caption(left));
            if (!string.IsNullOrEmpty(right)) row.Add(Caption(right, rightColour ?? Dim));
            return row;
        }

        public static string Signed(float v, int decimals = 0)
        {
            string body = v.ToString("N" + decimals, System.Globalization.CultureInfo.InvariantCulture);
            if (v > 0.05f) return "▲ +" + body;
            if (v < -0.05f) return "▼ " + body;
            return "— " + body;
        }

        public static Color FlowColour(float v) => v > 0.05f ? Green : v < -0.05f ? Red : Muted;

        public static Color GrievanceColour(float g)
            => g >= 70 ? Red : g >= 45 ? Amber : g >= 25 ? Hex("#9AA8BC") : Green;
    }
}

