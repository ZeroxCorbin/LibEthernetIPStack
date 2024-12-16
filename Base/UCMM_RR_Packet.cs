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
using System.Diagnostics;

namespace LibEthernetIPStack.Base;

// Volume 1 : paragraph 2-4 Message Router Request/Response Formats
public class UCMM_RR_Packet
{
    // Partial Header
    public ushort ItemCount = 2;
    public CommonPacketItemIdNumbers IdemId = CommonPacketItemIdNumbers.UnConnectedDataItem;
    public ushort DataLength;

    // High bit 0 for query, 1 for response
    public byte Service;

    // Only for response packet
    public CIPGeneralSatusCode GeneralStatus;
    public byte AdditionalStatus_Size;
    public ushort[] AdditionalStatus;

    // Only for request packet
    public byte[] Path;
    public byte[] Data;

    public bool IsOK => GeneralStatus == CIPGeneralSatusCode.Success;

    public UCMM_RR_Packet(CIPServiceCodes Service, bool IsRequest, byte[] Path, byte[] Data, CIPGeneralSatusCode status = CIPGeneralSatusCode.Success)
    {
        this.Service = (byte)Service;
        if (!IsRequest)
            this.Service = (byte)(this.Service | 0x80);

        this.Path = Path;
        this.Data = Data;

        GeneralStatus = status;
    }

    public bool IsService(CIPServiceCodes service)
    {
        byte s = (byte)(this.Service & 0x7F);

        return s == (byte)service || this.Service > 0x80 && s == (byte)CIPServiceCodes.UnconnectedSend;
    }

    public bool IsResponse => Service > 0x80;
    public bool IsQuery => Service < 0x80;

    // up to now it's only a response paquet decoding
    public UCMM_RR_Packet(byte[] DataArray, ref int Offset, int Lenght)
    {
        if (Offset + 20 > Lenght)
            GeneralStatus = CIPGeneralSatusCode.Not_enough_data;

        // Skip 16 bytes of the Command specific data
        // Volume 2 : Table 3-2.1 UCMM Request & Table 3-2.2 UCMM Reply
        Offset += 16;

        Service = DataArray[Offset];
        Offset += 1;

        //Skip reserved byte
        Offset += 1;

        GeneralStatus = (CIPGeneralSatusCode)DataArray[Offset]; // only 0 is OK
        Offset += 1;

        AdditionalStatus_Size = DataArray[Offset];
        Offset += 1;

        if (Offset + (AdditionalStatus_Size * 2) > Lenght)
            GeneralStatus = CIPGeneralSatusCode.Not_enough_data;

        if (AdditionalStatus_Size > 0)
        {
            AdditionalStatus = new ushort[AdditionalStatus_Size];
            for (int i = 0; i < AdditionalStatus_Size; i++)
            {
                AdditionalStatus[i] = BitConverter.ToUInt16(DataArray, Offset);
                Offset += 2;
            }
        }
    }

    public UCMM_RR_Packet(byte[] DataArray, ref int Offset, int Lenght, bool IsRequest)
    {
        GeneralStatus = CIPGeneralSatusCode.Success;

        if (Offset + 20 > Lenght)
            GeneralStatus = CIPGeneralSatusCode.Not_enough_data;

        // Skip 16 bytes of the Command specific data
        // Volume 2 : Table 3-2.1 UCMM Request & Table 3-2.2 UCMM Reply
        Offset += 16;

        Service = DataArray[Offset];
        Offset += 1;

        int pathlen = DataArray[Offset];
        Offset += 1;

        if (Offset + pathlen * 2 > Lenght)
            GeneralStatus = CIPGeneralSatusCode.Not_enough_data;

        Path = new byte[pathlen * 2];
        Array.Copy(DataArray, Offset, Path, 0, pathlen * 2);
        Offset += pathlen * 2;


    }

    // up to now it's only a request paquet
    public byte[] toByteArray()
    {
        if (Path == null || Path.Length % 2 != 0)
        {
            Trace.TraceError("Request_Path is not OK");
            return null;
        }

        DataLength = (ushort)(2 + Path.Length + (Data == null ? 0 : Data.Length));

        // Volume 2 : Table 3-2.1 UCMM Request
        byte[] retVal = new byte[10 + 6 + DataLength];
        Array.Copy(BitConverter.GetBytes(ItemCount), 0, retVal, 6, 2);

        Array.Copy(BitConverter.GetBytes((ushort)IdemId), 0, retVal, 12, 2);

        Array.Copy(BitConverter.GetBytes(DataLength), 0, retVal, 14, 2);

        retVal[16] = Service;
        retVal[17] = (byte)(Path.Length >> 1);

        Array.Copy(Path, 0, retVal, 10 + 8, Path.Length);

        if (Data != null)
            Array.Copy(Data, 0, retVal, 10 + 8 + Path.Length, Data.Length);

        return retVal;
    }

    public byte[] toByteArray(bool isResponse)
    {
        if (Path == null || Path.Length % 2 != 0)
        {
            Trace.TraceError("Request_Path is not OK");
            return null;
        }

        DataLength = (ushort)(4 + (Data == null ? 0 : Data.Length));

        // Volume 2 : Table 3-2.1 UCMM Request
        byte[] retVal = new byte[10 + 6 + DataLength];
        Array.Copy(BitConverter.GetBytes(ItemCount), 0, retVal, 6, 2);

        Array.Copy(BitConverter.GetBytes((ushort)IdemId), 0, retVal, 12, 2);

        Array.Copy(BitConverter.GetBytes(DataLength), 0, retVal, 14, 2);

        retVal[16] = Service;
        retVal[17] = 0x00; //reservered
        retVal[18] = (byte)GeneralStatus;
        retVal[19] = 0;

        //retVal[17] = (byte)(Path.Length >> 1);

        //Array.Copy(Path, 0, retVal, 10 + 8, Path.Length);

        if (Data != null)
            Array.Copy(Data, 0, retVal, 20, Data.Length);

        return retVal;
    }

}
