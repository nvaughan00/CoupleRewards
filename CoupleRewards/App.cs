using CoupleRewards.Core;

namespace CoupleRewards;

/// <summary>
/// App entry. Holds the two shared services: the on-phone data store and the Wi-Fi sync.
/// Sync stops while the app is in the background (phones suspend it anyway) and restarts when it comes back.
/// </summary>
public class App : Application
{
	public static Store Store { get; private set; } = null!;
	public static SyncService Sync { get; private set; } = null!;

	public App()
	{
		Store = new Store(FileSystem.AppDataDirectory);
		Sync = new SyncService(Store);
		Store.Changed += Theme.RaiseRefresh;
		Store.PartnerRedeemed += Notifier.PartnerRedeemed;
		RequestedThemeChanged += (_, _) => Theme.RaiseRefresh();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell()) { Title = "Couple Rewards" };
		window.Created += (_, _) => { Store.AwardAutoDone(); Sync.Start(); Notifier.Init(); };
		window.Resumed += (_, _) => { Store.AwardAutoDone(); Sync.Start(); Sync.Kick(); Theme.RaiseRefresh(); };
		// Re-checks the date, so tasks reset (and default-done tasks are awarded) after midnight.
		window.Activated += (_, _) => { Store.AwardAutoDone(); Theme.RaiseRefresh(); };
		window.Stopped += (_, _) => Sync.Stop();
		window.Destroying += (_, _) => Sync.Stop();
		return window;
	}
}
