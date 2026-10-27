using UnityEngine;

namespace Game.Combat.Unity
{
    public enum CharacterTeam { Neutral, Player, Enemy }

    [DisallowMultipleComponent]
    public sealed class CharacterFaction : MonoBehaviour
    {
        [SerializeField] private CharacterTeam team;
        public CharacterTeam Team => team;
        public void Configure(CharacterTeam value) => team = value;

        public static bool CanDamage(CharacterCombatController source, CharacterCombatController target, bool friendlyFire)
        {
            var from = source.GetComponent<CharacterFaction>();
            var to = target.GetComponent<CharacterFaction>();
            return friendlyFire || from == null || to == null || from.Team != to.Team;
        }
    }
}
