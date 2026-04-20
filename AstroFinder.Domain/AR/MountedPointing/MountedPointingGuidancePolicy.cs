namespace AstroFinder.Domain.AR.MountedPointing;

internal static class MountedPointingGuidancePolicy
{
    private const string LowConfidenceMessage = "Solve confidence is too low. Reframe to include more stars.";

    public static string Build(
        bool canGuideReliably,
        double horizontalErrorDeg,
        double verticalErrorDeg,
        double angularErrorDeg,
        MountedPointingSolveOptions options)
    {
        if (!canGuideReliably)
        {
            return LowConfidenceMessage;
        }

        var hasHorizontal = Math.Abs(horizontalErrorDeg) >= options.AxisIgnoreThresholdDeg;
        var hasVertical = Math.Abs(verticalErrorDeg) >= options.AxisIgnoreThresholdDeg;

        if (!hasHorizontal && !hasVertical)
        {
            return "You are close. Make a smaller adjustment.";
        }

        var horizontalWord = horizontalErrorDeg >= 0.0 ? "right" : "left";
        var verticalWord = verticalErrorDeg >= 0.0 ? "up" : "down";

        if (angularErrorDeg <= options.FineAdjustmentThresholdDeg)
        {
            return hasHorizontal && hasVertical
                ? $"You are close. Make a smaller {horizontalWord} and {verticalWord} adjustment."
                : hasHorizontal
                    ? $"You are close. Make a smaller {horizontalWord} adjustment."
                    : $"You are close. Make a smaller {verticalWord} adjustment.";
        }

        if (hasHorizontal && hasVertical)
        {
            var h = Math.Abs(horizontalErrorDeg);
            var v = Math.Abs(verticalErrorDeg);
            if (Math.Min(h, v) <= options.SlightAxisThresholdDeg)
            {
                return h >= v
                    ? $"Move {horizontalWord} and slightly {verticalWord}."
                    : $"Move {verticalWord} and slightly {horizontalWord}.";
            }

            return $"Move {horizontalWord} and {verticalWord}.";
        }

        return hasHorizontal
            ? $"Move {horizontalWord}."
            : $"Move {verticalWord}.";
    }
}
