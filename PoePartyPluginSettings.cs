using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System.Text.Json.Serialization;

namespace PoePartyPlugin;

public class PoePartyPluginSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    public ServerSettings ServerSettings { get; set; } = new ServerSettings();
    public PartySettings PartySettings { get; set; } = new PartySettings();
    public PatchMenu PatchMenu { get; set; } = new PatchMenu();
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




[Submenu]
public class PatchMenu
{
    public PatchSettings CastSkillWithTargetSettings { get; set; } = new PatchSettings();


    public PatchSettings CastSkillWithPositionSettings { get; set; } = new PatchSettings();


    public PatchSettings CastSkillWithPosition2Settings { get; set; } = new PatchSettings();


    public PatchSettings GemLevelUpSettings { get; set; } = new PatchSettings();
}



[Submenu]
public class PatchSettings
{

    public ToggleNode Enabled { get; set; } = new ToggleNode(value: false);

    [JsonIgnore]
    internal long CodePtrValue;

    [JsonIgnore]
    internal bool PatchFailed;



    [JsonIgnore]
    [Menu(null, "For debugging")]
    public TextNode CodePtr { get; set; } = new TextNode("");

}