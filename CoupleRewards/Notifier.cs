using CoupleRewards.Core;
#if ANDROID
using Android.App;
using Android.Content;
using AndroidX.Core.App;
#elif IOS
using UserNotifications;
#endif

namespace CoupleRewards;

/// <summary>
/// Phone notifications for "your partner redeemed a reward". There is no server: these are local notifications,
/// shown when this phone receives the redeem during a Wi-Fi sync (so only while the app is open on both phones).
/// </summary>
public static class Notifier
{
	/// <summary>Ask for permission to show notifications (once; the phone remembers the answer).</summary>
	public static void Init()
	{
#if ANDROID
		Channel();
		if (OperatingSystem.IsAndroidVersionAtLeast(33))
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				try { await Permissions.RequestAsync<Permissions.PostNotifications>(); } catch { }
			});
#elif IOS
		var center = UNUserNotificationCenter.Current;
		center.Delegate = new ShowWhileOpen();
		center.RequestAuthorization(UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound, (_, _) => { });
#endif
	}

	public static void PartnerRedeemed(IReadOnlyList<Ev> redeems)
	{
		try
		{
			// One notification per reward; if a long backlog arrives at once, a single summary instead.
			if (redeems.Count > 3)
			{
				var who = string.Join(" & ", redeems.Select(e => Defs.Names[e.P]).Distinct());
				Show($"{who} redeemed {redeems.Count} rewards 🎉", "Open Couple Rewards to see them.");
				return;
			}
			foreach (var e in redeems)
			{
				var r = Defs.Rewards[e.P].FirstOrDefault(r => r.Key == e.I);
				var what = r != null ? $"{r.Icon} {r.Name}" : e.I;
				Show($"{Defs.Names[e.P]} redeemed a reward 🎉", $"{what} ({-e.V:N0} pts)");
			}
		}
		catch { } // a notification problem must never break syncing
	}

	static int nextId = (int)(DateTime.UtcNow.Ticks % 100000);

	static void Show(string title, string body)
	{
#if ANDROID
		var ctx = Android.App.Application.Context;
		Channel();
		var open = ctx.PackageManager?.GetLaunchIntentForPackage(ctx.PackageName!);
		var tap = open == null ? null
			: PendingIntent.GetActivity(ctx, 0, open, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
		var n = new NotificationCompat.Builder(ctx, ChannelId)
			.SetSmallIcon(Resource.Mipmap.appicon)
			.SetContentTitle(title)
			.SetContentText(body)
			.SetPriority(NotificationCompat.PriorityHigh)
			.SetAutoCancel(true)
			.SetContentIntent(tap)
			.Build();
		NotificationManagerCompat.From(ctx).Notify(Interlocked.Increment(ref nextId), n);
#elif IOS
		var content = new UNMutableNotificationContent { Title = title, Body = body, Sound = UNNotificationSound.Default };
		var request = UNNotificationRequest.FromIdentifier($"redeem-{Interlocked.Increment(ref nextId)}", content, null);
		UNUserNotificationCenter.Current.AddNotificationRequest(request, _ => { });
#endif
	}

#if ANDROID
	const string ChannelId = "partner_redeems";

	static void Channel()
	{
		if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;
		var nm = (NotificationManager?)Android.App.Application.Context.GetSystemService(Context.NotificationService);
		nm?.CreateNotificationChannel(new NotificationChannel(ChannelId, "Partner’s rewards", NotificationImportance.High)
		{
			Description = "When your partner redeems a reward",
		});
	}
#elif IOS
	/// <summary>iPhones hide notifications from the app that's on screen unless told otherwise.</summary>
	class ShowWhileOpen : UNUserNotificationCenterDelegate
	{
		public override void WillPresentNotification(UNUserNotificationCenter center, UNNotification notification,
			Action<UNNotificationPresentationOptions> completionHandler) =>
			completionHandler(UNNotificationPresentationOptions.Banner | UNNotificationPresentationOptions.List
				| UNNotificationPresentationOptions.Sound);
	}
#endif
}
