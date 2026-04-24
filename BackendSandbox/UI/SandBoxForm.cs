using System.Diagnostics;
using System.Numerics;
using System.Drawing; // For Brushes, Graphics
using System.Windows.Forms;
using BackendSandbox.Core;
using BackendSandbox.Models;
using BackendSandbox.Utils;

namespace BackendSandbox.UI;

class SandboxForm : Form
{
    // 1. DECLARE fields here (Definition only)
    public Room Room;
    private readonly Player _player;
    private readonly Enemy _enemy;

    // Input states
    private bool _up, _down, _left, _right;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private TimeSpan _lastTime;

    // 2. INITIALIZE them inside the Constructor
    public SandboxForm()
    {
        DoubleBuffered = true;
        Width = 1280;
        Height = 720;

        // --- A. Load Room Logic ---
        Room = RoomLoader.InitialLoad();

        // The "Fallback" logic you wanted
        if (Room == null)
        {
            // If file missing, create default empty room
            Room = new Room(20, 12);
        }

        // --- B. Setup Entities ---
        _player = new Player(200, 200, 32, 32);

        // Add player to the room (This logic MUST be in the constructor)
        Room.Players.Add(_player);

        // Enemies are usually loaded from JSON, but we can add a test one here
        if (Room.Enemies.Count == 0)
        {
            _enemy = new Enemy(500, 300, 32, 32);
            Room.Enemies.Add(_enemy);
        }

        // --- C. Setup Input & Game Loop ---
        var localMouse = this.PointToClient(Cursor.Position);
        var lookingDirection = new Vector2(localMouse.X, localMouse.Y);

        MouseMove += (_, e) => lookingDirection = new Vector2(e.X, e.Y);

        MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) _player.Shoot(Room, lookingDirection);
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

        // Start Loop
        _lastTime = _stopwatch.Elapsed;
        var timer = new System.Windows.Forms.Timer { Interval = 16 };
        timer.Tick += (_, _) =>
        {
            var now = _stopwatch.Elapsed;
            var dt = (float)(now - _lastTime).TotalSeconds;
            _lastTime = now;

            if (dt > 0.1f) dt = 0.1f;
            UpdateGame(dt);
            Invalidate();
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

        Vector2 intendedMove = dir * _player.Speed * dt;
        Room? newRoom = GameLogic.TrySwitchRoom(_player, intendedMove, Room);

        if (newRoom != null)
        {
            // --- SWITCH ROOM LOGIC ---

            // A. Remove player from old room
            Room.Players.Remove(_player);

            // B. Swap the active room
            Room = newRoom;

            // C. Add player to new room
            Room.Players.Add(_player);

            // D. Skip normal movement this frame (to prevent glitches)
            return;
        }

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
                // Check bounds safety before drawing
                var tile = Room.GetTileAt(x, y);
                if (tile.IsSolid)
                {
                    e.Graphics.FillRectangle(Brushes.Gray, x * ts, y * ts, ts, ts);
                }
            }
        }

        // Draw Player
        if (!_player.IsDead)
            e.Graphics.FillRectangle(Brushes.Blue, _player.Bounds);

        // Draw Enemies
        foreach (var en in Room.Enemies)
            e.Graphics.FillRectangle(Brushes.Red, en.Bounds);

        // Draw Bullets
        foreach (var obj in Room.OtherEntities)
        {
            if (obj is Bullet)
                e.Graphics.FillEllipse(Brushes.Yellow, obj.Bounds);
        }

        string debugText = $"Current Room ID: {Room.RoomId}";

        // Create a simple font (Arial, Size 16)
        using (Font font = new Font("Arial", 16, FontStyle.Bold))
        {
            e.Graphics.DrawString(debugText, font, Brushes.White, 10, 10);
        }
    }
}