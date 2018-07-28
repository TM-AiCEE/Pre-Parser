using System;
using System.Runtime.Serialization;

namespace ParseUserLog
{
    using static String;

    [DataContract]
    public sealed class Player
    {
        #region Methods

        public bool Canonicalize()
        {
            Cards = Join(",", cards.SortCards());
            return Cards.Length == 5; // expecting "XX,YY"
        }

        #endregion Methods


        #region Properties

        public string Cards { get; private set; } = null;

        #endregion Properties


        #region Data fields

        [DataMember] public string playerName = null;
        [DataMember] public int chips = 0;
        [DataMember] public bool folded = false;
        [DataMember] public bool allIn = false;
        [DataMember] public string[] cards = null;
        [DataMember] public bool isSurvive = false;
        [DataMember] public int reloadCount = 0;
        [DataMember] public int roundBet = 0;
        [DataMember] public int bet = 0;
        [DataMember] public bool isOnline = false;
        [DataMember] public bool isHuman = false;

        #endregion Data fields
    }
}
