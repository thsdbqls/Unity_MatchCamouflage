using MatchCamouflage.GameModes;
using Unity.Netcode;
using UnityEngine;

namespace MatchCamouflage.Player
{
    // 총 발사는 반드시 서버에서 판정한다(클라이언트가 보낸 좌표를 그대로 믿지 않기 위해
    // 서버가 들고 있는 플레이어 transform 기준으로 레이캐스트를 쏜다).
    [RequireComponent(typeof(PlayerState))]
    public class PlayerShooter : NetworkBehaviour
    {
        [SerializeField] private Transform muzzle;
        [SerializeField] private float range = 60f;
        [SerializeField] private float fireCooldown = 0.5f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private float tracerDuration = 0.08f;
        [SerializeField] private float tracerWidth = 0.03f;

        private static Material s_TracerMaterial;

        private PlayerState _state;
        private float _lastFireTime;

        private void Awake()
        {
            _state = GetComponent<PlayerState>();
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (_state.IsJailed.Value || _state.IsEliminated.Value) return;

            if (Input.GetButtonDown("Fire1") && Time.time - _lastFireTime >= fireCooldown)
            {
                _lastFireTime = Time.time;
                RequestFireServerRpc();
            }
        }

        [ServerRpc]
        private void RequestFireServerRpc(ServerRpcParams rpcParams = default)
        {
            Transform origin = muzzle != null ? muzzle : transform;
            Vector3 endPoint = origin.position + origin.forward * range;

            if (Physics.Raycast(origin.position, origin.forward, out RaycastHit hit, range, hitMask))
            {
                endPoint = hit.point;
                var targetState = hit.collider.GetComponentInParent<PlayerState>();
                if (targetState != null && targetState != _state)
                {
                    GameModeManager.Instance.ServerResolveHit(OwnerClientId, targetState);
                }
            }

            PlayFireEffectClientRpc(origin.position, endPoint, _state.BodyColor.Value);
        }

        [ClientRpc]
        private void PlayFireEffectClientRpc(Vector3 origin, Vector3 endPoint, Color tracerColor)
        {
            SpawnTracer(origin, endPoint, tracerColor);
        }

        // 모든 클라이언트 화면에 보이는 총알 궤적(트레이서). 쏜 사람의 현재 위장 색으로 나타난다.
        private void SpawnTracer(Vector3 start, Vector3 end, Color color)
        {
            var go = new GameObject("Tracer");
            var lineRenderer = go.AddComponent<LineRenderer>();
            lineRenderer.material = GetTracerMaterial();
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
            lineRenderer.startWidth = tracerWidth;
            lineRenderer.endWidth = tracerWidth * 0.3f;

            var startColor = color;
            startColor.a = 1f;
            var endColor = color;
            endColor.a = 0f;
            lineRenderer.startColor = startColor;
            lineRenderer.endColor = endColor;
            lineRenderer.numCapVertices = 4;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Destroy(go, tracerDuration);
        }

        private static Material GetTracerMaterial()
        {
            if (s_TracerMaterial == null)
            {
                // URP Unlit은 LineRenderer의 start/endColor(버텍스 컬러)를 무시해서 항상 흰색으로 보였다.
                // Sprites/Default는 버텍스 컬러를 그대로 곱해서 반영하므로 트레이서 색이 제대로 보인다.
                var shader = Shader.Find("Sprites/Default");
                s_TracerMaterial = new Material(shader);
            }
            return s_TracerMaterial;
        }
    }
}
