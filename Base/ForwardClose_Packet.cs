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
using LibEthernetIPStack.Shared;
using System;

namespace LibEthernetIPStack.Base;

public class ForwardClose_Packet
{
    private ForwardOpen_Packet OrignalPkt;

    public EnIPAttribut T2O { get; private set; }

    public ForwardClose_Packet(ForwardOpen_Packet FwOpen, EnIPAttribut T2O)
    {
        OrignalPkt = FwOpen;
        this.T2O = T2O;
    }
    // by now only use for request
    public byte[] toByteArray()
    {
        byte[] fwclose = new byte[12 + (OrignalPkt.Connection_Path_Size * 2)];
        fwclose[0] = OrignalPkt.Priority_TimeTick;
        fwclose[1] = OrignalPkt.Timeout_Ticks;
        Array.Copy(BitConverter.GetBytes(OrignalPkt.ConnectionSerialNumber), 0, fwclose, 2, 2);
        Array.Copy(BitConverter.GetBytes(ForwardOpen_Packet.OriginatorVendorId), 0, fwclose, 4, 2);
        Array.Copy(BitConverter.GetBytes(ForwardOpen_Packet.OriginatorSerialNumber), 0, fwclose, 6, 4);
        fwclose[10] = OrignalPkt.Connection_Path_Size;
        fwclose[11] = 0;
        Array.Copy(OrignalPkt.Connection_Path, 0, fwclose, 12, OrignalPkt.Connection_Path.Length);
        return fwclose;
    }
}
