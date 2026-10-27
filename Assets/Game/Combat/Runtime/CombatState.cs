using System;

namespace Game.Combat
{
    public struct CombatState : IEquatable<CombatState>
    {
        public bool Attacking;
        public bool HitApplied;
        public bool Buffered;
        public int AttackIndex;
        public int NextAttackIndex;
        public uint AttackSequence;
        public float Elapsed;
        public uint HitSequence;
        public float HitElapsed;
        public float HitStopRemaining;
        public bool Dodging;
        public bool DodgeRewarded;
        public uint DodgeSequence;
        public float DodgeElapsed;
        public float DodgeCooldownRemaining;
        public float DodgeDirectionX, DodgeDirectionZ;
        public float DodgeOriginX, DodgeOriginY, DodgeOriginZ;

        public bool Equals(CombatState other) => Attacking == other.Attacking && HitApplied == other.HitApplied &&
            Buffered == other.Buffered && AttackIndex == other.AttackIndex && NextAttackIndex == other.NextAttackIndex &&
            AttackSequence == other.AttackSequence && Elapsed.Equals(other.Elapsed) &&
            HitSequence == other.HitSequence && HitElapsed.Equals(other.HitElapsed) && HitStopRemaining.Equals(other.HitStopRemaining) &&
            Dodging == other.Dodging && DodgeRewarded == other.DodgeRewarded && DodgeSequence == other.DodgeSequence &&
            DodgeElapsed.Equals(other.DodgeElapsed) && DodgeCooldownRemaining.Equals(other.DodgeCooldownRemaining) &&
            DodgeDirectionX.Equals(other.DodgeDirectionX) && DodgeDirectionZ.Equals(other.DodgeDirectionZ) &&
            DodgeOriginX.Equals(other.DodgeOriginX) && DodgeOriginY.Equals(other.DodgeOriginY) && DodgeOriginZ.Equals(other.DodgeOriginZ);
        public bool Matches(CombatState other) => Attacking == other.Attacking && HitApplied == other.HitApplied &&
            Buffered == other.Buffered && AttackIndex == other.AttackIndex && NextAttackIndex == other.NextAttackIndex &&
            AttackSequence == other.AttackSequence && Math.Abs(Elapsed - other.Elapsed) < .005f &&
            HitSequence == other.HitSequence && Math.Abs(HitElapsed - other.HitElapsed) < .005f &&
            Math.Abs(HitStopRemaining - other.HitStopRemaining) < .005f &&
            Dodging == other.Dodging && DodgeRewarded == other.DodgeRewarded && DodgeSequence == other.DodgeSequence &&
            Math.Abs(DodgeElapsed - other.DodgeElapsed) < .005f && Math.Abs(DodgeCooldownRemaining - other.DodgeCooldownRemaining) < .005f &&
            Math.Abs(DodgeDirectionX - other.DodgeDirectionX) < .005f && Math.Abs(DodgeDirectionZ - other.DodgeDirectionZ) < .005f &&
            Math.Abs(DodgeOriginX - other.DodgeOriginX) < .015f && Math.Abs(DodgeOriginY - other.DodgeOriginY) < .015f &&
            Math.Abs(DodgeOriginZ - other.DodgeOriginZ) < .015f;
        public override bool Equals(object obj) => obj is CombatState other && Equals(other);
        public override int GetHashCode() => AttackSequence.GetHashCode();
    }

    public readonly struct AttackSettings
    {
        public readonly float Duration;
        public readonly float HitTime;
        public readonly float StaminaCost;
        public readonly float BaseDamage;
        public readonly float AttackPowerScale;
        public readonly float ForwardDistance;
        public readonly float MovementStart;
        public readonly float MovementEnd;
        private readonly float[] _movementCurve;
        private readonly float[] _rootRight, _rootForward;

        public AttackSettings(float duration, float hitTime, float staminaCost, float baseDamage, float attackPowerScale,
            float forwardDistance = 0f, float movementStart = 0f, float movementEnd = 0f, float[] movementCurve = null,
            float[] rootRight = null, float[] rootForward = null)
        {
            if (!Finite(duration) || duration <= 0f || !Finite(hitTime) || hitTime < 0f || hitTime >= duration ||
                !Finite(staminaCost) || staminaCost < 0f || !Finite(baseDamage) || baseDamage < 0f ||
                !Finite(attackPowerScale) || attackPowerScale < 0f) throw new ArgumentOutOfRangeException(nameof(duration));
            Duration = duration;
            HitTime = hitTime;
            StaminaCost = staminaCost;
            BaseDamage = baseDamage;
            AttackPowerScale = attackPowerScale;
            if (!Finite(forwardDistance) || forwardDistance < 0f ||
                !Finite(movementStart) || !Finite(movementEnd) ||
                (forwardDistance > 0f && (movementStart < 0f || movementEnd <= movementStart || movementEnd > duration)))
                throw new ArgumentOutOfRangeException(nameof(forwardDistance));
            ForwardDistance = forwardDistance;
            MovementStart = movementStart;
            MovementEnd = movementEnd;
            _movementCurve = movementCurve == null ? null : (float[])movementCurve.Clone();
            _rootRight = CopyTrajectory(rootRight);
            _rootForward = CopyTrajectory(rootForward);
            if (_movementCurve != null)
            {
                if (_movementCurve.Length < 2 || _movementCurve[0] != 0f || _movementCurve[_movementCurve.Length - 1] != 1f)
                    throw new ArgumentException("Movement curve must start at zero and end at one.", nameof(movementCurve));
                for (int i = 0; i < _movementCurve.Length; i++)
                    if (!Finite(_movementCurve[i]) || _movementCurve[i] < 0f || _movementCurve[i] > 1f ||
                        (i > 0 && _movementCurve[i] < _movementCurve[i - 1]))
                        throw new ArgumentException("Movement curve must be finite and nondecreasing.", nameof(movementCurve));
            }
        }

        public float MovementBetween(float fromTime, float toTime) => toTime <= fromTime ? 0f : _rootForward != null
            ? SampleTrajectory(_rootForward, toTime) - SampleTrajectory(_rootForward, fromTime)
            : ForwardDistance <= 0f ? 0f : ForwardDistance * (MovementProgress(toTime) - MovementProgress(fromTime));
        public float RightMovementBetween(float fromTime, float toTime) => _rootRight == null || toTime <= fromTime ? 0f
            : SampleTrajectory(_rootRight, toTime) - SampleTrajectory(_rootRight, fromTime);
        private static float[] CopyTrajectory(float[] samples)
        {
            if (samples == null) return null;
            if (samples.Length < 2 || samples[0] != 0) throw new ArgumentException("Trajectory must start at zero.");
            foreach (float sample in samples) if (!Finite(sample)) throw new ArgumentException("Trajectory must be finite.");
            return (float[])samples.Clone();
        }
        private float SampleTrajectory(float[] samples, float time)
        {
            float position = Math.Max(0, Math.Min(1, time / Duration)) * (samples.Length - 1);
            int index = Math.Min((int)position, samples.Length - 2);
            return samples[index] + (samples[index + 1] - samples[index]) * (position - index);
        }

        private float MovementProgress(float time)
        {
            float progress = Math.Max(0f, Math.Min(1f, (time - MovementStart) / (MovementEnd - MovementStart)));
            if (_movementCurve == null) return progress * progress * (3f - 2f * progress);
            float sample = progress * (_movementCurve.Length - 1);
            int index = Math.Min((int)sample, _movementCurve.Length - 2);
            return _movementCurve[index] + (_movementCurve[index + 1] - _movementCurve[index]) * (sample - index);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct CombatStep
    {
        public readonly CombatState State;
        public readonly float Cost;
        public readonly bool ResolveHit;
        public readonly float ForwardMovement;
        public readonly float RightMovement;
        public CombatStep(CombatState state, float cost, bool resolveHit, float forwardMovement = 0f, float rightMovement = 0f)
        { State = state; Cost = cost; ResolveHit = resolveHit; ForwardMovement = forwardMovement; RightMovement = rightMovement; }
    }
}
