using System.Collections.Generic;
using Game.Enemies.Unity;
using Game.Network;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace Game.Enemies.Editor
{
    public static class EnemyCatalogTools
    {
        private const string PrefabListPath = "Assets/Game/Prefabs/GameNetworkPrefabs.asset";

        [MenuItem("Game/Enemies/Register Catalog Prefabs")]
        public static void Register()
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabListPath);
            if (list == null) { Debug.LogError("找不到游戏网络 Prefab 列表。"); return; }
            Undo.RecordObject(list, "Register enemy prefabs");
            int added = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyCatalog"))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var definition in catalog.Definitions)
                {
                    if (definition == null || definition.Prefab == null) continue;
                    bool exists = false;
                    foreach (var entry in list.PrefabList) if (entry.Prefab == definition.Prefab) exists = true;
                    if (!exists) { list.Add(new NetworkPrefab { Prefab = definition.Prefab }); added++; }
                }
            }
            EditorUtility.SetDirty(list); AssetDatabase.SaveAssets();
            Debug.Log("敌人网络 Prefab 注册完成，新增 " + added + " 个。");
            Validate();
        }

        [MenuItem("Game/Enemies/Validate Enemy Catalog")]
        public static void Validate()
        {
            int errors = 0, count = 0;
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabListPath);
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyCatalog"))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                var ids = new HashSet<string>();
                foreach (var definition in catalog.Definitions)
                {
                    count++;
                    if (definition == null || !definition.IsConfigured || !ids.Add(definition.EnemyId))
                    { Debug.LogError("敌人目录有空条目、缺失配置或重复 ID。", catalog); errors++; continue; }
                    var enemy = definition.Prefab.GetComponent<NetworkEnemy>();
                    if (enemy == null || definition.Prefab.GetComponent<NetworkObject>() == null || enemy.Catalog != catalog)
                    { Debug.LogError("敌人 Prefab 缺少网络组件或引用了不同的敌人目录。", definition); errors++; }
                    bool registered = false;
                    if (list != null) foreach (var entry in list.PrefabList) if (entry.Prefab == definition.Prefab) registered = true;
                    if (!registered) { Debug.LogError("敌人 Prefab 尚未注册，请执行 Register Catalog Prefabs。", definition); errors++; }
                }
            }
            Debug.Log("敌人目录校验：" + count + " 个配置，" + errors + " 个错误。");
        }
    }
}
