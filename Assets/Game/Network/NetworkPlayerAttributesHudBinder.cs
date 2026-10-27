using Game.Attributes.Unity;
using Game.Presentation.UI;
using UnityEngine;

namespace Game.Network
{
    [DisallowMultipleComponent]
    public sealed class NetworkPlayerAttributesHudBinder : MonoBehaviour
    {
        [SerializeField] private PlayerAttributesHud hud;
        [SerializeField] private PlayerFoodHud foodHud;

        private void Awake()
        {
            if (hud == null)
            {
                Debug.LogError("[NetworkPlayerAttributesHudBinder] 请引用场景中自己配置的 Player Attributes Hud。", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (hud == null || !hud.isActiveAndEnabled) return;
            NetworkCharacter player = NetworkCharacter.LocalPlayer;
            CharacterAttributes source = player != null && player.IsSpawned && player.IsOwner
                ? player.GetComponent<CharacterAttributes>() : null;
            if (!ReferenceEquals(hud.BoundSource, source)) hud.Bind(source);
            if (foodHud != null) foodHud.Bind(source != null ? source.GetComponent<NetworkFoods>() : null);
        }

        private void OnDisable()
        {
            if (hud != null) hud.Unbind();
            if (foodHud != null) foodHud.Bind(null);
        }
    }
}
