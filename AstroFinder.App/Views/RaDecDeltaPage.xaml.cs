using System.Globalization;
using System.Windows.Input;
using AstroFinder.App.Services;

namespace AstroFinder.App.Views;

public partial class RaDecDeltaPage : ContentPage
{
    internal const double RaGraduationDialWrapHours = 12.0;

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
        ManualGotoUIKitButton.IsEnabled = data.IsManualGotoEnabled;
        UpdateCalibrationStatus();
        UpdateModeHeader();
        UpdateCalculatorVisibility();
        UpdateRaGraduationCalculator();
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
            ManualGotoUIKitButton.Text = "Show Summary";
            AboutManualGotoButton.Text = "About Manual";
            UpdateModeHeader();
            UpdateCalculatorVisibility();
            return;
        }

        DetailsLabel.Text = _baseDetails;
        DetailsLabel.FormattedText = null;
        AboutLinkRows.IsVisible = false;
        ManualGotoUIKitButton.Text = "Manual Goto";
        AboutManualGotoButton.Text = "About Manual";
        UpdateModeHeader();
        UpdateCalculatorVisibility();
    }

    private void OnCurrentRaGraduationChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateRaGraduationCalculator();
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
            ManualGotoUIKitButton.Text = "Manual Goto";
            AboutManualGotoButton.Text = "Show Summary";
            UpdateModeHeader();
            UpdateCalculatorVisibility();
            return;
        }

        DetailsLabel.Text = _baseDetails;
        DetailsLabel.FormattedText = null;
        AboutLinkRows.IsVisible = false;
        ManualGotoUIKitButton.Text = "Manual Goto";
        AboutManualGotoButton.Text = "About Manual";
        UpdateModeHeader();
        UpdateCalculatorVisibility();
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

    private void UpdateCalculatorVisibility()
    {
        RaGraduationCalculatorCard.IsVisible =
            _data.IsManualGotoEnabled &&
            !_isManualGotoMode &&
            !_isAboutMode;
    }

    private void UpdateModeHeader()
    {
        if (_isManualGotoMode)
        {
            ModeHeader.Title = "Manual Goto";
            ModeHeader.IsVisible = true;
            return;
        }

        if (_isAboutMode)
        {
            ModeHeader.Title = "About Manual Goto";
            ModeHeader.IsVisible = true;
            return;
        }

        ModeHeader.Title = string.Empty;
        ModeHeader.IsVisible = false;
    }

    private void UpdateRaGraduationCalculator()
    {
        if (string.IsNullOrWhiteSpace(CurrentRaGraduationEntry.Text))
        {
            TargetRaGraduationLabel.Text = "-";
            RaGraduationDeltaLabel.Text = string.Empty;
            RaGraduationDeltaLabel.IsVisible = false;
            RaGraduationStatusLabel.Text = string.Empty;
            RaGraduationErrorLabel.IsVisible = false;
            return;
        }

        if (!TryParseGraduation(CurrentRaGraduationEntry.Text, out var currentGraduation))
        {
            TargetRaGraduationLabel.Text = "-";
            RaGraduationDeltaLabel.Text = string.Empty;
            RaGraduationDeltaLabel.IsVisible = false;
            RaGraduationStatusLabel.Text = "";
            RaGraduationErrorLabel.Text = "Enter a numeric graduation value, for example 4.2.";
            RaGraduationErrorLabel.IsVisible = true;
            return;
        }

        var dialDeltaHours = CalculateRaDialDeltaHours(_data.DeltaRaDegrees);
        var targetGraduation = CalculateTargetRaGraduation(currentGraduation, _data.DeltaRaDegrees);
        TargetRaGraduationLabel.Text = $"{targetGraduation:F1}h";
        RaGraduationDeltaLabel.Text = $"RA dial move: {FormatSignedHours(dialDeltaHours)}";
        RaGraduationDeltaLabel.IsVisible = true;
        RaGraduationStatusLabel.Text = $"Starting at {NormalizeRaGraduation(currentGraduation):F1}h, turn the dial to {targetGraduation:F1}h.";
        RaGraduationErrorLabel.IsVisible = false;
    }

    private string BuildManualGotoInstructions()
    {
        var data = _data;
        var deltaHaDegrees = -data.DeltaRaDegrees;
        var deltaHaHours = deltaHaDegrees / 15.0;
        var raDialHours = deltaHaHours;

        var raDirection = data.DeltaRaDegrees >= 0
            ? "Target is east in RA (+RA)."
            : "Target is west in RA (-RA).";

        var decDirection = data.DeltaDecDegrees >= 0
            ? "Target is north in Dec (+Dec)."
            : "Target is south in Dec (-Dec).";

        var decPositiveMove = data.DeltaDecDegrees >= 0;
        var raPositiveMove = raDialHours >= 0;

        var decKnobDirection = $"Turn the Dec fine-adjust {GetTurnDirection(decPositiveMove, _decPositiveClockwise)} when looking at the knob.";
        var settingsCircleDirection = $"Turn the settings circle {GetTurnDirection(raPositiveMove, _raPositiveClockwise)} when looking at the circle facing north.";

        var mountLabel = string.IsNullOrWhiteSpace(data.SelectedMountDisplayName)
            ? "Star Adventurer 2i"
            : data.SelectedMountDisplayName;

         return "Current Move\n" +
             $"Mount: {mountLabel}\n" +
             $"Reference -> target: {data.ReferenceStarName} -> {data.TargetName}\n\n" +
             $"1. Move Dec by {FormatSignedDegrees(data.DeltaDecDegrees)}. {decDirection} {decKnobDirection}\n\n" +
             $"2. Move the RA settings circle by {FormatSignedHours(raDialHours)} on the date/time dial scale. {settingsCircleDirection}\n\n" +
             $"3. Current RA delta: {FormatSignedDegrees(data.DeltaRaDegrees)}. {raDirection}\n\n" +
             $"4. Current HA delta: {FormatSignedHours(deltaHaHours)}. HA is the same RA-side move in mount time units (1.0h ≈ 15deg).\n\n" +
             "Manual GoTo Workflow (SA2i)\n\n" +
             "1. Move Dec by the amount shown above.\n" +
             "Use the Dec fine-adjust knob. This is often the larger correction.\n\n" +
             "2. Move the RA settings circle by the amount shown above.\n" +
             "Finish in the same direction each time to reduce backlash.\n\n" +
             "3. Take a short test exposure.\n" +
             "Use 2-5 seconds at high ISO. This acts as your real finder scope.\n\n" +
             "4. Identify the field.\n" +
             "Compare star patterns in an app and look for bright stars, recognizable shapes, and faint fuzzies.\n\n" +
             "5. Refine with small nudges.\n" +
             "RA = left/right, Dec = up/down. Expect 1-3 iterations.\n\n" +
             "Practical Insights\n" +
             "- RA circles are coarse alignment tools: they get you in the neighborhood.\n" +
             "- Dec errors usually dominate: fix Dec first during refinement.\n" +
             "- Fastest workflow is hybrid: move -> test exposure -> refine.\n" +
             "- Optional speed upgrade: red-dot finder, small guidescope, or plate solving.\n\n" +
             "Mental Model\n" +
             "Numbers get me close. Stars get me there.";
    }

    private static string GetTurnDirection(bool positiveMove, bool positiveMoveIsClockwise)
    {
        return positiveMove == positiveMoveIsClockwise ? "clockwise" : "anti-clockwise";
    }

    internal static double CalculateRaDialDeltaHours(double deltaRaDegrees)
    {
        return -deltaRaDegrees / 15.0;
    }

    internal static double CalculateTargetRaGraduation(double currentGraduation, double deltaRaDegrees)
    {
        return NormalizeRaGraduation(currentGraduation + CalculateRaDialDeltaHours(deltaRaDegrees));
    }

    internal static double NormalizeRaGraduation(double graduationHours)
    {
        var normalized = graduationHours % RaGraduationDialWrapHours;
        return normalized < 0 ? normalized + RaGraduationDialWrapHours : normalized;
    }

    internal static bool TryParseGraduation(string? text, out double value)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
        {
            return true;
        }

        var normalizedText = text?.Trim().Replace(',', '.');
        return double.TryParse(normalizedText, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
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
