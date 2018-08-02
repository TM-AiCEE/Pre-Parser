using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace ParseUserLog
{
    using static Console;

    [DataContract]
    public sealed class Record
    {
        #region Methods

        public static Record Parse(string json, string path, int ln)
        {
            try
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return new DataContractJsonSerializer(typeof(Record)).ReadObject(stream) as Record;
                }
            }
            catch (Exception ex)
            {
                Error.WriteLine(ex.Message);
                Error.WriteLine($"Path: {path}");
                Error.WriteLine($"Line: {ln}");
                return null;
            }
        }

        #endregion Methods


        #region Data fields

        [DataMember] public string eventName = null;
        [DataMember] public Data data = null;

        #endregion Data fields
    }
}
