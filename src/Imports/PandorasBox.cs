using Celeste.Mod.CommunalHelper.Components;
using Celeste.Mod.CommunalHelper.Entities;
using MonoMod.ModInterop;
using System.Collections.Generic;

namespace Celeste.Mod.CommunalHelper.Imports;

public static class PandorasBox
{
    #region Dream Dash Controller
    
    [ModImportName("PandorasBox.DreamDashController")]
    public static class DreamDashController
    {
        public delegate void AddRemoveTypesDelegate(HashSet<Type> types);

        public static AddRemoveTypesDelegate AddSetupIgnoringTypes;
        public static AddRemoveTypesDelegate RemoveSetupIgnoringTypes;
    
        public static AddRemoveTypesDelegate AddControlledTypes;
        public static AddRemoveTypesDelegate RemoveControlledTypes;

        public delegate void GetGameplaySettingsForDelegate(Entity entity,
            out bool? allowSameDirectionDash,
            out bool? allowDreamDashRedirection,
            out bool? overrideDreamDashSpeed,
            out bool? neverSlowDown,
            out bool? useEntrySpeedAngle,
            out bool? bounceOnCollision,
            out bool? collideStickToWalls,
            out float? sameDirectionSpeedMultiplier,
            out float? dreamDashSpeed);
        public static GetGameplaySettingsForDelegate GetGameplaySettingsFor;

        public delegate void GetVisualSettingsForDelegate(Entity entity,
            out Color? activeBackColor,
            out Color? disabledBackColor,
            out Color? activeLineColor,
            out Color? disabledLineColor,
            out Color[][] activeParticleLayerColors,
            out Color[][] disabledParticleLayerColors);
        public static GetVisualSettingsForDelegate GetVisualSettingsFor;
    }
    
    public static void AddSetupIgnoringTypes(HashSet<Type> types)
        => DreamDashController.AddSetupIgnoringTypes?.Invoke(types);
    public static void RemoveSetupIgnoringTypes(HashSet<Type> types)
        => DreamDashController.RemoveSetupIgnoringTypes?.Invoke(types);
    
    public static void AddControlledTypes(HashSet<Type> types)
        => DreamDashController.AddControlledTypes?.Invoke(types);
    public static void RemoveControlledTypes(HashSet<Type> types)
        => DreamDashController.RemoveControlledTypes?.Invoke(types);

    public static void GetGameplaySettingsFor(Entity entity,
        out bool? allowSameDirectionDash,
        out bool? allowDreamDashRedirection,
        out bool? overrideDreamDashSpeed,
        out bool? neverSlowDown,
        out bool? useEntrySpeedAngle,
        out bool? bounceOnCollision,
        out bool? collideStickToWalls,
        out float? sameDirectionSpeedMultiplier,
        out float? dreamDashSpeed)
    {
        allowSameDirectionDash = null;
        allowDreamDashRedirection = null;
        overrideDreamDashSpeed = null;
        neverSlowDown = null;
        useEntrySpeedAngle = null;
        bounceOnCollision = null;
        collideStickToWalls = null;
        sameDirectionSpeedMultiplier = null;
        dreamDashSpeed = null;
        
        DreamDashController.GetGameplaySettingsFor?.Invoke(entity,
            out allowSameDirectionDash,
            out allowDreamDashRedirection,
            out overrideDreamDashSpeed,
            out neverSlowDown,
            out useEntrySpeedAngle,
            out bounceOnCollision,
            out collideStickToWalls,
            out sameDirectionSpeedMultiplier,
            out dreamDashSpeed);
    }

    public static void GetVisualSettingsFor(Entity entity,
        out Color? activeBackColor,
        out Color? disabledBackColor,
        out Color? activeLineColor,
        out Color? disabledLineColor,
        out Color[][] activeParticleLayerColors,
        out Color[][] disabledParticleLayerColors)
    {
        activeBackColor = null;
        disabledBackColor = null;
        activeLineColor = null;
        disabledLineColor = null;
        activeParticleLayerColors = null;
        disabledParticleLayerColors = null;

        DreamDashController.GetVisualSettingsFor?.Invoke(entity,
            out activeBackColor,
            out disabledBackColor,
            out activeLineColor,
            out disabledLineColor,
            out activeParticleLayerColors,
            out disabledParticleLayerColors);
    }
    
    #endregion
    
    private static readonly HashSet<Type> SetupIgnoringTypes =
    [
        typeof(CustomDreamBlock),
        typeof(ConnectedDreamBlock),
        typeof(DreamCrumbleWallOnRumble),
        typeof(DreamFallingBlock),
        typeof(DreamFloatySpaceBlock),
        typeof(DreamMoveBlock),
        typeof(DreamSwapBlock),
        typeof(DreamSwitchGate),
        typeof(DreamZipMover),
        typeof(ChainedDreamFallingBlock)
    ];
    private static readonly HashSet<Type> ControlledTypes =
    [
        typeof(DreamSprite.DreamSpriteMarker),
        typeof(DreamTunnelEntry)
    ];
    
    internal static void Initialize()
    {
        typeof(DreamDashController).ModInterop();

        AddSetupIgnoringTypes(SetupIgnoringTypes);
        AddControlledTypes(ControlledTypes);
    }
}
