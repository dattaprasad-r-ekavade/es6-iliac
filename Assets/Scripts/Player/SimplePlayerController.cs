using UnityEngine;

/// <summary>
/// Minimal WASD + mouse-look controller for early 3D world prototyping.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class SimplePlayerController : MonoBehaviour
{
    // 3.5 m/s walk, ~6.5 m/s sprint. The old 8 m/s walk was faster than a sprinting human
    // and made every world feel small — you crossed a city in forty seconds, so distance
    // carried no weight and travel systems had nothing to be worth using for. It also sat
    // above EnemyBrain's 4.2 m/s, meaning nothing could ever catch the player. See
    // Docs/GAMEPLAY_DESIGN.md § Traversal and scale.
    [SerializeField] private float moveSpeed = 3.5f;

    /// <summary>
    /// Applied by default, not on a held key. <see cref="moveSpeed"/> stays the authored walk
    /// that the 7–8 minute city metric derives from; this is what the player actually travels
    /// at, and 3.5 x 1.5 = 5.25 m/s crosses the 1.6 km city in about five minutes.
    ///
    /// Playtest, 2026-08-14: "walking is slow". It was — sprint cost nothing and had no
    /// downside, so the only thing 3.5 m/s achieved was requiring the player to hold Shift for
    /// four unbroken minutes. Morrowind, Oblivion and Skyrim all default to running for the
    /// same reason. Kept below <see cref="EnemyBrain"/>'s speed on purpose: outrunning a fight
    /// should be a decision, not the default state of the world.
    /// </summary>
    [SerializeField] private float runMultiplier = 1.5f;
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

        // Inverted 2026-08-14: run by default, hold the modifier to drop to the authored walk.
        float speed = GameInput.Sprint.IsPressed() ? moveSpeed : moveSpeed * runMultiplier;
        speed *= DebugSpeed.Multiplier;

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
