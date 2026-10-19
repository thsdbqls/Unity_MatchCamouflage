using UnityEngine;

namespace MatchCamouflage.Player
{
    // 칠할 수 있는 벽/바닥에 붙이는 마커 컴포넌트. 지정 색이 없으면 렌더러의 머티리얼 색을 그대로 쓴다.
    [DisallowMultipleComponent]
    public class Paintable : MonoBehaviour
    {
        [SerializeField] private bool useRendererColor = true;
        [SerializeField] private Color overrideColor = Color.white;

        public Color SurfaceColor
        {
            get
            {
                if (!useRendererColor) return overrideColor;
                var renderer = GetComponent<Renderer>();
                return renderer != null ? renderer.sharedMaterial.color : overrideColor;
            }
        }
    }
}
