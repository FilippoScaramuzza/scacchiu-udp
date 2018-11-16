namespace Chess
{
    public class Cell
    {
        bool empty;
        string pawn;

        public Cell()
        {
            empty = true;
            pawn = "";
        }

        public Cell(string pawnName)
        {
            empty = false;
            pawn = pawnName;
        }
    }
}