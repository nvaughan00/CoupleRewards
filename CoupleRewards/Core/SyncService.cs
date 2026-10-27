using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace CoupleRewards.Core;

/// <summary>
/// Phone-to-phone sync over the local Wi-Fi, no server and no accounts.
/// Every phone (1) listens on a TCP port and (2) every few seconds connects to the partner's address.
/// Each connection swaps whatever ledger entries the other side is missing, in both directions,
/// so entering the address on just one of the two phones is enough for both to stay in sync.
/// Protocol (one JSON object per line): client hello -> server reply -> client reply -> "ok".
/// </summary>
public class SyncService
{
    public const int DefaultPort = 5055;
    readonly Store store;
    readonly SemaphoreSlim wake = new(0, 1);
    CancellationTokenSource? cts;
    TcpListener? listener;

    public int Port { get; }
    public string Status { get; private set; } = "Not started";
    public event Action? StatusChanged;

    public SyncService(Store store, int port = DefaultPort)
    {
        this.store = store;
        Port = port;
        store.Changed += Kick;
    }

    /// <summary>Sync right now instead of waiting for the next tick.</summary>
    public void Kick() { try { wake.Release(); } catch (SemaphoreFullException) { } }

    public void Start()
    {
        if (cts != null) return;
        cts = new CancellationTokenSource();
        var ct = cts.Token;
        _ = Task.Run(() => Listen(ct));
        _ = Task.Run(() => Loop(ct));
    }

    public void Stop()
    {
        cts?.Cancel();
        cts = null;
        try { listener?.Stop(); } catch { }
    }

    void SetStatus(string s)
    {
        if (s == Status) return;
        Status = s;
        StatusChanged?.Invoke();
    }

    static string Serialize(Msg m) => JsonSerializer.Serialize(m, RewardsJson.Default.Msg);
    static Msg? Parse(string? line) =>
        string.IsNullOrWhiteSpace(line) ? null : JsonSerializer.Deserialize(line, RewardsJson.Default.Msg);

    // ---------- server side ----------
    async Task Listen(CancellationToken ct)
    {
        TcpListener? l = null;
        try
        {
            l = new TcpListener(IPAddress.Any, Port);
            l.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            l.Start();
            listener = l;
            while (!ct.IsCancellationRequested)
            {
                var c = await l.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => Handle(c));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested) SetStatus("Can’t listen for your partner: " + ex.Message);
        }
        finally { try { l?.Stop(); } catch { } }
    }

    async Task Handle(TcpClient c)
    {
        try
        {
            using var client = c;
            using var t = new CancellationTokenSource(8000);
            using var s = client.GetStream();
            using var r = new StreamReader(s, Encoding.UTF8);
            using var w = new StreamWriter(s, new UTF8Encoding(false)) { AutoFlush = true };

            var hello = Parse(await r.ReadLineAsync(t.Token));
            if (hello?.Vv == null) return;
            await w.WriteLineAsync(Serialize(new Msg(store.Vector(), store.Since(hello.Vv))));
            var back = Parse(await r.ReadLineAsync(t.Token));
            if (back?.Events != null) store.Merge(back.Events);
            await w.WriteLineAsync("ok");
        }
        catch { }
    }

    // ---------- client side ----------
    async Task Loop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var peer = store.Peer;
            if (string.IsNullOrWhiteSpace(peer))
                SetStatus("Add your partner’s address to start syncing");
            else
            {
                try
                {
                    var (host, p) = Split(peer);
                    if (await Exchange(host, p, ct)) SetStatus("Synced at " + DateTime.Now.ToString("h:mm:ss tt"));
                    else SetStatus("Partner’s phone gave an unexpected reply");
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch { SetStatus("Can’t reach your partner’s phone. Is the app open on the same Wi-Fi?"); }
            }
            try { await wake.WaitAsync(TimeSpan.FromSeconds(4), ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    async Task<bool> Exchange(string host, int p, CancellationToken ct)
    {
        using var t = CancellationTokenSource.CreateLinkedTokenSource(ct);
        t.CancelAfter(8000);
        using var c = new TcpClient();
        await c.ConnectAsync(host, p, t.Token);
        using var s = c.GetStream();
        using var r = new StreamReader(s, Encoding.UTF8);
        using var w = new StreamWriter(s, new UTF8Encoding(false)) { AutoFlush = true };

        await w.WriteLineAsync(Serialize(new Msg(store.Vector(), null)));
        var theirs = Parse(await r.ReadLineAsync(t.Token));
        if (theirs?.Vv == null) return false;
        if (theirs.Events != null) store.Merge(theirs.Events);
        await w.WriteLineAsync(Serialize(new Msg(null, store.Since(theirs.Vv))));
        return await r.ReadLineAsync(t.Token) == "ok";
    }

    static (string host, int port) Split(string s)
    {
        s = s.Trim();
        var i = s.LastIndexOf(':');
        if (i > 0 && int.TryParse(s[(i + 1)..], out var p)) return (s[..i], p);
        return (s, DefaultPort);
    }

    /// <summary>This phone's Wi-Fi address, shown in Settings so the partner can type it in.</summary>
    public static string LocalIp()
    {
        // 1) A home-network address on the Wi-Fi adapter (Android: wlan0, iPhone: en0).
        string? any = null;
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var b = ua.Address.GetAddressBytes();
                    var home = b[0] == 10 || (b[0] == 192 && b[1] == 168) || (b[0] == 172 && b[1] >= 16 && b[1] <= 31);
                    if (!home) continue;
                    if (ni.Name.StartsWith("wlan", StringComparison.OrdinalIgnoreCase) || ni.Name == "en0")
                        return ua.Address.ToString();
                    any ??= ua.Address.ToString();
                }
            }
        }
        catch { }
        if (any != null) return any;

        // 2) Ask the OS which local address it would use to go online. ("Connecting" UDP sends nothing.)
        try
        {
            using var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sock.Connect(IPAddress.Parse("8.8.8.8"), 53);
            if (sock.LocalEndPoint is IPEndPoint ep) return ep.Address.ToString();
        }
        catch { }
        return "";
    }
}
