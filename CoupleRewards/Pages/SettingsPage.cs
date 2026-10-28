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
	readonly Button clearBtn = Theme.NewButton();
	readonly Button resetDayBtn = Theme.NewButton();
	readonly Entry manualPts = new() { Placeholder = "Points (use - to take away)", Keyboard = Keyboard.Telephone, FontSize = 17 };
	readonly Button addPtsBtn = Theme.NewButton();
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

		clearBtn.Clicked += (_, _) => ClearPoints();
		resetDayBtn.Clicked += (_, _) => ResetDay();
		addPtsBtn.Text = "Add";
		addPtsBtn.Clicked += (_, _) => AddPoints();
		manualPts.Completed += (_, _) => AddPoints();
		var manualRow = new Grid
		{
			ColumnSpacing = 8,
			ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
		};
		manualRow.Add(manualPts, 0, 0);
		manualRow.Add(addPtsBtn, 1, 0);
		totalsCard = Theme.Panel(new VerticalStackLayout
		{
			Spacing = 10,
			Children = { totals, manualRow, resetDayBtn, clearBtn },
		});

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

	async void ClearPoints()
	{
		var s = App.Store;
		var p = s.Person;
		var who = Defs.Names[p];
		var pts = s.Points(p);
		if (pts == 0) return;
		var ok = await DisplayAlertAsync($"Clear {who}’s points?",
			$"This sets {who}’s {pts:N0} points to 0 on both phones. It can’t be undone.", "Clear points", "Cancel");
		if (!ok) return;
		s.Clear(p);
		Render();
	}

	async void ResetDay()
	{
		var s = App.Store;
		var p = s.Person;
		var who = Defs.Names[p];
		if (!s.AnyDoneToday(p)) return;
		var ok = await DisplayAlertAsync($"Reset {who}’s day?",
			$"All of {who}’s tasks become available again for today, on both phones. Points already earned are kept.",
			"Reset day", "Cancel");
		if (!ok) return;
		s.ResetDay(p);
		Render();
	}

	async void AddPoints()
	{
		var s = App.Store;
		var p = s.Person;
		var who = Defs.Names[p];
		var text = (manualPts.Text ?? "").Replace(",", "").Replace(" ", "").Replace("+", "");
		if (!int.TryParse(text, out var amount) || amount == 0)
		{
			await DisplayAlertAsync("Enter a number", "Type how many points to add, like 250. Use a minus sign to take points away, like -100.", "OK");
			return;
		}
		var pts = s.Points(p);
		if (amount < 0 && pts + amount < 0)
		{
			await DisplayAlertAsync("Not enough points", $"{who} only has {pts:N0} points.", "OK");
			return;
		}
		var ok = await DisplayAlertAsync(
			amount > 0 ? $"Add {amount:N0} points to {who}?" : $"Take {-amount:N0} points from {who}?",
			$"{who} will have {pts + amount:N0} points.", amount > 0 ? "Add" : "Take away", "Cancel");
		if (!ok) return;
		manualPts.Unfocus();
		s.Bonus(p, amount);
		manualPts.Text = "";
		Render();
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
		var canClear = s.Points(s.Person) != 0;
		clearBtn.Text = $"Clear {Defs.Names[s.Person]}’s points";
		Theme.StyleButton(clearBtn, canClear, filled: false);
		if (canClear) clearBtn.TextColor = Color.FromArgb("#D6453D"); // red: destructive
		clearBtn.BorderColor = Theme.Line;
		clearBtn.BorderWidth = 1;
		resetDayBtn.Text = $"Reset {Defs.Names[s.Person]}’s day";
		Theme.StyleButton(resetDayBtn, s.AnyDoneToday(s.Person), filled: false);
		resetDayBtn.BorderColor = Theme.Line;
		resetDayBtn.BorderWidth = 1;
		manualPts.TextColor = Theme.Ink;
		manualPts.PlaceholderColor = Theme.Muted;
		manualPts.BackgroundColor = Theme.Bg;
		Theme.StyleButton(addPtsBtn, true);
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
