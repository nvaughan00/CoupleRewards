using Microsoft.Maui.Controls.Shapes;

namespace CoupleRewards;

/// <summary>Colors and small shared UI helpers. The accent color follows whose rewards are showing.</summary>
public static class Theme
{
	public static readonly Color NickAccent = Color.FromArgb("#2F6FE4");
	public static readonly Color WhitneyAccent = Color.FromArgb("#D93A5E");

	public static Color Accent => App.Store.Person == 1 ? WhitneyAccent : NickAccent;

	public static bool Dark => Application.Current?.RequestedTheme == AppTheme.Dark;
	public static Color Bg => Dark ? Color.FromArgb("#17121B") : Color.FromArgb("#FFF6F2");
	public static Color Card => Dark ? Color.FromArgb("#241C2B") : Colors.White;
	public static Color Ink => Dark ? Color.FromArgb("#F6EDF6") : Color.FromArgb("#2B2030");
	public static Color Muted => Dark ? Color.FromArgb("#A897AB") : Color.FromArgb("#85778A");
	public static Color Line => Dark ? Color.FromArgb("#35293D") : Color.FromArgb("#F1E1E3");

	/// <summary>Raised on the main thread whenever anything visible may need updating (data, theme, app resumed).</summary>
	public static event Action? Refresh;
	public static void RaiseRefresh() => MainThread.BeginInvokeOnMainThread(() => Refresh?.Invoke());

	public static void StylePage(ContentPage page)
	{
		page.BackgroundColor = Bg;
		page.SafeAreaEdges = SafeAreaEdges.All; // keep content clear of the notch, status bar and keyboard
		Shell.SetNavBarIsVisible(page, false);
		Shell.SetTabBarBackgroundColor(page, Card);
		Shell.SetTabBarForegroundColor(page, Accent);
		Shell.SetTabBarTitleColor(page, Accent);
		Shell.SetTabBarUnselectedColor(page, Muted);
	}

	public static Border Panel(View content) => new()
	{
		Content = content,
		Padding = new Thickness(12),
		StrokeThickness = 1,
		StrokeShape = new RoundRectangle { CornerRadius = 16 },
	};

	public static void StylePanel(Border b, double opacity = 1)
	{
		b.BackgroundColor = Card;
		b.Stroke = new SolidColorBrush(Line);
		b.Opacity = opacity;
	}

	public static Button NewButton() => new()
	{
		FontAttributes = FontAttributes.Bold,
		FontSize = 15,
		CornerRadius = 12,
		Padding = new Thickness(14, 8),
		MinimumHeightRequest = 44,
		MinimumWidthRequest = 88,
		VerticalOptions = LayoutOptions.Center,
	};

	public static void StyleButton(Button b, bool enabled, bool filled = true)
	{
		b.IsEnabled = enabled;
		b.BackgroundColor = !enabled ? Line : filled ? Accent : Colors.Transparent;
		b.TextColor = !enabled ? Muted : filled ? Colors.White : Ink;
	}

	/// <summary>Where an element sits inside <paramref name="root"/> (center-top), allowing for scrolling.</summary>
	public static Point PositionIn(VisualElement v, VisualElement root)
	{
		double x = v.Width / 2, y = 0;
		Element? e = v;
		while (e is VisualElement ve && ve != root)
		{
			x += ve.X + ve.TranslationX;
			y += ve.Y + ve.TranslationY;
			if (ve.Parent is ScrollView sv) { x -= sv.ScrollX; y -= sv.ScrollY; }
			e = ve.Parent;
		}
		return new Point(x, y);
	}
}

/// <summary>The "Nick’s points / 1,234" block at the top of Rewards and Tasks.</summary>
public class PointsHeader : VerticalStackLayout
{
	readonly Label who = new() { FontSize = 14 };
	readonly Label pts = new() { FontSize = 40, FontAttributes = FontAttributes.Bold };

	public PointsHeader()
	{
		Padding = new Thickness(18, 14, 18, 6);
		Children.Add(who);
		Children.Add(pts);
	}

	public void Update()
	{
		var s = App.Store;
		who.Text = $"{s.Who}’s points";
		who.TextColor = Theme.Muted;
		pts.Text = s.Points(s.Person).ToString("N0");
		pts.TextColor = Theme.Accent;
	}
}

/// <summary>Rounded square holding an emoji.</summary>
public class IconTile : Border
{
	readonly Label label = new() { FontSize = 24, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };

	public IconTile()
	{
		WidthRequest = 46;
		HeightRequest = 46;
		StrokeThickness = 0;
		StrokeShape = new RoundRectangle { CornerRadius = 12 };
		VerticalOptions = LayoutOptions.Center;
		Content = label;
	}

	public string Emoji { set => label.Text = value; }
	public void Restyle() => BackgroundColor = Theme.Bg;
}
