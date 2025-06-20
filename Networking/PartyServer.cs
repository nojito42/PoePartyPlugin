using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PoePartyPlugin.Networking;

public class PartyMessage
{
    public string Type { get; set; }
    public string Content { get; set; }
    public string Sender { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class PartyMember
{
    public string Name { get; set; }
    public string Role { get; set; } = "Follower";
    public string IPAddress { get; set; }
    public bool IsLocal { get; set; } = false;
    public DateTime LastSeen { get; set; } = DateTime.Now;

    public TcpClient TcpClient { get; set; }
    public NetworkStream Stream => TcpClient?.GetStream();

    public void Disconnect()
    {
        try { Stream?.Close(); TcpClient?.Close(); } catch { }
    }
}

internal class PartyServer(PoePartyPlugin plugin)
{
    public readonly PoePartyPlugin Plugin = plugin;
    private TcpListener _listener;
    private Thread _listenerThread;
    private readonly ConcurrentDictionary<string, PartyMember> _partyMembers = new();
    public bool IsRunning = false;

    public string ServerIP { get; set; }
    public int ConnectedPartyMembers => _partyMembers.Count;

    public static string GetLocalIPv4()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();
        }
        return IPAddress.Loopback.ToString();
    }

    public void Start()
    {
        if (IsRunning) return;

        ServerIP = GetLocalIPv4();
        if (!int.TryParse(Plugin.Settings.ServerSettings.Port, out int port))
        {
            Plugin.LogMessage("Port invalide. Vérifiez les paramètres.");
            return;
        }

        try
        {
            _listener = new TcpListener(IPAddress.Parse(ServerIP), port);
            _listener.Start();
            IsRunning = true;
            Plugin.LogMessage($"PartyServer lancé sur {ServerIP}:{port}");

            _listenerThread = new Thread(ListenerLoop) { IsBackground = true };
            _listenerThread.Start();
        }
        catch (Exception ex)
        {
            Plugin.LogMessage($"Erreur lors du démarrage du serveur : {ex.Message}");
            Stop();
        }
    }

    private void ListenerLoop()
    {
        try
        {
            while (IsRunning)
            {
                if (_listener.Pending())
                {
                    var tcpClient = _listener.AcceptTcpClient();
                    var clientIP = ((IPEndPoint)tcpClient.Client.RemoteEndPoint)?.Address.ToString();

                    if (_partyMembers.Values.Any(c => c.IPAddress == clientIP))
                    {
                        Plugin.LogMessage($"Connexion refusée pour {clientIP} : IP déjà connectée");
                        tcpClient.Close();
                        continue;
                    }

                    var member = new PartyMember
                    {
                        TcpClient = tcpClient,
                        IPAddress = clientIP
                    };

                    Plugin.LogMessage($"Connexion entrante depuis : {member.IPAddress}");
                    Task.Run(() => HandleClient(member));
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.LogMessage($"Erreur dans la boucle serveur : {ex.Message}");
        }
    }

    private async void HandleClient(PartyMember member)
    {
        try
        {
            var buffer = new byte[4096];
            var stream = member.Stream;
            bool identified = false;

            while (IsRunning && member.TcpClient.Connected)
            {
                if (stream.DataAvailable)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Plugin.LogMessage($"Message brut de {member.IPAddress}: {json}");

                    var msg = JsonSerializer.Deserialize<PartyMessage>(json);

                    if (!identified && msg.Type == "Hello")
                    {
                        member.Name = msg.Sender;

                        if (!member.IsMemberOnSameParty(Plugin))
                        {
                            Plugin.LogMessage($"Refusé : {member.Name} n'est pas dans la party locale.");
                            member.Disconnect();
                            return;
                        }

                        if (_partyMembers.ContainsKey(member.Name))
                        {
                            Plugin.LogMessage($"Nom déjà utilisé : {member.Name}");
                            member.Disconnect();
                            return;
                        }

                        _partyMembers.TryAdd(member.Name, member);
                        Plugin.LogMessage($"Client accepté : {member.Name} ({member.IPAddress})");
                        identified = true;

                        await SendMessageToClient(member.Name, new PartyMessage
                        {
                            Type = "Welcome",
                            Content = "Connected",
                            Sender = "Server"
                        });
                    }
                    else if (identified)
                    {
                        member.LastSeen = DateTime.Now;
                        Plugin.LogMessage($"[{msg.Sender}] {msg.Type} — {msg.Content}");
                    }
                }
                else
                {
                    await Task.Delay(50);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.LogMessage($"Erreur avec {member.Name ?? member.IPAddress}: {ex.Message}");
        }
        finally
        {
            if (!string.IsNullOrEmpty(member.Name))
                _partyMembers.TryRemove(member.Name, out _);

            member.Disconnect();
            Plugin.LogMessage($"Déconnexion de : {member.Name ?? member.IPAddress}");
        }
    }

    public async Task BroadcastMessage(PartyMessage message)
    {
        var json = JsonSerializer.Serialize(message);
        var data = Encoding.UTF8.GetBytes(json);

        var tasks = _partyMembers.Values
            .Where(m => m.TcpClient?.Connected == true)
            .Select(m => SendDataToClient(m, data));

        await Task.WhenAll(tasks);
    }

    public async Task SendMessageToClient(string name, PartyMessage message)
    {
        if (_partyMembers.TryGetValue(name, out var member) && member.TcpClient?.Connected == true)
        {
            var json = JsonSerializer.Serialize(message);
            var data = Encoding.UTF8.GetBytes(json);
            await SendDataToClient(member, data);
        }
    }

    private async Task SendDataToClient(PartyMember member, byte[] data)
    {
        try
        {
            await member.Stream.WriteAsync(data, 0, data.Length);
        }
        catch (Exception ex)
        {
            Plugin.LogMessage($"Erreur d'envoi à {member.Name}: {ex.Message}");
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;

        foreach (var m in _partyMembers.Values)
            m.Disconnect();

        _partyMembers.Clear();

        try
        {
            _listener?.Stop();
            _listenerThread?.Join(500);
        }
        catch (Exception ex)
        {
            Plugin.LogMessage($"Erreur à l'arrêt du serveur : {ex.Message}");
        }

        _listener = null;
        _listenerThread = null;

        Plugin.LogMessage("PartyServer arrêté.");
    }

    public override string ToString()
    {
        if (_listener?.Server.LocalEndPoint is IPEndPoint endpoint)
            return $"IP: {endpoint.Address}, Port: {endpoint.Port}, Clients: {ConnectedPartyMembers}";
        return "Serveur non initialisé";
    }
}

static class PartyServerExtensions
{
    public static bool IsMemberOnSameParty(this PartyMember member, PoePartyPlugin plugin)
    {
        return plugin.GameController
                     .IngameState
                     .IngameUi
                     .PartyElement
                     .PlayerElements
                     .Any(p => p.PlayerName == member.Name);
    }
}