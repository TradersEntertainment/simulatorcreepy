// Externally-made vehicle models — the player's own 3D, rolling on the streets.
//
// The delivery contract mirrors the buildings': drop otomobil.glb, yukarabasi.glb or
// fayton.glb into StreamingAssets/Models/vehicles and it replaces the procedural saloon,
// van or horse cart. Vehicles ride the crowd's instanced draw path, which speaks vertex
// colours and a per-bucket tint — no textures. So each import is baked once: submeshes are
// welded into a single mesh, the 128px texture is sampled into vertex colours, the long
// axis is turned to +Z (the nose convention the probe enforces), and the whole thing is
// scaled and seated to the exact bounds of the procedural mesh it replaces.

using System.Threading.Tasks;
using UnityEngine;
using GLTFast;

namespace Mesruiyet.World
{
    public static class VehicleModels
    {
        // Index matches CrowdSystem._carMeshes: saloon, van, cart.
        static readonly string[] Files = { "otomobil", "yukarabasi", "fayton" };

        public static async Task LoadInto(CrowdSystem crowd)
        {
            string dir = System.IO.Path.Combine(Application.streamingAssetsPath, "Models", "vehicles");
            if (!System.IO.Directory.Exists(dir)) return;

            for (int kind = 0; kind < Files.Length; kind++)
            {
                string path = System.IO.Path.Combine(dir, Files[kind] + ".glb");
                if (!System.IO.File.Exists(path)) continue;

                var import = new GltfImport();
                if (!await import.Load(path))
                {
                    Debug.LogWarning($"[VehicleModels] yüklenemedi: {path}");
                    continue;
                }

                var root = new GameObject("veh_" + Files[kind]);
                root.SetActive(false);
                var instantiator = new GameObjectInstantiator(import, root.transform);
                if (!await import.InstantiateMainSceneAsync(instantiator))
                {
                    Debug.LogWarning($"[VehicleModels] kurulamadı: {path}");
                    Object.Destroy(root);
                    continue;
                }

                var baked = Bake(root, crowd.CarMeshBounds(kind), Files[kind]);
                Object.Destroy(root);
                if (baked != null) crowd.ReplaceCarMesh(kind, baked);
            }
        }

        static Mesh Bake(GameObject root, Bounds reference, string name)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0) return null;

            var verts = new System.Collections.Generic.List<Vector3>();
            var norms = new System.Collections.Generic.List<Vector3>();
            var cols = new System.Collections.Generic.List<Color>();
            var tris = new System.Collections.Generic.List<int>();

            foreach (var f in filters)
            {
                var mesh = f.sharedMesh;
                if (mesh == null) continue;
                var toRoot = root.transform.worldToLocalMatrix * f.transform.localToWorldMatrix;
                var renderer = f.GetComponent<MeshRenderer>();
                var readable = ReadableTexture(renderer != null ? renderer.sharedMaterial : null);
                Color flat = renderer != null && renderer.sharedMaterial != null
                    ? (renderer.sharedMaterial.HasProperty("baseColorFactor")
                        ? renderer.sharedMaterial.GetColor("baseColorFactor") : Color.white)
                    : Color.white;

                var v = mesh.vertices;
                var n = mesh.normals;
                var uv = mesh.uv;
                int baseIndex = verts.Count;
                for (int i = 0; i < v.Length; i++)
                {
                    verts.Add(toRoot.MultiplyPoint3x4(v[i]));
                    norms.Add(toRoot.MultiplyVector(n.Length == v.Length ? n[i] : Vector3.up).normalized);
                    cols.Add(readable != null && uv.Length == v.Length
                        ? readable.GetPixelBilinear(uv[i].x, uv[i].y) : flat);
                }
                var t = mesh.triangles;
                for (int i = 0; i < t.Length; i++) tris.Add(baseIndex + t[i]);
            }
            if (verts.Count == 0) return null;

            // Long axis to +Z. Generators deliver either way; the probe reads the nose off
            // the longest axis, so this has to hold before any instance is drawn.
            var b = MeasuredBounds(verts);
            if (b.size.x > b.size.z)
                for (int i = 0; i < verts.Count; i++)
                {
                    var p = verts[i]; verts[i] = new Vector3(p.z, p.y, -p.x);
                    var m = norms[i]; norms[i] = new Vector3(m.z, m.y, -m.x);
                }

            // Fit the procedural predecessor's box: same length, same ground line, centred.
            b = MeasuredBounds(verts);
            float scale = reference.size.z / Mathf.Max(b.size.z, 0.01f);
            var shift = new Vector3(-b.center.x, 0, -b.center.z);
            float ground = reference.min.y - (b.min.y + shift.y) * scale;
            for (int i = 0; i < verts.Count; i++)
                verts[i] = (verts[i] + shift) * scale + new Vector3(0, ground, 0);

            var baked = new Mesh { name = "Vehicle_" + name };
            baked.SetVertices(verts);
            baked.SetNormals(norms);
            baked.SetColors(cols);
            baked.SetTriangles(tris, 0);
            Debug.Log($"[VehicleModels] {name} hazır · {verts.Count}v · uzunluk {reference.size.z:0.00} m");
            return baked;
        }

        static Bounds MeasuredBounds(System.Collections.Generic.List<Vector3> verts)
        {
            var b = new Bounds(verts[0], Vector3.zero);
            foreach (var v in verts) b.Encapsulate(v);
            return b;
        }

        /// <summary>glTFast textures are not CPU-readable; blit once through a RenderTexture.</summary>
        static Texture2D ReadableTexture(Material mat)
        {
            if (mat == null) return null;
            Texture tex = mat.mainTexture;
            if (tex == null && mat.HasProperty("baseColorTexture")) tex = mat.GetTexture("baseColorTexture");
            if (tex == null) return null;

            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0,
                                                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }
    }
}
