namespace GroundedMolar.Core;

public static class MolarMarkerOpacity
{
    public const double DefaultUnapproached = 0.45;

    public static double Clamp(double opacity) => double.IsFinite(opacity) ? Math.Clamp(opacity, 0, 1) : DefaultUnapproached;

    public static double Resolve(MolarApproachState approachState, double unapproachedOpacity) =>
        approachState == MolarApproachState.Unapproached ? Clamp(unapproachedOpacity) : 1;
}
