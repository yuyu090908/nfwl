using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NFWL.EditorTools
{
    /// <summary>
    /// First-time setup for people new to Unity.
    /// When the project opens, creates Assets/Scenes/Main.unity (camera, light and the match object),
    /// registers it in Build Settings and opens it, so you can just press Play.
    /// Menu: NFWL > Open Main Scene / Rebuild Main Scene.
    /// </summary>
    [InitializeOnLoad]
    static class NFWLProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string OpenedKey = "NFWL.MainSceneOpened";

        static NFWLProjectSetup()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        static void OnEditorReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (!File.Exists(ScenePath))
            {
                CreateMainScene();
                return;
            }

            // Once per editor session: if nothing useful is open, open the main scene.
            if (SessionState.GetBool(OpenedKey, false)) return;
            SessionState.SetBool(OpenedKey, true);
            var active = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && !active.isDirty)
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
        }

        [MenuItem("NFWL/Open Main Scene")]
        static void OpenMainScene()
        {
            if (!File.Exists(ScenePath))
            {
                CreateMainScene();
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
        }

        [MenuItem("NFWL/Rebuild Main Scene")]
        static void RebuildMainScene()
        {
            if (EditorUtility.DisplayDialog("NFWL", "Main.unity を作り直しますか？", "作り直す", "キャンセル"))
            {
                CreateMainScene();
            }
        }

        static void CreateMainScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("NFWL Match").AddComponent<MatchManager>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.Refresh();
            SessionState.SetBool(OpenedKey, true);
            Debug.Log("NFWL: Assets/Scenes/Main.unity を作成しました。上の ▶ (Play) を押すと遊べます。");
        }
    }
}
