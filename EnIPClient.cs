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
using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using Newtonsoft.Json;
using LibEthernetIPStack.ObjectsLibrary;
using System.Linq;
using Newtonsoft.Json.Converters;
using System.Collections.ObjectModel;
using System.Data;
using NLog;
using System.Threading.Tasks;

namespace LibEthernetIPStack
{
    public delegate void DeviceArrivalHandler(EnIPRemoteDevice device);

    [JsonConverter(typeof(StringEnumConverter))]
    public enum EnIPNetworkStatus { OnLine, OnLineReadRejected, OnLineWriteRejected, OnLineForwardOpenReject, OffLine };
    public enum EnIPForwardOpenStatus { ForwardOpen, ForwardClose };

    public class EnIPClient
    {
        public EnIPUDPTransport udp;
        int TcpTimeout;

        public event DeviceArrivalHandler DeviceArrival;

        // Local endpoint is important for broadcast messages
        // When more than one interface are present, broadcast
        // requests are sent on the first one, not all !
        public EnIPClient(string End_point, int TcpTimeout = 100)
        {
            this.TcpTimeout = TcpTimeout;
            udp = new EnIPUDPTransport(End_point, 0);
            udp.EncapMessageReceived += new EncapMessageReceivedHandler(on_MessageReceived);
        }

        void on_MessageReceived(object sender, byte[] packet, Encapsulation_Packet EncapPacket, int offset, int msg_length, IPEndPoint remote_address)
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
                        EnIPRemoteDevice device = new EnIPRemoteDevice(remote_address, TcpTimeout, packet, EncapPacket, ref offset);
                        DeviceArrival(device);
                    }
                }
            }
        }

        // Unicast ListIdentity
        public void DiscoverServers(IPEndPoint ep)
        {
            Encapsulation_Packet p = new Encapsulation_Packet(EncapsulationCommands.ListIdentity);
            p.Command = EncapsulationCommands.ListIdentity;
            udp.Send(p, ep);
            Trace.WriteLine("Send ListIdentity to " + ep.Address.ToString());
        }
        // Broadcast ListIdentity
        public void DiscoverServers()
        {
            DiscoverServers(udp.GetBroadcastAddress());
        }
    }

    public class EnIPRemoteDevice
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public delegate void NetworkStatusUpdateDel(EnIPNetworkStatus status, string msg);
        public event NetworkStatusUpdateDel NetworkStatusUpdate;

        public delegate void ForwardOpenStatusUpdateDel(EnIPForwardOpenStatus status);
        public event ForwardOpenStatusUpdateDel ForwardOpenStatusUpdate;

        public string ProductName { get; set; }
        public uint SerialNumber { get; set; }
        public IdentityObjectState State { get; set; }
        public short Status { get; set; }
        public string IPAddress => new IPAddress(SocketAddress.sin_addr).ToString();
        public ushort VendorId { get; set; }
        public ushort DeviceType { get; set; }
        public ushort ProductCode { get; set; }
        public List<byte> Revision { get; set; } = new List<byte>();
        public EnIPSocketAddress SocketAddress
        {
            get => socketAddress;
            set
            {
                socketAddress = value;
                if(this.ep == null || this.ep.Address.ToString() != new IPAddress(value.sin_addr).ToString())
                {
                    this.ep = new IPEndPoint(System.Net.IPAddress.Parse(IPAddress), value.sin_port);
                    epUdp = new IPEndPoint(ep.Address, 2222);
                }
            }
        }
        private EnIPSocketAddress socketAddress;
        [JsonIgnore]
        // Data comming from the reply to ListIdentity query
        // get set are used by the property grid in EnIPExplorer
        public ushort DataLength;
        [JsonIgnore]
        public ushort EncapsulationVersion { get; set; }

        public Encapsulation_Packet IdentityEncapPacket { get; set; }

        private IPEndPoint ep { get;  set; } // The Tcp endpoint
        private IPEndPoint epUdp { get; set; } // The Udp endpoint : same IP, port 2222
        // Not a property to avoid browsable in propertyGrid, also [Browsable(false)] could be used
        public IPAddress IPAdd() { return ep.Address; }

        [JsonIgnore]
        public bool autoConnect = true;
        [JsonIgnore]
        public bool autoRegisterSession = true;

        private uint SessionHandle = 0; // When Register Session is set

        private EnIPTCPClientTransport Tcpclient;
        private static EnIPUDPTransport UdpListener;

        private object LockTransaction = new object();

        // A global packet for response frames
        private byte[] packet = new byte[1500];

        public ObservableCollection<EnIPClass> SupportedClassLists { get; private set; } = new ObservableCollection<EnIPClass>();

        public event DeviceArrivalHandler DeviceArrival;

        private void FromListIdentityResponse(byte[] DataArray, ref int Offset)
        {
            Offset += 2; // 0x000C 

            DataLength = BitConverter.ToUInt16(DataArray, Offset);
            Offset += 2;

            EncapsulationVersion = BitConverter.ToUInt16(DataArray, Offset);
            Offset += 2;

            // Maybe it should be used in place of the ep
            // if a host embbed more than one device, sure it sends different tcp/udp port ?
            // FIXME if you know.
            SocketAddress = new EnIPSocketAddress(DataArray, ref Offset);

            VendorId = BitConverter.ToUInt16(DataArray, Offset);
            Offset += 2;

            DeviceType = BitConverter.ToUInt16(DataArray, Offset);
            Offset += 2;

            ProductCode = BitConverter.ToUInt16(DataArray, Offset);
            Offset += 2;

            Revision.Add(DataArray[Offset]);
            Offset++;

            Revision.Add(DataArray[Offset]);
            Offset++;

            Status = BitConverter.ToInt16(DataArray, Offset);
            Offset += 2;

            SerialNumber = BitConverter.ToUInt32(DataArray, Offset);
            Offset += 4;

            int strSize = DataArray[Offset];
            Offset += 1;

            ProductName = Encoding.ASCII.GetString(DataArray, Offset, strSize);
            Offset += strSize;

            State = (IdentityObjectState)DataArray[Offset];

            Offset += 1;
        }
        // The remote udp endpoint is given here, it's also the tcp one
        // This constuctor is used with the ListIdentity response buffer
        // No local endpoint given here, the TCP/IP stack should do the job
        // if more than one interface is present
        public EnIPRemoteDevice(IPEndPoint ep, int TcpTimeout, byte[] DataArray, Encapsulation_Packet encapsulation, ref int Offset)
        {
            this.ep = ep;
            IdentityEncapPacket = encapsulation;
            epUdp = new IPEndPoint(ep.Address, 2222);
            Tcpclient = new EnIPTCPClientTransport(TcpTimeout);
            FromListIdentityResponse(DataArray, ref Offset);
        }

        public EnIPRemoteDevice()
        {
            Tcpclient = new EnIPTCPClientTransport(100);
        }

        public void Dispose()
        {
            if (IsConnected())
                Disconnect();
        }

        public void Class1Activate(IPEndPoint ep)
        {
            if (UdpListener == null)
                UdpListener = new EnIPUDPTransport(ep.Address.ToString(), ep.Port);
        }

        public void Class1AddMulticast(string IP)
        {
            if (UdpListener != null)
                UdpListener.JoinMulticastGroup(IP);
        }

        public void Class1AttributEnrolment(EnIPAttribut att)
        {
            if (UdpListener != null)
                UdpListener.ItemMessageReceived += new ItemMessageReceivedHandler(att.On_ItemMessageReceived);
        }

        public void Class1AttributUnEnrolment(EnIPAttribut att)
        {
            if (UdpListener != null)
                UdpListener.ItemMessageReceived -= new ItemMessageReceivedHandler(att.On_ItemMessageReceived);
        }

        public void Class1SendO2T(SequencedAddressItem Item)
        {
            if (UdpListener != null)
                UdpListener.Send(Item, epUdp);
        }

        public void CopyData(EnIPRemoteDevice newset)
        {
            DataLength = newset.DataLength;
            EncapsulationVersion = newset.EncapsulationVersion;
            SocketAddress = newset.SocketAddress;
            VendorId = newset.VendorId;
            DeviceType = newset.DeviceType;
            ProductCode = newset.ProductCode;
            Revision = newset.Revision;
            Status = newset.Status;
            SerialNumber = newset.SerialNumber;
            ProductName = newset.ProductName;
            State = newset.State;
        }

        // Certainly here if SocketAddress is fullfil it could be the
        // value to test
        // FIXME if you know.
        public bool Equals(EnIPRemoteDevice other)
        {
            return ep.Equals(other.ep);
        }

        public bool IsConnected()
        {
            return Tcpclient.IsConnected();
        }

        public bool Connect()
        {
            if (Tcpclient.IsConnected() == true) return true;

            SessionHandle = 0;

            lock (LockTransaction)
                return Tcpclient.Connect(ep);
        }

        public void Disconnect()
        {
            SessionHandle = 0;

            lock (LockTransaction)
                Tcpclient.Disconnect();
        }

        // Unicast TCP ListIdentity for remote device, not UDP it's my choice because in such way 
        // firewall could be configured only for TCP port (TCP is required for the others exchanges)
        public bool DiscoverServer()
        {
            if (autoConnect) Connect();

            try
            {
                if (Tcpclient.IsConnected())
                {
                    Encapsulation_Packet p = new Encapsulation_Packet(EncapsulationCommands.ListIdentity);
                    p.Command = EncapsulationCommands.ListIdentity;

                    int Length;
                    int Offset = 0;
                    Encapsulation_Packet Encapacket;

                    lock (LockTransaction)
                        Length = Tcpclient.SendReceive(p, out Encapacket, out Offset, ref packet);

                    Trace.WriteLine("Send ListIdentity to " + ep.Address.ToString());

                    if (Length < 26) return false; // never appears in a normal situation

                    if (Encapacket.Command == EncapsulationCommands.ListIdentity && Encapacket.Length != 0 && Encapacket.IsOK)
                    {
                        Offset += 2;
                        FromListIdentityResponse(packet, ref Offset);
                        if (DeviceArrival != null)
                            DeviceArrival(this);
                        return true;
                    }
                    else
                        Trace.WriteLine("Unicast TCP ListIdentity fail");
                }
            }
            catch
            {
                Trace.WriteLine("Unicast TCP ListIdentity fail");
            }

            return false;
        }

        // Needed for a lot of operations
        private void RegisterSession()
        {
            if (autoConnect) Connect();

            if (Tcpclient.IsConnected() == true && SessionHandle == 0)
            {
                byte[] b = new byte[] { 1, 0, 0, 0 };
                Encapsulation_Packet p = new Encapsulation_Packet(EncapsulationCommands.RegisterSession, 0, b);

                int ret;
                Encapsulation_Packet rep;
                int Offset = 0;

                lock (LockTransaction)
                    ret = Tcpclient.SendReceive(p, out rep, out Offset, ref packet);

                if (ret == 28)
                    if (rep.IsOK)
                        SessionHandle = rep.Sessionhandle;
            }
        }

        public EnIPNetworkStatus SendUCMM_RR_Packet(byte[] DataPath, CIPServiceCodes Service, byte[]? data, ref int Offset, ref int Lenght, out byte[] packet)
        {
            packet = this.packet;

            if (autoRegisterSession) RegisterSession();
            if (SessionHandle == 0) return EnIPNetworkStatus.OffLine;

            try
            {
                UCMM_RR_Packet m = new UCMM_RR_Packet(Service, true, DataPath, data);
                Encapsulation_Packet p = new Encapsulation_Packet(EncapsulationCommands.SendRRData, SessionHandle, m.toByteArray());

                Encapsulation_Packet rep;
                Offset = 0;

                lock (LockTransaction)
                    Lenght = Tcpclient.SendReceive(p, out rep, out Offset, ref packet);

                string ErrorMsg = "TCP Error";

                if (Lenght > 24)
                {
                    if (rep.IsOK && rep.Command == EncapsulationCommands.SendRRData)
                    {
                        m = new UCMM_RR_Packet(packet, ref Offset, Lenght);
                        if (m.IsOK && m.IsService(Service))
                        {
                            // all is OK, and Offset is ready set at the beginning of data[]
                            return UpdateStatus(EnIPNetworkStatus.OnLine);
                        }
                        else
                            ErrorMsg = m.GeneralStatus.ToString();
                    }
                    else
                        ErrorMsg = rep.Status.ToString();
                }

                var o = Service.ToString() + " : " + ErrorMsg + " - Node " + EnIPPath.GetPath(DataPath) + " - Endpoint " + ep.ToString();

                if (ErrorMsg == "TCP Error")
                    return UpdateStatus(EnIPNetworkStatus.OffLine, o);

                if (Service == CIPServiceCodes.SetAttributeSingle)
                    return UpdateStatus(EnIPNetworkStatus.OnLineWriteRejected, o);
                else
                    return UpdateStatus(EnIPNetworkStatus.OnLineReadRejected, o);
            }
            catch(Exception ex)
            {
                UpdateStatus(EnIPNetworkStatus.OffLine, "Error while sending request to endpoint. " + ep.ToString());
                return UpdateStatus(EnIPNetworkStatus.OffLine, ex);
            }
        }

        public EnIPNetworkStatus SetClassInstanceAttribut_Data(byte[] DataPath, CIPServiceCodes Service, byte[] data, ref int Offset, ref int Lenght, out byte[] packet)
        {
            return SendUCMM_RR_Packet(DataPath, Service, data, ref Offset, ref Lenght, out packet);
        }
        public EnIPNetworkStatus GetClassInstanceAttribut_Data(byte[] ClassDataPath, CIPServiceCodes Service, ref int Offset, ref int Lenght, out byte[] packet)
        {
            return SendUCMM_RR_Packet(ClassDataPath, Service, null, ref Offset, ref Lenght, out packet);
        }

        public List<EnIPClass> GetObjectList()
        {
            SupportedClassLists.Clear();

            if (autoRegisterSession) RegisterSession();
            if (SessionHandle == 0) return null;

            // Class 2, Instance 1, Attribut 1
            byte[] MessageRouterObjectList = EnIPPath.GetPath("2.1.1");

            int Lenght = 0;
            int Offset = 0;

            if (GetClassInstanceAttribut_Data(MessageRouterObjectList, CIPServiceCodes.GetAttributeSingle, ref Offset, ref Lenght, out packet) == EnIPNetworkStatus.OnLine)
            {
                ushort NbClasses = BitConverter.ToUInt16(packet, Offset);
                Offset += 2;
                for (int i = 0; i < NbClasses; i++)
                {
                    SupportedClassLists.Add(new EnIPClass(this, BitConverter.ToUInt16(packet, Offset)));
                    Offset += 2;
                }
            }

            if (SupportedClassLists.Count == 0) // service not supported : add basic class, but some could be not present
            {
                SupportedClassLists.Add(new EnIPClass(this, (ushort)CIPObjectLibrary.Identity));
                SupportedClassLists.Add(new EnIPClass(this, (ushort)CIPObjectLibrary.MessageRouter));
                SupportedClassLists.Add(new EnIPClass(this, (ushort)CIPObjectLibrary.Assembly));
                SupportedClassLists.Add(new EnIPClass(this, (ushort)CIPObjectLibrary.TCPIPInterface));
                SupportedClassLists.Add(new EnIPClass(this, (ushort)CIPObjectLibrary.EtherNetLink));
                SupportedClassLists.Add(new EnIPClass(this, (ushort)CIPObjectLibrary.ConnectionManager));
            }

            return SupportedClassLists.ToList();
        }

        public void UnRegisterSession()
        {
            if (SessionHandle != 0)
            {
                Encapsulation_Packet p = new Encapsulation_Packet(EncapsulationCommands.RegisterSession, SessionHandle);

                lock (LockTransaction)
                    Tcpclient.Send(p);

                SessionHandle = 0;
            }
        }

        // Gives a compressed Path with a list of Attributs
        private byte[] GetForwardOpenPath(EnIPAttribut Config, EnIPAttribut O2T, EnIPAttribut T2O)
        {
            byte[] ConstructDataPath = new byte[20];
            int offset = 0;

            ushort? Cid, Aid;

            EnIPAttribut previousAtt = null;

            EnIPAttribut[] Atts = new EnIPAttribut[] { Config, O2T, T2O };

            foreach (EnIPAttribut att in Atts)
            {
                if (att != null)
                {
                    byte[] DataPath;

                    Cid = att.myInstance.myClass.Id;
                    if (previousAtt != null && Cid == previousAtt.myInstance.myClass.Id)
                        Cid = null;

                    Aid = att.Id == 3 && att.myInstance.myClass.Id == 4 ? null : att.Id;

                    DataPath = EnIPPath.GetPath(Cid, att.myInstance.Id, Aid, att != Config);
                    Array.Copy(DataPath, 0, ConstructDataPath, offset, DataPath.Length);
                    offset += DataPath.Length;

                    previousAtt = att;
                }
            }

            byte[] FinalPath = new byte[offset];
            Array.Copy(ConstructDataPath, 0, FinalPath, 0, offset);
            return FinalPath;

            // return something like  0x20, 0x04, 0x24, 0x80, 0x2C, 0x66, 0x2C, 0x65
        }

        public EnIPNetworkStatus ForwardOpen(EnIPAttribut Config, EnIPAttribut O2T, EnIPAttribut T2O, out ForwardClose_Packet ClosePacket, uint CycleTime, bool P2P = false, bool WriteConfig = false)
        {
            ForwardOpen_Config conf = new ForwardOpen_Config(O2T, T2O, P2P, CycleTime);
            return ForwardOpen(Config, O2T, T2O, out ClosePacket, conf, WriteConfig);
        }

        public EnIPNetworkStatus ForwardOpen(EnIPAttribut Config, EnIPAttribut O2T, EnIPAttribut T2O, out ForwardClose_Packet? ClosePacket, ForwardOpen_Config conf, bool WriteConfig = false)
        {
            ClosePacket = null;

            byte[] DataPath = GetForwardOpenPath(Config, O2T, T2O);

            if (WriteConfig == true && Config != null) // Add data segment
            {

                DataPath = EnIPPath.AddDataSegment(DataPath, Config.RawData);
                /*
                byte[] FinaleFrame = new byte[DataPath.Length + 2 + Config.RawData.Length];
                Array.Copy(DataPath, FinaleFrame, DataPath.Length);
                FinaleFrame[DataPath.Length] = 0x80;
                FinaleFrame[DataPath.Length + 1] = (byte)(Config.RawData.Length / 2); // Certainly the lenght is always even !!!
                Array.Copy(Config.RawData, 0, FinaleFrame, DataPath.Length + 2, Config.RawData.Length);
                DataPath = FinaleFrame;
                */
            }

            ForwardOpen_Packet FwPkt = new ForwardOpen_Packet(DataPath, conf);

            int Offset = 0;
            int Lenght = 0;
            byte[] packet;

            EnIPNetworkStatus Status;
            if (FwPkt.IsLargeForwardOpen)
                Status = SendUCMM_RR_Packet(EnIPPath.GetPath(6, 1), CIPServiceCodes.LargeForwardOpen, FwPkt.toByteArray(), ref Offset, ref Lenght, out packet);
            else
                Status = SendUCMM_RR_Packet(EnIPPath.GetPath(6, 1), CIPServiceCodes.ForwardOpen, FwPkt.toByteArray(), ref Offset, ref Lenght, out packet);

            if (Status == EnIPNetworkStatus.OnLine)
            {
                if (O2T != null)
                {
                    O2T.O2T_ConnectionId = BitConverter.ToUInt32(packet, Offset); // badly made
                    O2T.SequenceItem = new SequencedAddressItem(O2T.O2T_ConnectionId, 0, O2T.RawData); // ready to send
                }

                if (T2O != null)
                {
                    T2O.Class1Enrolment();
                    T2O.T2O_ConnectionId = BitConverter.ToUInt32(packet, Offset + 4);
                }
                ClosePacket = new ForwardClose_Packet(FwPkt, T2O);
            }

            Task.Run(() => ForwardOpenStatusUpdate?.Invoke(Status == EnIPNetworkStatus.OnLine ? EnIPForwardOpenStatus.ForwardOpen : EnIPForwardOpenStatus.ForwardClose));
            return UpdateStatus(Status);
        }

        [Obsolete("use ForwardClose(ClosePacket) instead")]
        public EnIPNetworkStatus ForwardClose(EnIPAttribut T2O, ForwardClose_Packet ClosePacket)
        {
            int Offset = 0;
            int Lenght = 0;
            byte[] packet;

            if (T2O != null)
                T2O.Class1UnEnrolment();

            Task.Run(() => ForwardOpenStatusUpdate?.Invoke(EnIPForwardOpenStatus.ForwardClose));
            return UpdateStatus(SendUCMM_RR_Packet(EnIPPath.GetPath(6, 1), CIPServiceCodes.ForwardClose, ClosePacket.toByteArray(), ref Offset, ref Lenght, out packet));
        }

        public EnIPNetworkStatus ForwardClose(ForwardClose_Packet ClosePacket)
        {
            int Offset = 0;
            int Lenght = 0;
            byte[] packet;

            if (ClosePacket.T2O != null)
                ClosePacket.T2O.Class1UnEnrolment();

            Task.Run(() => ForwardOpenStatusUpdate?.Invoke(EnIPForwardOpenStatus.ForwardClose));
            return UpdateStatus(SendUCMM_RR_Packet(EnIPPath.GetPath(6, 1), CIPServiceCodes.ForwardClose, ClosePacket.toByteArray(), ref Offset, ref Lenght, out packet));
        }


        private EnIPNetworkStatus UpdateStatus(EnIPNetworkStatus state, string msg = "")
        {
            if(state != EnIPNetworkStatus.OnLine)
                Logger.Error(msg);
            else
                Logger.Debug(msg);

            NetworkStatusUpdate?.Invoke(state, msg);
            return state;
        }

        private EnIPNetworkStatus UpdateStatus(EnIPNetworkStatus state, Exception ex)
        {
            Logger.Error(ex);

            NetworkStatusUpdate?.Invoke(state, ex.Message);
            return state;
        }
    }

    // Device data dictionnary top hierarchy 
    public abstract class EnIPCIPObject
    {
        // set is present to shows not greyed in the property grid
        public ushort Id { get; set; }
        public CIPObjectLibrary IdEnum => Enum.Parse<CIPObjectLibrary>(Id.ToString());
        public EnIPNetworkStatus Status { get; set; }
        public CIPObject DecodedMembers { get; set; }
        public byte[] RawData { get; set; }

        public abstract EnIPNetworkStatus ReadDataFromNetwork();
        public virtual bool EncodeFromDecodedMembers() { return false; } // Encode the existing RawData with the decoded membrer (maybe modified)
        public abstract EnIPNetworkStatus WriteDataToNetwork();

        public abstract string GetStrPath();
        [JsonIgnore]
        public EnIPRemoteDevice RemoteDevice { get; set; }

        protected EnIPNetworkStatus ReadDataFromNetwork(byte[] Path, CIPServiceCodes Service)
        {
            int Offset = 0;
            int Lenght = 0;
            byte[] packet;
            Status = RemoteDevice.GetClassInstanceAttribut_Data(Path, Service, ref Offset, ref Lenght, out packet);

            if (Status == EnIPNetworkStatus.OnLine)
            {
                RawData = new byte[Lenght - Offset];
                Array.Copy(packet, Offset, RawData, 0, Lenght - Offset);
            }
            return Status;
        }

        protected EnIPNetworkStatus WriteDataToNetwork(byte[] Path, CIPServiceCodes Service)
        {
            int Offset = 0;
            int Lenght = 0;
            byte[] packet;

            Status = RemoteDevice.SetClassInstanceAttribut_Data(Path, Service, RawData, ref Offset, ref Lenght, out packet);

            return Status;
        }
    }

    public class EnIPClass : EnIPCIPObject
    {

        Type DecoderClass;

        public EnIPClass(EnIPRemoteDevice RemoteDevice, ushort Id, Type? DecoderClass = null)
        {
            this.Id = Id;
            this.RemoteDevice = RemoteDevice;
            Status = EnIPNetworkStatus.OffLine;
            if (DecoderClass != null)
            {
                this.DecoderClass = DecoderClass;
                if (!DecoderClass.IsSubclassOf(typeof(CIPObject)))
                    throw new ArgumentException("Wrong Decoder class, not subclass of CIPObject", "DecoderClass");
            }
        }

        public override EnIPNetworkStatus WriteDataToNetwork()
        {
            return EnIPNetworkStatus.OnLineWriteRejected;
        }


        public override string GetStrPath()
        {
            return Id.ToString() + ".0";
        }

        public override EnIPNetworkStatus ReadDataFromNetwork()
        {

            // Read all class static attributes
            byte[] ClassDataPath = EnIPPath.GetPath(Id, 0, null);
            EnIPNetworkStatus ret = ReadDataFromNetwork(ClassDataPath, CIPServiceCodes.GetAttributesAll);

            // If rejected try to read all attributes one by one
            if (ret == EnIPNetworkStatus.OnLineReadRejected)
            {

                MemoryStream rawbuffer = new MemoryStream();

                ushort AttId = 1; // first static attribut number

                do
                {
                    ClassDataPath = EnIPPath.GetPath(Id, 0, AttId);
                    ret = ReadDataFromNetwork(ClassDataPath, CIPServiceCodes.GetAttributeSingle);

                    // push the buffer into the data stream
                    if (ret == EnIPNetworkStatus.OnLine)
                        rawbuffer.Write(RawData, 0, RawData.Length);
                    AttId++;
                }
                while (ret == EnIPNetworkStatus.OnLine);

                // yes OK like this, pull the data out of the stream into the RawData
                if (rawbuffer.Length != 0)
                {
                    Status = ret = EnIPNetworkStatus.OnLine; // all is OK even if the last request is (always) rejected
                    RawData = rawbuffer.ToArray();
                }

            }

            if (ret == EnIPNetworkStatus.OnLine)
            {
                CIPObjectLibrary classid = (CIPObjectLibrary)Id;
                try
                {
                    if (DecodedMembers == null)
                    {
                        try
                        {
                            if (DecoderClass == null)
                            {
                                // try to create the associated class object
                                //var o = Activator.CreateInstance(Assembly.GetExecutingAssembly().FullName, "LibEthernetIPStack.ObjectsLibrary.CIP_" + classid.ToString() + "_instance");
                                //DecodedMembers = (CIPObject)o.Unwrap();
                                DecodedMembers = new CIPObjectBaseClass(classid.ToString());
                            }
                            else
                            {
                                var o = Activator.CreateInstance(DecoderClass);
                                DecodedMembers = (CIPObject)o;

                            }
                        }
                        catch(Exception ex)
                        {
                            // echec, get the base class as described in Volume 1, §4-4.1 Class Attributes
                            DecodedMembers = new CIPObjectBaseClass(classid.ToString());
                        }
                    }
                    DecodedMembers.SetRawBytes(RawData);
                }
                catch { }
            }
            return ret;
        }
    }

    public class EnIPInstance : EnIPCIPObject
    {
        public EnIPClass myClass;
        public Type DecoderClass;

        public EnIPInstance(EnIPClass Class, ushort Id, Type? DecoderClass = null)
        {
            this.Id = Id;
            myClass = Class;
            RemoteDevice = Class.RemoteDevice;
            Status = EnIPNetworkStatus.OffLine;
            if (DecoderClass != null)
            {
                this.DecoderClass = DecoderClass;
                if (!DecoderClass.IsSubclassOf(typeof(CIPObject)))
                    throw new ArgumentException("Wrong Decoder class, not subclass of CIPObject", "DecoderClass");
            }
        }

        public override EnIPNetworkStatus WriteDataToNetwork()
        {
            return EnIPNetworkStatus.OnLineWriteRejected;
        }


        public override string GetStrPath()
        {
            return myClass.Id.ToString() + '.' + Id.ToString();
        }

        public override EnIPNetworkStatus ReadDataFromNetwork()
        {
            byte[] DataPath = EnIPPath.GetPath(myClass.Id, Id, null);
            EnIPNetworkStatus ret = ReadDataFromNetwork(DataPath, CIPServiceCodes.GetAttributesAll);
            if (ret == EnIPNetworkStatus.OnLine)
            {
                if (DecodedMembers == null)
                    AttachDecoderClass();

                try
                {
                    DecodedMembers.SetRawBytes(RawData);
                }
                catch { }
            }
            return ret;
        }

        public bool AttachDecoderClass()
        {
            CIPObjectLibrary classid = (CIPObjectLibrary)myClass.Id;
            try
            {
                if (DecoderClass == null)
                {
                    //var o = Activator.CreateInstance(Assembly.GetExecutingAssembly().FullName, "LibEthernetIPStack.ObjectsLibrary.CIP_" + classid.ToString() + "_instance");
                    //DecodedMembers = (CIPObject)o.Unwrap();
                    DecodedMembers = new CIPObjectBaseClass(classid.ToString());
                }
                else
                {
                    var o = Activator.CreateInstance(DecoderClass);
                    DecodedMembers = (CIPObject)o;

                }
                return true;
            }
            catch { }

            return false;

        }

        public EnIPNetworkStatus GetClassInstanceAttributList()
        {
            byte[] DataPath = EnIPPath.GetPath(myClass.Id, Id, null);

            int Offset = 0;
            int Lenght = 0;
            byte[] packet;

            Status = RemoteDevice.GetClassInstanceAttribut_Data(DataPath, CIPServiceCodes.GetAttributeList, ref Offset, ref Lenght, out packet);

            return Status;
        }

        // Never tested, certainly not like this
        public bool CreateRemoteInstance()
        {
            byte[] ClassDataPath = EnIPPath.GetPath(myClass.Id, Id, null);

            int Offset = 0;
            int Lenght = 0;
            byte[] packet;

            Status = RemoteDevice.SendUCMM_RR_Packet(ClassDataPath, CIPServiceCodes.Create, RawData, ref Offset, ref Lenght, out packet);

            if (Status == EnIPNetworkStatus.OnLine)
                return true;
            else
                return false;
        }
    }

    public delegate void T2OEventHandler(EnIPAttribut sender);

    public class EnIPAttribut : EnIPCIPObject
    {
        public EnIPInstance myInstance;
        // Forward Open
        public uint T2O_ConnectionId, O2T_ConnectionId;
        // It got the required data to close the previous ForwardOpen
        ForwardClose_Packet closePkt;
        // sequence for O->T
        public SequencedAddressItem SequenceItem;

        public event T2OEventHandler T2OEvent;

        public EnIPAttribut(EnIPInstance Instance, ushort Id)
        {
            this.Id = Id;
            myInstance = Instance;
            RemoteDevice = Instance.RemoteDevice;
            Status = EnIPNetworkStatus.OffLine;
        }

        public override bool EncodeFromDecodedMembers()
        {
            byte[] NewRaw = new byte[RawData.Length];

            try
            {
                int Idx = 0;
                if (DecodedMembers.EncodeAttr(Id, ref Idx, NewRaw) == true)
                {
                    RawData = NewRaw;
                    return true;
                }
                else
                    return false;
            }
            catch
            {
                return false;
            }
        }

        public override EnIPNetworkStatus WriteDataToNetwork()
        {
            byte[] DataPath = EnIPPath.GetPath(myInstance.myClass.Id, myInstance.Id, Id);
            return WriteDataToNetwork(DataPath, CIPServiceCodes.SetAttributeSingle);
        }

        public override string GetStrPath()
        {
            return myInstance.myClass.Id.ToString() + '.' + myInstance.Id.ToString() + "." + Id.ToString();
        }

        public override EnIPNetworkStatus ReadDataFromNetwork()
        {
            byte[] DataPath = EnIPPath.GetPath(myInstance.myClass.Id, myInstance.Id, Id);
            EnIPNetworkStatus ret = ReadDataFromNetwork(DataPath, CIPServiceCodes.GetAttributeSingle);
            if (ret == EnIPNetworkStatus.OnLine)
            {
                CIPObjectLibrary classid = (CIPObjectLibrary)myInstance.myClass.Id;
                try
                {
                    if (DecodedMembers == null) // No decoder
                    {
                        if (myInstance.DecodedMembers == null)
                            myInstance.AttachDecoderClass();

                        DecodedMembers = myInstance.DecodedMembers; // get the same object as the associated Instance
                    }
                    int Idx = 0;
                    DecodedMembers.DecodeAttr(Id, ref Idx, RawData);
                }
                catch { }
            }
            return ret;
        }

        public void Class1Enrolment()
        {
            RemoteDevice.Class1AttributEnrolment(this);
        }

        public void Class1UnEnrolment()
        {
            RemoteDevice.Class1AttributUnEnrolment(this);
        }

        public void Class1UpdateO2T()
        {
            SequenceItem.data = RawData; // Normaly don't change between call
            RemoteDevice.Class1SendO2T(SequenceItem);
        }

        // Coming from an udp class1 device, with a previous ForwardOpen action
        public void On_ItemMessageReceived(object sender, byte[] packet, SequencedAddressItem ItemPacket, int offset, int msg_length, IPEndPoint remote_address)
        {
            if (ItemPacket.ConnectionId != T2O_ConnectionId) return;

            if (msg_length - offset == 0) return;

            RawData = new byte[msg_length - offset];
            Array.Copy(packet, offset, RawData, 0, RawData.Length);

            if (DecodedMembers != null)
            {
                int Idx = 0;
                try
                {
                    DecodedMembers.DecodeAttr(Id, ref Idx, RawData);
                }
                catch { }
            }


            if (T2OEvent != null)
                T2OEvent(this);
        }

        [Obsolete("See Class1SampleClient2 sample : use ForwardOpen() on the EnIPRemoteDevice object")]
        public EnIPNetworkStatus ForwardOpen(bool p2p, bool T2O, bool O2T, uint CycleTime, int DurationSecond)
        {
            return EnIPNetworkStatus.OffLine;
        }
        [Obsolete("See Class1SampleClient2 sample : use ForwardClose() on the EnIPRemoteDevice object")]
        public EnIPNetworkStatus ForwardClose()
        {
            return EnIPNetworkStatus.OffLine;
        }
    }
}

