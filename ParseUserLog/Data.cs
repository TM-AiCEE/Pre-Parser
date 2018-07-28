using System.Linq;
using System.Runtime.Serialization;

namespace ParseUserLog
{
    [DataContract]
    public sealed class Data
    {
        #region Methods

        public bool Canonicalize()
        {
            table?.Canonicalize();
            return players?.All(player => player.Canonicalize()) == true;
        }

        public Player FindPlayer(string playerName) => players?.FirstOrDefault(player => player.playerName == playerName);

        public Winner FindWinner(string playerName) => winners?.FirstOrDefault(winner => winner.playerName == playerName);

        #endregion Methods


        #region Properties

        public Player CurrentPlayer => FindPlayer(action?.playerName);

        #endregion Properties


        #region Data fields

        [DataMember] public Player[] players = null;
        [DataMember] public Table table = null;
        [DataMember] public Action action = null;
        [DataMember] public Winner[] winners = null;

        #endregion Data fields
    }
}
