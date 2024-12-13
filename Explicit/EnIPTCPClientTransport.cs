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
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace LibEthernetIPStack.Explicit;
public class EnIPTCPClientTransport
{
    private TcpClient Tcpclient;
    private int Timeout = 100;

    public EnIPTCPClientTransport(int Timeout) => this.Timeout = Timeout;

    public bool IsConnected() => Tcpclient != null && Tcpclient.Connected;

    private ManualResetEvent ConnectedEvAndLock = new(false);

    // Asynchronous connection is the best way to manage the timeout
    private void On_ConnectedACK(object sender, SocketAsyncEventArgs e) => ConnectedEvAndLock.Set();
    public bool Connect(IPEndPoint ep)
    {
        if (IsConnected()) return true;
        try
        {
            Tcpclient = new TcpClient
            {
                ReceiveTimeout = Timeout
            };

            SocketAsyncEventArgs AsynchEvent = new()
            {
                RemoteEndPoint = ep
            };
            AsynchEvent.Completed += new EventHandler<SocketAsyncEventArgs>(On_ConnectedACK);

            // Go
            _ = ConnectedEvAndLock.Reset();
            _ = Tcpclient.Client.ConnectAsync(AsynchEvent);
            bool ret = ConnectedEvAndLock.WaitOne(Timeout * 2);  // Wait transaction 2 * Timeout

            // In fact if the connection ACK-SYN is late, it will be OK after
            if (!ret)
                Trace.WriteLine("Connection fail to " + ep.ToString());

            return ret;
        }
        catch
        {
            Tcpclient = null;
            Trace.WriteLine("Connection fail to " + ep.ToString());
            return false;
        }
    }

    public void Disconnect()
    {
        Tcpclient?.Close();
        Tcpclient = null;
    }

    public int SendReceive(Encapsulation_Packet SendPkt, out Encapsulation_Packet? ReceivePkt, out int Offset, ref byte[] packet)
    {
        ReceivePkt = null;
        Offset = 0;

        int Lenght = 0;
        try
        {
            // We are not working on a continous flow but with query/response datagram
            // So if something is here it's a previous lost (timeout) response packet
            // Flush all content.
            while (Tcpclient.Available != 0)
                _ = Tcpclient.Client.Receive(packet);

            _ = Tcpclient.Client.Send(SendPkt.toByteArray());
            Lenght = Tcpclient.Client.Receive(packet);
            if (Lenght > 24)
                ReceivePkt = new Encapsulation_Packet(packet, ref Offset, Lenght);
            if (Lenght == 0)
                Trace.WriteLine("Reception timeout with " + Tcpclient.Client.RemoteEndPoint.ToString());
        }
        catch
        {
            Trace.WriteLine("Error in TcpClient Send Receive");
            Tcpclient = null;
        }

        return Lenght;
    }

    public void Send(Encapsulation_Packet SendPkt)
    {
        try
        {
            _ = Tcpclient.Client.Send(SendPkt.toByteArray());
        }
        catch
        {
            Trace.WriteLine("Error in TcpClient Send");
            Tcpclient = null;
        }
    }
}
