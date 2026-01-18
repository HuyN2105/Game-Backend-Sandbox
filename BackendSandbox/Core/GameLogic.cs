using System.Numerics;

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

        foreach (var other in currentRoom.Entities)
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

        float correctedX = entity.Pos.X + moveVector.X;
        float targetY = entity.Pos.Y + moveVector.Y;

        var testEntityY = new Entity(new Vector2(correctedX + epsilon, targetY),
            (int)Math.Round(entity.Width - (2 * epsilon)), entity.Height);

        foreach (var other in currentRoom.Entities)
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

        return moveVector;
    }
}