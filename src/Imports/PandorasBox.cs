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
        public static Action<List<Type>> AddSetupIgnoringTypes;
        public static Action<List<Type>> RemoveSetupIgnoringTypes;
    
        public static Action<List<Type>> AddControlledTypes;
        public static Action<List<Type>> RemoveControlledTypes;

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
            out Color[] activeParticleLayerColors,
            out int[] activeParticleLayerIndices,
            out Color[] disabledParticleLayerColors,
            out int[] disabledParticleLayerIndices);
        public static GetVisualSettingsForDelegate GetVisualSettingsFor;
    }
    
    public static void AddSetupIgnoringTypes(List<Type> types)
        => DreamDashController.AddSetupIgnoringTypes?.Invoke(types);
    public static void RemoveSetupIgnoringTypes(List<Type> types)
        => DreamDashController.RemoveSetupIgnoringTypes?.Invoke(types);
    
    public static void AddControlledTypes(List<Type> types)
        => DreamDashController.AddControlledTypes?.Invoke(types);
    public static void RemoveControlledTypes(List<Type> types)
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
               
        if (DreamDashController.GetVisualSettingsFor is null)
            return;
        
        DreamDashController.GetVisualSettingsFor(entity,
            out activeBackColor,
            out disabledBackColor,
            out activeLineColor,
            out disabledLineColor,
            out Color[] packedActiveParticleLayerColors,
            out int[] activeParticleLayerIndices,
            out Color[] packedDisabledParticleLayerColors,
            out int[] disabledParticleLayerIndices);

        activeParticleLayerColors = Util.UnpackArray(packedActiveParticleLayerColors, activeParticleLayerIndices);
        disabledParticleLayerColors = Util.UnpackArray(packedDisabledParticleLayerColors, disabledParticleLayerIndices);
    }
    
    #endregion
    
    private static readonly List<Type> SetupIgnoringTypes =
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
    private static readonly List<Type> ControlledTypes =
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
