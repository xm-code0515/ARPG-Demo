using System;

namespace Game.Boss
{
    // The host selects each attack once per round. Combat confirms the actual start/end.
    public sealed class BossAttackCycle
    {
        private readonly Random _random;
        private readonly int _attackCount;
        private readonly float _shortCooldown, _recoveryDuration;
        private int _availableMask;
        private int _prepared = -1;
        private bool _inFlight;
        public float CooldownRemaining { get; private set; }
        public bool Recovering { get; private set; }
        public bool CanAttack => !_inFlight && CooldownRemaining <= 0f;
        public int PreparedAttack => _prepared;

        public BossAttackCycle(int attackCount, float shortCooldown, float recoveryDuration, int seed)
        {
            if (attackCount < 1 || attackCount > 30 || shortCooldown < 0f || recoveryDuration <= 0f)
                throw new ArgumentOutOfRangeException();
            _attackCount = attackCount;
            _shortCooldown = shortCooldown;
            _recoveryDuration = recoveryDuration;
            _random = new Random(seed);
            Reset();
        }

        public void Reset()
        {
            _availableMask = (1 << _attackCount) - 1;
            _prepared = -1;
            _inFlight = Recovering = false;
            CooldownRemaining = 0f;
        }

        public void Tick(float deltaTime)
        {
            CooldownRemaining = Math.Max(0f, CooldownRemaining - deltaTime);
            if (Recovering && CooldownRemaining <= 0f)
            {
                Recovering = false;
                _availableMask = (1 << _attackCount) - 1;
            }
        }

        public int PrepareAttack()
        {
            if (!CanAttack) return -1;
            if (_prepared >= 0) return _prepared;
            int remaining = 0;
            for (int i = 0; i < _attackCount; i++) if ((_availableMask & (1 << i)) != 0) remaining++;
            int choice = _random.Next(remaining);
            for (int i = 0; i < _attackCount; i++)
                if ((_availableMask & (1 << i)) != 0 && choice-- == 0) { _prepared = i; break; }
            return _prepared;
        }

        public void AttackStarted(int index)
        {
            if (!CanAttack || index != _prepared) return;
            _availableMask &= ~(1 << index);
            _prepared = -1;
            _inFlight = true;
        }

        public void AttackFinished()
        {
            if (!_inFlight) return;
            _inFlight = false;
            Recovering = _availableMask == 0;
            CooldownRemaining = Recovering ? _recoveryDuration : _shortCooldown;
        }
    }
}
