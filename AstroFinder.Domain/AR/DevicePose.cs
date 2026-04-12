namespace AstroFinder.Domain.AR;

/// <summary>
/// The orientation of the device in world space.
/// Heading is degrees clockwise from magnetic north (0–360).
/// Pitch is the elevation angle in degrees: 0 = horizon, +90 = zenith, –90 = nadir.
/// Roll is the screen rotation around the forward axis in degrees:
///   0 = screen top pointing toward zenith (phone held upright, portrait mode pointing at horizon),
///   +90 = screen top tilted to the right (clockwise when viewed from behind).
/// Roll is used to correctly orient the AR overlay when the phone is tilted sideways.
/// </summary>
public sealed record DevicePose(double HeadingDegrees, double PitchDegrees, double RollDegrees = 0.0)
{
    /// <summary>
    /// Sentinel used when sensor data is unavailable.
    /// Points north at the horizon with no roll.
    /// </summary>
    public static readonly DevicePose Unknown = new(0.0, 0.0, 0.0);

    /// <summary>
    /// Smoothed sensor quaternion (qx, qy, qz, qw) in Android ENU convention.
    /// When present, <see cref="ArProjectionService"/> uses this to build the camera matrix
    /// directly — avoiding Euler-angle gimbal lock near pitch ±90° (looking straight up/down).
    /// Null on platforms that do not provide quaternion data (iOS uses the Euler path).
    /// </summary>
    public (double qx, double qy, double qz, double qw)? Quaternion { get; init; }
}
