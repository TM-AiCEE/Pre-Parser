using System.Runtime.Serialization;

namespace ParseUserLog
{
    [DataContract]
    public sealed class Winner
    {
        #region Data fields

        [DataMember] public string playerName = null;
        [DataMember] public Hand hand = null;
        [DataMember] public int chips = 0;

        #endregion Data fields
    }
}
