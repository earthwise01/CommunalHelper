using Mono.Cecil.Cil;
using MonoMod.Cil;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CommunalHelper.Entities;

[TrackedAs(typeof(DreamBlock), true)]
public abstract class CustomDreamBlock : DreamBlock
{
    protected new struct DreamParticle
    {
        public Vector2 Position;
        public int Layer;
        public Color Color;
        public float TimeOffset;

        // Feather particle stuff
        public float Speed;
        public float Spin;
        public float MaxRotate;
        public float RotationCounter;
    }
    
    protected DreamParticle[] Particles;
    protected readonly MTexture[] FeatherTextures;
    protected readonly MTexture[] DoubleRefillStarTextures;

    protected bool PlayerHasDreamDash => playerHasDreamDash;

    // All dream colors in one array, independent of layer.
    public static readonly Color[] VanillaParticleColors = [
        Calc.HexToColor("FFEF11"), Calc.HexToColor("FF00D0"), Calc.HexToColor("08a310"),
        Calc.HexToColor("5fcde4"), Calc.HexToColor("7fb25e"), Calc.HexToColor("E0564C"),
        Calc.HexToColor("5b6ee1"), Calc.HexToColor("CC3B3B"), Calc.HexToColor("7daa64")
    ];

    protected readonly bool FeatherMode;
    private readonly float dashSpeed;
    protected readonly int RefillCount;
    
    protected bool Shattering = false;
    protected float ColorLerp = 0.0f;
    protected readonly bool QuickDestroy;

    protected bool LeftWobble = true;
    protected bool RightWobble = true;
    protected bool TopWobble = true;
    protected bool BottomWobble = true;

    private bool shakeToggle = false;
    private readonly ParticleType shakeParticle;
    private readonly float[] particleRemainders = new float[4];

    protected bool IsAwake;

    internal readonly EntityData CreationData;

    public CustomDreamBlock(EntityData data, Vector2 offset)
        : this(data.Position + offset, data.Width, data.Height, data.Bool("featherMode"), data.Float("dashSpeed", 240.0f), data.Bool("oneUse"), GetRefillCount(data), data.Bool("below"), data.Bool("quickDestroy"))
    {
        CreationData = data;
    }

    public CustomDreamBlock(Vector2 position, int width, int height, bool featherMode, float dashSpeed, bool oneUse, int refillCount, bool below, bool quickDestroy)
        : base(position, width, height, null, false, oneUse, below)
    {
        QuickDestroy = quickDestroy;
        RefillCount = refillCount;

        FeatherMode = featherMode;
        this.dashSpeed = dashSpeed;

        shakeParticle = new ParticleType(SwitchGate.P_Behind)
        {
            Color = activeLineColor,
            ColorMode = ParticleType.ColorModes.Static,
            Acceleration = Vector2.Zero,
            DirectionRange = MathF.PI / 2
        };

        FeatherTextures = [
            GFX.Game["particles/CommunalHelper/featherBig"],
            GFX.Game["particles/CommunalHelper/featherMedium"],
            GFX.Game["particles/CommunalHelper/featherSmall"]
        ];
        DoubleRefillStarTextures = [
            GFX.Game["objects/CommunalHelper/customDreamBlock/particles"].GetSubtexture(14, 0, 7, 7),
            GFX.Game["objects/CommunalHelper/customDreamBlock/particles"].GetSubtexture(7, 0, 7, 7),
            GFX.Game["objects/CommunalHelper/customDreamBlock/particles"].GetSubtexture(0, 0, 7, 7),
            GFX.Game["objects/CommunalHelper/customDreamBlock/particles"].GetSubtexture(7, 0, 7, 7)
        ];
    }

    private static int GetRefillCount(EntityData data)
        => data.Bool("doubleRefill") ? 2 : data.Int("refillCount", -1);

    public override void Removed(Scene scene)
    {
        base.Removed(scene);
        
        Glitch.Value = 0f;
    }

    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        
        IsAwake = true;
        SetupCustomParticles(Width, Height);
    }

    protected virtual void SetupCustomParticles(float canvasWidth, float canvasHeight)
    {
        float countFactor = (FeatherMode ? 0.5f : 0.7f) * (RefillCount != -1 ? 1.2f : 1);
        Particles = new DreamParticle[(int) (canvasWidth / 8f * (canvasHeight / 8f) * 0.7f * countFactor)];

        // Necessary to get the player's spritemode
        if (!IsAwake && RefillCount != -1)
            return;

        Color[] dashColors = new Color[3];
        if (RefillCount != -1)
        {
            dashColors[0] = Scene.Tracker.GetEntity<Player>()?.GetHairColor(RefillCount) ?? Color.White;
            dashColors[1] = Color.Lerp(dashColors[0], Color.White, 0.5f);
            dashColors[2] = Color.Lerp(dashColors[1], Color.White, 0.5f);
        }

        for (int i = 0; i < Particles.Length; i++)
        {
            int layer = Calc.Random.Choose(0, 1, 1, 2, 2, 2);
            Particles[i] = new DreamParticle
            {
                Position = new Vector2(Calc.Random.NextFloat(canvasWidth), Calc.Random.NextFloat(canvasHeight)),
                Layer = layer,
                Color = GetParticleColor(layer, dashColors),
                TimeOffset = Calc.Random.NextFloat()
            };

            #region Feather particle stuff

            if (!FeatherMode)
                continue;
            
            Particles[i].Speed = Calc.Random.Range(6f, 16f);
            Particles[i].Spin = Calc.Random.Range(8f, 12f) * 0.2f;
            Particles[i].RotationCounter = Calc.Random.NextAngle();
            Particles[i].MaxRotate = Calc.Random.Range(0.3f, 0.6f) * ((float) Math.PI / 2f);

            #endregion
        }
    }

    private Color GetParticleColor(int layer, Color[] dashColors)
    {
        Imports.PandorasBox.GetVisualSettingsFor(this, out _, out _, out _, out _,
            out Color[][] controllerActiveParticleLayerColors,
            out Color[][] controllerDisabledParticleLayerColors);
        
        return PlayerHasDreamDash
            ? RefillCount != -1
                ? dashColors[layer]
                : controllerActiveParticleLayerColors is not null
                    ? Calc.Random.Choose(controllerActiveParticleLayerColors[layer])
                    : layer switch
                    {
                        0 => Calc.Random.Choose(VanillaParticleColors[0], VanillaParticleColors[1], VanillaParticleColors[2]),
                        1 => Calc.Random.Choose(VanillaParticleColors[3], VanillaParticleColors[4], VanillaParticleColors[5]),
                        2 => Calc.Random.Choose(VanillaParticleColors[6], VanillaParticleColors[7], VanillaParticleColors[8]),
                        _ => throw new NotImplementedException()
                    }
            : controllerDisabledParticleLayerColors is not null
                ? Calc.Random.Choose(controllerDisabledParticleLayerColors[layer])
                : Color.LightGray * (0.5f + layer / 2f * 0.5f);
    }

    private void ShakeParticles()
    {
        for (int i = 0; i < 4; ++i)
        {
            Vector2 position;
            Vector2 positionRange;
            float angle;
            float numParticles;
            
            switch (i)
            {
                case 0:
                    position = CenterLeft + Vector2.UnitX;
                    positionRange = Vector2.UnitY * (Height - 4f);
                    angle = MathF.PI;
                    numParticles = Height / 32f;
                    break;
                
                case 1:
                    position = CenterRight;
                    positionRange = Vector2.UnitY * (Height - 4f);
                    angle = 0f;
                    numParticles = Height / 32f;
                    break;
                
                case 2:
                    position = TopCenter + Vector2.UnitY;
                    positionRange = Vector2.UnitX * (Width - 4f);
                    angle = -MathF.PI / 2f;
                    numParticles = Width / 32f;
                    break;
                
                default:
                    position = BottomCenter;
                    positionRange = Vector2.UnitX * (Width - 4f);
                    angle = MathF.PI / 2f;
                    numParticles = Width / 32f;
                    break;
            }

            numParticles *= 0.25f;
            particleRemainders[i] += numParticles;
            int amount = (int) particleRemainders[i];
            particleRemainders[i] -= amount;
            
            positionRange *= 0.5f;
            if (amount > 0f)
                SceneAs<Level>().ParticlesBG.Emit(shakeParticle, amount, position, positionRange, angle);
        }
    }

    public override void Update()
    {
        base.Update();

        if (FeatherMode && PlayerHasDreamDash)
            UpdateParticles();

        if (Visible && PlayerHasDreamDash && oneUse && Scene.OnInterval(0.03f))
        {
            if (shakeToggle)
                shake.X = Calc.Random.Next(-1, 2);
            else
                shake.Y = Calc.Random.Next(-1, 2);
            
            shakeToggle = !shakeToggle;
            if (!Shattering)
                ShakeParticles();
        }
    }

    protected virtual void UpdateParticles()
    {
        for (int i = 0; i < Particles.Length; i++)
        {
            Vector2 pos = Particles[i].Position;
            pos.Y += 0.5f * Particles[i].Speed * GetLayerScaleFactor(Particles[i].Layer) * Engine.DeltaTime;
            Particles[i].Position = pos;
            Particles[i].RotationCounter += Particles[i].Spin * Engine.DeltaTime;
        }
    }

    private static float GetLayerScaleFactor(int layer)
        => 1f / (0.3f + 0.25f * layer);

    public override void Render()
    {
        Camera camera = SceneAs<Level>().Camera;
        if (Right < camera.Left || Left > camera.Right || Bottom < camera.Top || Top > camera.Bottom)
            return;

        Imports.PandorasBox.GetVisualSettingsFor(this,
            out Color? controllerActiveBackColor,
            out Color? controllerDisabledBackColor,
            out Color? controllerActiveLineColor,
            out Color? controllerDisabledLineColor,
            out _, out _);
        Color backColor = Color.Lerp(PlayerHasDreamDash
            ? controllerActiveBackColor ?? activeBackColor
            : controllerDisabledBackColor ?? disabledBackColor, Color.White, ColorLerp);
        Color lineColor = PlayerHasDreamDash
            ? controllerActiveLineColor ?? activeLineColor
            : controllerDisabledLineColor ?? disabledLineColor;

        Draw.Rect(shake.X + X, shake.Y + Y, Width, Height, backColor);
        
        #region Particle Rendering

        Vector2 cameraPosition = camera.Position;
        foreach (DreamParticle particle in Particles)
        {
            int layer = particle.Layer;
            Vector2 position = particle.Position + cameraPosition * (0.3f + 0.25f * layer);
            
            float rotation = MathF.PI / 2f - 0.8f + MathF.Sin(particle.RotationCounter * particle.MaxRotate);
            if (FeatherMode)
                position += Calc.AngleToVector(rotation, 4f);
            
            position = PutInside(position);
            if (!CheckParticleCollide(position))
                continue;

            Color color = Color.Lerp(particle.Color, Color.Black, ColorLerp);

            if (FeatherMode)
                FeatherTextures[layer].DrawCentered(position, color, 1, rotation);
            else
            {
                MTexture[] textures = RefillCount != -1 ? DoubleRefillStarTextures : particleTextures;
                MTexture particleTexture;
                switch (layer)
                {
                    case 0:
                        int i = (int) ((particle.TimeOffset * 4f + animTimer) % 4f);
                        particleTexture = textures[3 - i];
                        break;
                    
                    case 1:
                        int j = (int) ((particle.TimeOffset * 2f + animTimer) % 2f);
                        particleTexture = textures[1 + j];
                        break;
                    
                    default:
                        particleTexture = textures[2];
                        break;
                }
                
                particleTexture.DrawCentered(position, color);
            }
        }

        #endregion

        if (whiteFill > 0f)
            Draw.Rect(X + shake.X, Y + shake.Y, Width, Height * whiteHeight, Color.White * whiteFill);

        if (TopWobble)
            WobbleLine(shake + new Vector2(X, Y), shake + new Vector2(X + Width, Y), 0f, lineColor, backColor);
        if (RightWobble)
            WobbleLine(shake + new Vector2(X + Width, Y), shake + new Vector2(X + Width, Y + Height), 0.7f, lineColor, backColor);
        if (BottomWobble)
            WobbleLine(shake + new Vector2(X + Width, Y + Height), shake + new Vector2(X, Y + Height), 1.5f, lineColor, backColor);
        if (LeftWobble)
            WobbleLine(shake + new Vector2(X, Y + Height), shake + new Vector2(X, Y), 2.5f, lineColor, backColor);

        Draw.Rect(shake + new Vector2(X, Y), 2f, 2f, lineColor);
        Draw.Rect(shake + new Vector2(X + Width - 2f, Y), 2f, 2f, lineColor);
        Draw.Rect(shake + new Vector2(X, Y + Height - 2f), 2f, 2f, lineColor);
        Draw.Rect(shake + new Vector2(X + Width - 2f, Y + Height - 2f), 2f, 2f, lineColor);
    }

    protected bool CheckParticleCollide(Vector2 position)
    {
        const float offset = 2f;
        return position.X >= X + offset && position.Y >= Y + offset && position.X < Right - offset && position.Y < Bottom - offset;
    }

    protected void WobbleLine(Vector2 from, Vector2 to, float offset, Color lineColor, Color backColor)
    {
        Vector2 vec = to - from;
        float length = vec.Length();
        Vector2 value = Vector2.Normalize(vec);
        Vector2 perp = new(value.Y, -value.X);

        float scaleFactor = 0f;
        int increment = 16;
        for (int i = 2; i < length - 2; i += increment)
        {
            float scale = MathHelper.Lerp(LineAmplitude(wobbleFrom + offset, i), LineAmplitude(wobbleTo + offset, i), wobbleEase);
            if (i + increment >= length)
                scale = 0f;

            float endFactor = Math.Min(increment, length - 2f - i);
            Vector2 segmentStart = from + value * i + perp * scaleFactor;
            Vector2 segmentEnd = from + value * (i + endFactor) + perp * scale;
            Draw.Line(segmentStart - perp, segmentEnd - perp, backColor);
            Draw.Line(segmentStart - perp * 2f, segmentEnd - perp * 2f, backColor);
            Draw.Line(segmentStart, segmentEnd, lineColor);

            scaleFactor = scale;
        }
    }

    #region Shattering

    protected bool ShatterCheck()
        => !Shattering;

    protected virtual void BeginShatter()
    {
        if (!ShatterCheck())
            return;
        
        Shattering = true;
        Audio.Play(CustomSFX.game_connectedDreamBlock_dreamblock_shatter, Position);
        Add(new Coroutine(ShatterSequence()));
    }

    private IEnumerator ShatterSequence()
    {
        if (QuickDestroy)
        {
            Collidable = false;
            foreach (StaticMover entity in staticMovers)
                entity.Entity.Collidable = false;
        }
        else
            yield return 0.28f;

        while (ColorLerp < 2.0f)
        {
            ColorLerp += Engine.DeltaTime * 10.0f;
            yield return null;
        }

        ColorLerp = 1.0f;
        if (!QuickDestroy)
            yield return 0.05f;

        Level level = SceneAs<Level>();
        level.Shake(.65f);
        Vector2 camera = level.Camera.Position;

        for (int i = 0; i < Particles.Length; i++)
        {
            Vector2 position = Particles[i].Position;
            position += camera * (0.3f + (0.25f * Particles[i].Layer));
            position = PutInside(position);

            Color flickerColor = Color.Lerp(Particles[i].Color, Color.White, 0.6f);
            ParticleType type = new(Lightning.P_Shatter)
            {
                ColorMode = ParticleType.ColorModes.Fade,
                Color = Particles[i].Color,
                Color2 = flickerColor,
                Source = FeatherMode ? FeatherTextures[Particles[i].Layer] : particleTextures[2],
                SpinMax = FeatherMode ? MathF.PI : 0,
                RotationMode = FeatherMode ? ParticleType.RotationModes.Random : ParticleType.RotationModes.None,
                Direction = (position - Center).Angle()
            };
            level.ParticlesFG.Emit(type, 1, position, Vector2.One * 3f);
        }
        OneUseDestroy();

        Glitch.Value = 0.22f;
        while (Glitch.Value > 0.0f)
        {
            Glitch.Value -= 0.5f * Engine.DeltaTime;
            yield return null;
        }
        Glitch.Value = 0.0f;
        RemoveSelf();
    }

    protected new virtual void OneUseDestroy()
    {
        Collidable = Visible = false;
        DisableStaticMovers();
    }

    #endregion

    #region Hooks

    internal static void Load()
    {
        On.Celeste.DreamBlock.OnPlayerExit += DreamBlock_OnPlayerExit;
        On.Celeste.DreamBlock.OneUseDestroy += DreamBlock_OneUseDestroy;

        On.Celeste.Player.DreamDashBegin += Player_DreamDashBegin;
        On.Celeste.Player.DreamDashUpdate += Player_DreamDashUpdate;
        IL.Celeste.Player.DreamDashEnd += Player_DreamDashEnd;

        ConnectedDreamBlock.Load();
        DreamMoveBlock.Load();
        DreamCrumbleWallOnRumble.Load();
    }

    internal static void Unload()
    {
        On.Celeste.DreamBlock.OnPlayerExit -= DreamBlock_OnPlayerExit;
        On.Celeste.DreamBlock.OneUseDestroy -= DreamBlock_OneUseDestroy;

        On.Celeste.Player.DreamDashBegin -= Player_DreamDashBegin;
        On.Celeste.Player.DreamDashUpdate -= Player_DreamDashUpdate;
        IL.Celeste.Player.DreamDashEnd -= Player_DreamDashEnd;

        ConnectedDreamBlock.Unload();
        DreamMoveBlock.Unload();
        DreamCrumbleWallOnRumble.Unload();
    }

    private static void DreamBlock_OnPlayerExit(On.Celeste.DreamBlock.orig_OnPlayerExit orig, DreamBlock dreamBlock, Player player)
    {
        orig(dreamBlock, player);
        
        if (dreamBlock is not CustomDreamBlock { RefillCount: > -1 } customDreamBlock)
            return;
        
        player.Dashes = customDreamBlock.RefillCount;
        
        Color color = player.GetHairColor(customDreamBlock.RefillCount);
        ParticleType shatter = new(Refill.P_ShatterTwo)
        {
            Friction = 2f,
            LifeMin = 0.4f,
            LifeMax = 0.6f,
            Color = Color.Lerp(color, Color.White, 0.5f),
            Color2 = color,
            ColorMode = ParticleType.ColorModes.Choose
        };
        player.SceneAs<Level>().ParticlesFG.Emit(shatter, 5, player.Position, Vector2.Zero, player.DashDir.Angle());
        
        Audio.Play(customDreamBlock.RefillCount == 2 ? SFX.game_10_pinkdiamond_touch : SFX.game_gen_diamond_touch, player.Position);
    }

    private static void DreamBlock_OneUseDestroy(On.Celeste.DreamBlock.orig_OneUseDestroy orig, DreamBlock self)
    {
        if (self is CustomDreamBlock { Collidable: true } customDreamBlock)
            customDreamBlock.BeginShatter();
        else
            orig(self);
    }

    private static void Player_DreamDashBegin(On.Celeste.Player.orig_DreamDashBegin orig, Player player)
    {
        orig(player);
        
        if (player.dreamBlock is not CustomDreamBlock customDreamBlock)
            return;
        
        if (customDreamBlock.FeatherMode)
        {
            player.Stop(player.dreamSfxLoop);
            player.Loop(player.dreamSfxLoop, CustomSFX.game_connectedDreamBlock_dreamblock_fly_travel);
        }

        // Ensures the player always properly enters a dream block even when it's moving fast
        if (customDreamBlock is DreamZipMover or DreamSwapBlock)
            player.Position += player.DashDir.Sign();
        
        // Only override speed if there isn't a Dream Dash Controller affecting this block
        Imports.PandorasBox.GetGameplaySettingsFor(player.dreamBlock, out _, out _, out bool? overrideDreamDashSpeed, out _, out _, out _, out _, out _, out _);
        if (!(overrideDreamDashSpeed ?? false))
            player.Speed = player.DashDir * customDreamBlock.dashSpeed;
    }

    private static int Player_DreamDashUpdate(On.Celeste.Player.orig_DreamDashUpdate orig, Player player)
    {
        if (player.dreamBlock is not CustomDreamBlock { FeatherMode: true } customDreamBlock)
            return orig(player);
        
        Vector2 input = Input.Aim.Value.SafeNormalize();
        Vector2 vector = player.Speed.SafeNormalize();
        if (input == Vector2.Zero || vector == Vector2.Zero)
            return orig(player);
        
        vector = Vector2.Dot(input, vector) != -0.8f ? vector.RotateTowards(input.Angle(), 5f * Engine.DeltaTime) : vector;
        vector = vector.CorrectJoystickPrecision();
        player.DashDir = vector;
        player.Speed = vector * customDreamBlock.dashSpeed;

        return orig(player);
    }

    // Currently secret/unimplemented, setting RefillCount to -2 will not refill dash
    private static void Player_DreamDashEnd(ILContext il)
    {
        ILCursor cursor = new(il);
        
        if (!cursor.TryGotoNext(instr => instr.MatchCallvirt<Player>("RefillDash")))
            return;
        
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate<Func<Player, bool>>(player => player.GetData().Get<DreamBlock>("dreamBlock") is CustomDreamBlock { RefillCount: -2 });
        cursor.Emit(OpCodes.Brtrue_S, cursor.Next.Next);
    }

    #endregion
}
