using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GdnsdZonefileApi
{
    public class Soa
    {
        /*
    +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
    /                     MNAME                     /
    /                                               /
    +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
    /                     RNAME                     /
    +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
    |                    SERIAL                     |
    |                                               |
    +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
    |                    REFRESH                    |
    |                                               |
    +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
    |                     RETRY                     |
    |                                               |
    +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
    |                    EXPIRE                     |
    |                                               |
    +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
    |                    MINIMUM                    |
    |                                               |
    +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
         */

        /// <summary>
        /// The domain-name of the name server that was the original or primary source of data for this zone.
        /// </summary>
        public string PrimaryNameServer { get; set; }

        /// <summary>
        /// A domain-name which specifies the mailbox of the person responsible for this zone.
        /// </summary>
        public string HostmasterEmail { get; set; }

        /// <summary>
        /// The unsigned 32 bit version number of the original copy of the zone.Zone transfers preserve this value.This value wraps and should be compared using sequence space arithmetic.
        /// </summary>
        public uint SerialNumber { get; set; }
        /// <summary>
        /// A 32 bit time interval before the zone should be refreshed.
        /// </summary>
        public string TimeToRefresh { get; set; }
        /// <summary>
        /// A 32 bit time interval that should elapse before a failed refresh should be retried.
        /// </summary>
        public string TimeToRetry { get; set; }
        /// <summary>
        /// A 32 bit time value that specifies the upper limit on the time interval that can elapse before the zone is no longer authoritative.
        /// </summary>
        public string TimeToExpire { get; set; }
        /// <summary>
        /// The unsigned 32 bit minimum TTL field that should be exported with any RR from this zone.
        /// </summary>
        public string MinimumTTL { get; set; }
    }
}
