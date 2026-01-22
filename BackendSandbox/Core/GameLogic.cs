using System.Numerics;
using System.Drawing; // For Brushes
using BackendSandbox.Models;

namespace BackendSandbox.Core;

public static class GameLogic
{
    public static Vector2 ValidMove(Entity entity, Vector2 moveVector, Room currentRoom)
    {
        float epsilon = 0.3f;

        // =========================================================
        // 1. RESOLVE X AXIS
        // =========================================================
        float targetX = entity.Pos.X + moveVector.X;

        // --- Tilemap Collision (Snap to Wall) ---
        if (moveVector.X > 0) // Moving Right
        {
            // Check Top-Right and Bottom-Right corners
            if (IsWall(targetX + entity.Width, entity.Pos.Y + epsilon, currentRoom) ||
                IsWall(targetX + entity.Width, entity.Pos.Y + entity.Height - epsilon, currentRoom))
            {
                // Snap to the Left edge of the tile we hit
                int tileX = (int)((targetX + entity.Width) / currentRoom.TileSize);
                float wallLeftEdge = tileX * currentRoom.TileSize;
                
                // Distance = WallLeft - MyRight
                moveVector.X = wallLeftEdge - (entity.Pos.X + entity.Width) - 0.01f; 
            }
        }
        else if (moveVector.X < 0) // Moving Left
        {
            // Check Top-Left and Bottom-Left corners
            if (IsWall(targetX, entity.Pos.Y + epsilon, currentRoom) ||
                IsWall(targetX, entity.Pos.Y + entity.Height - epsilon, currentRoom))
            {
                // Snap to the Right edge of the tile we hit
                int tileX = (int)(targetX / currentRoom.TileSize);
                float wallRightEdge = (tileX + 1) * currentRoom.TileSize;

                // Distance = WallRight - MyLeft
                moveVector.X = wallRightEdge - entity.Pos.X + 0.01f;
            }
        }

        // Re-calculate targetX in case the wall check changed moveVector.X
        targetX = entity.Pos.X + moveVector.X;

        // --- Entity Collision ---
        var testEntityX = new Entity(entity.EntityType, new Vector2(targetX, entity.Pos.Y + epsilon), entity.Width,
            (int)Math.Round(entity.Height - (2 * epsilon)), Brushes.Transparent);

        foreach (var other in currentRoom.OtherEntities)
        {
            if (ReferenceEquals(other, entity) || other.IsWalkThrough) continue;

            if (testEntityX.IsCollide(other))
            {
                if (moveVector.X > 0)
                    moveVector.X = other.Pos.X - (entity.Pos.X + entity.Width);
                else if (moveVector.X < 0)
                    moveVector.X = (other.Pos.X + other.Width) - entity.Pos.X;
                break;
            }
        }

        foreach (var other in currentRoom.Enemies)
        {
            if (ReferenceEquals(entity, other)) continue;

            if (testEntityX.IsCollide(other))
            {
                if (moveVector.X > 0)
                    moveVector.X = other.Pos.X - (entity.Pos.X + entity.Width);
                else if (moveVector.X < 0)
                    moveVector.X = (other.Pos.X + other.Width) - entity.Pos.X;
                break;
            }
        }

        // =========================================================
        // 2. RESOLVE Y AXIS
        // =========================================================
        float correctedX = entity.Pos.X + moveVector.X;
        float targetY = entity.Pos.Y + moveVector.Y;

        // --- Tilemap Collision (Snap to Wall) ---
        if (moveVector.Y > 0) // Moving Down
        {
            // Check Bottom-Left and Bottom-Right
            if (IsWall(correctedX + epsilon, targetY + entity.Height, currentRoom) ||
                IsWall(correctedX + entity.Width - epsilon, targetY + entity.Height, currentRoom))
            {
                // Snap to the Top edge of the tile
                int tileY = (int)((targetY + entity.Height) / currentRoom.TileSize);
                float wallTopEdge = tileY * currentRoom.TileSize;

                moveVector.Y = wallTopEdge - (entity.Pos.Y + entity.Height) - 0.01f;
            }
        }
        else if (moveVector.Y < 0) // Moving Up
        {
            // Check Top-Left and Top-Right
            if (IsWall(correctedX + epsilon, targetY, currentRoom) ||
                IsWall(correctedX + entity.Width - epsilon, targetY, currentRoom))
            {
                // Snap to the Bottom edge of the tile
                int tileY = (int)(targetY / currentRoom.TileSize);
                float wallBottomEdge = (tileY + 1) * currentRoom.TileSize;

                moveVector.Y = wallBottomEdge - entity.Pos.Y + 0.01f;
            }
        }

        // Re-calculate targetY
        targetY = entity.Pos.Y + moveVector.Y;

        // --- Entity Collision ---
        var testEntityY = new Entity(entity.EntityType, new Vector2(correctedX + epsilon, targetY),
            (int)Math.Round(entity.Width - (2 * epsilon)), entity.Height, Brushes.Transparent);

        foreach (var other in currentRoom.OtherEntities)
        {
            if (ReferenceEquals(other, entity) || other.IsWalkThrough) continue;

            if (testEntityY.IsCollide(other))
            {
                if (moveVector.Y > 0)
                    moveVector.Y = other.Pos.Y - (entity.Pos.Y + entity.Height);
                else if (moveVector.Y < 0)
                    moveVector.Y = (other.Pos.Y + other.Height) - entity.Pos.Y;
                break;
            }
        }

        foreach (var other in currentRoom.Enemies)
        {
            if (ReferenceEquals(entity, other)) continue;

            if (testEntityY.IsCollide(other))
            {
                if (moveVector.Y > 0)
                    moveVector.Y = other.Pos.Y - (entity.Pos.Y + entity.Height);
                else if (moveVector.Y < 0)
                    moveVector.Y = (other.Pos.Y + other.Height) - entity.Pos.Y;
                break;
            }
        }

        return moveVector;
    }

    public static bool IsBulletHitSomething(Bullet bullet, Vector2 moveVector, Room currentRoom, List<Entity> candidates)
    {
        Vector2 nextPos = bullet.Pos + moveVector;

        // Check Walls (Tiles) - Just check 4 corners
        if (CheckTileCollision(nextPos.X, nextPos.Y, bullet.Width, bullet.Height, currentRoom))
        {
            return true;
        }

        // Check Entities
        var testBullet = new Bullet(nextPos, bullet.Width, bullet.Height, bullet.MovingDirection,
            bullet.IsOwnedByPlayer, Brushes.Transparent);

        foreach (var other in candidates)
        {
            if (ReferenceEquals(other, bullet)) continue;
            if (other is Bullet) continue;
            if (other.IsDead) continue;

            if (testBullet.IsCollide(other))
            {
                if (bullet.IsOwnedByPlayer && other is Enemy enemy)
                {
                    enemy.TakeDamage(bullet.Damage);
                    return true;
                }
                if (!bullet.IsOwnedByPlayer && other is Player player)
                {
                    player.TakeDamage(bullet.Damage);
                    return true;
                }

                if (!other.IsWalkThrough && !other.IsOwnedByPlayer) return true;
            }
        }

        return false;
    }

    // Helper: Simple boolean check for bullet/logic
    private static bool CheckTileCollision(float x, float y, int w, int h, Room room)
    {
        if (IsWall(x, y, room)) return true;
        if (IsWall(x + w, y, room)) return true;
        if (IsWall(x, y + h, room)) return true;
        if (IsWall(x + w, y + h, room)) return true;
        return false;
    }

    private static bool IsWall(float x, float y, Room room)
    {
        int gridX = (int)(x / room.TileSize);
        int gridY = (int)(y / room.TileSize);
        return room.GetTileAt(gridX, gridY).IsSolid;
    }
}