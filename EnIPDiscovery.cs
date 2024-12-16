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
using LibEthernetIPStack.Base;
using LibEthernetIPStack.Implicit;
using LibEthernetIPStack.Shared;
using System;
using System.Diagnostics;
using System.Net;

namespace LibEthernetIPStack;

public delegate void DeviceArrivalHandler(EnIPProducerDevice device);


public class EnIPDiscovery
{
    public EnIPUDPTransport udp;
    private int TcpTimeout;

    public event DeviceArrivalHandler DeviceArrival;

    // Local endpoint is important for broadcast messages
    // When more than one interface are present, broadcast
    // requests are sent on the first one, not all !
    public EnIPDiscovery(string End_point, int TcpTimeout = 100)
    {
        this.TcpTimeout = TcpTimeout;
        udp = new EnIPUDPTransport(End_point, 0);
        udp.EncapMessageReceived += new EncapMessageReceivedHandler(on_MessageReceived);
    }

    private void on_MessageReceived(object sender, byte[] packet, Encapsulation_Packet EncapPacket, int offset, int msg_length, IPEndPoint remote_address)
    {
        // ListIdentity response
        if (EncapPacket.Command == EncapsulationCommands.ListIdentity && EncapPacket.Length != 0 && EncapPacket.IsOK)
        {
            if (DeviceArrival != null)
            {
                int NbDevices = BitConverter.ToUInt16(packet, offset);

                offset += 2;
                for (int i = 0; i < NbDevices; i++)
                {
                    EnIPProducerDevice device = new(remote_address, TcpTimeout, packet, EncapPacket, ref offset);
                    DeviceArrival(device);
                }
            }
        }
    }

    // Unicast ListIdentity
    public void DiscoverServers(IPEndPoint ep)
    {
        Encapsulation_Packet p = new(EncapsulationCommands.ListIdentity)
        {
            Command = EncapsulationCommands.ListIdentity
        };
        udp.Send(p, ep);
        Trace.WriteLine("Send ListIdentity to " + ep.Address.ToString());
    }
    // Broadcast ListIdentity
    public void DiscoverServers() => DiscoverServers(udp.GetBroadcastAddress());
}


