using Unity.Netcode;
using UnityEngine;

namespace MatchCamouflage.Network
{
    // 테스트용 임시 UI. Play를 눌러도 Netcode는 자동으로 아무것도 시작하지 않으므로
    // Host/Client/Server 중 하나를 직접 시작해야 Player Prefab이 스폰된다.
    // 나중에 제대로 된 로비 UI로 교체하면 이 스크립트는 지워도 된다.
    public class NetworkBootstrap : MonoBehaviour
    {
        private void OnGUI()
        {
            if (NetworkManager.Singleton == null) return;

            if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
            {
                GUI.Label(new Rect(10, 10, 300, 20),
                    $"Running as {(NetworkManager.Singleton.IsHost ? "Host" : NetworkManager.Singleton.IsServer ? "Server" : "Client")}");
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, 150, 110));
            if (GUILayout.Button("Host")) NetworkManager.Singleton.StartHost();
            if (GUILayout.Button("Client")) NetworkManager.Singleton.StartClient();
            if (GUILayout.Button("Server")) NetworkManager.Singleton.StartServer();
            GUILayout.EndArea();
        }
    }
}
