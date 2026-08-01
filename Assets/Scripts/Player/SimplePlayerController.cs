using UnityEngine;

/// <summary>
/// Minimal WASD + mouse-look controller for early 3D world prototyping.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class SimplePlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float sprintMultiplier = 1.85f;
    [SerializeField] private float lookSensitivity = 0.12f;
    [SerializeField] private float gravity = -24f;
    [SerializeField] private float jumpSpeed = 7f;
    [SerializeField] private Transform cameraPivot;

    public void SetCameraPivot(Transform pivot) => cameraPivot = pivot;

    private CharacterController _controller;
    private float _pitch;
    private float _yaw;
    private float _verticalVelocity;
    private bool _lookReady;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        if (cameraPivot == null)
        {
            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                cameraPivot = cam.transform.parent != null ? cam.transform.parent : cam.transform;
            }
        }

        _yaw = transform.eulerAngles.y;
    }

    private void OnEnable()
    {
        // Keep CharacterController enabled; only this script is toggled by game flow.
        if (_controller == null) _controller = GetComponent<CharacterController>();
        if (_controller != null && !_controller.enabled) _controller.enabled = true;

        _verticalVelocity = -2f;
        _lookReady = false;

        // Drop onto ground if we were left floating while disabled.
        SnapToGround();
    }

    private void Update()
    {
        if (GameStateService.Instance != null && !GameStateService.Instance.GameplayInputAllowed)
        {
            return;
        }

        if (GameInput.PrimaryAttack.WasPressedThisFrame()
            && Cursor.lockState != CursorLockMode.Locked)
        {
            GameStateService.Instance?.SetState(GameState.Gameplay);
        }

        // Ignore the huge first mouse delta after cursor lock (common "spin" / stuck feel).
        Vector2 look = GameInput.Look.ReadValue<Vector2>() * lookSensitivity;
        if (!_lookReady)
        {
            _lookReady = true;
            look = Vector2.zero;
        }

        _yaw += look.x;
        _pitch = Mathf.Clamp(_pitch - look.y, -80f, 80f);
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        if (cameraPivot != null)
        {
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        Vector2 move = GameInput.Move.ReadValue<Vector2>();
        Vector3 input = Vector3.ClampMagnitude(new Vector3(move.x, 0f, move.y), 1f);

        float speed = moveSpeed;
        if (GameInput.Sprint.IsPressed())
        {
            speed *= sprintMultiplier;
        }

        Vector3 worldMove = transform.TransformDirection(input) * speed;
        bool grounded = _controller.isGrounded;
        if (grounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = -2f;
        }

        if (grounded && GameInput.Jump.WasPressedThisFrame())
        {
            _verticalVelocity = jumpSpeed;
        }

        _verticalVelocity += gravity * Time.deltaTime;
        worldMove.y = _verticalVelocity;
        _controller.Move(worldMove * Time.deltaTime);
    }

    private void SnapToGround()
    {
        if (_controller == null) return;

        // Ground under *this* position — not the Caldemar pad, which is what the old
        // SnapToWalkable returned and which teleported the player home on every re-enable.
        var pos = KessilWorldGenerator.SnapToGround(transform.position);
        float bottomOffset = _controller.center.y - (_controller.height * 0.5f) + _controller.skinWidth;
        var target = pos - Vector3.up * bottomOffset;
        bool wasEnabled = _controller.enabled;
        _controller.enabled = false;
        transform.position = target;
        _controller.enabled = wasEnabled;
    }
}
