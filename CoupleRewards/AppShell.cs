using CoupleRewards.Pages;

namespace CoupleRewards;

/// <summary>Bottom tab bar: Rewards, Tasks, Settings.</summary>
public class AppShell : Shell
{
	public AppShell()
	{
		FlyoutBehavior = FlyoutBehavior.Disabled;
		Items.Add(new TabBar
		{
			Items =
			{
				new ShellContent { Title = "Rewards", Icon = "tab_gift.png", ContentTemplate = new DataTemplate(typeof(RewardsPage)) },
				new ShellContent { Title = "Tasks", Icon = "tab_check.png", ContentTemplate = new DataTemplate(typeof(TasksPage)) },
				new ShellContent { Title = "Settings", Icon = "tab_gear.png", ContentTemplate = new DataTemplate(typeof(SettingsPage)) },
			},
		});
	}
}
