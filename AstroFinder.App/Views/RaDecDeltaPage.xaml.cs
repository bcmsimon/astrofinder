using System.Windows.Input;
using AstroFinder.App.Services;

namespace AstroFinder.App.Views;

public partial class RaDecDeltaPage : ContentPage
{
    private const string KamilChannelUrl = "https://www.youtube.com/@kamilkp";
    private const string AndreaChannelUrl = "https://www.youtube.com/@andreaminoia2002";
    private const string KamilVideoUrl = "https://www.youtube.com/watch?v=tNPIMKOB9k4&t=49s";
    private const string AndreaVideoUrl = "https://www.youtube.com/watch?v=PYheSckraMo";

    private readonly RaDecDeltaFlyoutData _data;
    private readonly ManualGotoCalibrationService _calibrationService;
    private readonly string _baseDetails;
    private bool _isManualGotoMode;
    private bool _isAboutMode;
    private bool _isDirectionCalibrated;
    private bool _raPositiveClockwise;
    private bool _decPositiveClockwise;

    public RaDecDeltaPage(RaDecDeltaFlyoutData data, ManualGotoCalibrationService? calibrationService = null)
    {
        _data = data;
        _calibrationService = calibrationService ?? new ManualGotoCalibrationService();
        _baseDetails = data.Details;

        CloseCommand = new Command(async () => await CloseAsync());
        ToggleManualGotoCommand = new Command(ToggleManualGoto);
        ToggleAboutManualGotoCommand = new Command(ToggleAboutManualGoto);
        CalibrateDirectionsCommand = new Command(async () => await RunDirectionCalibrationAsync());

        LoadDirectionCalibration();

        InitializeComponent();
        TitleLabel.Text = data.Title;
        ActiveMountLabel.Text = BuildActiveMountText(data.SelectedMountDisplayName);
        DetailsLabel.Text = _baseDetails;
        ModeHeader.Title = "Delta Summary";
        ManualGotoUIKitButton.IsEnabled = data.IsManualGotoEnabled;
        UpdateCalibrationStatus();
    }

    public ICommand CloseCommand { get; }

    public ICommand ToggleManualGotoCommand { get; }

    public ICommand ToggleAboutManualGotoCommand { get; }

    public ICommand CalibrateDirectionsCommand { get; }

    private async Task CloseAsync()
    {
        await Navigation.PopModalAsync();
    }

    private static async Task OpenUrlAsync(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            await Launcher.Default.OpenAsync(uri);
        }
    }

    private void ToggleManualGoto()
    {
        _isAboutMode = false;
        _isManualGotoMode = !_isManualGotoMode;

        if (_isManualGotoMode)
        {
            DetailsLabel.Text = BuildManualGotoInstructions();
            DetailsLabel.FormattedText = null;
            AboutLinkRows.IsVisible = false;
            ModeHeader.Title = "Manual Goto";
            ManualGotoUIKitButton.Text = "Show Summary";
            AboutManualGotoButton.Text = "About Manual Goto";
            return;
        }

        DetailsLabel.Text = _baseDetails;
        DetailsLabel.FormattedText = null;
        AboutLinkRows.IsVisible = false;
        ModeHeader.Title = "Delta Summary";
        ManualGotoUIKitButton.Text = "Manual Goto";
        AboutManualGotoButton.Text = "About Manual Goto";
    }

    private void ToggleAboutManualGoto()
    {
        _isManualGotoMode = false;
        _isAboutMode = !_isAboutMode;

        if (_isAboutMode)
        {
            DetailsLabel.Text = BuildManualGotoAboutText();
            DetailsLabel.FormattedText = null;
            AboutLinkRows.IsVisible = true;
            ModeHeader.Title = "About Manual Goto";
            ManualGotoUIKitButton.Text = "Manual Goto";
            AboutManualGotoButton.Text = "Show Summary";
            return;
        }

        DetailsLabel.Text = _baseDetails;
        DetailsLabel.FormattedText = null;
        AboutLinkRows.IsVisible = false;
        ModeHeader.Title = "Delta Summary";
        ManualGotoUIKitButton.Text = "Manual Goto";
        AboutManualGotoButton.Text = "About Manual Goto";
    }

    private static string BuildManualGotoAboutText() =>
        "Manual Goto System\n" +
        "This workflow is a practical hybrid method for non-GOTO trackers: use RA/Dec setting circles to get close, then finish with short test exposures and star-pattern matching.\n\n" +
        "Core idea\n" +
        "- Polar alignment quality sets the ceiling for all RA moves.\n" +
        "- RA circle moves are coarse neighborhood moves.\n" +
        "- Dec and test-shot refinement do the final centering.\n\n" +
        "Credits and reference links are listed below.";

    private async void OnKamilChannelTapped(object? sender, EventArgs e) =>
        await OpenUrlAsync(KamilChannelUrl);

    private async void OnAndreaChannelTapped(object? sender, EventArgs e) =>
        await OpenUrlAsync(AndreaChannelUrl);

    private async void OnKamilVideoTapped(object? sender, EventArgs e) =>
        await OpenUrlAsync(KamilVideoUrl);

    private async void OnAndreaVideoTapped(object? sender, EventArgs e) =>
        await OpenUrlAsync(AndreaVideoUrl);

    private async Task RunDirectionCalibrationAsync()
    {
        var proceed = await DisplayAlert(
            "Calibrate Directions",
            "Center a bright star, then do tiny RA/Dec test moves. You will map positive RA and positive Dec to clockwise or anti-clockwise for your setup.",
            "Start",
            "Cancel");

        if (!proceed)
        {
            return;
        }

        var raChoice = await DisplayActionSheet(
            "Step 1/2: For a positive RA/HA move on your setup, which direction should the app instruct?",
            "Cancel",
            null,
            "Clockwise",
            "Anti-clockwise");

        if (raChoice is not "Clockwise" and not "Anti-clockwise")
        {
            return;
        }

        var decChoice = await DisplayActionSheet(
            "Step 2/2: For a positive Dec move (toward +Dec), which direction should the app instruct?",
            "Cancel",
            null,
            "Clockwise",
            "Anti-clockwise");

        if (decChoice is not "Clockwise" and not "Anti-clockwise")
        {
            return;
        }

        _raPositiveClockwise = raChoice == "Clockwise";
        _decPositiveClockwise = decChoice == "Clockwise";
        _isDirectionCalibrated = true;
        SaveDirectionCalibration();
        UpdateCalibrationStatus();

        if (_isManualGotoMode)
        {
            DetailsLabel.Text = BuildManualGotoInstructions();
        }

        await DisplayAlert("Calibration saved", "Manual Goto directions now use your calibrated RA/Dec turn mapping.", "OK");
    }

    private void LoadDirectionCalibration()
    {
        if (_calibrationService.TryGetCalibration(_data.SelectedMountDisplayName, out var calibration))
        {
            _isDirectionCalibrated = true;
            _raPositiveClockwise = calibration.RaPositiveClockwise;
            _decPositiveClockwise = calibration.DecPositiveClockwise;
            return;
        }

        _isDirectionCalibrated = false;
        _raPositiveClockwise = false;
        _decPositiveClockwise = false;
    }

    private void SaveDirectionCalibration()
    {
        _calibrationService.SetSelectedMountName(_data.SelectedMountDisplayName);
        _calibrationService.SaveCalibration(
            _data.SelectedMountDisplayName,
            new ManualGotoDirectionCalibration(_raPositiveClockwise, _decPositiveClockwise));
    }

    private void UpdateCalibrationStatus()
    {
        var mountLabel = string.IsNullOrWhiteSpace(_data.SelectedMountDisplayName)
            ? "unknown mount"
            : _data.SelectedMountDisplayName.Trim();

        CalibrationStatusLabel.Text = _isDirectionCalibrated
            ? $"Direction calibration ({mountLabel}): complete"
            : $"Direction calibration ({mountLabel}): not set (using default assumptions)";
    }

    private string BuildManualGotoInstructions()
    {
        var data = _data;
        var deltaHaDegrees = -data.DeltaRaDegrees;
        var deltaHaHours = deltaHaDegrees / 15.0;
        var sa2iDialHours = deltaHaHours / 2.0;

        var raDirection = data.DeltaRaDegrees >= 0
            ? "Target is east in RA (+RA)."
            : "Target is west in RA (-RA).";

        var decDirection = data.DeltaDecDegrees >= 0
            ? "Target is north in Dec (+Dec)."
            : "Target is south in Dec (-Dec).";

        var decPositiveMove = data.DeltaDecDegrees >= 0;
        var raPositiveMove = sa2iDialHours >= 0;

        var decKnobDirection = $"Turn the Dec fine-adjust {GetTurnDirection(decPositiveMove, _decPositiveClockwise)}.";
        var settingsCircleDirection = $"Turn the settings circle {GetTurnDirection(raPositiveMove, _raPositiveClockwise)}.";

        var mountLabel = string.IsNullOrWhiteSpace(data.SelectedMountDisplayName)
            ? "Star Adventurer 2i"
            : data.SelectedMountDisplayName;

         return "Current Move\n" +
             $"Mount: {mountLabel}\n" +
             $"Reference -> target: {data.ReferenceStarName} -> {data.TargetName}\n\n" +
             $"1. Move Dec by {FormatSignedDegrees(data.DeltaDecDegrees)}. {decDirection} {decKnobDirection}\n\n" +
             $"2. Move the RA settings circle by {FormatSignedHours(sa2iDialHours)} on the date/time dial scale. {settingsCircleDirection}\n\n" +
             $"3. Current RA delta: {FormatSignedDegrees(data.DeltaRaDegrees)}. {raDirection}\n\n" +
             $"4. Current HA delta: {FormatSignedHours(deltaHaHours)}. HA is your RA-side move in mount time units (1.0h ≈ 15deg).\n\n" +
             "Repeatable Manual GoTo Workflow (SA2i)\n\n" +
             "1. Polar align first.\n" +
             "Use the polar scope carefully. Accuracy here directly determines how well RA moves land.\n\n" +
             "2. Pick a nearby bright reference star.\n" +
             "Choose a star that is easy to center and close in RA/Dec to your target.\n\n" +
             "3. Center the reference star.\n" +
             "Use live view or a short exposure and center it as tightly as possible.\n\n" +
             "4. Calibrate the RA circle.\n" +
             "Look up the star RA and rotate only the RA setting circle to match it.\n\n" +
             "5. Compute offsets to target.\n" +
             "Use RA difference (h/m) and Dec difference (deg). Keep movements approximate.\n\n" +
             "6. Move RA first.\n" +
             "Unlock RA and rotate to target RA. Finish in the same direction each time to reduce backlash.\n\n" +
             "7. Move Dec second.\n" +
             "Adjust Dec by the required amount. This is often the larger correction.\n\n" +
             "8. Take a short test exposure.\n" +
             "Use 2-5 seconds at high ISO. This acts as your real finder scope.\n\n" +
             "9. Identify the field.\n" +
             "Compare star patterns in an app and look for bright stars, recognizable shapes, and faint fuzzies.\n\n" +
             "10. Refine with small nudges.\n" +
             "RA = left/right, Dec = up/down. Expect 1-3 iterations.\n\n" +
             "Practical Insights\n" +
             "- RA circles are coarse alignment tools: they get you in the neighborhood.\n" +
             "- Dec errors usually dominate: fix Dec first during refinement.\n" +
             "- Fastest workflow is hybrid: RA circle -> star pattern check -> test exposure -> refine.\n" +
             "- Optional speed upgrade: red-dot finder, small guidescope, or plate solving.\n\n" +
             "Mental Model\n" +
             "Numbers get me close. Stars get me there.";
    }

    private static string GetTurnDirection(bool positiveMove, bool positiveMoveIsClockwise)
    {
        return positiveMove == positiveMoveIsClockwise ? "clockwise" : "anti-clockwise";
    }

    private static string FormatSignedDegrees(double degrees) =>
        degrees >= 0 ? $"+{degrees:F1}°" : $"{degrees:F1}°";

    private static string FormatSignedHours(double hours) =>
        hours >= 0 ? $"+{hours:F1}h" : $"{hours:F1}h";

    private static string BuildActiveMountText(string? selectedMountDisplayName)
    {
        return string.IsNullOrWhiteSpace(selectedMountDisplayName)
            ? "Active mount: not selected"
            : $"Active mount: {selectedMountDisplayName.Trim()}";
    }
}
