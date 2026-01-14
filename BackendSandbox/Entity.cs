using System.Numerics;

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
    
    public Player(int x, int y, int width, int height)
    {
        EntityColor = Color.Blue;
        ChangePos(x, y);
        Width = width;
        Height = height;
        IsOwnedByPlayer = true;
    }

    public void Move(Vector2 direction, float dt)
    {
        if (direction == Vector2.Zero)
            return;
        
        direction = Vector2.Normalize(direction);

        Pos.X += (int)Math.Round(direction.X * Speed * dt);
        Pos.Y += (int)Math.Round(direction.Y * Speed * dt);
    }
}

class Enemy : Entity
{
    
    public float Speed = 200f;
    
    public Enemy(int x, int y, int width, int height)
    {
        ChangePos(x, y);
        Width = width;
        Height = height;
        IsOwnedByPlayer = false;
    }
    
    public void Move(Vector2 direction, float dt)
    {
        if (direction == Vector2.Zero)
            return;

        direction = Vector2.Normalize(direction);

        Pos.X += (int)Math.Round(direction.X * Speed * dt);
        Pos.Y += (int)Math.Round(direction.Y * Speed * dt);
    }
}