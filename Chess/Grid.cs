using System;

namespace Chess
{
    public class Grid
    {
        Cell[,] cells = new Cell[8,8]; 

        public Grid()
        {

        }

        public Grid(Cell[,] cells)
        {
            this.cells = cells;
        }
    }
}
