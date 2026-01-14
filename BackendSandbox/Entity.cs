using System.Numerics;

namespace BackendSandbox;

public class Entity
{
    public int X;
    public int Y;
    public int Width;
    public int Height;

    public bool IsOwnedByPlayer;
    public bool IsWalkThrough;
    public bool IsVisible;
    public bool IsDead;
    public bool IsHazard;

    public RectangleF Bounds => new RectangleF(X, Y, Width, Height);

    public void ChangePos(int x, int y)
    {
        X = x;
        Y = y;
    }

    public bool IsContain(int x, int y)
    {
        return x >= X &&
               x <= X + Width &&
               y >= Y &&
               y <= Y + Height;
    }

    public bool IsCollide(Entity other)
    {
        return X + Width >= other.X && // r1 right edge past r2 left
               X <= other.X + other.Width && // r1 left edge past r2 right
               Y + Height >= other.Y && // r1 top edge past r2 bottom
               Y <= other.Y + other.Height;
    }
}

class Player : Entity
{

    public float Speed = 300f;
    
    public Player(int x, int y, int width, int height)
    {
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

        X += (int)Math.Round(direction.X * Speed * dt);
        Y += (int)Math.Round(direction.Y * Speed * dt);
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

        X += (int)Math.Round(direction.X * Speed * dt);
        Y += (int)Math.Round(direction.Y * Speed * dt);
    }
}