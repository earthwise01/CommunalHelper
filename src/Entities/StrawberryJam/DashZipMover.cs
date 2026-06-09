using System.Collections;
using System.Collections.Generic;

namespace Celeste.Mod.CommunalHelper.Entities.StrawberryJam;

[CustomEntity("CommunalHelper/SJ/DashZipMover")]
public class DashZipMover : Solid
{
    private class DashZipMoverPathRenderer : Entity
    {
        private readonly DashZipMover zipMover;

        private readonly MTexture cog;

        private readonly Vector2 from;
        private readonly Vector2 to;

        private readonly Vector2 sparkAdd;
        private readonly float sparkDirFromA;
        private readonly float sparkDirFromB;
        private readonly float sparkDirToA;
        private readonly float sparkDirToB;

        private readonly float length;

        private readonly Color ropeColor = Calc.HexToColor("046e19");
        private readonly Color ropeLightColor = Calc.HexToColor("329415");
        private readonly Color ropeShadowColor = Calc.HexToColor("003622");

        public DashZipMoverPathRenderer(DashZipMover zipMover, string cogSprite, Color ropeColor, Color ropeLightColor, Color ropeShadowColor)
        {
            Depth = Depths.SolidsBelow;
            this.zipMover = zipMover;

            from = zipMover.start + new Vector2(zipMover.Width / 2f, zipMover.Height / 2f);
            to = zipMover.target + new Vector2(zipMover.Width / 2f, zipMover.Height / 2f);

            sparkAdd = (from - to).SafeNormalize(5f).Perpendicular();
            float angle = (from - to).Angle();
            length = (to - from).Length();

            sparkDirFromA = angle + MathF.PI / 8f;
            sparkDirFromB = angle - MathF.PI / 8f;
            sparkDirToA = angle + MathF.PI - MathF.PI / 8f;
            sparkDirToB = angle + MathF.PI + MathF.PI / 8f;

            cog = GFX.Game[cogSprite];

            this.ropeColor = ropeColor;
            this.ropeLightColor = ropeLightColor;
            this.ropeShadowColor = ropeShadowColor;
        }

        public void CreateSparks()
        {
            SceneAs<Level>().ParticlesBG.Emit(ZipMover.P_Sparks, from + sparkAdd + Calc.Random.Range(-Vector2.One, Vector2.One), sparkDirFromA);
            SceneAs<Level>().ParticlesBG.Emit(ZipMover.P_Sparks, from - sparkAdd + Calc.Random.Range(-Vector2.One, Vector2.One), sparkDirFromB);
            SceneAs<Level>().ParticlesBG.Emit(ZipMover.P_Sparks, to + sparkAdd + Calc.Random.Range(-Vector2.One, Vector2.One), sparkDirToA);
            SceneAs<Level>().ParticlesBG.Emit(ZipMover.P_Sparks, to - sparkAdd + Calc.Random.Range(-Vector2.One, Vector2.One), sparkDirToB);
        }

        public override void Render()
        {
            if (length != 0f)
            {
                DrawCogs(Vector2.UnitY, ropeShadowColor);
                DrawCogs(Vector2.Zero);
            }

            if (zipMover.drawBlackBorder)
            {
                Rectangle outline = new Rectangle(
                    (int) (Math.Round(zipMover.X - (zipMover.scale.X - 1) * zipMover.Width / 2f) + zipMover.Shake.X),
                    (int) (Math.Round(zipMover.Y - (zipMover.scale.Y - 1) * zipMover.Height / 2f) + zipMover.Shake.Y),
                    (int) (zipMover.Width * 0.125f * MathF.Round(8 * zipMover.scale.X)),  // The width/height here needs to be handled relative to the Rounding value of the individually drawn patch segments
                    (int) (zipMover.Height * 0.125f * MathF.Round(8 * zipMover.scale.Y))  // As opposed to the width. Round(8 * 2/3) * (x / 8) != Round(x * 2/3)
                );
                outline.Inflate(1, 1);
                Draw.Rect(outline, Color.Black);
            }
        }

        private void DrawCogs(Vector2 offset, Color? colorOverride = null)
        {
            Vector2 direction = (to - from).SafeNormalize();
            Vector2 perp = direction.Perpendicular();
            Vector2 ropeOffsetA = perp * 4f;
            Vector2 ropeOffsetB = -perp * 4f;

            Vector2 ropeFromA = from + ropeOffsetA + offset;
            Vector2 ropeFromB = to + ropeOffsetB + offset;
            for (float num = 4f - zipMover.percent * MathF.PI * 8f % 4f; num < length; num += 4f)
            {
                float progress = num / length;
                float sinAmount = progress * (1 - progress) * 8f;
                Vector2 sinOffset = perp * MathF.Sin(num) * sinAmount;

                Vector2 ropeToA = from + ropeOffsetA + direction * num + sinOffset + offset;
                Vector2 ropeToB = to + ropeOffsetB - direction * num + sinOffset + offset;

                // Thicker vine rope, in the back, sort of outline
                if (colorOverride is not null)
                {
                    Draw.Line(ropeFromA, ropeToA, (Color) colorOverride, 3);
                    Draw.Line(ropeFromB, ropeToB, (Color) colorOverride, 3);
                }

                // Main "vine rope"
                Draw.Line(ropeFromA, ropeToA, colorOverride ?? ropeColor);
                Draw.Line(ropeFromB, ropeToB, colorOverride ?? ropeColor);

                // Leaves
                Draw.Line(ropeToA, ropeToA + direction * 4f, colorOverride ?? ropeLightColor);
                Draw.Line(ropeToB, ropeToB - direction * 4f, colorOverride ?? ropeLightColor);

                ropeFromA = ropeToA;
                ropeFromB = ropeToB;
            }

            float cogRotation = zipMover.percent * MathF.PI * 2f;
            cog.DrawCentered(from + offset, colorOverride ?? Color.White, 1f, cogRotation);
            cog.DrawCentered(to + offset, colorOverride ?? Color.White, 1f, cogRotation);
        }
    }


    private readonly Sprite streetlight;
    private readonly BloomPoint bloom;

    private readonly SoundSource sfx;

    private DashZipMoverPathRenderer pathRenderer;

    private readonly MTexture[,] blockEdgeTextures = new MTexture[3, 3];
    private readonly List<MTexture> innerCogTextures;
    private readonly MTexture tempTexture = new();

    private Vector2 start;
    private Vector2 target;
    private float percent;
    private bool triggered;

    private Vector2 scale = Vector2.One;

    private readonly bool drawBlackBorder;
    private readonly string moveSound;
    private readonly bool slow;
    private readonly string linkFlag;

    private static readonly Ease.Easer EaseSevenHalves = Util.MakeCustomEaser(3.5f);

    public DashZipMover(Vector2 position, int width, int height, Vector2 target,
        string spritePath, bool drawBlackBorder,
        string ropeColorCode, string ropeLightColorCode, string ropeShadowColorCode,
        string moveSound, bool slow, bool linked)
        : base(position, width, height, safe: false)
    {
        Depth = Depths.FGTerrain + 1;
        start = Position;
        this.target = target;
        this.slow = slow;

        if (linked)
            linkFlag = $"ZipMoverSync:{ropeColorCode}"; // matches Adventure Helper

        Add(new Coroutine(Sequence()));
        Add(new LightOcclude());

        string lightSpritePath = spritePath + "light";
        string blockSpritePath = spritePath + "block";
        string innerCogSpritePath = spritePath + "innercog";

        this.drawBlackBorder = drawBlackBorder;

        innerCogTextures = GFX.Game.GetAtlasSubtextures(innerCogSpritePath);

        Add(streetlight = new Sprite(GFX.Game, lightSpritePath));
        streetlight.Add("frames", "", 1f);
        streetlight.Play("frames");
        streetlight.Active = false;
        streetlight.SetAnimationFrame(1);
        streetlight.Position = new Vector2(Width / 2f - streetlight.Width / 2f, 0f);

        Add(bloom = new BloomPoint(1f, 6f));
        bloom.Position = new Vector2(Width / 2f, 10f);

        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                blockEdgeTextures[x, y] = GFX.Game[blockSpritePath].GetSubtexture(x * 8, y * 8, 8, 8);

        SurfaceSoundIndex = SurfaceIndex.Girder;

        OnDashCollide = OnDashed;

        this.moveSound = moveSound;

        sfx = new SoundSource();
        sfx.Position = new Vector2(Width, Height) / 2f;
        Add(sfx);

        pathRenderer = new DashZipMoverPathRenderer(this, spritePath + "cog", Calc.HexToColor(ropeColorCode), Calc.HexToColor(ropeLightColorCode), Calc.HexToColor(ropeShadowColorCode));
    }

    public DashZipMover(EntityData data, Vector2 offset)
        : this(data.Position + offset, data.Width, data.Height, data.Nodes[0] + offset,
            data.Attr("spritePath", "objects/CommunalHelper/strawberryJam/dashZipMover/"),
            data.Bool("drawBlackBorder", false),
            data.Attr("ropeColor", "046e19"),
            data.Attr("ropeLightColor", "329415"),
            data.Attr("ropeShadowColor", "003622"),
            data.Attr("soundEvent", CustomSFX.game_strawberryJam_dash_zip_mover_zip_mover),
            data.Bool("slow", false),
            data.Bool("linked", false))
    { }

    public DashCollisionResults OnDashed(Player player, Vector2 dir)
    {
        if (triggered)
            return DashCollisionResults.NormalCollision;

        triggered = true;

        scale = new Vector2(
            1f + Math.Abs(dir.Y) * 0.4f - Math.Abs(dir.X) * 0.4f,
            1f + Math.Abs(dir.X) * 0.4f - Math.Abs(dir.Y) * 0.4f);

        return DashCollisionResults.Rebound;
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        scene.Add(pathRenderer);
    }

    public override void Removed(Scene scene)
    {
        scene.Remove(pathRenderer);
        pathRenderer = null;
        SignalLinkedZipMovers(false);
        base.Removed(scene);
    }

    public override void Update()
    {
        base.Update();

        scale = Calc.Approach(scale, Vector2.One, 3f * Engine.DeltaTime);

        streetlight.Scale = scale;
        Vector2 zeroCenter = new Vector2(Width, Height) / 2f;
        streetlight.Position = zeroCenter + (new Vector2(zeroCenter.X - streetlight.Width / 2f, 0) - zeroCenter) * scale;
    }

    public override void Render()
    {
        Vector2 position = Position;
        Position += Shake;

        Rectangle rect = new Rectangle(
            (int) (Center.X + (X + 2 - Center.X) * scale.X),
            (int) (Center.Y + (Y + 2 - Center.Y) * scale.Y),
            (int) ((Width - 4) * scale.X),
            (int) ((Height - 4) * scale.Y));

        Draw.Rect(rect, Color.Black);

        int offset = 1;
        float angle = 0f;
        int count = innerCogTextures.Count;

        for (int y = 4; y <= Height - 4f; y += 8)
        {
            int prevOffset = offset;
            for (int x = 4; x <= Width - 4f; x += 8)
            {
                int index = (int) (Util.Mod((angle + offset * percent * MathF.PI * 4f) / (MathF.PI / 2f), 1f) * count);

                MTexture innerCog = innerCogTextures[index];
                Rectangle clipRect = new Rectangle(0, 0, innerCog.Width, innerCog.Height);
                Vector2 clipOffset = Vector2.Zero;

                if (x <= 4)
                {
                    clipOffset.X = 2f;
                    clipRect.X = 2;
                    clipRect.Width -= 2;
                }
                else if (x >= Width - 4f)
                {
                    clipOffset.X = -2f;
                    clipRect.Width -= 2;
                }

                if (y <= 4)
                {
                    clipOffset.Y = 2f;
                    clipRect.Y = 2;
                    clipRect.Height -= 2;
                }
                else if (y >= Height - 4f)
                {
                    clipOffset.Y = -2f;
                    clipRect.Height -= 2;
                }

                innerCog = innerCog.GetSubtexture(clipRect.X, clipRect.Y, clipRect.Width, clipRect.Height, tempTexture);
                Vector2 pos = Center + (Position + new Vector2(x, y) + clipOffset - Center) * scale;
                innerCog.DrawCentered(pos, Color.White * (offset < 0 ? 0.5f : 1f), scale);

                offset = -offset;
                angle += MathF.PI / 3f;
            }
            if (prevOffset == offset)
                offset = -offset;
        }

        for (int tileX = 0; tileX < Width / 8f; tileX++)
        {
            int textureX = tileX != 0 ? tileX != Width / 8f - 1f ? 1 : 2 : 0;
            for (int tileY = 0; tileY < Height / 8f; tileY++)
            {
                int textureY = tileY != 0 ? tileY != Height / 8f - 1f ? 1 : 2 : 0;

                if (textureX == 1 && textureY == 1)
                    continue;

                Vector2 pos = Center + (new Vector2(X + tileX * 8 + 4, Y + tileY * 8 + 4) - Center) * scale;
                blockEdgeTextures[textureX, textureY].DrawCentered(pos, Color.White, scale);
            }
        }

        base.Render();

        Position = position;
    }

   private void SpawnScrapeParticles(Vector2 to)
    {
        const float threePiOverFour = 3f * MathF.PI / 4f;
        const float piOverFour = MathF.PI / 4f;

        bool movingV = to.Y != ExactPosition.Y;
        bool movingH = to.X != ExactPosition.X;

        if (movingV && !movingH)
        {
            int dir = Math.Sign(to.Y - ExactPosition.Y);
            Vector2 collisionPoint = dir != 1 ? TopLeft : BottomLeft;
            int particleStart = dir != 1 ? 4 : Math.Min((int) Height - 12, 20);
            int particleHeight = dir != -1 ? (int) Height : Math.Max(16, (int) Height - 16);

            if (Scene.CollideCheck<Solid>(collisionPoint + new Vector2(-2f, dir * -2)))
                for (int y = particleStart; y < particleHeight; y += 8)
                    SceneAs<Level>().ParticlesFG.Emit(ZipMover.P_Scrape, TopLeft + new Vector2(0f, y + dir * 2f), dir == 1 ? -piOverFour : piOverFour);

            if (Scene.CollideCheck<Solid>(collisionPoint + new Vector2(Width + 2f, dir * -2)))
                for (int y = particleStart; y < particleHeight; y += 8)
                    SceneAs<Level>().ParticlesFG.Emit(ZipMover.P_Scrape, TopRight + new Vector2(-1f, y + dir * 2f), dir == 1 ? -threePiOverFour : threePiOverFour);

        }
        else if (movingH && !movingV)
        {
            int dir = Math.Sign(to.X - ExactPosition.X);
            Vector2 collisionPoint = dir != 1 ? TopLeft : TopRight;
            int particleStart = dir != 1 ? 4 : Math.Min((int) Width - 12, 20);
            int particleWidth = dir != -1 ? (int) Width : Math.Max(16, (int) Width - 16);

            if (Scene.CollideCheck<Solid>(collisionPoint + new Vector2(dir * -2, -2f)))
                for (int x = particleStart; x < particleWidth; x += 8)
                    SceneAs<Level>().ParticlesFG.Emit(ZipMover.P_Scrape, TopLeft + new Vector2(x + dir * 2f, -1f), dir == 1 ? threePiOverFour : piOverFour);

            if (Scene.CollideCheck<Solid>(collisionPoint + new Vector2(dir * -2, Height + 2f)))
                for (int x = particleStart; x < particleWidth; x += 8)
                    SceneAs<Level>().ParticlesFG.Emit(ZipMover.P_Scrape, BottomLeft + new Vector2(x + dir * 2f, 0f), dir == 1 ? -threePiOverFour : -piOverFour);
        }
    }

    private bool ShouldActivate(out bool isMainZipMover)
    {
        // trigger if signaled by another linked zip mover
        if (linkFlag is not null && Scene is Level level && level.Session.GetFlag(linkFlag))
        {
            isMainZipMover = false;
            triggered = true;
            return true;
        }

        return isMainZipMover = triggered;
    }

    private void SignalLinkedZipMovers(bool shouldActivate)
    {
        if (linkFlag is not null && Scene is Level level)
            level.Session.SetFlag(linkFlag, shouldActivate);
    }

    private IEnumerator Sequence()
    {
        float slownessFactor = slow ? 1.75f : 1f;

        while (true)
        {
            bool isMainZipMover;
            while (!ShouldActivate(out isMainZipMover))
                yield return null;

            SignalLinkedZipMovers(true);

            if (isMainZipMover)
            {
                sfx.Play(moveSound);
                sfx.instance.setPitch(1f / slownessFactor);
            }

            Input.Rumble(RumbleStrength.Medium, RumbleLength.Short);
            StartShaking(0.1f * slownessFactor);
            yield return 0.1f * slownessFactor;

            SignalLinkedZipMovers(false);

            streetlight.SetAnimationFrame(3);
            StopPlayerRunIntoAnimation = false;

            float at = 0f;
            while (at < 1f)
            {
                yield return null;

                at = Calc.Approach(at, 1f, 2f * Engine.DeltaTime * (1f / slownessFactor));
                percent = slow ? EaseSevenHalves(at) : Ease.SineIn(at);

                if (Scene.OnInterval(0.03f))
                    SpawnScrapeParticles(target);
                if (Scene.OnInterval(0.1f))
                    pathRenderer.CreateSparks();

                Vector2 position = Vector2.Lerp(start, target, percent);
                MoveTo(position);
            }

            SceneAs<Level>().Shake();
            Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
            StartShaking(0.2f * slownessFactor);
            streetlight.SetAnimationFrame(2);
            StopPlayerRunIntoAnimation = true;
            yield return 0.5f * slownessFactor;

            streetlight.SetAnimationFrame(1);
            triggered = false;
            target = start;
            start = Position;
            sfx.Stop();
        }
    }
}
