using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Enemies.Editor
{
    public static class EnemyNavigationTools
    {
        [MenuItem("Game/Enemies/Bake Scene Navigation")]
        public static void Bake()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("请退出运行后再烘焙导航。"); return; }
            foreach (var surface in Object.FindObjectsByType<NavMeshSurface>())
            {
                var scene = surface.gameObject.scene;
                if (string.IsNullOrEmpty(scene.path)) { Debug.LogError("请先保存场景。", surface); continue; }
                var previous = surface.navMeshData;
                string previousPath = AssetDatabase.GetAssetPath(previous);
                surface.BuildNavMesh();
                if (surface.navMeshData == null) { Debug.LogError("场景导航烘焙失败。", surface); continue; }
                string path = System.IO.Path.ChangeExtension(scene.path, null) + "_Navigation.asset";
                if (string.IsNullOrEmpty(previousPath))
                {
                    path = AssetDatabase.GenerateUniqueAssetPath(path);
                    AssetDatabase.CreateAsset(surface.navMeshData, path);
                }
                else
                {
                    var rebuilt = surface.navMeshData;
                    surface.RemoveData();
                    EditorUtility.CopySerialized(rebuilt, previous);
                    surface.navMeshData = previous;
                    EditorUtility.SetDirty(previous);
                    surface.AddData();
                    Object.DestroyImmediate(rebuilt);
                }
                EditorUtility.SetDirty(surface);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("导航已烘焙并保存：" + scene.name, surface);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
