using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using PoePartyPlugin.Networking;
using SharpDX;
using System.Net;
using System;
using System.Net.Sockets;
using System.Threading;
using Vector2 = System.Numerics.Vector2;
using ExileCore.PoEMemory.Components;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using System.Linq;

namespace PoePartyPlugin;

public class PoePartyPlugin : BaseSettingsPlugin<PoePartyPluginSettings>
{

    public PartyServer PartyServer;
    public PartyMember Me;
    private Thread clientListenerThread;
    private async Task<string> ScanForPartyServer(int port, int timeoutMs = 300)
    {
        string localSubnet = "192.168.1."; // à adapter selon ton réseau

        for (int i = 1; i < 255; i++)
        {
            string ip = localSubnet + i;
            using var client = new TcpClient();

            try
            {
                var connectTask = client.ConnectAsync(IPAddress.Parse(ip), port);
                if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs)) == connectTask && client.Connected)
                {
                    LogMessage($"Serveur trouvé : {ip}");
                    return ip;
                }
            }
            catch { } // Ignore les échecs de connexion
        }

        LogMessage("Aucun serveur trouvé sur le LAN.");
        return null;
    }
    private async void ConnectAsClient()
    {
        if (Me != null && Me.IsConnected)
        {
            LogMessage("Déjà connecté.");
            return;
        }

        try
        {
            Me = new PartyMember
            {
                Name = GameController.Player.GetComponent<Player>().PlayerName ?? "Unknown",
                Role = "Follower",
                IsLocal = false,
                IPAddress = PartyServer.GetLocalIPv4(),
                TcpClient = new TcpClient()
            };

            string ip = await ScanForPartyServer(int.Parse(Settings.ServerSettings.Port));
            if (ip == null)
            {
                LogError("Impossible de localiser un serveur Party sur le LAN.");
                return;
            }
            Me.TcpClient.Connect(IPAddress.Parse(ip), int.Parse(Settings.ServerSettings.Port));
            var stream = Me.Stream;
            Me.IsConnected = true;

            LogMessage($"Connecté en tant que {Me.Name} à {Me.IPAddress}");

            var hello = new PartyMessage
            {
                Type = "Hello",
                Sender = Me.Name,
                Content = ""
            };

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(hello);
            byte[] data = Encoding.UTF8.GetBytes(json);
            stream.Write(data, 0, data.Length);

            clientListenerThread = new Thread(ClientListenerLoop) { IsBackground = true };
            clientListenerThread.Start();
        }
        catch (Exception ex)
        {
            LogError($"Connexion échouée : {ex.Message}");
            this.DisconnectClientOrServer();
        }
    }
    private void ClientListenerLoop()
    {
        var buffer = new byte[4096];

        try
        {
            while (Me.IsConnected && Me.TcpClient.Connected)
            {
                if (Me.Stream.DataAvailable)
                {
                    int bytesRead = Me.Stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    var json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    // With this corrected line:
                    var msg = Newtonsoft.Json.JsonConvert.DeserializeObject<PartyMessage>(json);
                    LogMessage($"[Serveur] {msg.Type} — {msg.Content}");
                }
                else
                {
                    Thread.Sleep(50);
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Erreur client : {ex.Message}");
        }
        finally
        {
            Me.Disconnect();
            Me.IsConnected = false;
            LogMessage("Déconnecté du serveur.");
        }
    }
    public override bool Initialise()
    {

        Settings.ServerSettings.ToggleServer.OnValueChanged += (toggle, value) =>
        {
            ToggleServerConnect(value);
        };
        ToggleServerConnect(Settings.ServerSettings.ToggleServer.Value);

        Settings.Enable.OnValueChanged += (toggle, value) =>
        {
            if (value)
            {
                LogMessage("PoePartyPlugin enabled.");
                ToggleServerConnect(Settings.ServerSettings.ToggleServer.Value);
            }
            else
            {
                LogMessage("PoePartyPlugin disabled.");
                if (PartyServer != null)
                {
                    PartyServer.Stop();
                    PartyServer = null;
                }
            }
        };

        Settings.PartySettings.ConnectToServer.OnPressed += () =>
        {
            //handle cannot connect to own server if ServerSettings.toggleServer is true
            if (!Settings.ServerSettings.ToggleServer)
            {
                if (Me == null)
                {
                    ConnectAsClient();
                }
                else
                {
                    LogMessage("Already connected to the server.");
                }
                return;
            }
        };
        return true;
    }
    private void ToggleServerConnect(bool value)
    {
        if (value)
        {
            PartyServer = new PartyServer(this);
            PartyServer.Start();
            LogMessage("Party server started.");
        }
        else
        {
            if (PartyServer == null) return;
            PartyServer.Stop();
            PartyServer = null;
            LogMessage("Party server stopped.");
        }
    }
    public override Job Tick()
    {
        return null;
    }
    public override void Render()
    {
        if (PartyServer != null && PartyServer.IsRunning)
        {

            if (this.IsAnyPanelOpen()) return;
            var text = PartyServer.ToString();
            var textSize = Graphics.MeasureText(text);
            var partyRect = GameController.IngameState.IngameUi.PartyElement.GetClientRect();
            var position = new Vector2(.2f, partyRect.Top - textSize.Y - 0.2f);

            Graphics.DrawTextWithBackground(
                text,
                position,
                Color.Green,
                FontAlign.Left,
                new SharpDX.Color(0, 0, 0, 180)
            );

            Graphics.DrawFrame(partyRect, Color.Green, 3);
        }
    }
    public override void Dispose()
    {
        this.DisconnectClientOrServer();
        base.Dispose();
    }
    public override void OnClose()
    {
        this.DisconnectClientOrServer();
        base.OnClose();
    }
    public override void OnUnload()
    {
        this.DisconnectClientOrServer();
        base.OnUnload();
    }

}
public static class PoePartyPluginExtensions
{
    public static Element PartyElement(this PoePartyPlugin p)
    {
        return p.GameController.IngameState.IngameUi.PartyElement;
    }

    public static bool IsAnyPanelOpen(this PoePartyPlugin p)
    {
        var ui = p.GameController.IngameState.IngameUi;
        return ui.FullscreenPanels.Any(p => p.IsVisible) ||
            ui.LargePanels.Any(p => p.IsVisible) ||
            ui.OpenLeftPanel.IsVisible;
    }
    public static void DisconnectClientOrServer(this PoePartyPlugin p)
    {
        if (p.PartyServer != null)
        {
            p.PartyServer.Stop();
            p.PartyServer = null;
            p.LogMessage("Party server disconnected.");
        }
        else if (p.Me?.IsConnected == true)
        {
            p.Me?.Disconnect();
            p.LogMessage("Disconnected from the server.");
            p.Me.IsConnected = false;
            p.Me = null;
        }
    }
}