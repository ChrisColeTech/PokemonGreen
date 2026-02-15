using PokemonGreen.Core.Maps;
using PokemonGreen.Core.Systems;
using PlayerDirection = PokemonGreen.Core.Player.Direction;
using PlayerClass = PokemonGreen.Core.Player.Player;

namespace PokemonGreen.Core;

public class GameWorld
{
    // ── Constants ──────────────────────────────────────────────────────
    public const int TileSize = 16;
    public const float FadeDuration = 0.3f;
    public const int VisibleTilesY = 5;

    // ── Public state ──────────────────────────────────────────────────
    public TileMap? CurrentMap { get; private set; }
    public MapDefinition? CurrentMapDefinition { get; private set; }
    public string? CurrentWorldId { get; private set; }
    public PlayerClass Player { get; }
    public Camera Camera { get; }
    public InputManager Input { get; }
    public bool IsInitialized => CurrentMap != null;
    public float FadeAlpha { get; private set; }
    public bool IsTransitioning => _transitionState != TransitionState.None;

    // ── Transition internals ──────────────────────────────────────────
    private enum TransitionState { None, FadingOut, FadingIn }

    private TransitionState _transitionState;
    private float _fadeTimer;
    private WarpConnection? _pendingWarp;

    // ── Constructor ───────────────────────────────────────────────────

    public GameWorld(int viewportWidth, int viewportHeight)
    {
        Camera = new Camera(viewportWidth, viewportHeight);
        Input = new InputManager();
        Player = new PlayerClass(0, 0);
    }

    // ── LoadMap overloads ─────────────────────────────────────────────

    public void LoadMap(MapDefinition mapDef)
    {
        LoadMap(mapDef, null, null);
    }

    public void LoadMap(MapDefinition mapDef, float? spawnX, float? spawnY)
    {
        CurrentMapDefinition = mapDef;
        CurrentWorldId = mapDef.WorldId;
        CurrentMap = mapDef.CreateTileMap();

        float px = spawnX ?? CurrentMap.Width / 2f;
        float py = spawnY ?? CurrentMap.Height / 2f;
        Player.SetPosition(px, py);

        SnapCamera(px, py);
    }

    public void LoadMap(TileMap map)
    {
        CurrentMapDefinition = null;
        CurrentMap = map;

        float px = map.Width / 2f;
        float py = map.Height / 2f;
        Player.SetPosition(px, py);

        SnapCamera(px, py);
    }

    // ── Update ────────────────────────────────────────────────────────

    public void Update(float deltaTime)
    {
        if (CurrentMap == null)
            return;

        // 1. If fading between maps, advance the fade and return.
        if (_transitionState != TransitionState.None)
        {
            UpdateTransition(deltaTime);
            return;
        }

        // 2. Read input.
        Input.Update();

        // 3. If interact pressed, check for a door warp on the tile the player faces.
        if (Input.InteractPressed && CurrentMapDefinition != null)
        {
            var (dx, dy) = PokemonGreen.Core.Player.DirectionExtensions.ToVector(Player.Facing);
            var warp = CurrentMapDefinition.GetWarp(Player.TileX + dx, Player.TileY + dy, WarpTrigger.Interact);
            if (warp != null)
            {
                BeginTransition(warp);
                return;
            }
        }

        // 4. Pass movement input to player.
        var moveDir = Input.MoveDirection;
        if (moveDir.HasValue)
            Player.Move(ConvertDirection(moveDir.Value), Input.IsRunning, CurrentMap);
        else
            Player.StopMoving();

        // 5. If jump pressed, tell player to jump.
        if (Input.JumpPressed)
            Player.BeginJump(CurrentMap);

        // 6. Update player.
        Player.Update(deltaTime, CurrentMap);

        // 7. Check if player is on a step warp tile.
        if (CurrentMapDefinition != null)
        {
            var stepWarp = CurrentMapDefinition.GetWarp(Player.TileX, Player.TileY, WarpTrigger.Step);
            if (stepWarp != null)
            {
                BeginTransition(stepWarp);
                return;
            }
        }

        // 8. If player is at the map edge moving outward, try an edge transition.
        if (moveDir.HasValue && CurrentMapDefinition != null)
        {
            var edge = DirectionToEdge(moveDir.Value);
            if (edge.HasValue && IsAtMapEdge(edge.Value))
            {
                if (TryEdgeTransition(edge.Value, Player.TileX, Player.TileY))
                    return;
            }
        }

        // 9. Camera follows player.
        Camera.Update(
            Player.X * TileSize,
            Player.Y * TileSize,
            CurrentMap.Width,
            CurrentMap.Height,
            TileSize);
    }

    // ── Viewport resize ───────────────────────────────────────────────

    public void OnViewportResized(int newWidth, int newHeight)
    {
        Camera.ViewportWidth = newWidth;
        Camera.ViewportHeight = newHeight;

        if (CurrentMap != null)
        {
            // Recalculate zoom: base zoom is fixed, but fill zoom must adapt
            // so small maps always fill the screen at any window size.
            Camera.Zoom = CalculateZoom();
            Camera.Follow(Player.X * TileSize, Player.Y * TileSize);
            Camera.ClampToMap(CurrentMap.Width, CurrentMap.Height, TileSize);
        }
    }

    // ── Helpers exposed for Game1 ─────────────────────────────────────

    public void SetPlayerPosition(float x, float y)
    {
        Player.SetPosition(x, y);
    }

    // ── Transition logic ──────────────────────────────────────────────

    private void BeginTransition(WarpConnection warp)
    {
        _pendingWarp = warp;
        _transitionState = TransitionState.FadingOut;
        _fadeTimer = 0f;
    }

    private void UpdateTransition(float deltaTime)
    {
        _fadeTimer += deltaTime;

        if (_transitionState == TransitionState.FadingOut)
        {
            FadeAlpha = Math.Min(_fadeTimer / FadeDuration, 1f);

            if (_fadeTimer >= FadeDuration)
            {
                // Load the target map at full black.
                if (_pendingWarp != null)
                {
                    if (MapCatalog.TryGetMap(_pendingWarp.TargetMapId, out var targetMap) && targetMap != null)
                        LoadMap(targetMap, _pendingWarp.TargetX, _pendingWarp.TargetY);

                    _pendingWarp = null;
                }

                _transitionState = TransitionState.FadingIn;
                _fadeTimer = 0f;
            }
        }
        else if (_transitionState == TransitionState.FadingIn)
        {
            FadeAlpha = 1f - Math.Min(_fadeTimer / FadeDuration, 1f);

            if (_fadeTimer >= FadeDuration)
            {
                FadeAlpha = 0f;
                _transitionState = TransitionState.None;
            }
        }
    }

    // ── Camera / zoom helpers ─────────────────────────────────────────

    private void SnapCamera(float playerTileX, float playerTileY)
    {
        Camera.Zoom = CalculateZoom();
        Camera.X = playerTileX * TileSize;
        Camera.Y = playerTileY * TileSize;
        Camera.ClampToMap(CurrentMap!.Width, CurrentMap.Height, TileSize);
    }

    private float CalculateZoom()
    {
        // Zoom scales with viewport height so the player always sees
        // VisibleTilesY tiles vertically, regardless of window size.
        return Camera.ViewportHeight / (float)(VisibleTilesY * TileSize);
    }

    // ── Edge transition logic ─────────────────────────────────────────

    private bool IsAtMapEdge(MapEdge edge)
    {
        return edge switch
        {
            MapEdge.North => Player.TileY <= 0,
            MapEdge.South => Player.TileY >= CurrentMap!.Height - 1,
            MapEdge.West  => Player.TileX <= 0,
            MapEdge.East  => Player.TileX >= CurrentMap!.Width - 1,
            _ => false
        };
    }

    private bool TryEdgeTransition(MapEdge edge, int playerX, int playerY)
    {
        MapDefinition? target = null;

        // Check explicit per-map connection first.
        var conn = CurrentMapDefinition!.GetConnection(edge);
        if (conn != null)
            MapCatalog.TryGetMap(conn.TargetMapId, out target);

        // Fall back to auto-neighbor from world grid position.
        target ??= MapCatalog.GetNeighbor(CurrentMapDefinition, edge);

        if (target == null)
            return false;

        int offset = conn?.Offset ?? 0;
        int tx, ty;
        switch (edge)
        {
            case MapEdge.North: tx = playerX + offset; ty = target.Height - 1; break;
            case MapEdge.South: tx = playerX + offset; ty = 0;                 break;
            case MapEdge.West:  tx = target.Width - 1;  ty = playerY + offset; break;
            case MapEdge.East:  tx = 0;                  ty = playerY + offset; break;
            default: return false;
        }

        tx = Math.Clamp(tx, 0, target.Width - 1);
        ty = Math.Clamp(ty, 0, target.Height - 1);

        BeginTransition(new WarpConnection(0, 0, target.Id, tx, ty));
        return true;
    }

    // ── Direction conversion ──────────────────────────────────────────

    private static MapEdge? DirectionToEdge(Direction dir)
    {
        return dir switch
        {
            Direction.Up    => MapEdge.North,
            Direction.Down  => MapEdge.South,
            Direction.Left  => MapEdge.West,
            Direction.Right => MapEdge.East,
            _ => null
        };
    }

    private static PlayerDirection ConvertDirection(Direction dir)
    {
        return dir switch
        {
            Direction.Up    => PlayerDirection.Up,
            Direction.Down  => PlayerDirection.Down,
            Direction.Left  => PlayerDirection.Left,
            Direction.Right => PlayerDirection.Right,
            _ => PlayerDirection.Down
        };
    }
}
