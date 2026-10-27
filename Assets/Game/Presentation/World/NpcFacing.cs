using UnityEngine;

namespace Game.Presentation.World
{
    public sealed class NpcFacing : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float turnSpeed = 540f;
        [SerializeField] private Transform facingReference;
        private Quaternion _targetRotation;
        private bool _turning;

        public void Face(Transform actor)
        {
            if (actor == null) return;
            var direction = Vector3.ProjectOnPlane(actor.position - transform.position, Vector3.up);
            if (direction.sqrMagnitude < .0001f) return;
            var localForward = facingReference != null
                ? Vector3.ProjectOnPlane(transform.InverseTransformDirection(facingReference.forward), Vector3.up)
                : Vector3.forward;
            var modelOffset = localForward.sqrMagnitude > .0001f ? Quaternion.LookRotation(localForward) : Quaternion.identity;
            _targetRotation = Quaternion.LookRotation(direction) * Quaternion.Inverse(modelOffset);
            _turning = true;
        }

        private void LateUpdate()
        {
            if (!_turning) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, _targetRotation, turnSpeed * Time.deltaTime);
            if (Quaternion.Angle(transform.rotation, _targetRotation) < .01f) _turning = false;
        }

        private void OnDisable() => _turning = false;
    }
}
