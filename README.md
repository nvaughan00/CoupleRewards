# Couple Rewards (.NET MAUI)

A rewards app for Nick and Whitney that runs on Android and iPhone. Points are saved on each phone, and the two phones sync directly over your home Wi-Fi. There's no server, account or internet needed.

## What you need

- **Visual Studio 2026** (Community is free) with the **.NET Multi-platform App UI development** workload ticked in the Visual Studio Installer. This project targets .NET 10.
- **Android phone:** just a USB cable.
- **iPhone:** a Mac is required. Visual Studio 2026 builds for iPhone through "Pair to Mac", and Apple only allows iPhone apps to be signed on a Mac. You also need an Apple ID; see the iPhone section.

## Open the project

1. Unzip, then double-click `CoupleRewards.sln`.
2. Let Visual Studio restore packages (first open takes a few minutes).
3. If it asks to install Android SDK components, say yes.

## Install on an Android phone

1. On the phone: **Settings → About phone**, tap **Build number** 7 times to unlock Developer options.
2. **Settings → System → Developer options** → turn on **USB debugging**.
3. Plug the phone into the PC and tap **Allow** on the phone's prompt.
4. In Visual Studio, open the run target dropdown (the green ▶ button), choose **Android Local Devices**, and pick your phone.
5. Press ▶. The app installs and stays on the phone after you unplug it.

To get a standalone file instead: switch to **Release**, right-click the project → **Publish**, and copy the `.apk` to the phone.

## Install on an iPhone

1. On a Mac, install Xcode from the App Store and open it once.
2. In Visual Studio: **Tools → iOS → Pair to Mac** and connect to the Mac (both on the same network).
3. Add your Apple ID in Visual Studio (**Tools → Options**, search for "Apple Accounts"), then in the project's properties under **iOS → Bundle Signing** choose **Automatic Provisioning** and your Apple ID's team.
4. Plug the iPhone into the **Mac**, choose **iOS Local Devices** in the run dropdown, and press ▶.
5. On the iPhone: **Settings → Privacy & Security → Developer Mode** on, and trust your developer certificate under **Settings → General → VPN & Device Management**.

A free Apple ID works, but apps signed with one stop opening after about 7 days and need reinstalling. A paid Apple Developer membership extends that to a year.

## Set up sync (once)

1. Install on both phones and connect both to the **same Wi-Fi**.
2. On each phone, go to **Settings** and pick who this phone belongs to (Nick or Whitney).
3. On one phone, type the address shown on the **other** phone into **Partner's address** and tap **Save and sync**.

That's it. Both phones swap new points every few seconds while the app is open on both. Anything done while you're apart syncs the next time you're both home with the app open. The iPhone will ask once to allow access to the local network; tap **Allow**.

If the status says it can't reach the partner's phone: check both are on the same Wi-Fi (not a guest network, which usually blocks phone-to-phone traffic) and that the app is open on both. Home routers can hand out a new address now and then; just re-enter it.

## Change the rewards and tasks

Edit `CoupleRewards/Core/Defs.cs`. Each person has 10 tasks and 10 rewards.

- A task with several point values is a chain: `T("water", "💧", "Drink water", 20, 50, 100)` means ×1 = 20, ×2 = 50, ×3 = 100.
- Keep each `key` (the first value) unique per person and don't reuse an old key for something new.
- Reinstall on **both** phones after editing so their lists match.

The celebration size scales with the reward's cost: about 1,500 points gets a small firework, 5,000+ adds screen shake, 6,000+ adds confetti, and 9,000+ gets the full party with a flash, shockwave and emoji rain. That logic is in `FxLayer.cs` (`Celebrate`).

## How it works

- `Core/Store.cs`: every task completion and redemption is saved as an entry in a small JSON file on the phone. Point totals are added up from these entries, which is what lets the two phones merge cleanly. Tasks reset daily because "done today" just means "has an entry dated today".
- `Core/SyncService.cs`: each phone listens on port 5055 and connects to the partner's address to exchange any entries the other one is missing.
- `Pages/`: the Rewards, Tasks and Settings screens, written in C# (no XAML).
