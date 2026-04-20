using AstroFinder.App.Controls;

namespace AstroFinder.App.Views;

/// <summary>
/// Minimal AR debug page.
/// Everything that could mask anchoring issues is intentionally omitted:
/// no astronomy, no reticles, no guides, no hit testing, and no per-frame corrections.
/// </summary>
public partial class ArStarHopPage : ContentPage
{
    public ArStarHopPage()
    {
        InitializeComponent();
        ArCamera.StatusMessage += OnArStatusMessage;
        ArCamera.DiagnosticsChanged += OnDiagnosticsChanged;
    }

    protected override void OnDisappearing()
    {
        ArCamera.StatusMessage -= OnArStatusMessage;
        ArCamera.DiagnosticsChanged -= OnDiagnosticsChanged;
        base.OnDisappearing();
    }

    private void OnArStatusMessage(object? sender, string message)
    {
        HudStatusLabel.Text = $"Status: {message}";
    }

    private void OnDiagnosticsChanged(object? sender, ArDiagnosticStatus diagnostics)
    {
        HudPlatformLabel.Text = $"Platform: {diagnostics.PlatformName}";
        HudSessionLabel.Text = $"Session initialized: {(diagnostics.SessionInitialized ? "yes" : "no")}";
        HudTrackingLabel.Text = $"Tracking state: {diagnostics.TrackingState}";
        HudAnchorCountLabel.Text = $"Anchor placed count: {diagnostics.AnchorPlacedCount}";
        HudMarkerPoseLabel.Text = $"Marker pose: {diagnostics.MarkerPoseText}";
    }

    private async void OnCloseClicked(object? sender, EventArgs e) =>
        await Navigation.PopModalAsync();
}
