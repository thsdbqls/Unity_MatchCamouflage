using UnityEngine;

namespace MatchCamouflage.Player
{
    // 네트워크로 애니메이션 상태를 따로 동기화하지 않는다. NetworkTransform으로 이미 동기화되는
    // 위치가 프레임마다 얼마나 변했는지만 로컬에서 보고 Idle/Walk/Run을 재생한다.
    // 그래서 본인 화면이든 다른 플레이어 화면이든 동일한 스크립트 하나로 다 처리된다.
    public class PlayerAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float walkThreshold = 0.3f;
        [SerializeField] private float runThreshold = 4.5f;
        [SerializeField] private float crossFadeDuration = 0.15f;

        private Vector3 _lastPosition;
        private string _currentState;

        private void Start()
        {
            _lastPosition = transform.position;
        }

        private void Update()
        {
            if (animator == null || Time.deltaTime <= 0f) return;

            float moved = (transform.position - _lastPosition).magnitude;
            _lastPosition = transform.position;
            float speed = moved / Time.deltaTime;

            string target = speed < walkThreshold ? "Idle" : speed < runThreshold ? "Walk" : "Run";
            if (target != _currentState)
            {
                animator.CrossFadeInFixedTime(target, crossFadeDuration);
                _currentState = target;
            }
        }
    }
}
