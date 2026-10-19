using MatchCamouflage.Player;
using Unity.Netcode;
using UnityEngine;

namespace MatchCamouflage.Jail
{
    // 감옥 트리거. 투옥된 플레이어는 여기로 텔레포트되어 대기한다.
    // 투옥되지 않은 Hider가 이 구역 반경 안에 들어오면 투옥된 인원 전원을 자동으로 풀어준다.
    public class JailZone : NetworkBehaviour
    {
        [SerializeField] private Transform[] cellPoints;
        [SerializeField] private float rescueRadius = 2f;

        public Transform GetCellPoint(int index)
        {
            if (cellPoints == null || cellPoints.Length == 0) return transform;
            return cellPoints[index % cellPoints.Length];
        }

        private void Update()
        {
            if (!IsServer) return;
            // 간단한 폴링 방식 구제 체크. 플레이어 수가 많아지면 OverlapSphere 호출 비용을 고려해
            // 일정 간격(예: 0.2초)으로만 돌리도록 바꿔도 된다.
            if (Time.frameCount % 12 != 0) return;

            Collider[] hits = Physics.OverlapSphere(transform.position, rescueRadius);
            bool rescuerPresent = false;
            foreach (var hit in hits)
            {
                var state = hit.GetComponentInParent<PlayerState>();
                if (state != null && !state.IsJailed.Value && !state.IsEliminated.Value)
                {
                    rescuerPresent = true;
                    break;
                }
            }

            if (rescuerPresent)
            {
                MatchCamouflage.GameModes.GameModeManager.Instance.ServerReleaseAllFromJail();
            }
        }
    }
}
