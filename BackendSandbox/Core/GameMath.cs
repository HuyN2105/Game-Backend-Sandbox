using System.Numerics;
using BackendSandbox.Models;

namespace BackendSandbox.Core;

public static class GameMath
{
    public static PointF EntityCenter(Entity e)
    {
        return new PointF(
            e.Pos.X + e.Width / 2f,
            e.Pos.Y + e.Height / 2f
        );
    }

    public static PointF AimLine(Entity entity, Vector2 targetPos, float lineLength)
    {
        var origin = EntityCenter(entity);
        var target = new PointF(targetPos);

        float dx = target.X - origin.X;
        float dy = target.Y - origin.Y;

        float length = MathF.Sqrt(dx * dx + dy * dy);

        if (length < 0.0001f) return PointF.Empty;

        dx /= length;
        dy /= length;

        var end = new PointF(
            origin.X + dx * lineLength,
            origin.Y + dy * lineLength
        );
        
        return end;
    }
    
    public static PointF V2toPF(Vector2 v) => new(v.X, v.Y);
    
    public static Vector2 PFtoV2(PointF p) => new(p.X, p.Y);
}