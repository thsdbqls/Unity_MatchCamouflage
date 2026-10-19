using Unity.Netcode;
using UnityEngine;

namespace MatchCamouflage.Player
{
    // 오너 권위 이동(owner-authoritative). 본인 클라이언트에서만 입력을 처리해 로컬로 움직이고,
    // 같이 붙어있는 NetworkTransform(Owner Authoritative 모드)이 다른 클라이언트에게 위치를 동기화한다.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private float sprintSpeed = 7f;
        [SerializeField] private float lookSensitivity = 2f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private Transform cameraPivot;

        [Header("View Toggle (우클릭)")]
        [SerializeField] private Vector3 thirdPersonCameraLocalPosition = new Vector3(0f, 0.6f, -4f);
        [SerializeField] private Vector3 thirdPersonCameraLocalEuler = new Vector3(8f, 0f, 0f);

        private CharacterController _controller;
        private PlayerState _state;
        private Transform _cameraTransform;
        private Vector3 _firstPersonCameraLocalPosition;
        private Quaternion _firstPersonCameraLocalRotation;
        private bool _isThirdPerson;
        private float _verticalVelocity;
        private float _pitch;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _state = GetComponent<PlayerState>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                // 다른 플레이어의 카메라/오디오 리스너는 꺼서 하나만 활성화되게 한다.
                if (cameraPivot != null)
                {
                    var cam = cameraPivot.GetComponentInChildren<Camera>();
                    if (cam != null) cam.gameObject.SetActive(false);
                    var listener = cameraPivot.GetComponentInChildren<AudioListener>();
                    if (listener != null) listener.enabled = false;
                }
                enabled = false;
                return;
            }

            if (cameraPivot != null)
            {
                var cam = cameraPivot.GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    _cameraTransform = cam.transform;
                    _firstPersonCameraLocalPosition = _cameraTransform.localPosition;
                    _firstPersonCameraLocalRotation = _cameraTransform.localRotation;
                }
            }
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (Input.GetMouseButtonDown(1))
            {
                ToggleCameraView();
            }

            if (_state != null && (_state.IsJailed.Value || _state.IsEliminated.Value))
            {
                return; // 투옥/탈락 상태면 이동 불가
            }

            HandleLook();
            HandleMove();
        }

        private void ToggleCameraView()
        {
            if (_cameraTransform == null) return;

            _isThirdPerson = !_isThirdPerson;
            if (_isThirdPerson)
            {
                _cameraTransform.localPosition = thirdPersonCameraLocalPosition;
                _cameraTransform.localRotation = Quaternion.Euler(thirdPersonCameraLocalEuler);
            }
            else
            {
                _cameraTransform.localPosition = _firstPersonCameraLocalPosition;
                _cameraTransform.localRotation = _firstPersonCameraLocalRotation;
            }
        }

        private void HandleLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

            transform.Rotate(Vector3.up * mouseX);

            _pitch = Mathf.Clamp(_pitch - mouseY, -80f, 80f);
            if (cameraPivot != null)
            {
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private void HandleMove()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            bool sprint = Input.GetKey(KeyCode.LeftShift);

            Vector3 moveDir = (transform.right * h + transform.forward * v).normalized;
            float speed = sprint ? sprintSpeed : moveSpeed;

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -1f;
            }
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = moveDir * speed + Vector3.up * _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }
    }
}
