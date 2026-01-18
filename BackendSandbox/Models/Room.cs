using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms.VisualStyles;

namespace BackendSandbox;

public class World
{
    public class Room
    {
        public List<Enemy> Enemies { get; } = new();
        public List<Player> Players { get; } = new();
        public List<Entity> OtherEntities { get; } = new();

        public int Width = 720;
        public int Height = 720;

        public TimeSpan DamageColorTime = TimeSpan.FromMilliseconds(200);
        
        public Room? Up = null;
        public Room? Down = null;
        public Room? Left = null;
        public Room? Right = null;
        public Color BgColor = Color.Black;

        public Room(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public void GameProgress(float dt)
        {
            // Iterate BACKWARDS so we can safely remove bullets
            for (int i = OtherEntities.Count - 1; i >= 0; i--)
            {
                var obj = OtherEntities[i];
                if (obj is Bullet bullet)
                {
                    bullet.Move(Vector2.Zero, dt, this);
                }
            }
    
            // Cleanup dead enemies here (moved from OnPaint)
            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                if (Enemies[i].IsDead)
                {
                    Enemies.RemoveAt(i);
                }
            }
        }
    }
}