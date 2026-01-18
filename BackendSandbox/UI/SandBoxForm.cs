using System.Diagnostics;
using System.Numerics;

namespace BackendSandbox;

using System.Drawing;
using System.Windows.Forms;

class SandboxForm : Form
{
    public World.Room Room = new World.Room(1280, 720);
    
    private readonly Player _player;
    private readonly Enemy _enemy;
    private bool up, down, left, right;
    
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private TimeSpan lastTime;

    public SandboxForm()
    {
        DoubleBuffered = true;
        Width = 1280;
        Height = 720;

        _player = new Player(100, 100, 50, 50);
        
        _enemy = new Enemy(425, 319, 80, 80);

        _enemy.IsWalkThrough = false;
        
        Room.Entities.Add(_player);
        Room.Entities.Add(_enemy);
        

        MouseMove += (_, e) => _player.LookingDirection = new Vector2(e.X, e.Y);
        
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.W) up = true;
            if (e.KeyCode == Keys.S) down = true;
            if (e.KeyCode == Keys.A) left = true;
            if (e.KeyCode == Keys.D) right = true;
        };

        KeyUp += (_, e) =>
        {
            if (e.KeyCode == Keys.W) up = false;
            if (e.KeyCode == Keys.S) down = false;
            if (e.KeyCode == Keys.A) left = false;
            if (e.KeyCode == Keys.D) right = false;
        };


        var timer = new System.Windows.Forms.Timer { Interval = 16 };
        timer.Tick += (_, _) =>
        {
            var now = stopwatch.Elapsed;
            var dt = (float)(now - lastTime).TotalSeconds;
            lastTime = now;

            Update(dt);
            Invalidate();
        };
        timer.Start();
    }

    private void Update(float dt)
    {
        Vector2 direction = Vector2.Zero;

        if (up) direction.Y -= 1;
        if (down) direction.Y += 1;
        if (left) direction.X -= 1;
        if (right) direction.X += 1;

        _player.Move(direction, dt, Room);
    }

    
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.Black);
    
        var radius = Math.Max(_player.Width, _player.Height) + 20;
        
        var aimLineEnd = GameMath.AimLine(_player, _player.LookingDirection, radius);
        
        var brush = _player.IsCollide(_enemy)
            ? Brushes.Red
            : Brushes.Green;
        
        e.Graphics.FillRectangle(brush, _player.Bounds);
        e.Graphics.FillRectangle(Brushes.Blue, _enemy.Bounds);
        
        var playerCenter = GameMath.EntityCenter(_player);
        
        e.Graphics.DrawLine(Pens.Red, playerCenter.X, playerCenter.Y, aimLineEnd.X, aimLineEnd.Y);
        
    }
}