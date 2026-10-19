using MatchCamouflage.GameModes;
using Unity.Netcode;
using UnityEngine;

namespace MatchCamouflage.Player
{
    public enum PlayerRole
    {
        None = 0,
        Seeker = 1,   // 술래 (총으로 추적)
        Hider = 2,    // 숨는 유저 (페인트로 위장)
        TeamA = 3,    // 팀 데스매치 A팀
        TeamB = 4     // 팀 데스매치 B팀
    }

    // 플레이어의 역할/스트라이크/탈락/색칠 상태를 들고 있는 네트워크 동기화 컴포넌트.
    // 판정(피격 처리)은 전부 GameModeManager가 서버에서만 호출한다.
    public class PlayerState : NetworkBehaviour
    {
        public const int MaxStrikes = 3;

        public NetworkVariable<PlayerRole> Role = new NetworkVariable<PlayerRole>(
            PlayerRole.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<int> Strikes = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsJailed = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsEliminated = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<Color> BodyColor = new NetworkVariable<Color>(
            Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // 서버에서만 호출. 탈락/투옥 여부는 GameModeManager가 모드 규칙에 따라 결정해서 넘겨준다.
        public void ServerSetStrikes(int value)
        {
            if (!IsServer) return;
            Strikes.Value = Mathf.Clamp(value, 0, MaxStrikes);
        }

        public void ServerSetJailed(bool jailed)
        {
            if (!IsServer) return;
            IsJailed.Value = jailed;
        }

        public void ServerSetEliminated(bool eliminated)
        {
            if (!IsServer) return;
            IsEliminated.Value = eliminated;
        }

        public void ServerSetRole(PlayerRole role)
        {
            if (!IsServer) return;
            Role.Value = role;
        }

        public void ServerResetForRound()
        {
            if (!IsServer) return;
            Strikes.Value = 0;
            IsJailed.Value = false;
            IsEliminated.Value = false;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                GameModeManager.Instance.ServerRegisterPlayer(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && GameModeManager.Instance != null)
            {
                GameModeManager.Instance.ServerUnregisterPlayer(this);
            }
        }
    }
}
