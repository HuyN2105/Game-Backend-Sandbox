using System.Numerics;
using System.Drawing;
using BackendSandbox.Core;

namespace BackendSandbox.Models;

public class Entity
{
    public EntityTypes EntityType;
    public Vector2 Pos;
    public int Width;
    public int Height;

    public bool IsOwnedByPlayer;
    public bool IsWalkThrough;
    public bool IsVisible = true;
    public bool IsDead;

    // Temporary Visualization
    public Brush EntityColor;
    public RectangleF Bounds => new RectangleF(Pos.X, Pos.Y, Width, Height);

    public Entity(EntityTypes entityType, Vector2 pos, int width, int height, Brush entityColor)
    {
        EntityType = entityType;
        Pos = pos;
        Width = width;
        Height = height;
        EntityColor = entityColor ?? Brushes.White;
    }

    public bool IsCollide(Entity other)
    {
        return Pos.X + Width >= other.Pos.X &&
               Pos.X <= other.Pos.X + other.Width &&
               Pos.Y + Height >= other.Pos.Y &&
               Pos.Y <= other.Pos.Y + other.Height &&
               other is { IsWalkThrough: false, IsDead: false };
    }

    public virtual void TakeDamage(float damage)
    {
    }

    public virtual void Move(Vector2 direction, float dt, Room currentRoom)
    {
    }
}

public class Player : Entity
{
    public float Speed = 300f;
    public Vector2 LookingDirection = Vector2.Zero;
    public float Health = 100f;

    public Player(float x, float y, int width, int height)
        : base(EntityTypes.Player, new Vector2(x, y), width, height, Brushes.Blue)
    {
        IsOwnedByPlayer = true;
    }

    public override void Move(Vector2 direction, float dt, Room currentRoom)
    {
        if (IsDead) return;

        if (direction != Vector2.Zero)
        {
            direction = Vector2.Normalize(direction);

            Vector2 moveVector = direction * Speed * dt;

            Vector2 allowedMove = GameLogic.ValidMove(this, moveVector, currentRoom);
            Pos += allowedMove;
        }
    }

    public void Shoot(Room currentRoom)
    {
        Vector2 direction = LookingDirection - Pos;
        if (direction != Vector2.Zero) direction = Vector2.Normalize(direction);

        currentRoom.OtherEntities.Add(new Bullet(
            GameMath.PFtoV2(GameMath.AimLine(this, LookingDirection, Math.Max(this.Width, this.Height))), 10, 10,
            direction, true, Brushes.Yellow));
    }

    public override void TakeDamage(float damage)
    {
        Health -= damage;
        if (Health <= 0) IsDead = true;
    }
}

public class Enemy : Entity
{
    public float Speed = 200f;
    public float Health = 50f;

    public Enemy(float x, float y, int width, int height)
        : base(EntityTypes.Enemy, new Vector2(x, y), width, height, Brushes.Red)
    {
        IsOwnedByPlayer = false;
    }

    public override void Move(Vector2 direction, float dt, Room currentRoom)
    {
        if (IsDead) return;

        // Simple AI movement logic would go here
        if (direction != Vector2.Zero)
        {
            direction = Vector2.Normalize(direction);
            Vector2 moveVector = direction * Speed * dt;
            Pos += GameLogic.ValidMove(this, moveVector, currentRoom);
        }
    }

    public override void TakeDamage(float damage)
    {
        Health -= damage;
        if (Health <= 0) IsDead = true;
    }
}

public class Bullet : Entity
{
    public float Speed = 500f;
    public float Damage = 10f;
    public Vector2 MovingDirection;

    public Bullet(Vector2 pos, int width, int height, Vector2 movingDirection, bool isOwnedByPlayer, Brush color)
        : base(EntityTypes.Bullet, pos, width, height, color)
    {
        IsWalkThrough = true;
        MovingDirection = movingDirection;
        IsOwnedByPlayer = isOwnedByPlayer;
    }

    public void Move(float dt, Room currentRoom, List<Entity> nearbyEntities)
    {
        if (IsDead) return;

        Vector2 moveVector = MovingDirection * Speed * dt;

        if (GameLogic.IsBulletHitSomething(this, moveVector, currentRoom, nearbyEntities))
        {
            IsDead = true;
        }
        else
        {
            Pos += moveVector;
        }
    }
}