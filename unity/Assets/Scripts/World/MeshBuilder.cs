// MeshBuilder — every piece of geometry in the game is assembled here, from primitives, in code.
//
// No imported models, no prefabs, no .fbx. A building is boxes; a tree is a cone on a stick; a
// window is a quad with alpha 0 so CityLit lights it from within. Everything is written into
// one vertex-coloured mesh per layer, which is why the whole skyline costs a handful of draw
// calls and leaves the frame budget for the crowd simulation that lands in a later slice.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mesruiyet.World
{
    public sealed class MeshBuilder
    {
        readonly List<Vector3> _verts = new List<Vector3>(4096);
        readonly List<Vector3> _normals = new List<Vector3>(4096);
        readonly List<Color32> _colors = new List<Color32>(4096);
        readonly List<int> _tris = new List<int>(8192);

        public int VertexCount => _verts.Count;

        public void Clear()
        {
            _verts.Clear();
            _normals.Clear();
            _colors.Clear();
            _tris.Clear();
        }

        /// <summary>A quad, wound counter-clockwise from a..d as seen from the front face.</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            int i = _verts.Count;
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            Color32 c32 = color;

            _verts.Add(a); _verts.Add(b); _verts.Add(c); _verts.Add(d);
            for (int k = 0; k < 4; k++) { _normals.Add(n); _colors.Add(c32); }

            _tris.Add(i); _tris.Add(i + 1); _tris.Add(i + 2);
            _tris.Add(i); _tris.Add(i + 2); _tris.Add(i + 3);
        }

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            int i = _verts.Count;
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            Color32 c32 = color;

            _verts.Add(a); _verts.Add(b); _verts.Add(c);
            for (int k = 0; k < 3; k++) { _normals.Add(n); _colors.Add(c32); }

            _tris.Add(i); _tris.Add(i + 1); _tris.Add(i + 2);
        }

        /// <summary>A flat tile on the XZ plane, centred on <paramref name="centre"/>.</summary>
        public void AddTile(Vector3 centre, float size, Color color)
        {
            float h = size * 0.5f;
            AddQuad(
                centre + new Vector3(-h, 0, -h),
                centre + new Vector3(-h, 0, h),
                centre + new Vector3(h, 0, h),
                centre + new Vector3(h, 0, -h),
                color);
        }

        /// <summary>
        /// An axis-aligned box sitting on the ground. Top and side faces get slightly different
        /// shades so the silhouette reads even when the light is flat behind it.
        /// </summary>
        public void AddBox(Vector3 groundCentre, Vector3 size, Color color, bool includeBottom = false)
        {
            float hx = size.x * 0.5f, hz = size.z * 0.5f, y0 = groundCentre.y, y1 = groundCentre.y + size.y;
            Vector3 o = groundCentre;

            Color top = Shade(color, 1.06f);
            Color side = Shade(color, 0.88f);
            Color dark = Shade(color, 0.72f);

            var a = new Vector3(o.x - hx, y0, o.z - hz);
            var b = new Vector3(o.x + hx, y0, o.z - hz);
            var c = new Vector3(o.x + hx, y0, o.z + hz);
            var d = new Vector3(o.x - hx, y0, o.z + hz);
            var e = new Vector3(o.x - hx, y1, o.z - hz);
            var f = new Vector3(o.x + hx, y1, o.z - hz);
            var g = new Vector3(o.x + hx, y1, o.z + hz);
            var h = new Vector3(o.x - hx, y1, o.z + hz);

            AddQuad(e, h, g, f, top);          // +Y
            AddQuad(a, b, f, e, side);         // −Z
            AddQuad(c, d, h, g, side);         // +Z
            AddQuad(b, c, g, f, dark);         // +X
            AddQuad(d, a, e, h, dark);         // −X
            if (includeBottom) AddQuad(a, d, c, b, dark);
        }

        /// <summary>A four-sided pyramid — tree canopies, roofs, the monument's cap.</summary>
        public void AddPyramid(Vector3 groundCentre, float radius, float height, Color color)
        {
            Vector3 apex = groundCentre + Vector3.up * height;
            var a = groundCentre + new Vector3(-radius, 0, -radius);
            var b = groundCentre + new Vector3(radius, 0, -radius);
            var c = groundCentre + new Vector3(radius, 0, radius);
            var d = groundCentre + new Vector3(-radius, 0, radius);

            AddTriangle(a, b, apex, Shade(color, 0.9f));
            AddTriangle(b, c, apex, Shade(color, 1.05f));
            AddTriangle(c, d, apex, Shade(color, 0.86f));
            AddTriangle(d, a, apex, Shade(color, 0.96f));
        }

        /// <summary>
        /// A gable roof: two pitched slopes meeting at a ridge, with a triangular gable at each
        /// end. A pyramid gives a hipped roof, which is fine for a tower and wrong for a house —
        /// a row of flat-topped boxes is what makes a low-poly town read as an office park.
        /// <paramref name="alongX"/> runs the ridge along the x axis.
        /// </summary>
        public void AddGable(Vector3 groundCentre, float width, float depth, float height,
                             Color color, bool alongX, float overhang = 0.12f)
        {
            float hw = width * 0.5f + overhang;
            float hd = depth * 0.5f + overhang;

            // Ridge endpoints, along whichever axis was asked for.
            Vector3 r0, r1;
            Vector3 a, b, c, d;   // eaves corners, wound so each slope is a quad
            if (alongX)
            {
                r0 = groundCentre + new Vector3(-hw, height, 0);
                r1 = groundCentre + new Vector3(hw, height, 0);
                a = groundCentre + new Vector3(-hw, 0, -hd);
                b = groundCentre + new Vector3(hw, 0, -hd);
                c = groundCentre + new Vector3(hw, 0, hd);
                d = groundCentre + new Vector3(-hw, 0, hd);
            }
            else
            {
                r0 = groundCentre + new Vector3(0, height, -hd);
                r1 = groundCentre + new Vector3(0, height, hd);
                a = groundCentre + new Vector3(-hw, 0, -hd);
                b = groundCentre + new Vector3(-hw, 0, hd);
                c = groundCentre + new Vector3(hw, 0, hd);
                d = groundCentre + new Vector3(hw, 0, -hd);
            }

            AddQuad(a, b, r1, r0, Shade(color, 1.06f));   // sunward slope
            AddQuad(c, d, r0, r1, Shade(color, 0.82f));   // shaded slope
            AddTriangle(d, a, r0, Shade(color, 0.92f));   // gable ends
            AddTriangle(b, c, r1, Shade(color, 0.92f));
        }

        /// <summary>
        /// A window: a quad pushed a hair off the wall with alpha 0, which CityLit reads as
        /// "emissive". Lit windows are the whole reason the city reads as inhabited at dusk.
        /// </summary>
        public void AddWindow(Vector3 centre, Vector3 normal, float w, float h, Color glow)
        {
            Vector3 right = Vector3.Cross(Vector3.up, normal).normalized * (w * 0.5f);
            Vector3 up = Vector3.up * (h * 0.5f);
            Vector3 o = centre + normal * 0.02f;

            var col = new Color(glow.r, glow.g, glow.b, 0f);   // alpha 0 → self-lit
            AddQuad(o - right - up, o + right - up, o + right + up, o - right + up, col);
        }

        public static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (_verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Rewrite an existing mesh in place, so placing a building does not churn memory.</summary>
        public void Into(Mesh mesh)
        {
            mesh.Clear();
            if (_verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_tris, 0);
            mesh.RecalculateBounds();
        }
    }
}
