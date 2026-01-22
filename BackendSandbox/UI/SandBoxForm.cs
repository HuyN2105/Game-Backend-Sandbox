using System.Diagnostics;
using System.Numerics;
using System.Drawing; // For Brushes, Graphics
using System.Windows.Forms;
using BackendSandbox.Core;
using BackendSandbox.Models; // Needs Room, Player

namespace BackendSandbox.UI;

class SandboxForm : Form
{
    // Create a 20x12 tile room (approx 1280x768)
    public Room Room = new Room(20, 12);

    private readonly Player _player;
    private readonly Enemy _enemy;
    private bool _up, _down, _left, _right;

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private TimeSpan _lastTime;

    public SandboxForm()
    {
        DoubleBuffered = true;
        Width = 1280;
        Height = 720;

        // Center player
        _player = new Player(200, 200, 32, 32);
        _enemy = new Enemy(500, 300, 32, 32);

        Room.Players.Add(_player);
        Room.Enemies.Add(_enemy);

        // --- MOUSE & INPUT ---
        var localMouse = this.PointToClient(Cursor.Position);
        _player.LookingDirection = new Vector2(localMouse.X, localMouse.Y);

        MouseMove += (_, e) => _player.LookingDirection = new Vector2(e.X, e.Y);

        MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) _player.Shoot(Room);
        };

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.W) _up = true;
            if (e.KeyCode == Keys.S) _down = true;
            if (e.KeyCode == Keys.A) _left = true;
            if (e.KeyCode == Keys.D) _right = true;
        };

        KeyUp += (_, e) =>
        {
            if (e.KeyCode == Keys.W) _up = false;
            if (e.KeyCode == Keys.S) _down = false;
            if (e.KeyCode == Keys.A) _left = false;
            if (e.KeyCode == Keys.D) _right = false;
        };

        // --- GAME LOOP ---
        _lastTime = _stopwatch.Elapsed;
        var timer = new System.Windows.Forms.Timer { Interval = 16 };
        timer.Tick += (_, _) =>
        {
            var now = _stopwatch.Elapsed;
            var dt = (float)(now - _lastTime).TotalSeconds;
            _lastTime = now;

            if (dt > 0.1f) dt = 0.1f; // Cap lag spikes
            UpdateGame(dt);
            Invalidate(); // Redraw
        };
        timer.Start();
    }

    private void UpdateGame(float dt)
    {
        Vector2 dir = Vector2.Zero;
        if (_up) dir.Y -= 1;
        if (_down) dir.Y += 1;
        if (_left) dir.X -= 1;
        if (_right) dir.X += 1;

        _player.Move(dir, dt, Room);
        Room.GameProgress(dt);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.Black);

        // Draw Walls
        int ts = Room.TileSize;
        for (int x = 0; x < Room.WidthInTiles; x++)
        {
            for (int y = 0; y < Room.HeightInTiles; y++)
            {
                if (Room.Tiles[x, y].IsSolid)
                {
                    e.Graphics.FillRectangle(Brushes.Gray, x * ts, y * ts, ts, ts);
                }
            }
        }

        var radius = Math.Max(_player.Width, _player.Height) + 20;
        
        // Draw Player
        if (!_player.IsDead)
            e.Graphics.FillRectangle(_player.EntityColor, _player.Bounds);

        // Draw Enemies
        foreach (var en in Room.Enemies)
        {
            e.Graphics.FillRectangle(en.EntityColor, en.Bounds);

            // Draw aim line for an enemy later will be the gun also for AI

            var enemyAimLineEnd = GameMath.AimLine(en, _player.Pos, radius);
            var enemyCenter = GameMath.EntityCenter(en);

            e.Graphics.DrawLine(Pens.Red, enemyCenter, enemyAimLineEnd);
        }
        // Draw aim line for player later will be the gun

        var playerAimLineEnd = GameMath.AimLine(_player, _player.LookingDirection, radius);
        var playerCenter = GameMath.EntityCenter(_player);

        e.Graphics.DrawLine(Pens.Red, playerCenter, playerAimLineEnd);

        // Draw Bullets
        foreach (var obj in Room.OtherEntities)
        {
            if (obj is Bullet)
                e.Graphics.FillEllipse(Brushes.Yellow, obj.Bounds);
        }
    }
}