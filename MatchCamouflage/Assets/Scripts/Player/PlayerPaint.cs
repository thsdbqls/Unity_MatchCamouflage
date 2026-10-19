using Unity.Netcode;
using UnityEngine;

namespace MatchCamouflage.Player
{
    // 벽/바닥의 색을 묻혀서 위장하는 로직.
    // 대상 표면에 Paintable 컴포넌트를 붙이고 표면 색을 지정해두면,
    // 플레이어가 근처에서 칠하기 버튼을 누르고 있는 동안 몸 색이 그 색으로 서서히 변한다.
    [RequireComponent(typeof(PlayerState))]
    public class PlayerPaint : NetworkBehaviour
    {
        [SerializeField] private Transform paintRayOrigin;
        [SerializeField] private float paintRange = 2.5f;
        [SerializeField] private float paintDuration = 1.2f;
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private KeyCode paintKey = KeyCode.F;

        private PlayerState _state;
        private Color _paintStartColor;
        private float _paintTimer;
        private bool _isPainting;
        private MaterialPropertyBlock _mpb;

        private void Awake()
        {
            _state = GetComponent<PlayerState>();
            _mpb = new MaterialPropertyBlock();
        }

        public override void OnNetworkSpawn()
        {
            _state.BodyColor.OnValueChanged += OnBodyColorChanged;
            ApplyColor(_state.BodyColor.Value);
        }

        public override void OnNetworkDespawn()
        {
            _state.BodyColor.OnValueChanged -= OnBodyColorChanged;
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (_state.IsJailed.Value || _state.IsEliminated.Value)
            {
                _isPainting = false;
                return;
            }

            if (Input.GetKey(paintKey) && TryGetPaintTarget(out Color targetColor))
            {
                if (!_isPainting)
                {
                    _isPainting = true;
                    _paintTimer = 0f;
                    _paintStartColor = _state.BodyColor.Value;
                }

                _paintTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_paintTimer / paintDuration);
                Color blended = Color.Lerp(_paintStartColor, targetColor, t);

                // 오너가 자기 색을 바꿀 권한을 가짐(PlayerState.BodyColor는 Owner Write 권한).
                _state.BodyColor.Value = blended;
            }
            else
            {
                _isPainting = false;
            }
        }

        private bool TryGetPaintTarget(out Color color)
        {
            color = Color.white;
            Transform origin = paintRayOrigin != null ? paintRayOrigin : transform;
            if (Physics.Raycast(origin.position, origin.forward, out RaycastHit hit, paintRange))
            {
                var paintable = hit.collider.GetComponent<Paintable>();
                if (paintable != null)
                {
                    color = paintable.SurfaceColor;
                    return true;
                }
            }
            return false;
        }

        private void OnBodyColorChanged(Color previous, Color current)
        {
            ApplyColor(current);
        }

        private void ApplyColor(Color color)
        {
            if (bodyRenderer == null) return;
            bodyRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", color); // URP Lit 셰이더 기준. Built-in이면 "_Color"로 변경.
            bodyRenderer.SetPropertyBlock(_mpb);
        }
    }
}
