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
using Newtonsoft.Json;
using System;

namespace LibEthernetIPStack.Base;
// Volume 2 : Table 2-3.1 Encapsulation Packet
// No explicit information to distinguish between a request and a reply
public class Encapsulation_Packet
{
    public EncapsulationCommands Command { get; set; }
    public ushort Length { get; set; }
    [JsonIgnore]
    public uint Sessionhandle { get; set; }
    //  Volume 2 : Table 2-3.3 Error Codes - 0x0000 Success, others value error
    public EncapsulationStatus Status { get; } = EncapsulationStatus.Invalid_Session_Handle;
    // byte copy of the request into the response
    public byte[] SenderContext { get; set; } = new byte[8];
    public uint Options { get; set; }
    // Not used in the EncapsulationPacket receive objects
    public byte[]? Encapsulateddata { get; set; } = null;

    public bool IsOK => Status == EncapsulationStatus.Success;

    public Encapsulation_Packet() { }
    public Encapsulation_Packet(EncapsulationCommands command, uint sessionHandle = 0, byte[] encapsulatedData = null, EncapsulationStatus status = EncapsulationStatus.Success)
    {
        Command = command;
        Sessionhandle = sessionHandle;
        Encapsulateddata = encapsulatedData;
        Length = encapsulatedData != null ? (ushort)encapsulatedData.Length : (ushort)0;

        Status = status;
    }

    // From network
    public Encapsulation_Packet(byte[] packet, ref int offset, int length)
    {
        ushort Cmd = BitConverter.ToUInt16(packet, offset);

        if (!Enum.IsDefined(typeof(EncapsulationCommands), Cmd))
        {
            Status = EncapsulationStatus.Unsupported_Command;
            return;
        }

        Command = (EncapsulationCommands)Cmd;
        offset += 2;
        this.Length = BitConverter.ToUInt16(packet, offset);

        if (length < 24 + this.Length)
        {
            Status = EncapsulationStatus.Invalid_Length;
            return;
        }

        offset += 2;
        Sessionhandle = BitConverter.ToUInt32(packet, offset);
        offset += 4;
        Status = (EncapsulationStatus)BitConverter.ToUInt32(packet, offset);
        offset += 4;
        Array.Copy(packet, offset, SenderContext, 0, 8);
        offset += 8;
        Options = BitConverter.ToUInt32(packet, offset);
        offset += 4;  // value 24
        if(this.Length > 0)
        {
            Encapsulateddata = new byte[this.Length];
            Array.Copy(packet, offset, Encapsulateddata, 0, this.Length);
        }

    }

    public byte[] toByteArray()
    {
        byte[] ret = new byte[24 + Length];

        Array.Copy(BitConverter.GetBytes((ushort)Command), 0, ret, 0, 2);
        Array.Copy(BitConverter.GetBytes(Length), 0, ret, 2, 2);
        Array.Copy(BitConverter.GetBytes(Sessionhandle), 0, ret, 4, 4);
        Array.Copy(BitConverter.GetBytes((uint)Status), 0, ret, 8, 4);
        Array.Copy(SenderContext, 0, ret, 12, 8);
        Buffer.BlockCopy(BitConverter.GetBytes(Options), 0, ret, 20, 4);
        if (Encapsulateddata != null)
            Array.Copy(Encapsulateddata, 0, ret, 24, Length);
        return ret;
    }
}
