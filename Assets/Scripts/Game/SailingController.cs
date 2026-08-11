using UnityEngine;

/// <summary>
/// A boarding-capable boat.
///
/// Sailing is taught in Chapter 01's trade route (B400) and then becomes the world's
/// transport infrastructure between regions — the tutorial mechanic with the longest payoff
/// in the game, which is why it gets a real controller rather than a scripted ride.
///
/// The plan names sailing and stealth as the two largest unknowns in the slice, and VS4's
/// gate requires this to survive save/load and to be unable to strand the player. Every
/// failure path here therefore ends with the player back on solid ground:
///
/// - Disembarking always places the player on land, never in water.
/// - <see cref="ResetToMooring"/> recovers a boat that has been driven somewhere useless.
/// - A boat with no valid shore beside it returns the player to its mooring instead.
/// </summary>
public sealed class SailingController : MonoBehaviour
{
    [SerializeField] private float maxSpeed = 9f;
    [SerializeField] private float acceleration = 4f;
    [SerializeField] private float turnRate = 45f;
    [SerializeField] private float disembarkRange = 6f;

    /// <summary>Where this boat returns to when reset.</summary>
    private Vector3 _mooring;
    private Quaternion _mooringRotation;

    private Transform _rider;
    private float _speed;
    private CharacterController _riderController;

    public bool IsBoarded => _rider != null;
    public float Speed => _speed;
    public Vector3 Mooring => _mooring;

    private void Awake()
    {
        _mooring = transform.position;
        _mooringRotation = transform.rotation;
    }

    /// <summary>Set the point this boat resets to. Call after placing it in an authored scene.</summary>
    public void SetMooring(Vector3 position, Quaternion rotation)
    {
        _mooring = position;
        _mooringRotation = rotation;
    }

    public bool Board(Transform rider)
    {
        if (rider == null || IsBoarded) return false;

        _rider = rider;
        _riderController = rider.GetComponent<CharacterController>();

        // The rider is parented so the boat carries them. A CharacterController fights
        // parent motion, so it is disabled for the voyage and restored on disembark.
        if (_riderController != null) _riderController.enabled = false;
        rider.SetParent(transform, worldPositionStays: false);
        rider.localPosition = Vector3.up * 0.5f;

        var controller = rider.GetComponent<SimplePlayerController>();
        if (controller != null) controller.enabled = false;

        GameHud.Instance?.ShowToast("You take the tiller.");
        return true;
    }

    /// <summary>
    /// Steer. Throttle is -1..1, turn is -1..1. Called from the route tutorial and from
    /// whatever drives the boat in normal play.
    /// </summary>
    public void Steer(float throttle, float turn, float deltaTime)
    {
        if (!IsBoarded || deltaTime <= 0f) return;

        float target = Mathf.Clamp(throttle, -1f, 1f) * maxSpeed;
        _speed = Mathf.MoveTowards(_speed, target, acceleration * deltaTime);

        // A boat with no way on cannot turn. It is the one piece of real seamanship here and
        // it stops the boat spinning on the spot like a turret.
        float steerAuthority = Mathf.Clamp01(Mathf.Abs(_speed) / Mathf.Max(0.01f, maxSpeed * 0.3f));
        transform.Rotate(Vector3.up, Mathf.Clamp(turn, -1f, 1f) * turnRate * steerAuthority * deltaTime);

        transform.position += transform.forward * (_speed * deltaTime);
    }

    /// <summary>
    /// Step ashore. Always lands the player on ground — if no shore is within reach, the boat
    /// and rider are returned to the mooring rather than dropped in open water.
    /// </summary>
    public bool Disembark()
    {
        if (!IsBoarded) return false;

        var rider = _rider;
        if (!TryFindShore(out var shore))
        {
            ResetToMooring();
            return true;
        }

        Release(rider, shore);
        GameHud.Instance?.ShowToast("You step ashore.");
        return true;
    }

    /// <summary>
    /// Recovery. Returns the boat to its mooring, putting any rider ashore there. This is the
    /// escape hatch for a boat driven somewhere it cannot come back from.
    /// </summary>
    public void ResetToMooring()
    {
        var rider = _rider;

        transform.SetPositionAndRotation(_mooring, _mooringRotation);
        _speed = 0f;

        if (rider != null)
        {
            var landing = KessilWorldGenerator.PlaceOnLand(_mooring);
            Release(rider, landing);
            GameHud.Instance?.ShowToast("The boat drifts back to its mooring.");
        }
    }

    private void Release(Transform rider, Vector3 destination)
    {
        rider.SetParent(null, worldPositionStays: true);

        var controller = rider.GetComponent<SimplePlayerController>();
        if (controller != null) controller.enabled = true;

        // Position must be written while the CharacterController is off, or it snaps back.
        if (_riderController != null) _riderController.enabled = false;
        rider.position = destination;
        if (_riderController != null) _riderController.enabled = true;

        _rider = null;
        _riderController = null;
        _speed = 0f;
    }

    /// <summary>Look around the boat for dry land to step onto.</summary>
    private bool TryFindShore(out Vector3 shore)
    {
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            var probe = transform.position
                        + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * disembarkRange;

            var candidate = KessilWorldGenerator.PlaceOnLand(probe);
            if (candidate.y > WorldLayout.WaterLevel + 0.1f)
            {
                shore = candidate;
                return true;
            }
        }

        shore = transform.position;
        return false;
    }
}
