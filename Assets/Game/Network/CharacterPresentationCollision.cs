using System;
using UnityEngine;

namespace Game.Network
{
    public static class CharacterPresentationCollision
    {
        public static Vector3 Separate(Vector3 position, Vector3 centerOffset, float radius, float height,
            Vector3 otherCenter, float otherRadius, float otherHeight, bool preferPositiveX)
        {
            Vector3 center = position + centerOffset;
            float separation = radius + otherRadius;
            float axisHalfHeight = Math.Max(0f, height * 0.5f - radius);
            float otherAxisHalfHeight = Math.Max(0f, otherHeight * 0.5f - otherRadius);
            float verticalGap = Math.Max(0f, Math.Abs(center.y - otherCenter.y) - axisHalfHeight - otherAxisHalfHeight);
            if (verticalGap >= separation) return position;
            separation = (float)Math.Sqrt(separation * separation - verticalGap * verticalGap);
            Vector3 delta = center - otherCenter;
            delta.y = 0f;
            float distanceSquared = delta.sqrMagnitude;
            if (distanceSquared >= separation * separation) return position;
            if (distanceSquared < 0.000001f)
                return position + new Vector3(preferPositiveX ? separation : -separation, 0f, 0f);
            float distance = (float)Math.Sqrt(distanceSquared);
            return position + delta * ((separation - distance) / distance);
        }
    }
}
