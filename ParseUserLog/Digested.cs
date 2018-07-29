namespace ParseUserLog
{
    public sealed class Digested
    {
        #region Properties

        public string AllCards => cards + ";" + board;

        #endregion Properties


        #region Data fields

        public string cards;
        public string board;
        public string action;
        public double averageRank;
        public int count;

        #endregion Data fields
    }
}
