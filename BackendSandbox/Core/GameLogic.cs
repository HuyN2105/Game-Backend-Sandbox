using System.Numerics;
using System.Runtime.CompilerServices;

namespace BackendSandbox;

public static class GameLogic
{
    public static Vector2 ValidMove(Entity entity, Vector2 moveVector, World.Room currentRoom)
    {
        // A tiny buffer to prevent "snagging" on edges you are sliding against.
        float epsilon = 0.3f;

        float targetX = entity.Pos.X + moveVector.X;

        var testEntityX = new Entity(new Vector2(targetX, entity.Pos.Y + epsilon), entity.Width,
            (int)Math.Round(entity.Height - (2 * epsilon)));

        foreach (var other in currentRoom.OtherEntities)
        {
            if (ReferenceEquals(other, entity) || other.IsWalkThrough)
                continue;

            if (testEntityX.IsCollide(other))
            {
                if (moveVector.X > 0)
                {
                    // Snap to Left edge
                    moveVector.X = other.Pos.X - (entity.Pos.X + entity.Width);
                }
                else if (moveVector.X < 0)
                {
                    // Snap to Right edge
                    moveVector.X = (other.Pos.X + other.Width) - entity.Pos.X;
                }

                break;
            }
        }

        foreach (var other in currentRoom.Enemies)
        {
            if (ReferenceEquals(entity, other))
                continue;

            if (testEntityX.IsCollide(other))
            {
                if (moveVector.X > 0)
                {
                    // Snap to Left edge
                    moveVector.X = other.Pos.X - (entity.Pos.X + entity.Width);
                }
                else if (moveVector.X < 0)
                {
                    // Snap to Right edge
                    moveVector.X = (other.Pos.X + other.Width) - entity.Pos.X;
                }

                break;
            }
        }

        float correctedX = entity.Pos.X + moveVector.X;
        float targetY = entity.Pos.Y + moveVector.Y;

        var testEntityY = new Entity(new Vector2(correctedX + epsilon, targetY),
            (int)Math.Round(entity.Width - (2 * epsilon)), entity.Height);

        foreach (var other in currentRoom.OtherEntities)
        {
            if (ReferenceEquals(other, entity) || other.IsWalkThrough)
                continue;

            if (testEntityY.IsCollide(other))
            {
                if (moveVector.Y > 0)
                {
                    moveVector.Y = other.Pos.Y - (entity.Pos.Y + entity.Height);
                }
                else if (moveVector.Y < 0)
                {
                    moveVector.Y = (other.Pos.Y + other.Height) - entity.Pos.Y;
                }

                break;
            }
        }

        foreach (var other in currentRoom.Enemies)
        {
            if (ReferenceEquals(entity, other))
                continue;

            if (testEntityY.IsCollide(other))
            {
                if (moveVector.Y > 0)
                {
                    moveVector.Y = other.Pos.Y - (entity.Pos.Y + entity.Height);
                }
                else if (moveVector.Y < 0)
                {
                    moveVector.Y = (other.Pos.Y + other.Height) - entity.Pos.Y;
                }

                break;
            }
        }

        return moveVector;
    }

    public static bool IsBulletHitSomething(Bullet bullet, Vector2 moveVector, World.Room currentRoom)
    {
        Vector2 nextPos = bullet.Pos + moveVector;

        if (nextPos.X < 0 || nextPos.Y < 0) return true;

        var testBullet = new Bullet(nextPos, bullet.Width, bullet.Height, bullet.MovingDirection,
            bullet.IsOwnedByPlayer);

        foreach (var other in currentRoom.OtherEntities)
        {
            if (other.GetType() == typeof(Bullet) || other.IsWalkThrough) continue;
            if (testBullet.IsCollide(other)) return true; // Check testBullet, not bullet
        }

        if (bullet.IsOwnedByPlayer)
        {
            foreach (var enemy in currentRoom.Enemies)
            {
                if (enemy.IsDead) continue;
                if (testBullet.IsCollide(enemy))
                {
                    enemy.TakeDamage(bullet.damage);
                    return true;
                }
            }
        }

        if (!bullet.IsOwnedByPlayer)
        {
            foreach (var player in currentRoom.Players)
            {
                if (player.IsDead) continue;
                if (testBullet.IsCollide(player))
                {
                    player.TakeDamage(bullet.damage);
                    return true;
                }
            }
        }

        return false;
    }
}