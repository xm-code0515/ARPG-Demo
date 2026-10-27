using Game.Attributes.Unity;
using Game.Items;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Network
{
    public sealed class EnemyLootDropper : MonoBehaviour
    {
        [SerializeField] private GameObject pickupPrefab;
        private CharacterAttributes _attributes;
        private NetworkEnemy _enemy;
        private bool _dropped;
        private void Awake()
        {
            _attributes = GetComponent<CharacterAttributes>();
            _enemy = GetComponent<NetworkEnemy>();
            _attributes.Died += Drop;
        }
        private void OnDestroy() { if (_attributes != null) _attributes.Died -= Drop; }
        private void Drop()
        {
            if (_dropped || _enemy == null || !_enemy.IsSpawned || !_enemy.IsServer || _enemy.NetworkManager.ShutdownInProgress) return;
            _dropped = true;
            var table = _enemy.Definition != null ? _enemy.Definition.Loot : null;
            if (table == null) return;
            if (pickupPrefab == null || !_enemy.NetworkManager.NetworkConfig.Prefabs.Contains(pickupPrefab))
            { Debug.LogError("[EnemyLootDropper] 请配置并注册掉落物网络 Prefab。", this); return; }
            table.Roll(Spawn);
        }
        private void Spawn(ItemDefinition item, int quantity)
        {
            var template = pickupPrefab.GetComponent<NetworkWorldPickup>();
            if (template == null || template.Catalog == null || template.Catalog.IndexOf(item) < 0)
            { Debug.LogError("[EnemyLootDropper] 掉落物品尚未加入网络掉落物引用的物品目录。", this); return; }
            Vector2 offset = Random.insideUnitCircle * .5f;
            Vector3 position = transform.position + new Vector3(offset.x, .15f, offset.y);
            NetworkWorldPickup.SpawnOrMerge(_enemy.NetworkManager, pickupPrefab, item, quantity, NetworkWorldPickup.GroundPosition(position), gameObject.scene);
        }
    }
}
