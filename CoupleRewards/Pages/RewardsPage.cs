using CoupleRewards.Core;

namespace CoupleRewards.Pages;

/// <summary>My Rewards: current points, rewards to redeem, locked rewards, and the celebration.</summary>
public class RewardsPage : ContentPage
{
	readonly Grid body;
	readonly PointsHeader header = new();
	readonly VerticalStackLayout list = new() { Spacing = 10, Padding = new Thickness(14, 8, 14, 20) };
	readonly FxLayer fx = new();
	readonly List<Row> rows = new();
	int builtFor = -1;

	public RewardsPage()
	{
		Title = "Rewards";
		body = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };
		body.Add(header, 0, 0);
		body.Add(new ScrollView { Content = list }, 0, 1);
		Content = new Grid { Children = { body, fx } };

		Theme.Refresh += Render;
		Render();
	}

	void Render()
	{
		Theme.StylePage(this);
		var s = App.Store;
		if (builtFor != s.Person)
		{
			builtFor = s.Person;
			rows.Clear();
			list.Children.Clear();
			foreach (var r in Defs.Rewards[s.Person])
			{
				var row = new Row(r, Redeem);
				rows.Add(row);
				list.Children.Add(row.View);
			}
		}
		header.Update();
		var pts = s.Points(s.Person);
		foreach (var row in rows) row.Update(pts);
	}

	async void Redeem(RewardDef r)
	{
		var s = App.Store;
		var pts = s.Points(s.Person);
		if (pts < r.Cost) return;
		var ok = await DisplayAlertAsync($"Redeem {r.Name}?",
			$"This uses {r.Cost:N0} of {s.Who}’s {pts:N0} points.", "Redeem", "Cancel");
		if (!ok || !s.Redeem(s.Person, r)) return;
		fx.Celebrate(r.Cost, r.Name, body);
	}

	class Row
	{
		readonly RewardDef def;
		readonly IconTile icon = new();
		readonly Label name = new() { FontSize = 16, FontAttributes = FontAttributes.Bold };
		readonly Label sub = new() { FontSize = 13 };
		readonly Button button = Theme.NewButton();
		public Border View { get; }

		public Row(RewardDef def, Action<RewardDef> onRedeem)
		{
			this.def = def;
			name.Text = def.Name;
			button.Text = "Redeem";
			button.Clicked += (_, _) => onRedeem(def);

			var g = new Grid
			{
				ColumnSpacing = 12,
				ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
			};
			g.Add(icon, 0, 0);
			g.Add(new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Children = { name, sub } }, 1, 0);
			g.Add(button, 2, 0);
			View = Theme.Panel(g);
		}

		public void Update(int points)
		{
			var unlocked = points >= def.Cost;
			icon.Emoji = unlocked ? def.Icon : "🔒";
			icon.Restyle();
			name.TextColor = Theme.Ink;
			sub.TextColor = Theme.Muted;
			sub.Text = unlocked ? $"{def.Cost:N0} pts" : $"{def.Cost:N0} pts · {def.Cost - points:N0} to go";
			Theme.StyleButton(button, unlocked);
			Theme.StylePanel(View, unlocked ? 1 : 0.65);
		}
	}
}
