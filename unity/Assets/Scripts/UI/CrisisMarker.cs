// The red ring the design draws around a district that has had enough.
//
// A crisis has to be visible on the map, not only in the rail — the whole point of the crowd
// and the props is that the city tells you what is happening, and a quarter about to riot is
// the thing you most need to see without reading anything.
//
// Drawn with Painter2D so it costs no asset and no material: a dashed ellipse, thickened and
// brightened as the streak lengthens.

using UnityEngine;
using UnityEngine.UIElements;

namespace Mesruiyet.UI
{
    public sealed class CrisisRing : VisualElement
    {
        float _alarm;

        public CrisisRing(float width, float height)
        {
            style.width = width;
            style.height = height;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        /// <summary>0..1 — how far gone this district is. Drives thickness and brightness.</summary>
        public void SetAlarm(float alarm)
        {
            if (Mathf.Approximately(_alarm, alarm)) return;
            _alarm = alarm;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float w = contentRect.width, h = contentRect.height;
            if (w <= 1 || h <= 1) return;

            float rx = w * 0.5f - 3f;
            float ry = h * 0.5f - 3f;
            var centre = new Vector2(w * 0.5f, h * 0.5f);

            p.strokeColor = new Color(UiKit.Red.r, UiKit.Red.g, UiKit.Red.b, 0.55f + 0.4f * _alarm);
            p.lineWidth = 1.6f + 1.4f * _alarm;

            // Dashes, drawn as arc segments — Painter2D has no dash pattern of its own.
            const int Dashes = 14;
            for (int i = 0; i < Dashes; i++)
            {
                float a0 = i / (float)Dashes * Mathf.PI * 2f;
                float a1 = a0 + Mathf.PI * 2f / Dashes * 0.55f;

                p.BeginPath();
                p.MoveTo(centre + new Vector2(Mathf.Cos(a0) * rx, Mathf.Sin(a0) * ry));
                const int Steps = 5;
                for (int s = 1; s <= Steps; s++)
                {
                    float a = Mathf.Lerp(a0, a1, s / (float)Steps);
                    p.LineTo(centre + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry));
                }
                p.Stroke();
            }
        }
    }
}
