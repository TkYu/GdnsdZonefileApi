using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GdnsdZonefileApi
{
    public enum RecordType
    {
        //SOA, A, AAAA, NS, PTR, CNAME, MX, SRV, TXT, NAPTR, DYNA, DYNC
        DYNA, DYNC, A, AAAA, NS, CNAME, TXT
    }

    public class Record
    {
        public static bool TryParse(string content, out Record ret)
        {
            ret = new Record();
            try
            {
                var spl = content.Split(' ', '\t').Where(c => !string.IsNullOrWhiteSpace(c)).ToArray();
                if (spl.Length == 3)
                {
                    ret.HostLabel = spl[0];
                    ret.RecordType = Enum.Parse<RecordType>(spl[1]);
                    ret.RecordData = spl[2];
                    return true;
                }
                else if (spl.Length == 4)
                {
                    ret.HostLabel = spl[0];
                    if (spl[1].Contains('/'))
                    {
                        var splttl = spl[1].Split('/');
                        if (splttl.Length != 2) return false;
                        if (!int.TryParse(splttl[0], out var min) || !int.TryParse(splttl[0], out var max)) return false;
                        ret.TTL = $"{min}/{max}";
                    }
                    else
                    {
                        if (!int.TryParse(spl[1], out var ttl)) return false;
                        ret.TTL = ttl.ToString();
                    }
                    ret.RecordType = Enum.Parse<RecordType>(spl[2]);
                    ret.RecordData = spl[3];
                    return true;
                }
            }
            catch (Exception)
            {
                // ignored
            }
            return false;
        }

        public string HostLabel { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string TTL { get; set; }
        //public string RecordClass {get;set;} = "IN";//IN
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
        public RecordType RecordType { get; set; }
        public string RecordData { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public uint? Serial { get; set; }

        #region Overrides of Object

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{HostLabel}\t\t{(string.IsNullOrEmpty(TTL)?"":$"{TTL}\t\t")}{RecordType:G}\t\t{RecordData}";
        }

        #endregion
    }
}
