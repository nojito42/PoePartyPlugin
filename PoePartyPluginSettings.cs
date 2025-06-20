using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

namespace PoePartyPlugin;

public class PoePartyPluginSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    public ServerSettings ServerSettings { get; set; } = new ServerSettings();
    public PartySettings PartySettings { get; set; } = new PartySettings();

}

[Submenu]
public class ServerSettings
{
    public ToggleNode ToggleServer { get; set; } = new ToggleNode(true);
    public TextNode Port { get; set; } = new TextNode("5051");
}

[Submenu]
public class PartySettings
{
    public ButtonNode ConnectToServer { get; set; } = new ButtonNode();
}
