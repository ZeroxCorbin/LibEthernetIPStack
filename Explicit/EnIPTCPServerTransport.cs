﻿/*********************************************************************
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LibEthernetIPStack.Explicit;

public class EnIPTCPServerTransport
{
    public event EncapMessageReceivedHandler MessageReceived;
    private TcpListener tcpListener;

    private List<TcpClient> ClientsList = [];

    public bool IsListening { get; private set; } = false;
    public bool HasClients => ClientsList.Count > 0;

    public EnIPTCPServerTransport()
    {
        tcpListener = new TcpListener(IPAddress.Any, 0xAF12);
        Thread listenThread = new(ListenForClients)
        {
            IsBackground = true
        };
        listenThread.Start();
    }

    private void ListenForClients()
    {
        try
        {
            tcpListener.Start();
            IsListening = true;
            for (; ; )
            {
                // Blocking
                TcpClient client = tcpListener.AcceptTcpClient();
                Trace.WriteLine("Arrival of " + ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString());

                ClientsList.Add(client);

                //Thread 
                Thread clientThread = new(HandleClientComm)
                {
                    IsBackground = true
                };
                clientThread.Start(client);
            }
        }
        catch
        {
            IsListening = false;
            Trace.TraceError("Fatal Error in Tcp Listener Thread");
        }
    }

    public bool Send(byte[] packet, int size, IPEndPoint ep)
    {
        TcpClient tcpClient = ClientsList.Find((o) => ((IPEndPoint)o.Client.RemoteEndPoint).Equals(ep));

        if (tcpClient == null) return false;

        _ = tcpClient.Client.Send(packet, 0, size, SocketFlags.None);

        return true;
    }

    private void HandleClientComm(object client)
    {
        TcpClient tcpClient = (TcpClient)client;
        byte[] Rcp = new byte[1500];

        try
        {
            NetworkStream clientStream = tcpClient.GetStream();

            int Lenght = clientStream.Read(Rcp, 0, 1500);

            if (Lenght >= 24)
                try
                {
                    int Offset = 0;
                    Encapsulation_Packet Encapacket = new(Rcp, ref Offset, Lenght);
                   _ = Task.Run(() => MessageReceived?.Invoke(this, Rcp, Encapacket, Offset, Lenght, (IPEndPoint)tcpClient.Client.RemoteEndPoint));
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Exception in tcp recieve: " + ex.Message);
                }
            else
                Trace.TraceError("Too small packet received");
        }
        catch
        {
            // Client disconnected
            _ = ClientsList.Remove(tcpClient);
        }
    }
}
