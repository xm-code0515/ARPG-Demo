using Game.Attributes.Unity;
using Game.Presentation.UI;
using UnityEngine;

namespace Game.Network
{
    public sealed class BossHealthBarBinder : MonoBehaviour
    {
        [SerializeField] private BossHealthBarView view;
        public BossEncounterController CurrentBoss { get; private set; }
        private void LateUpdate()
        {
            if (view == null) return;
            var player = NetworkCharacter.LocalPlayer;
            var attributes = player != null ? player.GetComponent<CharacterAttributes>() : null;
            CurrentBoss = null;
            if (player != null && player.IsSpawned && attributes != null && attributes.IsReady && !attributes.Snapshot.IsDead)
            {
                float nearest = float.MaxValue;
                foreach (var boss in BossEncounterController.ActiveBosses)
                {
                    if (boss == null || !boss.IsSpawned || boss.Attributes == null || !boss.Attributes.IsReady || boss.Attributes.Snapshot.IsDead) continue;
                    float distance = (boss.transform.position - player.transform.position).sqrMagnitude;
                    if (distance <= boss.HealthBarRange * boss.HealthBarRange && distance < nearest) { nearest = distance; CurrentBoss = boss; }
                }
            }
            if (CurrentBoss != null) view.Show(CurrentBoss.DisplayName, CurrentBoss.Attributes);
            else view.Hide();
        }
        private void OnDisable()
        {
            CurrentBoss = null;
            if (view != null) view.Hide();
        }
    }
}
