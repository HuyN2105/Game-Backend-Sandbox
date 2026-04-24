using System.Numerics;
using System.Drawing;
using BackendSandbox.Core;

namespace BackendSandbox.Models;

public class Entity
{
    public Guid Id { get; } = Guid.NewGuid();
    public EntityTypes EntityType;
    public Vector2 Pos;
    public int Width;
    public int Height;

    public bool IsOwnedByPlayer;
    public bool IsWalkThrough;
    public bool IsVisible = true;
    public bool IsDead;

    public RectangleF Bounds => new RectangleF(Pos.X, Pos.Y, Width, Height);

    public Entity(EntityTypes entityType, Vector2 pos, int width, int height)
    {
        EntityType = entityType;
        Pos = pos;
        Width = width;
        Height = height;
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
    public float Health = 100f;

    public Player(float x, float y, int width, int height)
        : base(EntityTypes.Player, new Vector2(x, y), width, height)
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

    // TODO: fix the shoot also every other stuff that can cause problems for frontend and websocket
    public void Shoot(Room currentRoom, Vector2 shootDirection = default)
    {
        Vector2 direction = shootDirection == default ? shootDirection - Pos : shootDirection;
        if (direction != Vector2.Zero) direction = Vector2.Normalize(direction);

        Vector2 spawnPos = GameMath.AimLine(this, shootDirection, Math.Max(this.Width, this.Height));

        currentRoom.OtherEntities.Add(new Bullet(spawnPos, 10, 10, direction, true));
    }

    // TODO: send back information to client
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
        : base(EntityTypes.Enemy, new Vector2(x, y), width, height)
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

    public void Shoot(Room currentRoom)
    {
        
    }

    public void UpdateMoveAI(float dt, Room currentRoom)
    {
        if (IsDead || currentRoom.Players.Count == 0) return;

        Player? closestPlayer = null;
        float minDistanceSquared = float.MaxValue;

        foreach (var player in currentRoom.Players)
        {
            if (player.IsDead) continue;

            float distanceSquared = Vector2.DistanceSquared(Pos, player.Pos);
            if (distanceSquared < minDistanceSquared)
            {
                minDistanceSquared = distanceSquared;
                closestPlayer = player;
            }
        }

        if (closestPlayer != null)
        {
            float stopDistance = 80f;
            float stopDistanceSquared = stopDistance * stopDistance;

            if (minDistanceSquared > stopDistanceSquared)
            {
                Vector2 direction = closestPlayer.Pos - Pos;
                Move(direction, dt, currentRoom);
            }
        }
    }

    // TODO: Done w this
    public void UpdateShootAI(float dt, Room currentRoom)
    {
        if (IsDead || currentRoom.Players.Count == 0) return;
        Player? closestPlayer = null;
        float minDistanceSquared = float.MaxValue;
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

    public Bullet(Vector2 pos, int width, int height, Vector2 movingDirection, bool isOwnedByPlayer)
        : base(EntityTypes.Bullet, pos, width, height)
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