using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Presentation.World
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class QuestGroundArrowView : MonoBehaviour
    {
        [SerializeField] private Color objectiveColor = new Color(.25f, .85f, 1f, .85f);
        [SerializeField] private Color returnColor = new Color(1f, .8f, .2f, .85f);
        [SerializeField, Min(.01f)] private float groundOffset = .12f;
        [SerializeField, Min(.1f)] private float size = 1.25f;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _properties;
        private Mesh _ownedMesh;
        public bool IsVisible => _renderer != null && _renderer.enabled;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _properties = new MaterialPropertyBlock();
            var filter = GetComponent<MeshFilter>();
            if (filter.sharedMesh == null) filter.sharedMesh = _ownedMesh = CreateArrowMesh();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            Hide();
        }

        public static Mesh CreateArrowMesh()
        {
            var mesh = new Mesh { name = "QuestGroundArrow" };
            mesh.vertices = new[]
            {
                new Vector3(-.16f, 0, -.6f), new Vector3(.16f, 0, -.6f),
                new Vector3(.16f, 0, .02f), new Vector3(.5f, 0, .02f),
                new Vector3(0, 0, .8f), new Vector3(-.5f, 0, .02f), new Vector3(-.16f, 0, .02f)
            };
            mesh.triangles = new[] { 0, 6, 2, 0, 2, 1, 5, 4, 3 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        public void Show(Vector3 position, Vector3 direction, Vector3 normal, bool returning)
        {
            if (_renderer == null || direction.sqrMagnitude < .0001f) { Hide(); return; }
            direction = Vector3.ProjectOnPlane(direction, normal).normalized;
            if (direction.sqrMagnitude < .0001f) { Hide(); return; }
            float phase = Time.unscaledTime * 4f;
            transform.position = position + normal * groundOffset + direction * (Mathf.Sin(phase) * .08f);
            var rotation = Quaternion.LookRotation(direction, normal);
            transform.rotation = IsVisible ? Quaternion.Slerp(transform.rotation, rotation, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime)) : rotation;
            transform.localScale = Vector3.one * (size * (1f + .07f * Mathf.Sin(phase)));
            Color color = returning ? returnColor : objectiveColor;
            color.a *= .9f + .1f * Mathf.Sin(phase);
            _properties.SetColor("_BaseColor", color);
            _properties.SetColor("_Color", color);
            _renderer.SetPropertyBlock(_properties);
            _renderer.enabled = true;
        }

        public void Hide() { if (_renderer != null) _renderer.enabled = false; }
        private void OnDisable() => Hide();
        private void OnDestroy() { if (_ownedMesh != null) Destroy(_ownedMesh); }
    }
}
