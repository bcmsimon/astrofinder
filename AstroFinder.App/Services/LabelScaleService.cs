namespace AstroFinder.App.Services;

/// <summary>
/// Persists the user's label size preference (a scale multiplier) applied to all star-hop
/// map labels and AR overlay labels.
/// </summary>
public sealed class LabelScaleService
{
    private const string Key = "astrofinder.label-scale";
    public const float Min = 0.7f;
    public const float Max = 1.5f;
    public const float Default = 1.0f;

    public float LabelScale
    {
        get => Preferences.Default.Get(Key, Default);
        set => Preferences.Default.Set(Key, Math.Clamp(value, Min, Max));
    }
}
