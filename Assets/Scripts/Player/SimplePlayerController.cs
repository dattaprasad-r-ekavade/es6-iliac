using UnityEngine;
using UnityEngine.InputSystem;

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
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Drop onto ground if we were left floating while disabled.
        SnapToGround();
    }

    private void Update()
    {
        if (GameHud.Instance != null && GameHud.Instance.AnyMenuOpen)
        {
            return;
        }

        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null)
        {
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Ignore the huge first mouse delta after cursor lock (common "spin" / stuck feel).
        Vector2 look = mouse.delta.ReadValue() * lookSensitivity;
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

        Vector3 input = Vector3.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.z += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.z -= 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
        input = Vector3.ClampMagnitude(input, 1f);

        float speed = moveSpeed;
        if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
        {
            speed *= sprintMultiplier;
        }

        Vector3 worldMove = transform.TransformDirection(input) * speed;
        bool grounded = _controller.isGrounded;
        if (grounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = -2f;
        }

        if (grounded && keyboard.spaceKey.wasPressedThisFrame)
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

        var pos = IliacBayWorldGenerator.SnapToWalkable(transform.position);
        float bottomOffset = _controller.center.y - (_controller.height * 0.5f) + _controller.skinWidth;
        var target = pos - Vector3.up * bottomOffset;
        bool wasEnabled = _controller.enabled;
        _controller.enabled = false;
        transform.position = target;
        _controller.enabled = wasEnabled;
    }
}
