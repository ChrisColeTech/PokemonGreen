using Microsoft.Xna.Framework;

namespace PokemonGreen.Core.Rendering;

public class Camera3D
{
    public Vector3 Position { get; private set; }
    public Vector3 Target { get; private set; }
    public Vector3 Up { get; } = Vector3.Up;

    public float Yaw { get; set; }
    public float Pitch { get; set; } = -0.3f;
    public float Distance { get; set; } = 25f;

    public float MinDistance { get; set; } = 8f;
    public float MaxDistance { get; set; } = 50f;
    public float MinPitch { get; set; } = -1.2f;
    public float MaxPitch { get; set; } = -0.1f;

    public Matrix ViewMatrix { get; private set; }
    public Matrix ProjectionMatrix { get; private set; }

    private readonly float _fov;
    private readonly float _nearPlane;
    private readonly float _farPlane;

    public Camera3D(float fov = MathHelper.PiOver4, float nearPlane = 1f, float farPlane = 2000f)
    {
        _fov = fov;
        _nearPlane = nearPlane;
        _farPlane = farPlane;
    }

    public void Follow(Vector3 target, float targetHeight = 2f)
    {
        Target = target + Vector3.Up * targetHeight;

        float x = Target.X + MathF.Sin(Yaw) * MathF.Cos(Pitch) * Distance;
        float y = Target.Y - MathF.Sin(Pitch) * Distance;
        float z = Target.Z + MathF.Cos(Yaw) * MathF.Cos(Pitch) * Distance;

        Position = new Vector3(x, y, z);
    }

    public void Rotate(float deltaYaw, float deltaPitch)
    {
        Yaw += deltaYaw;
        Pitch = MathHelper.Clamp(Pitch + deltaPitch, MinPitch, MaxPitch);
    }

    public void Zoom(float delta)
    {
        Distance = MathHelper.Clamp(Distance + delta, MinDistance, MaxDistance);
    }

    public void Update(float aspectRatio)
    {
        ViewMatrix = Matrix.CreateLookAt(Position, Target, Up);
        ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(_fov, aspectRatio, _nearPlane, _farPlane);
    }

    public Vector3 GetForward()
    {
        return Vector3.Normalize(Target - Position);
    }

    public Vector3 GetFlatForward()
    {
        var forward = GetForward();
        forward.Y = 0;
        if (forward.LengthSquared() > 0)
            forward.Normalize();
        return forward;
    }

    public Vector3 GetRight()
    {
        return Vector3.Normalize(Vector3.Cross(GetFlatForward(), Vector3.Up));
    }
}
