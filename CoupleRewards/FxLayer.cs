namespace CoupleRewards;

/// <summary>
/// Transparent layer laid over a page for fireworks, confetti, the white flash and the toast message.
/// It never blocks taps. All particle work happens on the main thread.
/// </summary>
public class FxLayer : Grid, IDrawable
{
	class Particle
	{
		public float X, Y, VX, VY, G, Size;
		public int Life, MaxLife;
		public Color C = Colors.White;
		public string? Emoji;
	}

	class Ring { public float X, Y, R, A; }

	static readonly Random Rnd = new();
	static readonly string[] PartyEmoji = { "🎉", "🎊", "🥳", "🎈", "💖" };

	readonly GraphicsView canvas;
	readonly BoxView flash;
	readonly Border toast;
	readonly Label toastText;
	readonly List<Particle> particles = new();
	readonly List<Ring> rings = new();
	IDispatcherTimer? timer;
	double flashAlpha;
	int toastVersion;

	public FxLayer()
	{
		InputTransparent = true;
		CascadeInputTransparent = true;

		canvas = new GraphicsView { Drawable = this, BackgroundColor = Colors.Transparent, InputTransparent = true };
		flash = new BoxView { Color = Colors.White, Opacity = 0, InputTransparent = true };
		toastText = new Label { FontSize = 20, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center };
		toast = new Border
		{
			Content = toastText,
			Padding = new Thickness(22, 16),
			StrokeThickness = 0,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			Margin = new Thickness(24, 0, 24, 140),
			Opacity = 0,
			InputTransparent = true,
			Shadow = new Shadow { Brush = new SolidColorBrush(Colors.Black), Opacity = 0.35f, Radius = 24, Offset = new Point(0, 8) },
		};

		Children.Add(canvas);
		Children.Add(flash);
		Children.Add(toast);
	}

	float W => (float)Math.Max(Width, 1);
	float H => (float)Math.Max(Height, 1);

	static Color RandomColor() => Color.FromHsla(Rnd.NextDouble(), 0.95, 0.6);

	// ---------------- building blocks ----------------

	/// <summary>A firework burst of <paramref name="count"/> sparks at (x, y).</summary>
	public void Burst(double x, double y, int count, double speed)
	{
		var c = RandomColor();
		for (int i = 0; i < count; i++)
		{
			var a = Rnd.NextDouble() * Math.PI * 2;
			var v = Rnd.NextDouble() * speed;
			var life = 50 + Rnd.Next(30);
			particles.Add(new Particle
			{
				X = (float)x, Y = (float)y,
				VX = (float)(Math.Cos(a) * v), VY = (float)(Math.Sin(a) * v),
				G = 0.06f, Size = 3.5f, Life = life, MaxLife = life, C = c,
			});
		}
		Kick();
	}

	void Confetti(bool withEmoji)
	{
		var life = 160 + Rnd.Next(60);
		particles.Add(new Particle
		{
			X = (float)(Rnd.NextDouble() * W), Y = -20,
			VX = (float)(Rnd.NextDouble() * 2 - 1), VY = (float)(2 + Rnd.NextDouble() * 3),
			G = 0.02f, Size = 7, Life = life, MaxLife = life, C = RandomColor(),
			Emoji = withEmoji && Rnd.NextDouble() < 0.3 ? PartyEmoji[Rnd.Next(PartyEmoji.Length)] : null,
		});
		Kick();
	}

	public async void Toast(string text, int ms = 1200)
	{
		var mine = ++toastVersion;
		toastText.Text = text;
		toastText.TextColor = Theme.Ink;
		toast.BackgroundColor = Theme.Card;
		toast.Opacity = 1;
		toast.Scale = 0.85;
		for (int i = 1; i <= 6; i++) { toast.Scale = 0.85 + 0.15 * i / 6; await Task.Delay(16); }
		await Task.Delay(ms);
		for (int i = 5; i >= 0 && mine == toastVersion; i--) { toast.Opacity = i / 6.0; await Task.Delay(30); }
	}

	static async void Shake(VisualElement target, double strength)
	{
		const int frames = 24;
		for (int i = 0; i < frames; i++)
		{
			var d = strength * (1 - (double)i / frames);
			target.TranslationX = (Rnd.NextDouble() - 0.5) * 2 * d;
			target.TranslationY = (Rnd.NextDouble() - 0.5) * 2 * d;
			await Task.Delay(33);
		}
		target.TranslationX = 0;
		target.TranslationY = 0;
	}

	static void Buzz(bool big)
	{
		try
		{
			if (big) Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(450));
			else HapticFeedback.Default.Perform(HapticFeedbackType.Click);
		}
		catch { } // some devices have no vibration motor
	}

	// ---------------- the redemption party ----------------

	/// <summary>
	/// Scales with the reward's cost: 1,500 points is a small firework,
	/// 5,000+ adds screen shake, 6,000+ adds confetti, 9,000+ adds the flash, shockwave and emoji party.
	/// </summary>
	public async void Celebrate(int cost, string name, VisualElement shakeTarget)
	{
		var p = Math.Min(cost / 10000.0, 1.2);
		Toast("🎉 " + name + "!", 3200);
		Buzz(p >= 0.5);

		if (p >= 0.5) Shake(shakeTarget, p * 14);
		if (p >= 0.9)
		{
			flashAlpha = 0.85;
			rings.Add(new Ring { X = W / 2, Y = H / 2, R = 10, A = 1 });
			Kick();
		}
		if (p >= 0.6) _ = ConfettiRain((int)(p * 150), p >= 0.9);

		var bursts = (int)Math.Ceiling(p * 6);
		var sparks = 24 + (int)Math.Round(p * 44);
		for (int b = 0; b < bursts; b++)
		{
			Burst(W * (0.15 + Rnd.NextDouble() * 0.7), H * (0.12 + Rnd.NextDouble() * 0.4), sparks, 3 + p * 4);
			if (p > 0.5) await Task.Delay(230);
		}
	}

	async Task ConfettiRain(int count, bool withEmoji)
	{
		for (int i = 0; i < count; i++)
		{
			Confetti(withEmoji);
			await Task.Delay(12);
		}
	}

	// ---------------- animation loop ----------------

	void Kick()
	{
		if (timer != null) return;
		timer = Dispatcher.CreateTimer();
		timer.Interval = TimeSpan.FromMilliseconds(16);
		timer.Tick += (_, _) => Step();
		timer.Start();
	}

	void Step()
	{
		foreach (var p in particles)
		{
			p.X += p.VX; p.Y += p.VY; p.VY += p.G; p.VX *= 0.99f; p.Life--;
		}
		particles.RemoveAll(p => p.Life <= 0 || p.Y > H + 40);

		foreach (var r in rings) { r.R += 16; r.A -= 0.02f; }
		rings.RemoveAll(r => r.A <= 0);

		if (flashAlpha > 0) flashAlpha = Math.Max(0, flashAlpha - 0.05);
		flash.Opacity = flashAlpha;

		canvas.Invalidate();
		if (particles.Count == 0 && rings.Count == 0 && flashAlpha <= 0)
		{
			timer?.Stop();
			timer = null;
		}
	}

	public void Draw(ICanvas c, RectF dirtyRect)
	{
		foreach (var r in rings)
		{
			c.StrokeColor = Color.FromRgba(255, 160, 40, (int)(r.A * 255));
			c.StrokeSize = 12;
			c.DrawCircle(r.X, r.Y, r.R);
		}
		foreach (var p in particles)
		{
			var a = Math.Min(1f, p.Life / 30f);
			if (p.Emoji != null)
			{
				c.FontSize = 26;
				c.FontColor = Colors.Black.WithAlpha(a);
				c.DrawString(p.Emoji, p.X, p.Y, Microsoft.Maui.Graphics.HorizontalAlignment.Center);
			}
			else
			{
				c.FillColor = p.C.WithAlpha(a);
				c.FillRectangle(p.X, p.Y, p.Size, p.Size);
			}
		}
	}
}
