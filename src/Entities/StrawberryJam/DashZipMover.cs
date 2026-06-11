using System.Collections;
using System.Collections.Generic;
using Celeste.Mod.Helpers;

namespace Celeste.Mod.CommunalHelper.Entities.StrawberryJam;

[CustomEntity("CommunalHelper/SJ/DashZipMover")]
public class DashZipMover : Solid
{
    private class DashZipMoverPathRenderer : Entity
    {
        private class Segment
        {
            private readonly Vector2 from, to;
            private readonly Vector2 dir, perp;
            private readonly float length;

            private readonly Vector2 ropeOffsetA, ropeOffsetB;

            private readonly Vector2 sparkAdd;
            private readonly float sparkDirFromA, sparkDirFromB;
            private readonly float sparkDirToA, sparkDirToB;

            public readonly Rectangle Bounds;
            public bool Visible;

            public Segment(Vector2 from, Vector2 to)
            {
                this.from = from;
                this.to = to;

                dir = (to - from).SafeNormalize();
                perp = dir.Perpendicular();
                ropeOffsetA = perp * 4f;
                ropeOffsetB = -perp * 4f;

                sparkAdd = (from - to).SafeNormalize(5f).Perpendicular();
                float angle = (from - to).Angle();
                length = (to - from).Length();

                sparkDirFromA = angle + MathF.PI / 8f;
                sparkDirFromB = angle - MathF.PI / 8f;
                sparkDirToA = angle + MathF.PI - MathF.PI / 8f;
                sparkDirToB = angle + MathF.PI + MathF.PI / 8f;

                Bounds = Util.Rectangle(from, to);
                Bounds.Inflate(10, 10);
            }

            public void CreateSparks(Level level)
            {
                level.ParticlesBG.Emit(ZipMover.P_Sparks, from + sparkAdd + Calc.Random.Range(-Vector2.One, Vector2.One), sparkDirFromA);
                level.ParticlesBG.Emit(ZipMover.P_Sparks, from - sparkAdd + Calc.Random.Range(-Vector2.One, Vector2.One), sparkDirFromB);
                level.ParticlesBG.Emit(ZipMover.P_Sparks, to + sparkAdd + Calc.Random.Range(-Vector2.One, Vector2.One), sparkDirToA);
                level.ParticlesBG.Emit(ZipMover.P_Sparks, to - sparkAdd + Calc.Random.Range(-Vector2.One, Vector2.One), sparkDirToB);
            }

            public void Render(float percent, Color color, Color lightColor)
            {
                if (length <= 0)
                    return;

                Vector2 ropeFromA = from + ropeOffsetA + Vector2.UnitY;
                Vector2 ropeFromB = to + ropeOffsetB + Vector2.UnitY;

                float shiftProgress = 4f - percent * MathF.PI * 8f;
                for (float num = shiftProgress % 4f; num < length; num += 4f)
                {
                    float progress = num / length;
                    float sinAmplitude = progress * (1 - progress) * 8f;
                    Vector2 sinOffset = perp * MathF.Sin(num) * sinAmplitude;

                    Vector2 ropeToA = from + ropeOffsetA + dir * num + sinOffset;
                    Vector2 ropeToB = to + ropeOffsetB - dir * num + sinOffset;

                    // main vine rope
                    Draw.Line(ropeFromA, ropeToA, color);
                    Draw.Line(ropeFromB, ropeToB, color);

                    // leaves
                    Draw.Line(ropeToA, ropeToA + dir * 4f, lightColor);
                    Draw.Line(ropeToB, ropeToB - dir * 4f, lightColor);

                    ropeFromA = ropeToA;
                    ropeFromB = ropeToB;
                }
            }

            public void RenderShadow(float percent, Color shadowColor)
            {
                if (length <= 0)
                    return;

                Vector2 ropeFromA = from + ropeOffsetA + Vector2.UnitY;
                Vector2 ropeFromB = to + ropeOffsetB + Vector2.UnitY;

                float shiftProgress = 4f - percent * MathF.PI * 8f;
                for (float num = shiftProgress % 4f; num < length; num += 4f)
                {
                    float progress = num / length;
                    float sinAmplitude = progress * (1 - progress) * 8f;
                    Vector2 sinOffset = perp * MathF.Sin(num) * sinAmplitude;

                    Vector2 ropeToA = from + ropeOffsetA + dir * num + sinOffset + Vector2.UnitY;
                    Vector2 ropeToB = to + ropeOffsetB - dir * num + sinOffset + Vector2.UnitY;

                    // thicker vine rope as sort of outline
                    Draw.Line(ropeFromA, ropeToA, shadowColor, 3);
                    Draw.Line(ropeFromB, ropeToB, shadowColor, 3);

                    // leaves
                    Draw.Line(ropeToA, ropeToA + dir * 4f, shadowColor);
                    Draw.Line(ropeToB, ropeToB - dir * 4f, shadowColor);

                    ropeFromA = ropeToA;
                    ropeFromB = ropeToB;
                }
            }
        }

        private readonly DashZipMover zipMover;

        private readonly Rectangle bounds;

        private readonly Vector2[] nodes;
        private readonly Segment[] segments;

        private readonly MTexture cog;

        private readonly Color ropeColor;
        private readonly Color ropeLightColor;
        private readonly Color ropeShadowColor;

        public DashZipMoverPathRenderer(DashZipMover zipMover, Vector2[] nodes,
            string cogSprite, Color ropeColor, Color ropeLightColor, Color ropeShadowColor)
        {
            Depth = Depths.SolidsBelow;

            this.zipMover = zipMover;

            Vector2 centerOffset = new Vector2(zipMover.Width / 2f, zipMover.Height / 2f);
            this.nodes = new Vector2[nodes.Length];

            Vector2 prevNode = this.nodes[0] = nodes[0] + centerOffset;
            (Vector2 min, Vector2 max) = (prevNode, prevNode);

            segments = new Segment[nodes.Length - 1];
            for (int i = 0; i < segments.Length; i++)
            {
                Vector2 node = this.nodes[i + 1] = nodes[i + 1] + centerOffset;
                segments[i] = new Segment(prevNode, node);

                (min, max) = (Util.Min(min, node), Util.Max(max, node));

                prevNode = node;
            }

            bounds = new Rectangle((int) min.X, (int) min.Y, (int) (max.X - min.X), (int) (max.Y - min.Y));
            bounds.Inflate(10, 10);

            cog = GFX.Game[cogSprite];

            this.ropeColor = ropeColor;
            this.ropeLightColor = ropeLightColor;
            this.ropeShadowColor = ropeShadowColor;
        }

        public void CreateSparks()
        {
            if (Scene is not Level level)
                return;

            foreach (Segment segment in segments)
                segment.CreateSparks(level);
        }

        public override void Render()
        {
            if (Scene is not Level level)
                return;

            Rectangle cameraBounds = level.Camera.GetBounds();

            if (cameraBounds.Intersects(bounds))
            {
                float percent = zipMover.percent;

                foreach (Segment segment in segments)
                {
                    segment.Visible = cameraBounds.Intersects(segment.Bounds);
                    if (segment.Visible)
                        segment.RenderShadow(percent, ropeShadowColor);
                }

                foreach (Segment segment in segments)
                    if (segment.Visible)
                        segment.Render(percent, ropeColor, ropeLightColor);

                float cogRotation = zipMover.percent * MathF.PI * 2f;
                foreach (Vector2 node in nodes)
                {
                    cog.DrawCentered(node + Vector2.UnitY, ropeShadowColor, 1f, cogRotation);
                    cog.DrawCentered(node, Color.White, 1f, cogRotation);
                }
            }

            if (zipMover.drawBlackBorder && CullHelper.IsRectangleVisible(zipMover.X, zipMover.Y, zipMover.Width, zipMover.Height, 10, level.Camera))
            {
                Vector2 position = zipMover.Position + zipMover.Shake;
                int rectX = (int) Math.Round(position.X - zipMover.Width / 2f * (zipMover.scale.X - 1f));
                int rectY = (int) Math.Round(position.Y - zipMover.Height / 2f * (zipMover.scale.Y - 1f));
                int rectW = (int) Math.Round(position.X + zipMover.Width / 2f * (zipMover.scale.X + 1f)) - rectX;
                int rectH = (int) Math.Round(position.Y + zipMover.Height / 2f * (zipMover.scale.Y + 1f)) - rectY;
                Rectangle outlineRect = new Rectangle(rectX - 1, rectY - 1, rectW + 2, rectH + 2);

                Draw.Rect(outlineRect, Color.Black);
            }
        }
    }


    private readonly Sprite streetlight;
    private readonly BloomPoint bloom;

    private readonly SoundSource sfx;

    private DashZipMoverPathRenderer pathRenderer;

    private readonly MTexture[,] blockEdgeTextures = new MTexture[3, 3];
    private readonly List<MTexture> innerCogTextures;
    private readonly MTexture tempTexture = new();

    private readonly Vector2[] nodes;
    private float percent;
    private bool triggered;

    private Vector2 scale = Vector2.One;

    private readonly bool drawBlackBorder;
    private readonly string moveSound;
    private readonly bool slow;
    private readonly bool permanent;
    private readonly bool waiting;
    private readonly string linkFlag;

    private static readonly Ease.Easer EaseSevenHalves = Util.MakeCustomEaser(3.5f);

    public DashZipMover(Vector2 position, int width, int height, Vector2[] nodes,
        string spritePath, bool drawBlackBorder,
        string ropeColorCode, string ropeLightColorCode, string ropeShadowColorCode,
        string moveSound, bool slow, bool permanent, bool waiting, bool linked)
        : base(position, width, height, safe: false)
    {
        Depth = Depths.FGTerrain + 1;

        this.nodes = nodes;

        this.slow = slow;
        this.permanent = permanent;
        this.waiting = waiting;

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
        streetlight.Origin = new Vector2(streetlight.Width / 2f, Height / 2f);
        streetlight.Position = new Vector2(Width / 2f, Height / 2f);

        Add(bloom = new BloomPoint(1f, 6f));
        bloom.Position = new Vector2(Width / 2f, 10f);

        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                blockEdgeTextures[x, y] = GFX.Game[blockSpritePath].GetSubtexture(x * 8, y * 8, 8, 8);

        SurfaceSoundIndex = SurfaceIndex.Girder;

        OnDashCollide = OnDashed;

        this.moveSound = moveSound;

        sfx = new SoundSource();
        sfx.Position = new Vector2(Width / 2f, Height / 2f);
        Add(sfx);

        pathRenderer = new DashZipMoverPathRenderer(this, nodes, spritePath + "cog", Calc.HexToColor(ropeColorCode), Calc.HexToColor(ropeLightColorCode), Calc.HexToColor(ropeShadowColorCode));
    }

    public DashZipMover(EntityData data, Vector2 offset)
        : this(data.Position + offset, data.Width, data.Height, data.NodesWithPosition(offset),
            data.Attr("spritePath", "objects/CommunalHelper/strawberryJam/dashZipMover/"),
            data.Bool("drawBlackBorder", false),
            data.Attr("ropeColor", "046e19"),
            data.Attr("ropeLightColor", "329415"),
            data.Attr("ropeShadowColor", "003622"),
            data.Attr("soundEvent", CustomSFX.game_strawberryJam_dash_zip_mover_zip_mover),
            data.Bool("slow", false),
            data.Bool("permanent", false),
            data.Bool("waiting", false),
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

    public override void Awake(Scene scene)
    {
        base.Awake(scene);

        foreach (StaticMover staticMover in staticMovers)
        {
            if (staticMover.Entity is Spikes spikes)
                spikes.SetOrigins(Center);
        }
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

        foreach (StaticMover staticMover in staticMovers)
        {
            if (staticMover.Entity is not Spikes spikes)
                continue;

            foreach (Component component in spikes.Components)
            {
                if (component is Image image)
                    image.Scale = scale;
            }
        }

        streetlight.Scale = scale;

        bloom.Visible = streetlight.CurrentAnimationFrame != 0;
    }

    public override void Render()
    {
        if (Scene is not Level level || !CullHelper.IsRectangleVisible(X, Y, Width, Height, 10, level.Camera))
            return;

        Vector2 position = Position;
        Position += Shake;

        int rectX = (int) Math.Round(X - Width / 2f * (scale.X - 1f));
        int rectY = (int) Math.Round(Y - Height / 2f * (scale.Y - 1f));
        int rectW = (int) Math.Round(X + Width / 2f * (scale.X + 1f)) - rectX;
        int rectH = (int) Math.Round(Y + Height / 2f * (scale.Y + 1f)) - rectY;
        Rectangle backRect = new Rectangle(rectX + 2, rectY + 2, rectW - 4, rectH - 4);

        Draw.Rect(backRect, Color.Black);

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

            for (int i = 1; i < nodes.Length; i++)
            {
                Vector2 from = nodes[i - 1];
                Vector2 to = nodes[i];

                if (isMainZipMover)
                    sfx.Play(moveSound)
                       .instance.setPitch(1f / slownessFactor);

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
                        SpawnScrapeParticles(to);
                    if (Scene.OnInterval(0.1f))
                        pathRenderer.CreateSparks();

                    Vector2 position = Vector2.Lerp(from, to, percent);
                    MoveTo(position);
                }

                SceneAs<Level>().Shake();
                Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
                StartShaking(0.2f * slownessFactor);
                streetlight.SetAnimationFrame(2);
                StopPlayerRunIntoAnimation = true;
                yield return 0.5f * slownessFactor;

                sfx.Stop();

                bool reachedLastNode = i == nodes.Length - 1;

                if (waiting && !reachedLastNode)
                {
                    streetlight.SetAnimationFrame(1);
                    triggered = false;

                    while (!ShouldActivate(out isMainZipMover))
                        yield return null;

                    SignalLinkedZipMovers(true);
                }
            }

            if (permanent)
            {
                if (isMainZipMover)
                {
                    // close enough
                    Audio.Play($"event:/CommunalHelperEvents/game/zipMover/moon/finish", Center)
                         .setPitch(1f / slownessFactor);
                    Audio.Play($"event:/CommunalHelperEvents/game/zipMover/moon/tick", Center)
                         .setPitch(1f / slownessFactor);
                }

                SceneAs<Level>().Shake(0.15f);
                StartShaking(0.3f);
                streetlight.SetAnimationFrame(0);

                yield break;
            }

            streetlight.SetAnimationFrame(1);
            triggered = false;
            Array.Reverse(nodes);
        }
    }
}
