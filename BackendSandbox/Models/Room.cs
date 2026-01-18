using System.Collections.Generic;
using System.Drawing;

namespace BackendSandbox;

public class World
{
    public class Room
    {
        public List<Entity> Entities{get;} = new();
        public int Width = 720;
        public int Height = 720;

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
    }
}