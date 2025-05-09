﻿/**************************************************************************
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
// Volume 1 : Table 3-5.16 Forward_Open
// class for both request and response
// A lot of ushort in the bullshit specification, but byte in fact
// .. Codesys 3.5 EIP scanner, help a lot
public class ForwardOpen_Packet
{
    public bool IsLargeForwardOpen { get; private set; } = false;

    // TimeOut (duration) in ms = 2^Priority_TimeTick * Timeout_Ticks
    // So with Priority_TimeTick=10, Timeout_Ticks is ~ the number of seconds
    // FIXME:
    // I don't understand the usage, with Wago Plc it's not a timeout for the
    // continuous udp flow.
    public byte Priority_TimeTick { get; private set; } = 10;
    public byte Timeout_Ticks { get; private set; } = 10;

    private static uint _connectionId;
    public uint O2T_ConnectionId { get; private set; }
    public uint T2O_ConnectionId { get; private set; }

    // shared 
    private static ushort _globalConnectionSerialNumber = (ushort)new Random().Next(65535);

    public ushort ConnectionSerialNumber { get; private set; }
    public static ushort OriginatorVendorId { get; private set; } = 0xFADA;
    public static uint OriginatorSerialNumber { get; private set; } = 0x8BADF00D;

    // 0 => *4
    public byte ConnectionTimeoutMultiplier { get; set; }
    public byte[] Reserved = new byte[3];
    // It's O2T_API for reply, in microseconde
    public uint O2T_RPI { get; set; } = 0;
    public uint O2T_ConnectionParameters { get; set; } // size OK for ForwardOpen & LargeForwardOpen
    // It's T2A_API for reply
    public uint T2O_RPI { get; set; } = 0;
    public uint T2O_ConnectionParameters { get; set; } // size OK for ForwardOpen & LargeForwardOpen
    // volume 1 : Figure 3-4.2 Transport Class Trigger Attribute
    public byte TransportTrigger { get; private set; } = 0x01; // Client class 1, cyclic;
    public byte Connection_Path_Size { get; private set; }
    public byte[] Connection_Path { get; private set; }

    // Only use for request up to now
    // O2T & T2O could be use at the same time
    // using a Connection_Path with more than 1 reference
    // 1 Path : path is for Consumption & Production
    // 2 Path : First path is for Consumption, second path is for Production.
    public ForwardOpen_Packet(byte[] connection_Path, ForwardOpen_Config conf, uint? connectionId = null)
    {

        ConnectionSerialNumber = _globalConnectionSerialNumber++;

        if (conf.O2T_datasize > 511 - 2 || conf.T2O_datasize > 511 - 6)
            IsLargeForwardOpen = true;

        Connection_Path = connection_Path;

        if (connectionId == null)
        {
            // volume 2 : 3-3.7.1.3 Pseudo-Random Connection ID Per Connection
            _connectionId += 2;
            _connectionId |= (uint)(new Random().Next(65535) << 16);
        }
        else
            _connectionId = connectionId.Value;

        if (conf.IsO2T)
            O2T_ConnectionId = _connectionId;
        if (conf.IsT2O)
            T2O_ConnectionId = _connectionId + 1;
        /*
        // Volume 1:  chapter 3-5.5.1.1
        T->O Network Connection Parameters: 0x463b
        0... .... .... .... = Owner: Exclusive (0)
        .10. .... .... .... = Connection Type: Point to Point (2)
        .... 01.. .... .... = Priority: High Priority (1)
        .... ..1. .... .... = Connection Size Type: Variable (1)
        .... ...0 0011 1011 = Connection Size: 59
        */
        if (conf.IsT2O)
        {
            T2O_ConnectionParameters = 0x0000; // Fixed Datasize, Variable data size is 0x0200
            T2O_ConnectionParameters = (uint)((T2O_ConnectionParameters + (conf.T2O_Priority & 0x03)) << 10);
            if (conf.T2O_P2P)
                T2O_ConnectionParameters |= 0x4000;
            else
                T2O_ConnectionParameters |= 0x2000;

            if (conf.O2T_Exculsive)
                T2O_ConnectionParameters |= 0x8000;

            if (IsLargeForwardOpen)
            {
                T2O_ConnectionParameters = (T2O_ConnectionParameters << 16) + conf.T2O_datasize + 2;
            }
            else
                T2O_ConnectionParameters += (ushort)(conf.T2O_datasize + 2);

            T2O_RPI = conf.T2O_RPI;
        }
        if (conf.IsO2T)
        {

            O2T_ConnectionParameters = 0x0000; // Fixed Datasize, Variable data size is 0x0200
            O2T_ConnectionParameters = (uint)((O2T_ConnectionParameters + (conf.O2T_Priority & 0x03)) << 10);
            if (conf.O2T_P2P)
                O2T_ConnectionParameters |= 0x4000;
            else
                O2T_ConnectionParameters |= 0x2000;

            if (conf.O2T_Exculsive)
                O2T_ConnectionParameters |= 0x8000;

            if (IsLargeForwardOpen)
            {
                O2T_ConnectionParameters = conf.O2T_datasize != 0 ? (O2T_ConnectionParameters << 16) + conf.O2T_datasize + 2 + 4 : (O2T_ConnectionParameters << 16) + 2;
            }
            else
            {
                if (conf.O2T_datasize != 0)
                    O2T_ConnectionParameters += (ushort)(conf.O2T_datasize + 2 + 4);
                else
                    O2T_ConnectionParameters += 2;
            }

            O2T_RPI = conf.O2T_RPI;
        }
    }

    public ForwardOpen_Packet(byte[] data)
    {
        int idx = 22;

        if (data.Length < 36)
            return;

        Priority_TimeTick = data[idx];
        idx++;
        Timeout_Ticks = data[idx];
        idx++;
        O2T_ConnectionId = BitConverter.ToUInt32(data, idx);
        idx += 4;
        T2O_ConnectionId = BitConverter.ToUInt32(data, idx);
        idx += 4;
        ConnectionSerialNumber = BitConverter.ToUInt16(data, idx);
        idx += 2;
        OriginatorVendorId = BitConverter.ToUInt16(data, idx);
        idx += 2;
        OriginatorSerialNumber = BitConverter.ToUInt32(data, idx);
        idx += 4;
        ConnectionTimeoutMultiplier = data[idx];
        idx++;
        Array.Copy(data, idx, Reserved, 0, 3);
        idx += 3;
        O2T_RPI = BitConverter.ToUInt32(data, idx);
        idx += 4;
        if (IsLargeForwardOpen)
        {
            O2T_ConnectionParameters = BitConverter.ToUInt32(data, idx);
            idx += 4;
        }
        else
        {
            O2T_ConnectionParameters = BitConverter.ToUInt16(data, idx);
            idx += 2;
        }
        T2O_RPI = BitConverter.ToUInt32(data, idx);
        idx += 4;
        if (IsLargeForwardOpen)
        {
            T2O_ConnectionParameters = BitConverter.ToUInt32(data, idx);
            idx += 4;
        }
        else
        {
            T2O_ConnectionParameters = BitConverter.ToUInt16(data, idx);
            idx += 2;
        }
        TransportTrigger = data[idx];
        idx++;
        Connection_Path_Size = data[idx];
        idx++;
        Connection_Path = new byte[Connection_Path_Size * 2];
        Array.Copy(data, idx, Connection_Path, 0, Connection_Path.Length);

    }
    public void SetTriggerType(TransportClassTriggerAttribute type) => TransportTrigger = (byte)((TransportTrigger & 0x8F) | (byte)type);

    // by now only use for request
    public byte[] toRequestByteArray()
    {
        int pathSize = (Connection_Path.Length / 2) + (Connection_Path.Length % 2);
        Connection_Path_Size = (byte)pathSize;
        int shift = 0; // ForwardOpen or LargeForwardOpen

        byte[] fwopen = IsLargeForwardOpen ? (new byte[36 + (pathSize * 2) + 4]) : (new byte[36 + (pathSize * 2)]);
        fwopen[0] = Priority_TimeTick;
        fwopen[1] = Timeout_Ticks;
        Array.Copy(BitConverter.GetBytes(O2T_ConnectionId), 0, fwopen, 2, 4);
        Array.Copy(BitConverter.GetBytes(T2O_ConnectionId), 0, fwopen, 6, 4);
        Array.Copy(BitConverter.GetBytes(ConnectionSerialNumber), 0, fwopen, 10, 2);
        Array.Copy(BitConverter.GetBytes(OriginatorVendorId), 0, fwopen, 12, 2);
        Array.Copy(BitConverter.GetBytes(OriginatorSerialNumber), 0, fwopen, 14, 4);
        fwopen[18] = ConnectionTimeoutMultiplier;
        Array.Copy(Reserved, 0, fwopen, 19, 3);
        Array.Copy(BitConverter.GetBytes(O2T_RPI), 0, fwopen, 22, 4);
        if (IsLargeForwardOpen)
        {
            Array.Copy(BitConverter.GetBytes(O2T_ConnectionParameters), 0, fwopen, 26, 4);
            shift = 2;
        }
        else
            Array.Copy(BitConverter.GetBytes((ushort)O2T_ConnectionParameters), 0, fwopen, 26, 2);

        Array.Copy(BitConverter.GetBytes(T2O_RPI), 0, fwopen, 28 + shift, 4);

        if (IsLargeForwardOpen)
        {
            Array.Copy(BitConverter.GetBytes(T2O_ConnectionParameters), 0, fwopen, 32 + shift, 4);
            shift = 4;
        }
        else
            Array.Copy(BitConverter.GetBytes((ushort)T2O_ConnectionParameters), 0, fwopen, 32 + shift, 2);

        fwopen[34 + shift] = TransportTrigger;
        fwopen[35 + shift] = Connection_Path_Size;
        Array.Copy(Connection_Path, 0, fwopen, 36 + shift, Connection_Path.Length);

        return fwopen;
    }

    public byte[] toReplyByteArray()
    {
        byte[] fwopen = new byte[26];

        Array.Copy(BitConverter.GetBytes(O2T_ConnectionId), 0, fwopen, 0, 4);
        Array.Copy(BitConverter.GetBytes(T2O_ConnectionId), 0, fwopen, 4, 4);
        Array.Copy(BitConverter.GetBytes(ConnectionSerialNumber), 0, fwopen, 8, 2);
        Array.Copy(BitConverter.GetBytes(OriginatorVendorId), 0, fwopen, 10, 2);
        Array.Copy(BitConverter.GetBytes(OriginatorSerialNumber), 0, fwopen, 12, 4);
        Array.Copy(BitConverter.GetBytes(O2T_RPI), 0, fwopen, 16, 4);
        Array.Copy(BitConverter.GetBytes(T2O_RPI), 0, fwopen, 20, 4);
        fwopen[24] = 0;
        fwopen[25] = 0;

        return fwopen;
    }

}
