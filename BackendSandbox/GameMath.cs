using System.Numerics;

namespace BackendSandbox;

public static class GameMath
{
    public static PointF EntityCenter(Entity _Entity)
    {
        return new PointF(
            _Entity.Pos.X + _Entity.Width / 2f,
            _Entity.Pos.Y + _Entity.Height / 2f
        );
    }

    public static PointF AimLine(Entity _Entity, Vector2 TargetPos, float LineLength)
    {
        var origin = EntityCenter(_Entity);
        var target = new PointF(TargetPos);

        float dx = target.X - origin.X;
        float dy = target.Y - origin.Y;

        float length = MathF.Sqrt(dx * dx + dy * dy);

        if (length < 0.0001f) return PointF.Empty;

        dx /= length;
        dy /= length;

        var end = new PointF(
            origin.X + dx * LineLength,
            origin.Y + dy * LineLength
        );
        
        return end;
    }
}