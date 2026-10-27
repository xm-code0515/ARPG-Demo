using System.Collections.Generic;
using Game.Network;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace Game.Items.Editor
{
    public static class ItemCatalogTools
    {
        [MenuItem("Game/Items/Validate Item Catalog")]
        public static void Validate()
        {
            int errors = 0;
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Game/Items/Data/ItemCatalog.asset");
            if (catalog == null) { Debug.LogError("找不到物品目录。"); return; }
            var ids = new HashSet<string>();
            for (int i = 0; i < catalog.Count; i++)
            {
                var item = catalog.Get(i);
                if (item == null || string.IsNullOrWhiteSpace(item.ItemId) || !ids.Add(item.ItemId) || item.WorldAppearance == null)
                { Debug.LogError("物品目录包含空条目、重复 ID 或缺失外观。", catalog); errors++; }
                else if (item.WorldAppearance.GetComponentInChildren<NetworkObject>() != null)
                { Debug.LogError("物品外观应为普通模型 Prefab，不要添加 NetworkObject。", item); errors++; }
                if (item != null && item.Category == ItemCategory.Weapon)
                {
                    try
                    {
                        if (item.StackLimit != 1 || item.Weapon == null || item.Weapon.Appearance == null)
                            throw new System.InvalidOperationException("武器需要主手外观和战斗定义，堆叠上限应为 1。");
                        item.Weapon.CreateSettings();
                        if (item.Weapon.AttackPattern != null && item.Weapon.AttackPattern.AnimatorController == null)
                            throw new System.InvalidOperationException("攻击模式缺少 Animator Controller。");
                    }
                    catch (System.Exception error) { Debug.LogError(error.Message, item); errors++; }
                }
            }
            for (int i = 0; i < catalog.Count; i++)
            {
                var item = catalog.Get(i);
                if (item == null || item.Category != ItemCategory.Food) continue;
                try
                {
                    if (item.Food == null) throw new System.InvalidOperationException("食物缺少 Food Effect。");
                    item.Food.CreateModifiers("validation");
                }
                catch (System.Exception error) { Debug.LogError(error.Message, item); errors++; }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:LootTable"))
            {
                var table = AssetDatabase.LoadAssetAtPath<LootTable>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var entry in table.Entries)
                    if (entry == null || entry.item == null || catalog.IndexOf(entry.item) < 0 || entry.minimum < 1 || entry.maximum < entry.minimum)
                    { Debug.LogError("掉落表有未注册物品或非法数量范围。", table); errors++; }
            }
            var pickup = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Items/NetworkWorldPickup.prefab");
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/Game/Prefabs/GameNetworkPrefabs.asset");
            bool registered = false;
            if (list != null) foreach (var entry in list.PrefabList) if (entry.Prefab == pickup) registered = true;
            if (pickup == null || pickup.GetComponent<NetworkWorldPickup>() == null || pickup.GetComponent<NetworkWorldPickup>().Catalog != catalog || !registered)
            { Debug.LogError("掉落物网络 Prefab 缺失、目录不匹配或未注册。"); errors++; }
            Debug.Log("物品目录校验：" + catalog.Count + " 种物品，" + errors + " 个错误。");
        }
    }
}
