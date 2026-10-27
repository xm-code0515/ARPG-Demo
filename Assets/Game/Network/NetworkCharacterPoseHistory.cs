using System;
using System.Collections.Generic;
using Game.Character;
using UnityEngine;

namespace Game.Network
{
    public sealed class NetworkCharacterPoseHistory
    {
        private const int Capacity = CharacterPredictionBuffer.Capacity + 2;
        private readonly List<NetworkMovementSnapshot> _states = new List<NetworkMovementSnapshot>(Capacity);

        public void Clear() => _states.Clear();

        public void Add(NetworkMovementSnapshot snapshot)
        {
            if (!snapshot.HasState || (_states.Count > 0 && snapshot.ServerTime <= _states[_states.Count - 1].ServerTime))
                return;
            _states.Add(snapshot);
            if (_states.Count > Capacity) _states.RemoveAt(0);
        }

        public bool TrySample(double time, bool extrapolate, out CharacterMotorState state)
        {
            state = default;
            if (_states.Count == 0) return false;
            if (time <= _states[0].ServerTime)
            {
                state = _states[0].ToMotorState();
                return true;
            }
            for (int i = 1; i < _states.Count; i++)
            {
                NetworkMovementSnapshot to = _states[i];
                if (time > to.ServerTime) continue;
                NetworkMovementSnapshot from = _states[i - 1];
                float factor = (float)((time - from.ServerTime) / (to.ServerTime - from.ServerTime));
                if ((to.Position - from.Position).sqrMagnitude > 4f)
                    state = factor >= 1f ? to.ToMotorState() : from.ToMotorState();
                else
                {
                    NetworkMovementSnapshot nearest = factor < 0.5f ? from : to;
                    state = new CharacterMotorState(Vector3.Lerp(from.Position, to.Position, factor),
                        Quaternion.Slerp(from.Rotation, to.Rotation, factor),
                        Vector3.Lerp(from.Velocity, to.Velocity, factor),
                        Mathf.Lerp(from.MotorVerticalVelocity, to.MotorVerticalVelocity, factor),
                        nearest.IsGrounded, nearest.IsSprinting, nearest.KnockbackVelocity, nearest.KnockbackRemaining);
                }
                return true;
            }
            NetworkMovementSnapshot latest = _states[_states.Count - 1];
            state = latest.ToMotorState();
            if (extrapolate)
            {
                float ahead = (float)Math.Min(0.1, Math.Max(0, time - latest.ServerTime));
                Vector3 velocity = latest.Velocity;
                if (latest.IsGrounded) velocity.y = 0f;
                state = new CharacterMotorState(latest.Position + velocity * ahead, latest.Rotation,
                    latest.Velocity, latest.MotorVerticalVelocity, latest.IsGrounded, latest.IsSprinting,
                    latest.KnockbackVelocity, latest.KnockbackRemaining);
            }
            return true;
        }
    }
}
