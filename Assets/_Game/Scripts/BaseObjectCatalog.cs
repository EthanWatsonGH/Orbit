using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class BaseObjectDefinition
{
    public string Type { get; }
    public string Name { get; }
    readonly Func<LevelManager, GameObject> resolvePrefab;

    public BaseObjectDefinition(string type, string name, Func<LevelManager, GameObject> resolvePrefab)
    {
        Type = type;
        Name = name;
        this.resolvePrefab = resolvePrefab;
    }

    public GameObject GetPrefab(LevelManager manager) => manager == null ? null : resolvePrefab(manager);
    public Sprite LoadIcon() => Resources.Load<Sprite>(BaseObjectCatalog.IconResourceFolder + "/" + Type);
}

// This registry is the source for both object creation and the built-in object bar.
public static class BaseObjectCatalog
{
    public const string IconResourceFolder = "BaseObjectIcons";
    public static IReadOnlyList<BaseObjectDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        new BaseObjectDefinition("Puller", "Puller", m => m.PullerPrefab),
        new BaseObjectDefinition("Pusher", "Pusher", m => m.PusherPrefab),
        new BaseObjectDefinition("Booster", "Booster", m => m.BoosterPrefab),
        new BaseObjectDefinition("MomentumRedirector", "Momentum Redirector", m => m.MomentumRedirectorPrefab),
        new BaseObjectDefinition("BouncyWall", "Bouncy Wall", m => m.BouncyWallPrefab),
        new BaseObjectDefinition("SlipperyWall", "Slippery Wall", m => m.SlipperyWallPrefab),
        new BaseObjectDefinition("KillCircle", "Kill Circle", m => m.KillCirclePrefab),
        new BaseObjectDefinition("KillWall", "Kill Wall", m => m.KillWallPrefab),
        new BaseObjectDefinition("Finish", "Finish", m => m.FinishPrefab)
    });
    static readonly IReadOnlyList<BaseObjectDefinition> additional = Array.AsReadOnly(new[]
    {
        new BaseObjectDefinition("ConstantBooster", "Constant Booster", m => m.ConstantBoosterPrefab),
        new BaseObjectDefinition("ConstantMomentumRedirector", "Constant Momentum Redirector", m => m.ConstantMomentumRedirectorPrefab),
        new BaseObjectDefinition("ConstantPuller", "Constant Puller", m => m.ConstantPullerPrefab),
        new BaseObjectDefinition("ConstantPusher", "Constant Pusher", m => m.ConstantPusherPrefab),
        new BaseObjectDefinition("Text", "Text", m => m.TextPrefab)
    });
    static readonly Dictionary<string, BaseObjectDefinition> byType = All.Concat(additional).ToDictionary(item => item.Type);
    public static BaseObjectDefinition Find(string type) => type != null && byType.TryGetValue(type, out BaseObjectDefinition item) ? item : null;
}
