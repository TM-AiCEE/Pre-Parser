namespace ParseUserLog
{
    public sealed class Stage
    {
        #region Properties

        public double AverageRank => rank / count;
        public string AllCards => cards + ";" + board;
        public string Order => AllCards + ";" + action;

        #endregion Properties


        #region Data fields

        public string cards;
        public string board;
        public string action;
        public double rank;
        public int count;

        #endregion Data fields
    }
}
