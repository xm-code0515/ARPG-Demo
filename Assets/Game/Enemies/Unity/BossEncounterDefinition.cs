using UnityEngine;

namespace Game.Enemies.Unity
{
    [CreateAssetMenu(menuName = "Game/Enemies/Boss Encounter")]
    public sealed class BossEncounterDefinition : ScriptableObject
    {
        [SerializeField, Min(1f)] private float healthBarRange = 28f;
        [SerializeField, Min(0f)] private float betweenAttacks = .65f;
        [SerializeField, Min(.1f)] private float recoveryDuration = 8f;
        [SerializeField, Min(0f)] private float summonTime = 1.5f;
        public float HealthBarRange => healthBarRange;
        public float BetweenAttacks => betweenAttacks;
        public float RecoveryDuration => recoveryDuration;
        public float SummonTime => summonTime;
    }
}
