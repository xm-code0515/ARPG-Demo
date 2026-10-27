using UnityEngine;

namespace Game.Core
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            Debug.Log("Game Bootstrap initialized.");
        }
    }
}