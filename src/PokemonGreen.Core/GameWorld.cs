using PokemonGreen.Core.Battle;
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
    public const float FlashDuration = 0.15f;
    public const int VisibleTilesY = 5;
    private const int EncounterChance = 10; // 1 in N chance per step

    // ── Game state ──────────────────────────────────────────────────
    public enum GameState { Overworld, Battle, PauseMenu }
    public GameState State { get; private set; } = GameState.Overworld;

    // ── Public state ──────────────────────────────────────────────────
    public TileMap? CurrentMap { get; private set; }
    public MapDefinition? CurrentMapDefinition { get; private set; }
    public string? CurrentWorldId { get; private set; }
    public PlayerClass Player { get; }
    public Camera Camera { get; }
    public InputManager Input { get; }
    public bool IsInitialized => CurrentMap != null;
    public float FadeAlpha { get; private set; }
    public float FadeWhiteAlpha { get; private set; }
    public bool IsTransitioning => _transitionState != TransitionState.None;
    public BattleBackground CurrentBattleBackground { get; private set; }

    // ── Transition internals ──────────────────────────────────────────
    private enum TransitionState { None, FadingOut, FadingIn, FlashWhite, FadingToBattle, FadingFromBattle }

    private TransitionState _transitionState;
    private float _fadeTimer;
    private WarpConnection? _pendingWarp;
    private bool _exitingBattle;
    private int _prevTileX, _prevTileY;
    private bool _encounterCheckPending;
    private int _pendingEncTileX, _pendingEncTileY;
    private static readonly Random _encounterRandom = new();

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
        _prevTileX = Player.TileX;
        _prevTileY = Player.TileY;

        SnapCamera(px, py);
    }

    public void LoadMap(TileMap map)
    {
        CurrentMapDefinition = null;
        CurrentMap = map;

        float px = map.Width / 2f;
        float py = map.Height / 2f;
        Player.SetPosition(px, py);
        _prevTileX = Player.TileX;
        _prevTileY = Player.TileY;

        SnapCamera(px, py);
    }

    // ── Update ────────────────────────────────────────────────────────

    public void Update(float deltaTime)
    {
        if (CurrentMap == null)
            return;

        // 1. If fading/transitioning, advance and return.
        if (_transitionState != TransitionState.None)
        {
            UpdateTransition(deltaTime);
            return;
        }

        // 1b. In battle or pause state, skip overworld logic.
        if (State != GameState.Overworld)
            return;

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

        // 4. Check for edge transitions only - no input blocking
        var moveDir = Input.MoveDirection;
        if (moveDir.HasValue && CurrentMapDefinition != null)
        {
            var (dx, dy) = PokemonGreen.Core.Player.DirectionExtensions.ToVector(ConvertDirection(moveDir.Value));
            int targetX = Player.TileX + dx;
            int targetY = Player.TileY + dy;

            if (!CurrentMap.IsInBounds(targetX, targetY))
            {
                var edge = DirectionToEdge(moveDir.Value);
                if (edge.HasValue)
                {
                    int baseTile = CurrentMapDefinition.GetBaseTile(Player.TileX, Player.TileY);
                    if (IsTransitionTileForEdge(baseTile, edge.Value) && TryEdgeTransition(edge.Value, Player.TileX, Player.TileY))
                    {
                        return;
                    }
                }
            }
        }

        // 5. Pass movement input to player.
        if (moveDir.HasValue)
            Player.Move(ConvertDirection(moveDir.Value), Input.IsRunning, CurrentMap);
        else
            Player.StopMoving();

        // 6. If jump pressed, tell player to jump.
        if (Input.JumpPressed)
            Player.BeginJump(CurrentMap);

        // 7. Update player.
        Player.Update(deltaTime, CurrentMap);

        // 7b. Encounter check — mark pending when entering an encounter tile,
        // then trigger only once the player reaches the center of the tile.
        if (Player.TileX != _prevTileX || Player.TileY != _prevTileY)
        {
            _prevTileX = Player.TileX;
            _prevTileY = Player.TileY;

            if (IsEncounterTile(Player.TileX, Player.TileY))
            {
                _encounterCheckPending = true;
                _pendingEncTileX = Player.TileX;
                _pendingEncTileY = Player.TileY;
            }
            else
            {
                _encounterCheckPending = false;
            }
        }

        if (_encounterCheckPending
            && Player.TileX == _pendingEncTileX
            && Player.TileY == _pendingEncTileY)
        {
            float localX = Player.X - Player.TileX;
            float localY = Player.Y - Player.TileY;

            // Hitbox matches the visible flame sprite area (upper portion of tile).
            if (localX >= 0.15f && localX <= 0.85f
                && localY >= 0.0f && localY <= 0.5f)
            {
                _encounterCheckPending = false;
                if (_encounterRandom.Next(EncounterChance) == 0)
                {
                    BeginBattleTransition();
                    return;
                }
            }
        }

        // 8. Check if player is on a step warp tile.
        if (CurrentMapDefinition != null)
        {
            var stepWarp = CurrentMapDefinition.GetWarp(Player.TileX, Player.TileY, WarpTrigger.Step);
            if (stepWarp != null)
            {
                BeginTransition(stepWarp);
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

    /// <summary>Debug: immediately enter battle state (no transition).</summary>
    public void DebugEnterBattle()
    {
        State = GameState.Battle;
    }

    public void EnterPauseMenu()
    {
        if (State == GameWorld.GameState.Overworld)
            State = GameState.PauseMenu;
    }

    public void ExitPauseMenu()
    {
        if (State == GameState.PauseMenu)
            State = GameState.Overworld;
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
                // Load the target map at full black (map warp).
                if (_pendingWarp != null)
                {
                    if (MapCatalog.TryGetMap(_pendingWarp.TargetMapId, out var targetMap) && targetMap != null)
                        LoadMap(targetMap, _pendingWarp.TargetX, _pendingWarp.TargetY);

                    _pendingWarp = null;
                }

                // Switch back to overworld when exiting battle.
                if (_exitingBattle)
                {
                    State = GameState.Overworld;
                    _exitingBattle = false;
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
        else if (_transitionState == TransitionState.FlashWhite)
        {
            // White flash: ramp up over FlashDuration
            FadeWhiteAlpha = Math.Min(_fadeTimer / FlashDuration, 1f);

            if (_fadeTimer >= FlashDuration)
            {
                _transitionState = TransitionState.FadingToBattle;
                _fadeTimer = 0f;
            }
        }
        else if (_transitionState == TransitionState.FadingToBattle)
        {
            // Fade to black while white flash fades out
            FadeAlpha = Math.Min(_fadeTimer / FadeDuration, 1f);
            FadeWhiteAlpha = Math.Max(1f - _fadeTimer / (FadeDuration * 0.5f), 0f);

            if (_fadeTimer >= FadeDuration)
            {
                FadeWhiteAlpha = 0f;
                FadeAlpha = 1f;
                State = GameState.Battle;
                _transitionState = TransitionState.FadingFromBattle;
                _fadeTimer = 0f;
            }
        }
        else if (_transitionState == TransitionState.FadingFromBattle)
        {
            // Fade in the battle screen from black
            FadeAlpha = 1f - Math.Min(_fadeTimer / FadeDuration, 1f);

            if (_fadeTimer >= FadeDuration)
            {
                FadeAlpha = 0f;
                _transitionState = TransitionState.None;
            }
        }
    }

    // ── Battle transition ─────────────────────────────────────────────

    private void BeginBattleTransition()
    {
        CurrentBattleBackground = BattleBackgroundResolver.FromOverlayBehavior(
            GetEncounterBehavior(Player.TileX, Player.TileY));
        _transitionState = TransitionState.FlashWhite;
        _fadeTimer = 0f;
    }

    private string? GetEncounterBehavior(int x, int y)
    {
        int overlayTile = CurrentMap!.GetOverlayTile(x, y);
        if (overlayTile >= 0)
        {
            var def = TileRegistry.GetTile(overlayTile);
            if (def?.Category == TileCategory.Encounter)
                return def.OverlayBehavior;
        }

        var baseDef = TileRegistry.GetTile(CurrentMap.GetBaseTile(x, y));
        if (baseDef?.Category == TileCategory.Encounter)
            return baseDef.OverlayBehavior;

        return null;
    }

    /// <summary>
    /// Called by Game1 when the player clicks the Back button on the battle screen.
    /// Fades to black, switches back to overworld, fades in.
    /// </summary>
    public void ExitBattle()
    {
        if (State != GameState.Battle || _transitionState != TransitionState.None)
            return;

        _exitingBattle = true;
        _transitionState = TransitionState.FadingOut;
        _fadeTimer = 0f;
    }

    private bool IsEncounterTile(int x, int y)
    {
        // Check only the player's actual tile position.
        int overlayTile = CurrentMap!.GetOverlayTile(x, y);
        if (overlayTile >= 0)
        {
            var def = TileRegistry.GetTile(overlayTile);
            if (def?.Category == TileCategory.Encounter)
                return true;
        }

        var baseDef = TileRegistry.GetTile(CurrentMap.GetBaseTile(x, y));
        if (baseDef?.Category == TileCategory.Encounter)
            return true;

        return false;
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

    // Transition tile IDs (must match tile registry)
    private const int TransitionNorthId = 112;
    private const int TransitionSouthId = 113;
    private const int TransitionWestId  = 114;
    private const int TransitionEastId  = 115;

    private static bool IsTransitionTileForEdge(int tileId, MapEdge edge) => edge switch
    {
        MapEdge.North => tileId == TransitionNorthId,
        MapEdge.South => tileId == TransitionSouthId,
        MapEdge.West  => tileId == TransitionWestId,
        MapEdge.East  => tileId == TransitionEastId,
        _ => false
    };

    // Get the receiving transition tile ID for the opposite edge
    private static int ReceivingTileId(MapEdge sourceEdge) => sourceEdge switch
    {
        MapEdge.North => TransitionSouthId,  // crossing north → land on south transition
        MapEdge.South => TransitionNorthId,
        MapEdge.West  => TransitionEastId,
        MapEdge.East  => TransitionWestId,
        _ => -1
    };

    private static bool TryFindReceivingTransition(MapDefinition target, MapEdge sourceEdge, out int tx, out int ty)
    {
        int receivingId = ReceivingTileId(sourceEdge);
        tx = 0; ty = 0;

        // Scan the target's opposite edge for the receiving transition tile,
        // then spawn the player one tile inward so they aren't right on the edge.
        switch (sourceEdge)
        {
            case MapEdge.North: // scan target's south edge, spawn one tile up
                for (int x = 0; x < target.Width; x++)
                    if (target.GetBaseTile(x, target.Height - 1) == receivingId)
                    { tx = x; ty = target.Height - 2; return true; }
                break;
            case MapEdge.South: // scan target's north edge, spawn one tile down
                for (int x = 0; x < target.Width; x++)
                    if (target.GetBaseTile(x, 0) == receivingId)
                    { tx = x; ty = 1; return true; }
                break;
            case MapEdge.West: // scan target's east edge, spawn one tile left
                for (int y = 0; y < target.Height; y++)
                    if (target.GetBaseTile(target.Width - 1, y) == receivingId)
                    { tx = target.Width - 2; ty = y; return true; }
                break;
            case MapEdge.East: // scan target's west edge, spawn one tile right
                for (int y = 0; y < target.Height; y++)
                    if (target.GetBaseTile(0, y) == receivingId)
                    { tx = 1; ty = y; return true; }
                break;
        }
        return false;
    }

    private bool TryEdgeTransition(MapEdge edge, int playerX, int playerY)
    {
        int baseTile = CurrentMapDefinition!.GetBaseTile(playerX, playerY);
        if (!IsTransitionTileForEdge(baseTile, edge))
            return false;

        MapDefinition? target = null;
        var conn = CurrentMapDefinition.GetConnection(edge);
        if (conn != null)
            MapCatalog.TryGetMap(conn.TargetMapId, out target);
        
        if (target == null)
            target = MapCatalog.GetNeighbor(CurrentMapDefinition, edge);
        
        if (target == null)
            return false;

        if (TryFindReceivingTransition(target, edge, out int tx, out int ty))
        {
            BeginTransition(new WarpConnection(0, 0, target.Id, tx, ty));
            return true;
        }

        int offset = conn?.Offset ?? 0;
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
