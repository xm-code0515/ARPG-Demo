using Game.Presentation.UI;
using UnityEngine;

namespace Game.Network
{
    public sealed class NetworkPlayerDeathScreenBinder : MonoBehaviour
    {
        [SerializeField] private PlayerDeathScreen screen;

        private void Update()
        {
            if (screen == null) return;
            NetworkCharacter player = NetworkCharacter.LocalPlayer;
            NetworkPlayerRespawn source = player != null && player.IsSpawned && player.IsOwner
                ? player.GetComponent<NetworkPlayerRespawn>() : null;
            if (!ReferenceEquals(screen.BoundSource, source)) screen.Bind(source);
        }

        private void OnDisable()
        {
            if (screen != null) screen.Bind(null);
        }
    }
}
