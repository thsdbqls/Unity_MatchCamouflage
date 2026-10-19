using System.Collections.Generic;
using MatchCamouflage.Jail;
using MatchCamouflage.Player;
using Unity.Netcode;
using UnityEngine;

namespace MatchCamouflage.GameModes
{
    public enum GameModeType
    {
        Manhunt = 0,        // 경찰과 도둑: 술래 1+명 vs 숨는 유저. 3회 피격 시 완전 탈락.
        TeamDeathmatch = 1  // 2팀으로 나뉘어 상대팀을 맞추면 즉시 탈락.
    }

    // 씬에 하나만 존재하는 네트워크 싱글톤. 역할/팀 배정, 피격 판정, 승리 조건을 모두 서버에서 처리한다.
    public class GameModeManager : NetworkBehaviour
    {
        public static GameModeManager Instance { get; private set; }

        [SerializeField] private GameModeType selectedMode = GameModeType.Manhunt;
        [SerializeField] private JailZone jailZone;
        [SerializeField] private int seekerCount = 1;

        public NetworkVariable<GameModeType> ActiveMode = new NetworkVariable<GameModeType>(
            GameModeType.Manhunt, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly List<PlayerState> _allPlayers = new List<PlayerState>();

        private void Awake()
        {
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                ActiveMode.Value = selectedMode;
            }
        }

        // 플레이어가 스폰될 때 PlayerState에서 호출(또는 NetworkManager.OnClientConnectedCallback에서 호출).
        public void ServerRegisterPlayer(PlayerState state)
        {
            if (!IsServer) return;
            if (!_allPlayers.Contains(state))
            {
                _allPlayers.Add(state);
            }
            AssignRole(state);
        }

        public void ServerUnregisterPlayer(PlayerState state)
        {
            if (!IsServer) return;
            _allPlayers.Remove(state);
        }

        private void AssignRole(PlayerState state)
        {
            if (ActiveMode.Value == GameModeType.Manhunt)
            {
                int seekersSoFar = _allPlayers.FindAll(p => p.Role.Value == PlayerRole.Seeker).Count;
                PlayerRole role = seekersSoFar < seekerCount ? PlayerRole.Seeker : PlayerRole.Hider;
                state.ServerSetRole(role);
            }
            else
            {
                int teamACount = _allPlayers.FindAll(p => p.Role.Value == PlayerRole.TeamA).Count;
                int teamBCount = _allPlayers.FindAll(p => p.Role.Value == PlayerRole.TeamB).Count;
                PlayerRole role = teamACount <= teamBCount ? PlayerRole.TeamA : PlayerRole.TeamB;
                state.ServerSetRole(role);
            }
            state.ServerResetForRound();
        }

        // PlayerShooter가 서버에서 피격을 감지했을 때 호출.
        public void ServerResolveHit(ulong shooterClientId, PlayerState target)
        {
            if (!IsServer) return;
            if (target.IsJailed.Value || target.IsEliminated.Value) return;

            var shooterState = FindByClientId(shooterClientId);
            if (shooterState == null) return;

            if (ActiveMode.Value == GameModeType.Manhunt)
            {
                ResolveManhuntHit(shooterState, target);
            }
            else
            {
                ResolveTeamDeathmatchHit(shooterState, target);
            }
        }

        private void ResolveManhuntHit(PlayerState shooter, PlayerState target)
        {
            // 술래만 Hider를 맞출 수 있다.
            if (shooter.Role.Value != PlayerRole.Seeker) return;
            if (target.Role.Value != PlayerRole.Hider) return;

            int newStrikes = target.Strikes.Value + 1;
            target.ServerSetStrikes(newStrikes);

            if (newStrikes >= PlayerState.MaxStrikes)
            {
                target.ServerSetEliminated(true);
                CheckManhuntWinCondition();
            }
            else
            {
                SendToJail(target);
            }
        }

        private void SendToJail(PlayerState target)
        {
            target.ServerSetJailed(true);
            if (jailZone != null)
            {
                var targetTransform = target.transform;
                var controller = target.GetComponent<CharacterController>();
                Transform cell = jailZone.GetCellPoint(target.Strikes.Value);

                // CharacterController가 있으면 직접 position 대입 전에 꺼야 텔레포트가 먹힌다.
                if (controller != null) controller.enabled = false;
                targetTransform.position = cell.position;
                targetTransform.rotation = cell.rotation;
                if (controller != null) controller.enabled = true;
            }
        }

        public void ServerReleaseAllFromJail()
        {
            if (!IsServer) return;
            foreach (var p in _allPlayers)
            {
                if (p.IsJailed.Value && !p.IsEliminated.Value)
                {
                    p.ServerSetJailed(false);
                }
            }
        }

        private void ResolveTeamDeathmatchHit(PlayerState shooter, PlayerState target)
        {
            bool sameTeam = shooter.Role.Value == target.Role.Value;
            if (sameTeam) return; // 아군 피해 없음

            target.ServerSetEliminated(true);
            CheckTeamDeathmatchWinCondition();
        }

        private void CheckManhuntWinCondition()
        {
            bool anyHiderLeft = _allPlayers.Exists(p => p.Role.Value == PlayerRole.Hider && !p.IsEliminated.Value);
            if (!anyHiderLeft)
            {
                Debug.Log("[Manhunt] 모든 Hider 탈락 - 술래 승리");
                // TODO: 라운드 종료 처리 / UI 브로드캐스트
            }
        }

        private void CheckTeamDeathmatchWinCondition()
        {
            bool teamAAlive = _allPlayers.Exists(p => p.Role.Value == PlayerRole.TeamA && !p.IsEliminated.Value);
            bool teamBAlive = _allPlayers.Exists(p => p.Role.Value == PlayerRole.TeamB && !p.IsEliminated.Value);

            if (!teamAAlive || !teamBAlive)
            {
                string winner = teamAAlive ? "Team A" : teamBAlive ? "Team B" : "무승부";
                Debug.Log($"[TeamDeathmatch] 라운드 종료 - 승자: {winner}");
                // TODO: 라운드 종료 처리 / UI 브로드캐스트
            }
        }

        private PlayerState FindByClientId(ulong clientId)
        {
            return _allPlayers.Find(p => p.OwnerClientId == clientId);
        }
    }
}
