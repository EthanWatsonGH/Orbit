using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BaseObjectDefinition
{
    public string Type { get; }
    public string Name { get; }

    public BaseObjectDefinition(string type, string name)
    {
        Type = type;
        Name = name;
    }

    public Sprite LoadIcon() => Resources.Load<Sprite>(BaseObjectCatalog.IconResourceFolder + "/" + Type);
}

// Order here is the default; the player's saved order is applied afterward.
public static class BaseObjectCatalog
{
    public const string IconResourceFolder = "BaseObjectIcons";

    public static IReadOnlyList<BaseObjectDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        new BaseObjectDefinition("Puller", "Puller"),
        new BaseObjectDefinition("Pusher", "Pusher"),
        new BaseObjectDefinition("Booster", "Booster"),
        new BaseObjectDefinition("MomentumRedirector", "Momentum Redirector"),
        new BaseObjectDefinition("BouncyWall", "Bouncy Wall"),
        new BaseObjectDefinition("SlipperyWall", "Slippery Wall"),
        new BaseObjectDefinition("KillCircle", "Kill Circle"),
        new BaseObjectDefinition("KillWall", "Kill Wall"),
        new BaseObjectDefinition("Finish", "Finish"),
    });
}
