/*********************************************************************
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
using LibEthernetIPStack.Shared;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LibEthernetIPStack.Implicit;

// Could be used for client & server implementation
// for port 0xAF12 as well as 0x8AE (server mode)
public class EnIPUDPTransport
{
    public event EncapMessageReceivedHandler EncapMessageReceived;
    public event ItemMessageReceivedHandler ItemMessageReceived;

    private UdpClient m_exclusive_conn;

    public EnIPUDPTransport(string Local_IP, int Port) : this(new IPEndPoint(!string.IsNullOrEmpty(Local_IP) ? IPAddress.Parse(Local_IP) : IPAddress.Any, Port)) { }
    public EnIPUDPTransport(EndPoint ep)
    {
        m_exclusive_conn = new UdpClient(AddressFamily.InterNetwork);
        m_exclusive_conn.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        m_exclusive_conn.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, false);
        m_exclusive_conn.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
        m_exclusive_conn.Client.Bind(ep);

        _ = m_exclusive_conn.BeginReceive(OnReceiveData, m_exclusive_conn);
    }

    public void JoinMulticastGroup(string IPMulti) => JoinMulticastGroup(IPAddress.Parse(IPMulti));
    public void JoinMulticastGroup(IPAddress IPMulti)
    {
        try
        {
            m_exclusive_conn.JoinMulticastGroup(IPMulti);
        }
        catch { }
    }

    private void OnReceiveData(IAsyncResult asyncResult)
    {
        UdpClient conn = (UdpClient)asyncResult.AsyncState;
        //try
        //{
            IPEndPoint ep = new(IPAddress.Any, 0);
            byte[] local_buffer;
            int rx = 0;

            try
            {
                local_buffer = conn.EndReceive(asyncResult, ref ep);
                rx = local_buffer.Length;
            }
            catch (Exception) // ICMP port unreachable
            {
                //restart data receive
                _ = conn.BeginReceive(OnReceiveData, conn);
                return;
            }

            if (rx < 14)    // Sure it's too small
            {
                //restart data receive
                _ = conn.BeginReceive(OnReceiveData, conn);
                return;
            }

            try
            {
                int Offset = 0;
                Encapsulation_Packet Encapacket = new(local_buffer, ref Offset, rx);
                //verify message
                if (Encapacket.IsOK)
                {
                    EncapMessageReceived?.Invoke(this, local_buffer, Encapacket, Offset, rx, ep);
                }
                else
                {
                    SequencedAddressItem Itempacket = new(local_buffer, ref Offset, rx);
                    if (Itempacket.IsOK && ItemMessageReceived != null)
                        ItemMessageReceived(this, local_buffer, Itempacket, Offset, rx, ep);
                }
            }
            //catch (Exception ex)
            //{
            //    Trace.TraceError("Exception in udp recieve: " + ex.Message);
            //}
            finally
            {
                //restart data receive
                _ = conn.BeginReceive(OnReceiveData, conn);
            }
        //}
        //catch (Exception ex)
        //{
        //    //restart data receive
        //    if (conn.Client != null)
        //    {
        //        Trace.TraceError("Exception in Ip OnRecieveData: " + ex.Message);
        //        _ = conn.BeginReceive(OnReceiveData, conn);
        //    }
        //}
    }

    public void Send(Encapsulation_Packet Packet, IPEndPoint ep)
    {
        byte[] b = Packet.toByteArray();
        _ = m_exclusive_conn.Send(b, b.Length, ep);
    }

    public void Send(SequencedAddressItem Packet, IPEndPoint ep)
    {
        byte[] b = Packet.toByteArray();
        _ = m_exclusive_conn.Send(b, b.Length, ep);
    }

    // A lot of problems on Mono (Raspberry) to get the correct broadcast @
    // so this method is overridable (this allows the implementation of operating system specific code)
    // Marc solution http://stackoverflow.com/questions/8119414/how-to-query-the-subnet-masks-using-mono-on-linux for instance
    //
    protected virtual IPEndPoint _GetBroadcastAddress()
    {
        // general broadcast
        IPEndPoint ep = new(IPAddress.Parse("255.255.255.255"), 0xAF12);
        // restricted local broadcast (directed ... routable)
        foreach (System.Net.NetworkInformation.NetworkInterface adapter in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            foreach (System.Net.NetworkInformation.UnicastIPAddressInformation ip in adapter.GetIPProperties().UnicastAddresses)
                if (LocalEndPoint.Address.Equals(ip.Address))
                {
                    try
                    {
                        string[] strCurrentIP = ip.Address.ToString().Split('.');
                        string[] strIPNetMask = ip.IPv4Mask.ToString().Split('.');
                        StringBuilder BroadcastStr = new();
                        for (int i = 0; i < 4; i++)
                        {
                            _ = BroadcastStr.Append(((byte)(int.Parse(strCurrentIP[i]) | ~int.Parse(strIPNetMask[i]))).ToString());
                            if (i != 3) _ = BroadcastStr.Append('.');
                        }
                        ep = new IPEndPoint(IPAddress.Parse(BroadcastStr.ToString()), 0xAF12);
                    }
                    catch { }  //On mono IPv4Mask feature not implemented
                }

        return ep;
    }

    private IPEndPoint? BroadcastAddress = null;
    public IPEndPoint GetBroadcastAddress()
    {
        BroadcastAddress ??= _GetBroadcastAddress();
        return BroadcastAddress;
    }

    // Give 0.0.0.0:xxxx if the socket is open with System.Net.IPAddress.Any
    // Some more complex solutions could avoid this, that's why this property is virtual
    public virtual IPEndPoint LocalEndPoint => (IPEndPoint)m_exclusive_conn.Client.LocalEndPoint;
}

