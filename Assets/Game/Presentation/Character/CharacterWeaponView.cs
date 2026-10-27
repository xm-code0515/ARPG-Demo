using Game.Combat.Unity;
using UnityEngine;

namespace Game.Presentation.Character
{
    public sealed class CharacterWeaponView : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        private CharacterCombatController _combat;
        private WeaponDefinition _weapon;
        private GameObject _appearance;
        private Transform _hand;

        private void Awake()
        {
            _combat = GetComponent<CharacterCombatController>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
        }

        private void LateUpdate()
        {
            if (_combat == null || animator == null || !animator.enabled || !animator.isHuman) return;
            if (_hand == null) _hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (_hand == null || _weapon == _combat.CurrentWeapon) return;
            _weapon = _combat.CurrentWeapon;
            if (_appearance != null) Destroy(_appearance);
            if (_weapon.Appearance == null) return;
            _appearance = Instantiate(_weapon.Appearance, _hand, false);
            _appearance.name = "HeldWeapon";
            _appearance.transform.localPosition = _weapon.HeldPosition;
            _appearance.transform.localRotation = Quaternion.Euler(_weapon.HeldEulerAngles);
            _appearance.transform.localScale = _weapon.HeldScale;
            foreach (var collider in _appearance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }
    }
}
