using AstroFinder.Engine.Geometry;
using AstroFinder.Engine.Primitives;

namespace AstroFinder.Domain.AR;

/// <summary>
/// Projects sky objects into screen-space AR overlay coordinates.
///
/// <para><b>Coordinate conventions (matching reference implementation):</b></para>
/// <list type="bullet">
///   <item>World frame (ENU): +X east, +Y north, +Z up.</item>
///   <item>Camera frame: +X screen-right, +Y screen-up, +Z forward through back camera.</item>
///   <item>Screen frame: origin top-left, +X right, +Y down.</item>
/// </list>
///
/// <para><b>Pipeline:</b></para>
/// <list type="number">
///   <item>RA/Dec -> Alt/Az via <see cref="SkyProjection.EquatorialToHorizontal"/>.</item>
///   <item>Alt/Az -> ENU unit vector via <see cref="ArMath.AltAzToEnu"/>.</item>
///   <item>ENU -> camera via <see cref="PoseMatrix.Multiply(Vec3)"/>.</item>
///   <item>Camera -> screen via NDC pinhole projection in <see cref="ArMath.ProjectToScreen"/>.</item>
/// </list>
/// </summary>
public sealed class ArProjectionService
{
    private const double OnTargetThresholdDeg = 1.5;

    /// <summary>
    /// Projects all sky objects in <paramref name="input"/> into screen-space
    /// for the given <paramref name="pose"/> and <paramref name="intrinsics"/>.
    /// </summary>
    public ArOverlayFrame Project(
        ArRouteInput input,
        PoseMatrix pose,
        CameraIntrinsics intrinsics)
    {
        ProjectedSkyObject ProjectStar(ArStarPoint star)
        {
            var horizontal = SkyProjection.EquatorialToHorizontal(
                new EquatorialCoordinate(star.RaHours, star.DecDegrees),
                input.ObserverLatitudeDegrees,
                input.ObserverLongitudeDegrees,
                input.ObservationTime);

            var enu = ArMath.AltAzToEnu(horizontal.AltitudeDegrees, horizontal.AzimuthDegrees);
            var screenPoint = ArMath.ProjectToScreen(pose, enu, intrinsics);
            var inFront = ArMath.IsInFrontOfCamera(pose, enu);
            var withinViewport = screenPoint.HasValue && ArMath.IsWithinViewport(screenPoint.Value, intrinsics);

            return new ProjectedSkyObject(
                Id: star.Label ?? star.Role.ToString(),
                Label: star.Label,
                ScreenPoint: screenPoint,
                InFrontOfCamera: inFront,
                WithinViewport: withinViewport,
                AltitudeDeg: horizontal.AltitudeDegrees,
                AzimuthDeg: horizontal.AzimuthDegrees,
                DirectionEnu: enu,
                Role: star.Role,
                Magnitude: star.Magnitude);
        }

        var targetObj = ProjectStar(input.Target);
        var asterismStars = input.AsterismStars.Select(ProjectStar).ToList();
        var hopSteps = input.HopSteps.Select(ProjectStar).ToList();
        var backgroundStars = input.BackgroundStars.Select(ProjectStar).ToList();

        // Angular error from camera forward to target.
        var angularError = ArMath.AngularSeparationDeg(
            new Vec3(0.0, 0.0, 1.0),
            pose.Multiply(targetObj.DirectionEnu));

        // Off-screen guidance arrow (null when on-target).
        var arrow = ArMath.OffscreenArrowGuidance(pose, targetObj.DirectionEnu, OnTargetThresholdDeg);

        // Route segments between consecutive hop nodes that are both visible.
        var segments = new List<(ArScreenPoint From, ArScreenPoint To)>();
        for (int i = 0; i + 1 < hopSteps.Count; i++)
        {
            var a = hopSteps[i];
            var b = hopSteps[i + 1];
            if (a.ScreenPoint.HasValue && b.ScreenPoint.HasValue
                && a.InFrontOfCamera && b.InFrontOfCamera)
            {
                segments.Add((a.ScreenPoint.Value, b.ScreenPoint.Value));
            }
        }

        return new ArOverlayFrame(
            Target: targetObj,
            AsterismStars: asterismStars,
            AsterismSegments: input.AsterismSegments,
            HopSteps: hopSteps,
            BackgroundStars: backgroundStars,
            Intrinsics: intrinsics,
            OffscreenArrow: arrow,
            TargetAngularDistanceDegrees: angularError,
            CenterReticleText: angularError <= OnTargetThresholdDeg ? "On target" : null,
            RouteSegments: segments);
    }
}
