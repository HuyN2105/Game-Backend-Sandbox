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

    public float LastTakeDamageTime = -1f;

    // Temporary
    public Brush EntityColor;

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

    public virtual void TakeDamage(float damage)
    {
    }

    public virtual void Move(Vector2 direction, float dt, World.Room currentRoom)
    {
    }
}

public class Player : Entity
{
    public float Speed = 300f;
    public Vector2 LookingDirection = Vector2.Zero;
    public float Health = 100f;

    public Player(int x, int y, int width, int height) : base(new Vector2(x, y), width, height)
    {
        EntityColor = Brushes.Blue;
        IsOwnedByPlayer = true;
    }

    public Player(Vector2 pos, int width, int height) : base(pos, width, height)
    {
        EntityColor = Brushes.Blue;
        IsOwnedByPlayer = true;
    }

    public override void Move(Vector2 direction, float dt, World.Room currentRoom)
    {
        if (IsDead)
            currentRoom.Players.Remove(this);

        if (direction == Vector2.Zero)
            return;

        direction = Vector2.Normalize(direction);

        var moveVector = direction * Speed * dt;
        var allowedMove = GameLogic.ValidMove(this, moveVector, currentRoom);

        Pos += allowedMove;

        foreach (var other in currentRoom.Enemies)
        {
            other.LookingDirection = Pos;
        }
    }

    public void Shoot(World.Room currentRoom)
    {
        // Calculate vector from Player to Mouse
        Vector2 direction = LookingDirection - Pos;

        // Normalize it (make length 1) so speed is consistent
        if (direction != Vector2.Zero)
        {
            direction = Vector2.Normalize(direction);
        }

        // Now pass the normalized direction
        currentRoom.OtherEntities.Add(new Bullet(Pos, Width, Height, direction, true));
    }

    public override void TakeDamage(float damage)
    {
        Health -= damage;

        if (Health <= 0)
        {
            IsDead = true;
        }
    }
}

public class Enemy : Entity
{
    public float Speed = 200f;
    public Vector2 LookingDirection = Vector2.Zero;
    public float Health = 50f;

    public Enemy(int x, int y, int width, int height) : base(new Vector2(x, y), width, height)
    {
        EntityColor = Brushes.Blue;
        IsOwnedByPlayer = false;
        IsWalkThrough = false;
    }

    public Enemy(Vector2 pos, int width, int height) : base(pos, width, height)
    {
        EntityColor = Brushes.Blue;
        IsOwnedByPlayer = false;
    }

    public override void Move(Vector2 direction, float dt, World.Room currentRoom)
    {
        if (IsDead)
            currentRoom.Enemies.Remove(this);

        if (direction == Vector2.Zero)
            return;

        direction = Vector2.Normalize(direction);

        var moveVector = direction * Speed * dt;
        var allowedMove = GameLogic.ValidMove(this, moveVector, currentRoom);

        Pos += allowedMove;
    }

    public void Shoot(World.Room currentRoom)
    {
        currentRoom.OtherEntities.Add(new Bullet(Pos, Width, Height, LookingDirection, false));
    }

    public override void TakeDamage(float damage)
    {
        Health -= damage;

        if (Health <= 0)
        {
            IsDead = true;
        }
    }
}

public class Bullet : Entity
{
    public float speed = 500f;
    public float damage = 10f;
    public Vector2 MovingDirection = Vector2.Zero;

    public Bullet(int x, int y, int width, int height, Vector2 movingDirection, bool isOwnedByPlayer) : base(
        new Vector2(x, y), width, height)
    {
        IsWalkThrough = true;
        MovingDirection = movingDirection;
        IsOwnedByPlayer = isOwnedByPlayer;
        EntityColor = Brushes.Red;
    }

    public Bullet(Vector2 pos, int width, int height, Vector2 movingDirection, bool isOwnedByPlayer) : base(pos, width,
        height)
    {
        IsWalkThrough = true;
        MovingDirection = movingDirection;
        IsOwnedByPlayer = isOwnedByPlayer;
        EntityColor = Brushes.Red;
    }

    public override void Move(Vector2 direction, float dt, World.Room currentRoom)
    {
        if (IsDead) currentRoom.OtherEntities.Remove(this);

        Vector2 moveVector = MovingDirection * speed * dt;

        if (GameLogic.IsBulletHitSomething(this, moveVector, currentRoom))
        {
            currentRoom.OtherEntities.Remove(this);
        }

        Pos += moveVector;
    }
}