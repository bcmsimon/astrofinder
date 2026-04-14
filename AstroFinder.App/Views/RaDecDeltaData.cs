namespace AstroFinder.App.Views;

public sealed record RaDecDeltaFlyoutData(
	string Title,
	string Details,
	string ReferenceStarName,
	string TargetName,
	double DeltaRaDegrees,
	double DeltaDecDegrees,
	string? SelectedMountDisplayName,
	bool IsManualGotoEnabled);
