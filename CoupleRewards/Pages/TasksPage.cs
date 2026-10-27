using CoupleRewards.Core;

namespace CoupleRewards.Pages;

/// <summary>My Tasks: daily tasks (some chained x1, x2, x3) that add points. Everything resets at midnight.</summary>
public class TasksPage : ContentPage
{
	readonly Grid root;
	readonly PointsHeader header = new();
	readonly VerticalStackLayout list = new() { Spacing = 10, Padding = new Thickness(14, 8, 14, 20) };
	readonly FxLayer fx = new();
	readonly List<Row> rows = new();
	int builtFor = -1;
	IDispatcherTimer? midnight;

	public TasksPage()
	{
		Title = "Tasks";
		var body = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };
		body.Add(header, 0, 0);
		body.Add(new ScrollView { Content = list }, 0, 1);
		root = new Grid { Children = { body, fx } };
		Content = root;

		Theme.Refresh += Render;
		Render();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Render();
		// Re-check once a minute so the list resets at midnight while the app is open.
		midnight ??= Dispatcher.CreateTimer();
		midnight.Interval = TimeSpan.FromMinutes(1);
		midnight.Tick -= OnMinute;
		midnight.Tick += OnMinute;
		midnight.Start();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		midnight?.Stop();
	}

	void OnMinute(object? sender, EventArgs e)
	{
		App.Store.AwardAutoDone(); // a new day may have started while the app was open
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
			foreach (var t in Defs.Tasks[s.Person])
			{
				var row = new Row(t, Complete);
				rows.Add(row);
				list.Children.Add(row.View);
			}
		}
		header.Update();
		foreach (var row in rows) row.Update(s.Done(s.Person, row.Def.Key));
	}

	void Complete(Row row)
	{
		var s = App.Store;
		var where = Theme.PositionIn(row.Button, root);
		var gain = s.Complete(s.Person, row.Def);
		if (gain <= 0) return;
		try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
		fx.Burst(where.X, where.Y, 14, 3);
		fx.Toast($"+{gain} pts");
	}

	class Row
	{
		public TaskDef Def { get; }
		readonly IconTile icon = new();
		readonly Label name = new() { FontSize = 16, FontAttributes = FontAttributes.Bold };
		readonly Label sub = new() { FontSize = 13 };
		public Button Button { get; } = Theme.NewButton();
		public Border View { get; }

		public Row(TaskDef def, Action<Row> onDone)
		{
			Def = def;
			icon.Emoji = def.Icon;
			name.Text = def.NameFor(0);
			Button.Clicked += (_, _) => onDone(this);

			var g = new Grid
			{
				ColumnSpacing = 12,
				ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
			};
			g.Add(icon, 0, 0);
			g.Add(new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Children = { name, sub } }, 1, 0);
			g.Add(Button, 2, 0);
			View = Theme.Panel(g);
		}

		public void Update(int stepsDone)
		{
			var steps = Def.Steps;
			var finished = stepsDone >= steps.Length;
			var chain = steps.Length > 1;

			var dots = chain ? string.Join(" ", steps.Select((_, j) => j < stepsDone ? "●" : "○")) + " · " : "";
			sub.Text = finished
				? dots + (Def.AutoDone ? $"Auto-done today · +{steps.Sum()} pts ✓" : "Done for today ✓")
				: dots + $"+{steps[stepsDone]} pts" + (chain ? $" (×{stepsDone + 1})" : "");

			icon.Restyle();
			name.Text = Def.NameFor(stepsDone);
			name.TextColor = Theme.Ink;
			sub.TextColor = Theme.Muted;
			Button.Text = finished ? "✓" : "Done";
			Theme.StyleButton(Button, !finished);
			Theme.StylePanel(View, finished ? 0.65 : 1);
		}
	}
}
