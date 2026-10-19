using Unity.Netcode;
using UnityEngine;

namespace MatchCamouflage.Player
{
    // 화면 정중앙에 십자형 조준점을 그린다. 본인 화면에만 보이고, 투옥/탈락 중에는 숨긴다.
    [RequireComponent(typeof(PlayerState))]
    public class PlayerCrosshair : NetworkBehaviour
    {
        [SerializeField] private float armLength = 10f;
        [SerializeField] private float thickness = 2f;
        [SerializeField] private float gap = 4f;
        [SerializeField] private Color color = Color.white;

        private PlayerState _state;

        private void Awake()
        {
            _state = GetComponent<PlayerState>();
        }

        private void OnGUI()
        {
            if (!IsOwner) return;
            if (_state.IsJailed.Value || _state.IsEliminated.Value) return;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            var previousColor = GUI.color;
            GUI.color = color;

            // 좌/우
            GUI.DrawTexture(new Rect(cx - gap - armLength, cy - thickness * 0.5f, armLength, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + gap, cy - thickness * 0.5f, armLength, thickness), Texture2D.whiteTexture);
            // 상/하
            GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy - gap - armLength, thickness, armLength), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy + gap, thickness, armLength), Texture2D.whiteTexture);

            GUI.color = previousColor;
        }
    }
}
