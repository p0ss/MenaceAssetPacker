using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.ViewModels;

/// <summary>
/// ViewModel for the home/splash screen with navigation tiles.
/// </summary>
public sealed class HomeViewModel : ViewModelBase
{
    /// <summary>
    /// Observable health state service for UI binding on the Home screen.
    /// Provides installation health status that can be displayed on the home view.
    /// </summary>
    public AppHealthStateService HealthState => AppHealthStateService.Instance;
}
