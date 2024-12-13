/**************************************************************************
*                           MIT License
* 
* Copyright (C) 2016 Frederic Chaxel <fchaxel@free.fr>
*
* Permission is hereby granted, free of charge, to any person obtaining
* a copy of this software and associated documentation files (the
* "Software"), to deal in the Software without restriction, including
* without limitation the rights to use, copy, modify, merge, publish,
* distribute, sublicense, and/or sell copies of the Software, and to
* permit persons to whom the Software is furnished to do so, subject to
* the following conditions:
*
* The above copyright notice and this permission notice shall be included
* in all copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
* EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
* MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
* IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
* CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
* TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
* SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*
*********************************************************************/
using System;
using System.Net;

namespace LibEthernetIPStack.Base;
// Volume 2 : 2-6.3.3 Sockaddr Info Item
public class EnIPSocketAddress
{

    public short sin_family { get; set; }
    public ushort sin_port { get; set; }
    public uint sin_addr { get; set; }

    // Too small for IPV6 !
    //public byte[] sin_zero = new byte[8];

    public EnIPSocketAddress(IPEndPoint ep)
    {
        sin_family = (short)ep.AddressFamily;
        sin_port = (ushort)ep.Port;
        sin_addr = BitConverter.ToUInt32(ep.Address.GetAddressBytes(), 0);
    }
    public EnIPSocketAddress(byte[] DataArray, ref int Offset)
    {
        sin_family = (short)((DataArray[0 + Offset] << 8) + DataArray[1 + Offset]);
        sin_port = (ushort)((DataArray[2 + Offset] << 8) + DataArray[3 + Offset]);
        sin_addr = (uint)((DataArray[7 + Offset] << 24) + (DataArray[6 + Offset] << 16)
                        + (DataArray[5 + Offset] << 8) + DataArray[4 + Offset]);
        Offset += 16;
    }
    public EnIPSocketAddress() { }
    public IPEndPoint toIPEndpoint()
    {
        IPEndPoint ep = new(new IPAddress(sin_addr), sin_port);
        return ep;
    }

    public byte[] toByteArray()
    {
        byte[] retVal;

        retVal = new byte[16];

        retVal[0] = (byte)(sin_family >> 8);
        retVal[1] = (byte)(sin_family & 0xFF);
        retVal[2] = (byte)(sin_port >> 8);
        retVal[3] = (byte)(sin_port & 0xFF);

        retVal[4] = (byte)(sin_addr & 0xFF);
        retVal[5] = (byte)((sin_addr & 0xFF00) >> 8);
        retVal[6] = (byte)((sin_addr & 0xFF0000) >> 16);
        retVal[7] = (byte)((sin_addr & 0xFF000000) >> 24);

        return retVal;
    }
}