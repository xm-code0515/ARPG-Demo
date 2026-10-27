using UnityEngine;

namespace Game.Character.Actions
{
    [CreateAssetMenu(menuName = "Game/Character/Action Settings", fileName = "CharacterActions")]
    public sealed class CharacterActionDefinition : ScriptableObject
    {
        [SerializeField, Min(0f)] private float jumpCost = 15f;
        [SerializeField, Min(.01f)] private float normalJumpHeight = 1.5f;
        [SerializeField, Min(.01f)] private float tiredJumpHeight = .2f;
        [SerializeField, Min(0f)] private float sprintCostPerSecond = 10f;
        [SerializeField, Min(1f)] private float sprintMultiplier = 1.6f;
        [SerializeField, Min(0f)] private float recoveryPerSecond = 12f;
        [SerializeField, Min(0f)] private float recoveryDelay = 1.25f;
        [SerializeField, Min(0f)] private float sprintResumeStamina = 10f;

        public CharacterActionSettings CreateSettings() => new CharacterActionSettings(jumpCost, normalJumpHeight,
            tiredJumpHeight, sprintCostPerSecond, sprintMultiplier, recoveryPerSecond, recoveryDelay, sprintResumeStamina);
    }
}
