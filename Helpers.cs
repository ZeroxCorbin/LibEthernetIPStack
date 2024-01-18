using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LibEthernetIPStack
{
    public static class Helpers
    {
        public static IPAddress UInt32ToIPAddress(UInt32 address)
        {
            return new IPAddress(new byte[] {
                (byte)((address>>24) & 0xFF) ,
                (byte)((address>>16) & 0xFF) ,
                (byte)((address>>8)  & 0xFF) ,
                (byte)( address & 0xFF)});
        }
    }
}
