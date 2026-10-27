using Game.Attributes;
using Game.Attributes.Unity;
using UnityEngine;

namespace Game.Presentation.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerAttributesHud : MonoBehaviour
    {
        [SerializeField] private AttributeBarView healthBar;
        [SerializeField] private AttributeBarView staminaBar;
        [SerializeField] private CanvasGroup visibilityGroup;
        [SerializeField] private CharacterAttributes standaloneSource;

        public IAttributeReadModel BoundSource { get; private set; }

        private void OnEnable()
        {
            if (standaloneSource != null) Bind(standaloneSource);
            else Refresh(default);
        }

        private void OnDisable() => Unbind();

        public void Bind(IAttributeReadModel source)
        {
            if (ReferenceEquals(BoundSource, source)) return;
            Unbind();
            BoundSource = source;
            if (BoundSource != null) BoundSource.Changed += Refresh;
            Refresh(BoundSource != null && BoundSource.IsReady ? BoundSource.Snapshot : default);
        }

        public void Unbind()
        {
            if (BoundSource != null) BoundSource.Changed -= Refresh;
            BoundSource = null;
            Refresh(default);
        }

        private void Refresh(AttributeSnapshot snapshot)
        {
            bool ready = BoundSource != null && BoundSource.IsReady;
            if (visibilityGroup != null) visibilityGroup.alpha = ready ? 1f : 0f;
            if (!ready)
            {
                if (healthBar != null) healthBar.Clear();
                if (staminaBar != null) staminaBar.Clear();
                return;
            }
            if (healthBar != null) healthBar.SetValue(snapshot.Health, snapshot.Values.MaxHealth);
            if (staminaBar != null)
            {
                if (snapshot.HasStaminaState) staminaBar.SetValue(snapshot.Stamina, snapshot.Values.MaxStamina);
                else staminaBar.Clear();
            }
        }
    }
}
