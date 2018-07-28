using System.Runtime.Serialization;

namespace ParseUserLog
{
    [DataContract]
    public sealed class Hand
    {
        #region Data fields

        [DataMember] public string[] cards = null;
        [DataMember] public double rank = 0.0;
        [DataMember] public string message = null;

        #endregion Data fields
    }
}
