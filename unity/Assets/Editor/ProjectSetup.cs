// Everything the project needs that cannot be created at runtime, created from code anyway.
//
// Three things live in the editor rather than in Bootstrap: the single empty scene that Build
// Settings insists on, the PanelSettings asset UI Toolkit needs to open a runtime panel, and
// the player settings the agent loop depends on (chiefly runInBackground — without it the
// unfocused player pauses and every bridge command times out).
//
// BuildScript calls Ensure() before every build, so the agent loop stays one command and a
// fresh clone of the repo builds without anyone opening the editor GUI.

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mesruiyet.EditorTools
{
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Boot.unity";
        const string MaterialPath = "Assets/Resources/CityLit.mat";
        const string ThemePath = "Assets/Resources/MesruiyetTheme.tss";
        const string PanelPath = "Assets/Resources/MesruiyetPanel.asset";

        [MenuItem("Meşruiyet/Projeyi hazırla")]
        public static void Setup() => Ensure(force: true);

        /// <summary>Create anything missing. Cheap and idempotent, so it runs before every build.</summary>
        public static void Ensure(bool force = false)
        {
            Directory.CreateDirectory("Assets/Resources");
            Directory.CreateDirectory("Assets/Scenes");

            EnsurePanelSettings();
            EnsureCityMaterial();
            EnsureGltfShaders();
            EnsureBootScene(force);
            EnsurePlayerSettings();

            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- UI Toolkit panel

        static void EnsurePanelSettings()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null)
            {
                // The default runtime theme is one import line; authoring it as text avoids
                // depending on the editor's "Create > UI Toolkit" menu ever having been used.
                File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(ThemePath, ImportAssetOptions.ForceSynchronousImport);
                theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            }

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }

            panel.themeStyleSheet = theme;
            // The HUD is laid out against the mockup's 1920×1080, then scaled to the window.
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.clearColor = false;
            EditorUtility.SetDirty(panel);
        }

        // ---------------------------------------------------------------- the city material

        /// <summary>
        /// Ship a material asset with GPU instancing switched on.
        /// 
        /// Not cosmetic: Unity strips shader variants that no material in the build asks for,
        /// and materials created at runtime do not count. Without this asset the INSTANCING_ON
        /// variant is stripped, RenderMeshInstanced silently draws every agent at the origin,
        /// and the entire crowd is invisible in a player build while working fine in the editor.
        /// </summary>
        static void EnsureCityMaterial()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/Shaders/CityLit.shader");
            if (shader == null)
            {
                Debug.LogError("[ProjectSetup] CityLit.shader bulunamadı.");
                return;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }

            mat.shader = shader;
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);

            // A URP/Lit material in Resources, for the glTF characters. The characters' own
            // materials use glTFast's shader graphs, which the build strips because nothing in a
            // scene references them; swapping onto this material at load time sidesteps the
            // whole problem, and shipping it in Resources is what keeps URP/Lit itself aboard.
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null)
            {
                const string unitPath = "Assets/Resources/UnitLit.mat";
                var unit = AssetDatabase.LoadAssetAtPath<Material>(unitPath);
                if (unit == null)
                {
                    unit = new Material(lit);
                    AssetDatabase.CreateAsset(unit, unitPath);
                }
                unit.shader = lit;
                EditorUtility.SetDirty(unit);
            }
        }

        /// <summary>
        /// Keep glTFast's shaders in the build. The external building models arrive with real
        /// textures, and those survive a player build only if the shaders their materials use
        /// are in Always Included Shaders — nothing in a scene references them, so Unity would
        /// otherwise strip the lot and the models would land with empty material slots.
        /// </summary>
        static void EnsureGltfShaders()
        {
            string[] wanted =
            {
                "Shader Graphs/glTF-pbrMetallicRoughness",
                "Shader Graphs/glTF-unlit",
                "glTF/PbrMetallicRoughness",
                "glTF/Unlit",
            };

            var settings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                "ProjectSettings/GraphicsSettings.asset");
            if (settings == null) return;
            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return;

            foreach (var name in wanted)
            {
                var shader = Shader.Find(name);
                if (shader == null) continue;

                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) { present = true; break; }
                if (present) continue;

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                Debug.Log($"[ProjectSetup] Always Included Shaders += {name}");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- the one scene

        static void EnsureBootScene(bool force)
        {
            bool exists = File.Exists(ScenePath);
            if (!exists || force)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var go = new GameObject("Bootstrap");
                go.AddComponent<Mesruiyet.Core.Bootstrap>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            // Exactly one scene in the build, and it is this one. The world is code.
            var current = EditorBuildSettings.scenes;
            bool alreadyOnly = current.Length == 1 && current[0].path == ScenePath && current[0].enabled;
            if (!alreadyOnly)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // ---------------------------------------------------------------- player settings

        static void EnsurePlayerSettings()
        {
            PlayerSettings.companyName = "Meşruiyet";
            PlayerSettings.productName = "Mesruiyet";

            // The agent drives a window that never has focus. Without this the player pauses
            // between commands and the bridge times out — the single most confusing failure
            // mode in the whole loop, so it is set here rather than left to a human.
            PlayerSettings.runInBackground = true;

            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, true);
        }
    }
}

