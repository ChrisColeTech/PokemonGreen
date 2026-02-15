using System;
using Microsoft.Xna.Framework;

namespace PokemonGreen.Core.Systems;

/// <summary>
/// Simulates a day/night cycle by interpolating between 24 hourly tint colors.
/// 30 real minutes = 24 game hours. The tint is applied as a color multiply over the overworld.
/// Hourly RGB values taken directly from the old engine's DayTint.cs.
/// </summary>
public class DayNightCycle
{
    // 24 game hours in 30 real minutes = 1800 seconds total, 75 seconds per game hour
    private const float SecondsPerGameHour = 75f;
    private const float TotalCycleSeconds = SecondsPerGameHour * 24f; // 1800

    // Hourly tint colors from old engine (RGB multipliers 0.0-1.0)
    private static readonly Vector3[] HourlyTints =
    {
        new(0.160f, 0.180f, 0.330f), // 00
        new(0.160f, 0.180f, 0.330f), // 01
        new(0.160f, 0.180f, 0.330f), // 02
        new(0.170f, 0.185f, 0.345f), // 03
        new(0.225f, 0.235f, 0.375f), // 04
        new(0.350f, 0.265f, 0.415f), // 05
        new(0.500f, 0.400f, 0.500f), // 06
        new(0.720f, 0.660f, 0.555f), // 07
        new(0.900f, 0.785f, 0.815f), // 08
        new(0.950f, 0.980f, 0.905f), // 09
        new(1.000f, 0.985f, 0.945f), // 10
        new(1.000f, 1.000f, 0.950f), // 11
        new(1.000f, 1.000f, 1.000f), // 12 (noon — full brightness)
        new(1.000f, 1.000f, 0.985f), // 13
        new(1.000f, 1.000f, 0.955f), // 14
        new(0.995f, 1.000f, 0.950f), // 15
        new(0.955f, 0.975f, 0.850f), // 16
        new(0.845f, 0.885f, 0.740f), // 17
        new(0.700f, 0.690f, 0.560f), // 18
        new(0.545f, 0.460f, 0.390f), // 19
        new(0.490f, 0.320f, 0.380f), // 20
        new(0.250f, 0.235f, 0.370f), // 21
        new(0.180f, 0.205f, 0.350f), // 22
        new(0.160f, 0.180f, 0.330f), // 23
    };

    private float _elapsedSeconds;

    /// <summary>Current game hour (0-23).</summary>
    public int CurrentHour => (int)(_elapsedSeconds / SecondsPerGameHour) % 24;

    /// <summary>Whether the cycle is active. Disable for indoor maps etc.</summary>
    public bool Enabled { get; set; } = true;

    public DayNightCycle()
    {
        // Start at noon (hour 12) so the player sees full brightness first
        _elapsedSeconds = 12f * SecondsPerGameHour;
    }

    public void Update(float deltaTime)
    {
        if (!Enabled)
            return;

        _elapsedSeconds += deltaTime;
        if (_elapsedSeconds >= TotalCycleSeconds)
            _elapsedSeconds -= TotalCycleSeconds;
    }

    /// <summary>
    /// Returns the current tint as a MonoGame Color, interpolated between the
    /// two nearest hours based on elapsed minutes within the current hour.
    /// </summary>
    public Color GetCurrentTint()
    {
        if (!Enabled)
            return Color.White;

        float gameHourFloat = _elapsedSeconds / SecondsPerGameHour;
        int hour = (int)gameHourFloat % 24;
        int nextHour = (hour + 1) % 24;
        float t = gameHourFloat - (int)gameHourFloat; // fractional part = progress within the hour

        Vector3 tint = Vector3.Lerp(HourlyTints[hour], HourlyTints[nextHour], t);

        return new Color(tint.X, tint.Y, tint.Z);
    }
}
