using System;
using System.Numerics;
using BackendSandbox.Models;

namespace BackendSandbox.Core;

public static class GameMath
{
    public static Vector2 EntityCenter(Entity e)
    {
        return new Vector2(
            e.Pos.X + e.Width / 2f,
            e.Pos.Y + e.Height / 2f
        );
    }

    public static Vector2 AimLine(Entity entity, Vector2 targetPos, float lineLength)
    {
        var origin = EntityCenter(entity);

        float dx = targetPos.X - origin.X;
        float dy = targetPos.Y - origin.Y;

        float length = MathF.Sqrt(dx * dx + dy * dy);

        if (length < 0.0001f) return Vector2.Zero;

        dx /= length;
        dy /= length;

        return new Vector2(
            origin.X + dx * lineLength,
            origin.Y + dy * lineLength
        );
    }
}