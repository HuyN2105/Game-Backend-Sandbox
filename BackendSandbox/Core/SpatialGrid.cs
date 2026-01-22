using BackendSandbox.Models;

namespace BackendSandbox.Core;

public class SpatialGrid
{
    private const int CELL_SIZE = 64;
    private List<Entity>[,] _cells;
    private int _cols;
    private int _rows;

    public SpatialGrid(int pixelWidth, int pixelHeight)
    {
        _cols = (int)Math.Ceiling((float)pixelWidth / CELL_SIZE);
        _rows = (int)Math.Ceiling((float)pixelHeight / CELL_SIZE);

        _cells = new List<Entity>[_cols, _rows];

        for (int x = 0; x < _cols; x++)
            for (int y = 0; y < _rows; y++)
                _cells[x, y] = new List<Entity>();
    }

    public void Clear()
    {
        for (int x = 0; x < _cols; x++)
            for (int y = 0; y < _rows; y++)
                _cells[x, y].Clear();
    }

    public void Insert(Entity entity)
    {
        int startX = Math.Max(0, (int)(entity.Pos.X / CELL_SIZE));
        int startY = Math.Max(0, (int)(entity.Pos.Y / CELL_SIZE));
        int endX = Math.Min(_cols - 1, (int)((entity.Pos.X + entity.Width) / CELL_SIZE));
        int endY = Math.Min(_rows - 1, (int)((entity.Pos.Y + entity.Height) / CELL_SIZE));

        for (int x = startX; x <= endX; x++)
            for (int y = startY; y <= endY; y++)
                _cells[x, y].Add(entity);
    }

    public List<Entity> Retrieve(Entity entity)
    {
        var nearby = new List<Entity>();
        int startX = Math.Max(0, (int)(entity.Pos.X / CELL_SIZE));
        int startY = Math.Max(0, (int)(entity.Pos.Y / CELL_SIZE));
        int endX = Math.Min(_cols - 1, (int)((entity.Pos.X + entity.Width) / CELL_SIZE));
        int endY = Math.Min(_rows - 1, (int)((entity.Pos.Y + entity.Height) / CELL_SIZE));

        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                nearby.AddRange(_cells[x,y]);
            }
        }
        return nearby;
    }
}