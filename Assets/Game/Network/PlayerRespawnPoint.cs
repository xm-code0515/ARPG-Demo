using UnityEngine;

namespace Game.Network
{
    [DisallowMultipleComponent]
    public sealed class PlayerRespawnPoint : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float spacing = 3f;
        [SerializeField] private bool snapToGround = true;
        [SerializeField] private LayerMask groundMask = 1;
        [SerializeField, Min(1f)] private float groundProbeHeight = 10f;
        [SerializeField, Min(0f)] private float groundOffset = .1f;

        public void GetPose(int slot, out Vector3 position, out Quaternion rotation)
        {
            position = transform.TransformPoint(new Vector3(slot % 4, 0f, slot / 4) * spacing);
            rotation = transform.rotation;
            if (snapToGround && Physics.Raycast(position + Vector3.up * groundProbeHeight,
                Vector3.down, out RaycastHit hit, groundProbeHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
                position.y = hit.point.y + groundOffset;
        }
    }
}
