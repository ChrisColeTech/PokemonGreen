using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokemonGreen.Core;
using PokemonGreen.Core.Graphics;

namespace PokemonGreen.Core.Rendering;

public class PlayerRenderer
{
    private readonly Player _player;
    private readonly TextureStore _textures;
    private const int SpriteFrameWidth = 64;
    private const int SpriteFrameHeight = 64;

    public PlayerRenderer(Player player, TextureStore textures)
    {
        _player = player;
        _textures = textures;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        var texture = GetTextureForState(_player.State);

        var frameCount = Math.Max(1, texture.Width / SpriteFrameWidth);
        var rowCount = Math.Max(1, texture.Height / SpriteFrameHeight);
        
        var frameX = _player.FrameIndex * SpriteFrameWidth;
        var frameY = Math.Min((int)_player.Facing, rowCount - 1) * SpriteFrameHeight;
        var sourceRect = new Rectangle(frameX, frameY, SpriteFrameWidth, SpriteFrameHeight);

        var jumpOffset = CalculateJumpOffset();
        var drawPosition = new Vector2(
            _player.X - SpriteFrameWidth / 2,
            _player.Y - SpriteFrameHeight / 2 - jumpOffset);

        spriteBatch.Draw(texture, drawPosition, sourceRect, Color.White);
    }

    private Texture2D GetTextureForState(PlayerState state)
    {
        return state switch
        {
            PlayerState.Run => _textures.PlayerRun,
            PlayerState.Walk => _textures.PlayerWalk,
            PlayerState.Jump => _textures.PlayerJump,
            PlayerState.Climb => _textures.PlayerClimb,
            PlayerState.Combat => _textures.PlayerCombat,
            PlayerState.Spellcast => _textures.PlayerSpellcast,
            _ => _textures.PlayerIdle
        };
    }

    private float CalculateJumpOffset()
    {
        if (_player.State != PlayerState.Jump)
            return 0f;

        var arc = MathF.Sin(_player.AnimationProgress * MathF.PI);
        return arc * 18f;
    }
}
