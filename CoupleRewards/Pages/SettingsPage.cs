using CoupleRewards.Core;

namespace CoupleRewards.Pages;

/// <summary>Settings: the Nick / Whitney switch, both point totals, and Wi-Fi sync setup.</summary>
public class SettingsPage : ContentPage
{
	readonly Label whoTitle = Heading("Whose rewards are on this phone?");
	readonly Border segment;
	readonly Button nickBtn = Theme.NewButton(), whitBtn = Theme.NewButton();

	readonly Label totalsTitle = Heading("Points");
	readonly Label totals = new() { FontSize = 16 };
	readonly Border totalsCard;

	readonly Label syncTitle = Heading("Sync with your partner’s phone");
	readonly Label myAddrLabel = new() { Text = "This phone’s address (tap to copy)", FontSize = 13 };
	readonly Label myAddr = new() { FontSize = 22, FontAttributes = FontAttributes.Bold };
	readonly Label peerLabel = new() { Text = "Partner’s address", FontSize = 13 };
	readonly Entry peer = new() { Placeholder = "e.g. 192.168.1.23", Keyboard = Keyboard.Url, FontSize = 17 };
	readonly Button saveBtn = Theme.NewButton(), syncBtn = Theme.NewButton();
	readonly Label status = new() { FontSize = 14 };
	readonly Label help = new() { FontSize = 13 };
	readonly Border syncCard;

	public SettingsPage()
	{
		Title = "Settings";

		nickBtn.Text = Defs.Names[0];
		whitBtn.Text = Defs.Names[1];
		nickBtn.Clicked += (_, _) => App.Store.Who = Defs.Names[0];
		whitBtn.Clicked += (_, _) => App.Store.Who = Defs.Names[1];
		var seg = new Grid
		{
			ColumnSpacing = 4,
			ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
		};
		seg.Add(nickBtn, 0, 0);
		seg.Add(whitBtn, 1, 0);
		segment = new Border
		{
			Content = seg,
			Padding = new Thickness(4),
			StrokeThickness = 0,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
		};

		totalsCard = Theme.Panel(totals);

		peer.Text = App.Store.Peer;
		saveBtn.Text = "Save and sync";
		syncBtn.Text = "Sync now";
		saveBtn.Clicked += (_, _) => { peer.Unfocus(); App.Store.Peer = peer.Text ?? ""; App.Sync.Kick(); };
		syncBtn.Clicked += (_, _) => App.Sync.Kick();
		myAddr.GestureRecognizers.Add(new TapGestureRecognizer
		{
			Command = new Command(async () =>
			{
				try { await Clipboard.Default.SetTextAsync(myAddr.Text); } catch { }
			}),
		});
		help.Text = "Type the address shown on the other phone into one phone and tap Save. "
			+ "That’s all; both phones then keep each other up to date whenever the app is open on both, "
			+ "on the same Wi-Fi. Changes made while apart sync the next time you’re both home.";

		var buttons = new Grid
		{
			ColumnSpacing = 8,
			ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
		};
		buttons.Add(saveBtn, 0, 0);
		buttons.Add(syncBtn, 1, 0);

		syncCard = Theme.Panel(new VerticalStackLayout
		{
			Spacing = 8,
			Children = { myAddrLabel, myAddr, peerLabel, peer, buttons, status, help },
		});

		Content = new ScrollView
		{
			Content = new VerticalStackLayout
			{
				Padding = new Thickness(14, 16, 14, 24),
				Spacing = 10,
				Children = { whoTitle, segment, totalsTitle, totalsCard, syncTitle, syncCard },
			},
		};

		Theme.Refresh += Render;
		// Only the status line changes when a sync happens, so update just that.
		App.Sync.StatusChanged += () => MainThread.BeginInvokeOnMainThread(() => status.Text = App.Sync.Status);
		ShowAddress();
		Render();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		ShowAddress(); // the Wi-Fi address can change between visits
		Render();
	}

	void ShowAddress()
	{
		var ip = SyncService.LocalIp();
		myAddr.Text = string.IsNullOrEmpty(ip) ? "Not on Wi-Fi" : ip;
	}

	static Label Heading(string text) => new()
	{
		Text = text,
		FontSize = 17,
		FontAttributes = FontAttributes.Bold,
		Margin = new Thickness(2, 8, 2, 0),
	};

	void Render()
	{
		Theme.StylePage(this);
		var s = App.Store;

		foreach (var h in new[] { whoTitle, totalsTitle, syncTitle }) h.TextColor = Theme.Ink;
		segment.BackgroundColor = Theme.Line;
		Theme.StyleButton(nickBtn, true, filled: s.Person == 0);
		Theme.StyleButton(whitBtn, true, filled: s.Person == 1);

		totals.Text = string.Join("\n", Defs.Names.Select((n, i) => $"{n}: {s.Points(i):N0} pts"));
		totals.TextColor = Theme.Ink;
		Theme.StylePanel(totalsCard);

		myAddr.TextColor = Theme.Accent;
		foreach (var l in new[] { myAddrLabel, peerLabel, help }) l.TextColor = Theme.Muted;
		peer.TextColor = Theme.Ink;
		peer.PlaceholderColor = Theme.Muted;
		peer.BackgroundColor = Theme.Bg;
		Theme.StyleButton(saveBtn, true);
		Theme.StyleButton(syncBtn, true, filled: false);
		syncBtn.BorderColor = Theme.Line;
		syncBtn.BorderWidth = 1;
		status.Text = App.Sync.Status;
		status.TextColor = Theme.Ink;
		Theme.StylePanel(syncCard);
	}
}
