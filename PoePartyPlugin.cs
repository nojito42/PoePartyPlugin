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

namespace PoePartyPlugin;

public class PoePartyPlugin : BaseSettingsPlugin<PoePartyPluginSettings>
{

    private PartyServer _server;
    private PartyMember me;
    private Thread clientListenerThread;
    private bool isConnectedToServer = false;

    private void ConnectAsClient()
    {
        if (isConnectedToServer)
        {
            LogMessage("Déjà connecté.");
            return;
        }

        try
        {
            me = new PartyMember
            {
                Name = GameController.Player.GetComponent<Player>().PlayerName ?? "Unknown",
                Role = "Follower",
                IsLocal = false,
                IPAddress = PartyServer.GetLocalIPv4(),
                TcpClient = new TcpClient()
            };

            me.TcpClient.Connect(IPAddress.Parse(me.IPAddress), int.Parse(Settings.ServerSettings.Port));
            var stream = me.Stream;
            isConnectedToServer = true;

            LogMessage($"Connecté en tant que {me.Name} à {me.IPAddress}");

            var hello = new PartyMessage
            {
                Type = "Hello",
                Sender = me.Name,
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
            me?.Disconnect();
            isConnectedToServer = false;
        }
    }


    private void ClientListenerLoop()
    {
        var buffer = new byte[4096];

        try
        {
            while (isConnectedToServer && me.TcpClient.Connected)
            {
                if (me.Stream.DataAvailable)
                {
                    int bytesRead = me.Stream.Read(buffer, 0, buffer.Length);
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
            me.Disconnect();
            isConnectedToServer = false;
            LogMessage("Déconnecté du serveur.");
        }
    }
    public override bool Initialise()
    {
       
        Settings.ServerSettings.ToggleServer.OnValueChanged += (toggle,value) =>
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
                if (_server != null)
                {
                    _server.Stop();
                    _server = null;
                }
            }
        };

        Settings.PartySettings.ConnectToServer.OnPressed += () =>
        {
            //handle cannot connect to own server if ServerSettings.toggleServer is true
            if (!Settings.ServerSettings.ToggleServer)
            {
                if(!isConnectedToServer)
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
            _server = new PartyServer(this);
            _server.Start();
            LogMessage("Party server started.");
        }
        else
        {
            if (_server == null) return;
            _server.Stop();
            _server = null;
            LogMessage("Party server stopped.");
        }
    }

    public override void AreaChange(AreaInstance area)
    {
    }

    public override Job Tick()
    {
        return null;
    }

    public override void Render()
    {
        if (_server != null && _server.IsRunning)
        {
            var text = _server.ToString();
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
        if (_server != null)
        {
            _server.Stop();
            _server = null;
            LogMessage("Party server disposed.");
        }
        base.Dispose();
    }
    public override void OnClose()
    {
        if (_server != null)
        {
            _server.Stop();
            _server = null;
            LogMessage("Party server closed.");
        }
        base.OnClose();
    }
    public override void OnUnload()
    {
        if (_server != null)
        {
            _server.Stop();
            _server = null;
            LogMessage("Party server unloaded.");
        }
        base.OnUnload();
    }

}