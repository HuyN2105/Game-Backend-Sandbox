using System.Numerics;
using System.Drawing;

namespace BackendSandbox;

public class Entity
{
    public Vector2 Pos;
    public int Width;
    public int Height;

    public bool IsOwnedByPlayer;
    public bool IsWalkThrough;
    public bool IsVisible;
    public bool IsDead;
    public bool IsHazard;

    // Temporary
    public Color EntityColor;

    public RectangleF Bounds => new RectangleF(Pos.X, Pos.Y, Width, Height);

    public Entity(Vector2 pos, int width, int height)
    {
        Pos = pos;
        Width = width;
        Height = height;
    }

    public void ChangePos(int x, int y)
    {
        Pos.X = x;
        Pos.Y = y;
    }

    public bool IsContain(int x, int y)
    {
        return x >= Pos.X &&
               x <= Pos.X + Width &&
               y >= Pos.Y &&
               y <= Pos.Y + Height;
    }

    public bool IsCollide(Entity other)
    {
        return Pos.X + Width >= other.Pos.X && // r1 right edge past r2 left
               Pos.X <= other.Pos.X + other.Width && // r1 left edge past r2 right
               Pos.Y + Height >= other.Pos.Y && // r1 top edge past r2 bottom
               Pos.Y <= other.Pos.Y + other.Height;
    }
}

class Player : Entity
{
    public float Speed = 300f;
    public Vector2 LookingDirection = Vector2.Zero;

    public Player(int x, int y, int width, int height) : base(new Vector2(x, y), width, height)
    {
        EntityColor = Color.Blue;
        IsOwnedByPlayer = true;
    }

    public Player(Vector2 pos, int width, int height) : base(pos, width, height)
    {
        EntityColor = Color.Blue;
        IsOwnedByPlayer = true;
    }

    public void Move(Vector2 direction, float dt, World.Room currentRoom)
    {
        if (direction == Vector2.Zero)
            return;

        direction = Vector2.Normalize(direction);

        var moveVector = new Vector2(direction.X * Speed * dt, direction.Y * Speed * dt);
        var allowedMove = GameLogic.ValidMove(this, moveVector, currentRoom);

        Pos += allowedMove;
    }
}

class Enemy : Entity
{
    public float Speed = 200f;

    public Enemy(int x, int y, int width, int height) : base(new Vector2(x, y), width, height)
    {
        EntityColor = Color.Green;
        IsOwnedByPlayer = false;
    }

    public Enemy(Vector2 pos, int width, int height) : base(pos, width, height)
    {
        EntityColor = Color.Green;
        IsOwnedByPlayer = false;
    }

    public void Move(Vector2 direction, float dt, World.Room currentRoom)
    {
        if (direction == Vector2.Zero)
            return;

        direction = Vector2.Normalize(direction);
        
        var moveVector = new Vector2(direction.X * Speed * dt, direction.Y * Speed * dt);
        var allowedMove = GameLogic.ValidMove(this, moveVector, currentRoom);

        Pos += allowedMove;
    }
}