using System.Runtime.Serialization;

namespace ParseUserLog
{
    [DataContract]
    public sealed class Action
    {
        #region Data fields

        [DataMember] public string action = null;
        [DataMember] public string playerName = null;
        [DataMember] public int amount = 0;
        [DataMember] public int chips = 0;

        #endregion Data fields
    }
}
