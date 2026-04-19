using Celeste.Mod.Helpers;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Celeste.Mod.CommunalHelper.Entities;

[CustomEntity("CommunalHelper/ConnectedDreamBlock")]
[Tracked(true)]
public class ConnectedDreamBlock : CustomDreamBlock
{
    private readonly struct SpaceJamTile
    {
        private readonly int[] edges;
        public readonly bool Exist;

        public SpaceJamTile(int x, int y, bool exist)
        {
            edges = new int[4];
            for (int i = 0; i < edges.Length; i++)
                edges[i] = -1;

            Exist = exist;
        }

        public bool TryGetEdge(Edges edge, out int result)
        {
            result = edges[(int) edge];
            return result != -1;
        }

        public int this[Edges edge]
        {
            get => edges[(int) edge];
            set => edges[(int) edge] = value;
        }
    }

    private struct SpaceJamEdge(Vector2 startV, Vector2 endV, float wobbleOff, bool flipNormal, Edges facing)
    {
        public Vector2 Start = startV, End = endV;
        public float WobbleOffset = wobbleOff;
        public bool FlipNormal = flipNormal;
        public Edges Facing = facing;
    }

    private readonly struct SpaceJamCorner(int x, int y, bool ur, bool ul, bool dr, bool dl, bool iur, bool iul, bool idr, bool idl)
    {
        public readonly bool
            UpRight = ur, UpLeft = ul, DownRight = dr, DownLeft = dl,
            InUpRight = iur, InUpLeft = iul, InDownRight = idr, InDownLeft = idl;
        public readonly int x = x, y = y;
    }

    private List<SpaceJamEdge> groupEdges;
    private List<SpaceJamCorner> groupCorners;
    private Rectangle GroupRect => new(
        (int) groupBoundsMin.X,
        (int) groupBoundsMin.Y,
        (int) (groupBoundsMax.X - groupBoundsMin.X),
        (int) (groupBoundsMax.Y - groupBoundsMin.Y));

    private enum Edges
    {
        North,
        East,
        South,
        West,
    }

    private Vector2 groupBoundsMin;
    private Vector2 groupBoundsMax;
    private Vector2 groupOffset;

    private bool hasGroup;

    protected bool MasterOfGroup { get; private set; }
    protected Dictionary<Platform, Vector2> Moves;
    protected List<ConnectedDreamBlock> Group;
    protected List<JumpThru> JumpThrus;
    protected ConnectedDreamBlock Master;

    protected bool IncludeJumpThrus = false;

    public ConnectedDreamBlock(EntityData data, Vector2 offset)
        : base(data, offset) { }

    public override void Awake(Scene scene)
    {
        base.Awake(scene);

        if (hasGroup)
            return;

        // Setup group
        MasterOfGroup = true;

        Moves = [];
        Group = [];
        JumpThrus = [];

        groupBoundsMin = new Vector2(X, Y);
        groupBoundsMax = new Vector2(Right, Bottom);
        groupEdges = [];
        groupCorners = [];
        AddToGroupAndFindChildren(this);
        SetupCustomParticles(0, 0); // Parameters are ignored

        groupOffset = new Vector2(groupBoundsMin.X, groupBoundsMin.Y) - Position;

        float groupW = groupBoundsMax.X - groupBoundsMin.X;
        float groupH = groupBoundsMax.Y - groupBoundsMin.Y;

        // Setup edges of the group
        int groupTileW = (int) (groupW / 8.0f);
        int groupTileH = (int) (groupH / 8.0f);
        SpaceJamTile[,] tiles = new SpaceJamTile[groupTileW + 2, groupTileH + 2];
        for (int x = 0; x < groupTileW + 2; x++)
            for (int y = 0; y < groupTileH + 2; y++)
                tiles[x, y] = new SpaceJamTile(x - 1, y - 1, TileHasGroupDreamBlock(x - 1, y - 1));

        for (int x = 1; x < groupTileW + 1; x++)
            for (int y = 1; y < groupTileH + 1; y++)
                if (tiles[x, y].Exist)
                    AutoEdge(tiles, x, y);

        Vector2 groupCenter = new(groupW / 2, groupH / 2);
        for (int i = 0; i < groupEdges.Count; i++)
        {
            SpaceJamEdge edge = groupEdges[i];
            float angle = Calc.Angle(groupCenter, Vector2.Lerp(edge.Start, edge.End, 0.5f)) + Calc.HalfCircle;
            groupEdges[i] = new SpaceJamEdge(edge.Start, edge.End, edge.WobbleOffset + angle, edge.FlipNormal, edge.Facing);
        }
    }

    protected override void SetupCustomParticles(float canvasWidth, float canvasHeight)
    {
        if (MasterOfGroup)
            base.SetupCustomParticles(groupBoundsMax.X - groupBoundsMin.X, groupBoundsMax.Y - groupBoundsMin.Y);
    }

    protected override void UpdateParticles()
    {
        if (MasterOfGroup)
            base.UpdateParticles();
    }

    private void AutoEdge(SpaceJamTile[,] tiles, int x, int y)
    {
        SpaceJamTile self = tiles[x, y];
        SpaceJamTile nNorth = tiles[x, y - 1];
        SpaceJamTile nEast = tiles[x + 1, y];
        SpaceJamTile nSouth = tiles[x, y + 1];
        SpaceJamTile nWest = tiles[x - 1, y];
        SpaceJamTile nSouthEast = tiles[x + 1, y + 1];
        SpaceJamTile nSouthWest = tiles[x - 1, y + 1];
        SpaceJamTile nNorthEast = tiles[x + 1, y - 1];
        SpaceJamTile nNorthWest = tiles[x - 1, y - 1];

        #region Corner stuff

        bool upRight = !nNorth.Exist && !nEast.Exist;
        bool upLeft = !nNorth.Exist && !nWest.Exist;
        bool downRight = !nSouth.Exist && !nEast.Exist;
        bool downLeft = !nSouth.Exist && !nWest.Exist;
        bool inUpRight = nNorth.Exist && nEast.Exist && !nNorthEast.Exist;
        bool inUpLeft = nNorth.Exist && nWest.Exist && !nNorthWest.Exist;
        bool inDownRight = nSouth.Exist && nEast.Exist && !nSouthEast.Exist;
        bool inDownLeft = nSouth.Exist && nWest.Exist && !nSouthWest.Exist;
        if (upRight || upLeft || downRight || downLeft || inUpRight || inUpLeft || inDownRight || inDownLeft)
            groupCorners.Add(new SpaceJamCorner(x - 1, y - 1, upRight, upLeft, downRight, downLeft, inUpRight, inUpLeft, inDownRight, inDownLeft));

        #endregion

        if (!nNorth.Exist)
        {
            if (nWest.TryGetEdge(Edges.North, out int idx))
            {
                SpaceJamEdge edge = groupEdges[idx];
                edge.End.X += 8;
                groupEdges[idx] = edge;
                self[Edges.North] = idx;
            }
            else
            {
                SpaceJamEdge newEdge;
                newEdge.End = newEdge.Start = TileToPoint(x - 1, y - 1);
                newEdge.End.X += 8;
                newEdge.WobbleOffset = 0.0f;
                newEdge.FlipNormal = false;
                newEdge.Facing = Edges.North;
                self[Edges.North] = groupEdges.Count;
                groupEdges.Add(newEdge);
            }
        }

        if (!nEast.Exist)
        {
            if (nNorth.TryGetEdge(Edges.East, out int idx))
            {
                SpaceJamEdge edge = groupEdges[idx];
                edge.End.Y += nSouth.Exist && nSouthEast.Exist ? 9 : 8;
                groupEdges[idx] = edge;
                self[Edges.East] = idx;
            }
            else
            {
                SpaceJamEdge newEdge;
                newEdge.End = newEdge.Start = TileToPoint(x, y - 1);
                newEdge.End.Y += nSouth.Exist && nSouthEast.Exist ? 9 : 8;
                if (nNorth.Exist)
                    newEdge.Start.Y -= 1;
                newEdge.WobbleOffset = 0.7f;
                newEdge.FlipNormal = false;
                newEdge.Facing = Edges.East;
                self[Edges.East] = groupEdges.Count;
                groupEdges.Add(newEdge);
            }
        }

        if (!nSouth.Exist)
        {
            if (nWest.TryGetEdge(Edges.South, out int idx))
            {
                SpaceJamEdge edge = groupEdges[idx];
                edge.End.X += 8;
                groupEdges[idx] = edge;
                self[Edges.South] = idx;
            }
            else
            {
                SpaceJamEdge newEdge;
                newEdge.Start = TileToPoint(x - 1, y);
                newEdge.Start.Y -= 1;
                newEdge.End = newEdge.Start;
                newEdge.End.X += 8;
                newEdge.WobbleOffset = 1.5f;
                newEdge.FlipNormal = true;
                newEdge.Facing = Edges.South;
                self[Edges.South] = groupEdges.Count;
                groupEdges.Add(newEdge);
            }
        }

        if (!nWest.Exist)
        {
            if (nNorth.TryGetEdge(Edges.West, out int idx))
            {
                SpaceJamEdge edge = groupEdges[idx];
                edge.End.Y += nSouth.Exist && nSouthWest.Exist ? 9 : 8;
                groupEdges[idx] = edge;
                self[Edges.West] = idx;
            }
            else
            {
                SpaceJamEdge newEdge;
                newEdge.Start = TileToPoint(x - 1, y - 1);
                newEdge.Start.X += 1;
                newEdge.End = newEdge.Start;
                newEdge.End.Y += nSouth.Exist && nSouthWest.Exist ? 9 : 8;
                if (nNorth.Exist)
                    newEdge.Start.Y -= 1;
                newEdge.WobbleOffset = 2.5f;
                newEdge.FlipNormal = true;
                newEdge.Facing = Edges.West;
                self[Edges.West] = groupEdges.Count;
                groupEdges.Add(newEdge);
            }
        }
    }

    private void AddToGroupAndFindChildren(ConnectedDreamBlock from)
    {
        if (from.X < groupBoundsMin.X)
            groupBoundsMin.X = from.X;
        if (from.Y < groupBoundsMin.Y)
            groupBoundsMin.Y = from.Y;
        if (from.Right > groupBoundsMax.X)
            groupBoundsMax.X = from.Right;
        if (from.Bottom > groupBoundsMax.Y)
            groupBoundsMax.Y = from.Bottom;

        from.hasGroup = true;
        Group.Add(from);
        Moves.Add(from, from.Position);
        if (from != this)
            from.Master = this;

        if (IncludeJumpThrus)
        {
            foreach (JumpThru jumpThru in Scene.CollideAll<JumpThru>(new Rectangle((int) from.X - 1, (int) from.Y, (int) from.Width + 2, (int) from.Height))
                                               .Where(jumpThru => !JumpThrus.Contains(jumpThru)))
                AddJumpThru(jumpThru);
            foreach (JumpThru jumpThru in Scene.CollideAll<JumpThru>(new Rectangle((int) from.X, (int) from.Y - 1, (int) from.Width, (int) from.Height + 2))
                                               .Where(jumpThru => !JumpThrus.Contains(jumpThru)))
                AddJumpThru(jumpThru);
        }

        foreach (ConnectedDreamBlock connectedBlock in Scene.Tracker.GetEntities<ConnectedDreamBlock>()
                                                                    .Cast<ConnectedDreamBlock>()
                                                                    .Where(connectedBlock =>
                                                                        !connectedBlock.hasGroup
                                                                        && connectedBlock.FeatherMode == from.FeatherMode
                                                                        && Scene.CollideCheck(new Rectangle((int) from.X, (int) from.Y, (int) from.Width, (int) from.Height),
                                                                            connectedBlock)))
            AddToGroupAndFindChildren(connectedBlock);
    }

    private void AddJumpThru(JumpThru jp)
    {
        JumpThrus.Add(jp);
        Moves.Add(jp, jp.Position);

        foreach (ConnectedDreamBlock connectedBlock in Scene.Tracker.GetEntities<ConnectedDreamBlock>()
                                                                    .Cast<ConnectedDreamBlock>()
                                                                    .Where(connectedBlock =>
                                                                        !connectedBlock.hasGroup
                                                                        && connectedBlock.FeatherMode == FeatherMode
                                                                        && Scene.CollideCheck(new Rectangle((int) jp.X - 1, (int) jp.Y, (int) jp.Width + 2, (int) jp.Height),
                                                                            connectedBlock)))
            AddToGroupAndFindChildren(connectedBlock);
    }

    protected virtual DashCollisionResults OnDash(Player player, Vector2 dir)
        => PlayerHasDreamDash
            ? DashCollisionResults.NormalOverride
            : DashCollisionResults.NormalCollision;

    protected virtual DashCollisionResults OnDashJumpThru(Player player, Vector2 dir)
        => PlayerHasDreamDash
            ? DashCollisionResults.NormalOverride
            : DashCollisionResults.NormalCollision;

    public override void Render()
    {
        Camera camera = SceneAs<Level>().Camera;
        Vector2 groupPosition = new(groupBoundsMin.X, groupBoundsMin.Y);

        Vector2 pos = Position + groupOffset + shake;

        if (!CullHelper.IsRectangleVisible(pos.X, pos.Y, GroupRect.Width, GroupRect.Height, 0, camera))
            return;

        if (!MasterOfGroup)
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

        if (whiteFill > 0f)
        {
            lineColor = Color.Lerp(lineColor, Color.White, whiteFill);
            if (whiteHeight == 1f)
                backColor = Color.Lerp(backColor, Color.White, whiteFill);
        }

        #region Background Rendering

        foreach (ConnectedDreamBlock block in Group.Where(block => !(block.Right < camera.Left)
            && !(block.Left > camera.Right)
            && !(block.Bottom < camera.Top)
            && !(block.Top > camera.Bottom)))
            Draw.Rect(block.Position + shake, block.Width, block.Height, backColor);

        #endregion

        #region Particle Rendering

        foreach (CustomDreamParticle particle in Particles)
        {
            int layer = particle.Layer;
            Vector2 position = particle.Position + camera.Position * (0.3f + 0.25f * layer);

            float rotation = 0f;
            MTexture particleTexture;
            if (FeatherMode)
            {
                rotation = MathF.PI / 2f - 0.8f + MathF.Sin(particle.RotationCounter * particle.MaxRotate);
                position += Calc.AngleToVector(rotation, 4f);
                particleTexture = FeatherTextures[layer];
            }
            else
            {
                MTexture[] textures = RefillCount != -1 ? DoubleRefillStarTextures : particleTextures;
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
            }
            particleTexture ??= Draw.Particle;

            position = PutInside(position, GroupRect);
            if (!CullHelper.IsRectangleVisible(position.X, position.Y, particleTexture.Width, particleTexture.Height, 8f, camera))
                continue;

            bool particleIsInside = Group.Any(block => block.CheckParticleCollide(position));
            if (!particleIsInside)
                continue;

            Color color = Color.Lerp(particle.Color, Color.Black, ColorLerp);
            if (whiteFill > 0f && whiteHeight == 1f)
                color = Color.Lerp(color, Color.White, whiteFill);

            particleTexture.DrawCentered(position + Shake + shake, color, 1f, rotation);
        }

        #endregion

        #region (De)activation Rendering

        if (whiteFill == 1f && whiteHeight < 1f)
        {
            float whiteFillBottom = GroupRect.Y + GroupRect.Height * whiteHeight;
            foreach (ConnectedDreamBlock block in Group.Where(block => !(block.Right < camera.Left)
                                                           && !(block.Left > camera.Right)
                                                           && !(block.Bottom < camera.Top)
                                                           && !(block.Top > camera.Bottom))
                                                       .Where(block => block.Top <= whiteFillBottom))
                Draw.Rect(block.Position + shake, block.Width, Calc.Clamp(whiteFillBottom - block.Y, 1f, block.Height), Color.White);
        }

        #endregion

        #region Edge & Corner Rendering

        if (whiteFill > 0f && whiteHeight < 1f)
            backColor = Color.Lerp(backColor, Color.White, whiteFill);

        foreach (SpaceJamCorner corner in groupCorners)
            RenderCorner(groupPosition + shake, corner, lineColor, backColor);

        foreach (SpaceJamEdge edge in groupEdges)
        {
            Vector2 start = edge.Start, end = edge.End;
            if (edge.FlipNormal)
            {
                start = edge.End;
                end = edge.Start;

                if (start.X == end.X)
                {
                    start.X -= 1;
                    end.X -= 1;
                }
                if (start.Y == end.Y)
                {
                    start.Y += 1;
                    end.Y += 1;
                }
            }

            WobbleLine(groupBoundsMin + start + shake, groupBoundsMin + end + shake, edge.WobbleOffset, lineColor, backColor);
        }

        #endregion
    }

    private static void RenderCorner(Vector2 position, SpaceJamCorner corner, Color line, Color back)
    {
        int x = (int) (corner.x * 8 + position.X);
        int y = (int) (corner.y * 8 + position.Y);

        // Simple corners:
        if (corner.UpRight)
            Draw.Rect(x + 6, y, 2, 2, line);
        if (corner.UpLeft)
            Draw.Rect(x, y, 2, 2, line);
        if (corner.DownRight)
            Draw.Rect(x + 6, y + 6, 2, 2, line);
        if (corner.DownLeft)
            Draw.Rect(x, y + 6, 2, 2, line);

        // Inner corners:
        if (corner.InUpRight)
        {
            Draw.Rect(x + 6, y, 4, 3, back);
            Draw.Rect(x + 5, y - 1, 3, 3, back);
            Draw.Line(x + 7, y, x + 10, y, line);
            Draw.Line(x + 7, y, x + 7, y - 1, line);
        }
        if (corner.InUpLeft)
        {
            Draw.Rect(x - 2, y, 4, 3, back);
            Draw.Rect(x, y - 1, 3, 3, back);
            Draw.Line(x - 2, y, x, y, line);
            Draw.Line(x, y + 1, x, y - 1, line);
        }
        if (corner.InDownRight)
        {
            Draw.Rect(x + 6, y + 5, 4, 3, back);
            Draw.Rect(x + 5, y + 6, 3, 3, back);
            Draw.Line(x + 7, y + 7, x + 10, y + 7, line);
            Draw.Line(x + 8, y + 8, x + 8, y + 9, line);
        }
        if (corner.InDownLeft)
        {
            Draw.Rect(x - 2, y + 5, 4, 3, back);
            Draw.Rect(x, y + 6, 3, 3, back);
            Draw.Line(x - 2, y + 7, x + 1, y + 7, line);
            Draw.Line(x + 1, y + 8, x + 1, y + 9, line);
        }
    }

    public override void MoveHExact(int move)
    {
        base.MoveHExact(move);
        groupBoundsMax.X += move;
        groupBoundsMin.X += move;
    }

    public override void MoveVExact(int move)
    {
        base.MoveVExact(move);
        groupBoundsMax.Y += move;
        groupBoundsMin.Y += move;
    }

    private void SpawnFastRoutineParticles()
    {
        if (!MasterOfGroup)
            return;

        Level level = SceneAs<Level>();

        foreach (SpaceJamEdge edge in groupEdges)
        {
            float width = edge.End.X - edge.Start.X;
            float centerH = edge.Start.X + width / 2f;
            float height = edge.End.Y - edge.Start.Y;
            float centerV = edge.Start.Y + height / 2f;

            switch (edge.Facing)
            {
                case Edges.North:
                    level.ParticlesFG.Emit(Strawberry.P_WingsBurst, (int) width, groupBoundsMin + new Vector2(centerH, edge.Start.Y), Vector2.UnitX * width / 2f, Color.White, MathF.PI);
                    break;

                case Edges.South:
                    level.ParticlesFG.Emit(Strawberry.P_WingsBurst, (int) width, groupBoundsMin + new Vector2(centerH, edge.End.Y), Vector2.UnitX * width / 2f, Color.White, 0f);
                    break;

                case Edges.West:
                    level.ParticlesFG.Emit(Strawberry.P_WingsBurst, (int) height, groupBoundsMin + new Vector2(edge.Start.X, centerV), Vector2.UnitY * height / 2f, Color.White, MathF.PI * 3f / 2f);
                    break;

                case Edges.East:
                    level.ParticlesFG.Emit(Strawberry.P_WingsBurst, (int) height, groupBoundsMin + new Vector2(edge.End.X, centerV), Vector2.UnitY * height / 2f, Color.White, MathF.PI / 2f);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private void SpawnSlowRoutineParticles()
    {
        if (!MasterOfGroup)
            return;

        Level level = SceneAs<Level>();
        Camera camera = level.Camera;

        float whiteFillBottom = GroupRect.Y + GroupRect.Height * whiteHeight;
        foreach (ConnectedDreamBlock block in Group.Where(block =>
                                                       !(block.Right < camera.Left)
                                                       && !(block.Left > camera.Right)
                                                       && !(block.Bottom < camera.Top)
                                                       && !(block.Top > camera.Bottom))
                                                   .Where(block => block.Top <= whiteFillBottom
                                                       && block.Bottom >= whiteFillBottom))
            for (int i = 0; i < block.Width; i += 4)
                level.ParticlesFG.Emit(Strawberry.P_WingsBurst, new Vector2(block.X + i, whiteFillBottom + 1f));
    }

    private bool TileHasGroupDreamBlock(int x, int y)
    {
        Rectangle rect = TileToRectangle(x, y);
        rect.Offset((int) groupBoundsMin.X, (int) groupBoundsMin.Y);
        return Group.Any(block => block.CollideRect(rect));
    }

    private static Rectangle TileToRectangle(int x, int y)
    {
        Vector2 p = TileToPoint(x, y);
        return new Rectangle((int) p.X, (int) p.Y, 8, 8);
    }

    private static Vector2 TileToPoint(int x, int y)
        => new(x * 8, y * 8);

    private void ConnectedFootstepRipple(Vector2 position)
    {
        if (!PlayerHasDreamDash)
            return;
        ConnectedDreamBlock groupMaster = MasterOfGroup ? this : Master;

        foreach (ConnectedDreamBlock block in groupMaster.Group)
        {
            DisplacementRenderer.Burst burst = SceneAs<Level>().Displacement.AddBurst(position, 0.5f, 0f, 40f);
            burst.WorldClipCollider = block.Collider;
            burst.WorldClipPadding = 1;
        }
    }

    protected override void BeginShatter()
    {
        if (!ShatterCheck())
            return;

        Audio.Play(CustomSFX.game_connectedDreamBlock_dreamblock_shatter, Position);

        ConnectedDreamBlock groupMaster = MasterOfGroup ? this : Master;
        foreach (ConnectedDreamBlock block in groupMaster.Group)
        {
            block.Shattering = true;
            block.Add(new Coroutine(block.ShatterSequence()));
        }
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

        if (MasterOfGroup)
        {
            Level level = SceneAs<Level>();
            level.Shake(.65f);
            Vector2 camera = level.Camera.Position;
            Rectangle rect = GroupRect;

            Vector2 centre = new(rect.Center.X, rect.Center.Y);
            for (int i = 0; i < Particles.Length; i++)
            {
                Vector2 position = Particles[i].Position;
                position += camera * (0.3f + 0.25f * Particles[i].Layer);
                position = PutInside(position, rect);
                bool inside = Group.Any(block => block.CollidePoint(position));
                if (!inside)
                    continue;

                Color flickerColor = Color.Lerp(Particles[i].Color, Color.White, 0.6f);
                ParticleType type = new(Lightning.P_Shatter)
                {
                    ColorMode = ParticleType.ColorModes.Fade,
                    Color = Particles[i].Color,
                    Color2 = flickerColor,
                    Source = FeatherMode ? FeatherTextures[Particles[i].Layer] : particleTextures[2],
                    SpinMax = FeatherMode ? MathF.PI : 0,
                    RotationMode = FeatherMode ? ParticleType.RotationModes.Random : ParticleType.RotationModes.None,
                    Direction = (position - centre).Angle()
                };
                level.ParticlesFG.Emit(type, 1, position, Vector2.One * 3f);
            }

            foreach (ConnectedDreamBlock block in Group)
                block.OneUseDestroy();

            foreach (JumpThru jumpThru in JumpThrus)
                jumpThru.RemoveSelf();

            Glitch.Value = 0.22f;
            while (Glitch.Value > 0.0f)
            {
                Glitch.Value -= 0.5f * Engine.DeltaTime;
                yield return null;
            }
            Glitch.Value = 0.0f;
        }

        RemoveSelf();
    }

    private static Vector2 PutInside(Vector2 pos, Rectangle r)
    {
        while (pos.X < r.X)
            pos.X += r.Width;

        while (pos.X > r.X + r.Width)
            pos.X -= r.Width;

        while (pos.Y < r.Y)
            pos.Y += r.Height;

        while (pos.Y > r.Y + r.Height)
            pos.Y -= r.Height;

        return pos;
    }

    #region Hooks

    private static ILHook hook_DreamBlock_Activate, hook_DreamBlock_Deactivate;
    private static ILHook hook_DreamBlock_FastActivate, hook_DreamBlock_FastDeactivate;

    public static void Load()
    {
        On.Celeste.DreamBlock.FootstepRipple += DreamBlock_FootstepRipple;

        hook_DreamBlock_Activate = new ILHook(typeof(DreamBlock).GetMethod(nameof(DreamBlock.Activate))!.GetStateMachineTarget()!, DreamBlockSlowRoutine);
        hook_DreamBlock_Deactivate = new ILHook(typeof(DreamBlock).GetMethod(nameof(DreamBlock.Deactivate))!.GetStateMachineTarget()!, DreamBlockSlowRoutine);
        hook_DreamBlock_FastActivate = new ILHook(typeof(DreamBlock).GetMethod(nameof(DreamBlock.FastActivate))!.GetStateMachineTarget()!, DreamBlockFastRoutine);
        hook_DreamBlock_FastDeactivate = new ILHook(typeof(DreamBlock).GetMethod(nameof(DreamBlock.FastDeactivate))!.GetStateMachineTarget()!, DreamBlockFastRoutine);
    }

    public static void Unload()
    {
        On.Celeste.DreamBlock.FootstepRipple -= DreamBlock_FootstepRipple;

        hook_DreamBlock_Activate?.Dispose();
        hook_DreamBlock_Activate = null;
        hook_DreamBlock_Deactivate?.Dispose();
        hook_DreamBlock_Deactivate = null;
        hook_DreamBlock_FastActivate?.Dispose();
        hook_DreamBlock_FastActivate = null;
        hook_DreamBlock_FastDeactivate?.Dispose();
        hook_DreamBlock_FastDeactivate = null;

    }

    private static void DreamBlock_FootstepRipple(On.Celeste.DreamBlock.orig_FootstepRipple orig, DreamBlock dreamBlock, Vector2 pos)
    {
        if (dreamBlock is ConnectedDreamBlock connectedDreamBlock)
            connectedDreamBlock.ConnectedFootstepRipple(pos);
        else
            orig(dreamBlock, pos);
    }

    private static void DreamBlockSlowRoutine(ILContext il)
    {
        ILCursor cursor = new(il);

        ILLabel afterParticlesEmittedLabel = null;

        if (!cursor.TryGotoNextBestFit(MoveType.After,
            instr => instr.MatchLdarg0(),
            instr => instr.OpCode == OpCodes.Ldfld && ((FieldReference) instr.Operand).Name.Contains("level"),
            instr => instr.MatchLdcR4(out _),
            instr => instr.MatchCallOrCallvirt<Scene>(nameof(Scene.OnInterval)),
            instr => instr.MatchBrfalse(out afterParticlesEmittedLabel)))
            throw new Exception("Unable to find particle spawning to modify.");

        cursor.EmitLdloc1(); // dreamBlock
        cursor.EmitDelegate(SpawnConnectedDreamBlockParticles);
        cursor.EmitBrtrue(afterParticlesEmittedLabel);

        return;

        static bool SpawnConnectedDreamBlockParticles(DreamBlock dreamBlock)
        {
            if (dreamBlock is not ConnectedDreamBlock connectedDreamBlock)
                return false;

            connectedDreamBlock.SpawnSlowRoutineParticles();
            return true;
        }
    }

    private static void DreamBlockFastRoutine(ILContext il)
    {
        ILCursor cursor = new(il);

        if (!cursor.TryGotoNextBestFit(MoveType.Before,
            instr => instr.MatchLdarg0(),
            instr => instr.OpCode == OpCodes.Ldfld && ((FieldReference) instr.Operand).Name.Contains("level"),
            instr => instr.MatchLdfld<Level>(nameof(Level.ParticlesFG))))
            throw new Exception("Unable to find particle spawning to modify.");

        ILLabel afterParticlesEmittedLabel = cursor.DefineLabel();

        cursor.EmitLdloc1(); // dreamBlock
        cursor.EmitDelegate(SpawnConnectedDreamBlockParticles);
        cursor.EmitBrtrue(afterParticlesEmittedLabel);

        cursor.Index = -1;
        if (!cursor.TryGotoPrev(MoveType.After,
            instr => instr.MatchCallOrCallvirt<ParticleSystem>(nameof(ParticleSystem.Emit))))
            throw new Exception("Unable to find end of particle spawning to jump after.");
        cursor.MarkLabel(afterParticlesEmittedLabel);

        return;

        static bool SpawnConnectedDreamBlockParticles(DreamBlock dreamBlock)
        {
            if (dreamBlock is not ConnectedDreamBlock connectedDreamBlock)
                return false;

            connectedDreamBlock.SpawnFastRoutineParticles();
            return true;
        }
    }

    #endregion

}
