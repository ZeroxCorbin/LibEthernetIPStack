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
// This is here a SequencedAddress + a connected Data Item
// Receive via Udp after a ForwardOpen
// Volume 2 : 2-6 Common Packet Format
public class SequencedAddressItem
{
    // SequencedAddress
    public ushort TypeId;
    public ushort Lenght = 8;
    public uint ConnectionId;
    public uint SequenceNumber;
    // Connected or Unconnected Data Item
    public ushort TypeId2;
    public ushort Lenght2 = 8;
    public ushort SequenceCount; // ??

    public byte[] data;

    public SequencedAddressItem(uint ConnectionId = 0, uint SequenceNumber = 0, byte[]? data = null)
    {
        this.ConnectionId = ConnectionId;
        this.SequenceNumber = SequenceNumber;
        this.data = data;
    }

    public SequencedAddressItem(byte[] DataArray, ref int Offset, int Lenght)
    {
        // Itemcount=2, by now, could change maybe in this code !
        Offset += 2;
        TypeId = BitConverter.ToUInt16(DataArray, Offset);
        if (TypeId != (ushort)CommonPacketItemIdNumbers.SequencedAddressItem) return;
        Offset += 4;
        ConnectionId = BitConverter.ToUInt32(DataArray, Offset);
        Offset += 4;
        SequenceNumber = BitConverter.ToUInt32(DataArray, Offset);
        Offset += 4;

        TypeId2 = BitConverter.ToUInt16(DataArray, Offset);
        if (TypeId2 is not ((ushort)CommonPacketItemIdNumbers.ConnectedDataItem) and
            not ((ushort)CommonPacketItemIdNumbers.UnConnectedDataItem)) return;

        Offset += 2;
        Lenght2 = BitConverter.ToUInt16(DataArray, Offset);
        Offset += 2;

        if (Lenght2 + Offset != Lenght)
        {
            TypeId = 0; // invalidate the frame
            return;
        }

        if (Lenght2 != 0)
        {
            SequenceCount = BitConverter.ToUInt16(DataArray, Offset);
            Offset += 2;
        }
        // Offset is now at the beginning of the raw data
    }

    public byte[] toByteArray(byte[]? newdata = null)
    {
        byte[] retVal;

        if (newdata != null) data = newdata;

        if (data == null)
        {
            Lenght2 = 0;
            retVal = new byte[18];
        }
        else
        {
            Lenght2 = (ushort)(data.Length + 2 + 4); // +2 SequenceNumber bis (2 bytes !) +4 : 32 bits header
            retVal = new byte[18 + Lenght2];
        }

        // Itemcount=2
        retVal[0] = 2;
        Array.Copy(BitConverter.GetBytes((ushort)CommonPacketItemIdNumbers.SequencedAddressItem), 0, retVal, 2, 2);
        Array.Copy(BitConverter.GetBytes(Lenght), 0, retVal, 4, 2);
        Array.Copy(BitConverter.GetBytes(ConnectionId), 0, retVal, 6, 4);
        Array.Copy(BitConverter.GetBytes(SequenceNumber), 0, retVal, 10, 4);
        Array.Copy(BitConverter.GetBytes((ushort)CommonPacketItemIdNumbers.ConnectedDataItem), 0, retVal, 14, 2);

        Array.Copy(BitConverter.GetBytes(Lenght2), 0, retVal, 16, 2);

        if (Lenght2 != 0)
        {
            // Don't really understand this sequence count
            Array.Copy(BitConverter.GetBytes((ushort)SequenceNumber), 0, retVal, 18, 2);
            Array.Copy(BitConverter.GetBytes((uint)0x00000001), 0, retVal, 20, 4); // 32 bits header
            Array.Copy(data, 0, retVal, 24, data.Length);
        }

        SequenceNumber++;

        return retVal;
    }

    public bool IsOK => TypeId == 0x8002 && TypeId2 == 0x00b1;
}
