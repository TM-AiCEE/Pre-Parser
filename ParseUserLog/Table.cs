using System;
using System.Runtime.Serialization;

namespace ParseUserLog
{
    using static String;

    [DataContract]
    public sealed class Table
    {
        #region Methods

        public void Canonicalize() => Board = Join(",", board.SortCards());

        public bool Same(Table other) => tableNumber == other.tableNumber && smallBlind.playerName == other.smallBlind.playerName && bigBlind.playerName == other.bigBlind.playerName;

        #endregion Methods


        #region Properties

        public string Board { get; private set; } = null;

        #endregion Properties


        #region Data fields

        [DataMember] public int tableNumber = 0;
        [DataMember] public int status = 0;
        [DataMember] public string roundName = null;
        [DataMember] public string[] board = null; // 0,3,4,5
        [DataMember] public int roundCount = 0;
        [DataMember] public int raiseCount = 0;
        [DataMember] public int betCount = 0;
        [DataMember] public int totalBet = 0;
        [DataMember] public int initChips = 0;
        [DataMember] public int maxReloadCount = 0;
        [DataMember] public Blind smallBlind = null;
        [DataMember] public Blind bigBlind = null;

        #endregion Data fields

    }
}
