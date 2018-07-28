using System.Runtime.Serialization;

namespace ParseUserLog
{
    [DataContract]
    public sealed class Blind
    {
        #region Data fields

        [DataMember] public string playerName = null;
        [DataMember] public int amount = 0;

        #endregion Data fields
    }
}
