using CommunityToolkit.Mvvm.ComponentModel;
using LibEthernetIPStack.Base;
using LibEthernetIPStack.CIP;
using LibEthernetIPStack.Explicit;
using LibEthernetIPStack.Implicit;
using LibEthernetIPStack.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LibEthernetIPStack;
public partial class EnIPProducer : ObservableObject, IDisposable
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    public event DeviceArrivalHandler DeviceArrival;

    public delegate void NetworkStatusUpdateDel(EnIPNetworkStatus status, string msg);
    public event NetworkStatusUpdateDel NetworkStatusUpdate;

    public delegate void ForwardOpenStatusUpdateDel(EnIPForwardOpenStatus status);
    public event ForwardOpenStatusUpdateDel ForwardOpenStatusUpdate;

    [ObservableProperty] private string productName;
    [ObservableProperty] private uint serialNumber;
    [ObservableProperty] private IdentityObjectState state;
    [ObservableProperty] private short status;

    public string IPAddress => new IPAddress(SocketAddress.sin_addr).ToString();

    [ObservableProperty] private ushort vendorId;
    [ObservableProperty] private ushort deviceType;
    [ObservableProperty] private ushort productCode;

    [ObservableProperty] private ObservableCollection<byte> revision = [];

    private EnIPSocketAddress socketAddress;
    public EnIPSocketAddress SocketAddress
    {
        get => socketAddress;
        set
        {
            _ = SetProperty(ref socketAddress, value);
            if (ep == null || ep.Address.ToString() != new IPAddress(value.sin_addr).ToString())
            {
                ep = new IPEndPoint(System.Net.IPAddress.Parse(IPAddress), value.sin_port);
                epUdp = new IPEndPoint(ep.Address, 2222);
            }

            if (epTcpEncap == null || epTcpEncap.Address.ToString() != new IPAddress(value.sin_addr).ToString())
            {
                epTcpEncap = new IPEndPoint(System.Net.IPAddress.Parse(IPAddress), value.sin_port);
                epUdpEncap = new IPEndPoint(epTcpEncap.Address, value.sin_port);
                epUdpP2P = new IPEndPoint(epTcpEncap.Address, 2222);
            }
        }
    }

    // Data comming from the reply to ListIdentity query
    // get set are used by the property grid in EnIPExplorer
    [ObservableProperty][property: JsonIgnore] private ushort dataLength;
    [ObservableProperty][property: JsonIgnore] private ushort encapsulationVersion;

    [ObservableProperty] private Encapsulation_Packet identityEncapPacket;

    private IPEndPoint ep;
    private IPEndPoint epUdp;

    private IPEndPoint epTcpEncap;
    private IPEndPoint epUdpEncap;
    private IPEndPoint epUdpBroadcast;
    private IPEndPoint epUdpP2P;

    // Not a property to avoid browsable in propertyGrid, also [Browsable(false)] could be used
    public IPAddress IPAdd() => ep.Address;

    [JsonIgnore] public bool autoConnect = true;
    [JsonIgnore] public bool autoRegisterSession = true;

    private uint SessionHandle = 0; // When Register Session is set

    private EnIPTCPClientTransport Tcpclient;
    private static EnIPUDPTransport UdpListener;

    [ObservableProperty][property: JsonIgnore] private CIP_Identity_instance identity_instance;
    [ObservableProperty][property: JsonIgnore] private CIP_MessageRouter_instance messageRouter_instance;

    [ObservableProperty][property: JsonIgnore] private EnIPAttribut assembly_Inputs;
    [ObservableProperty][property: JsonIgnore] private byte[] assembly_InputsData = new byte[300];

    [ObservableProperty][property: JsonIgnore] private EnIPAttribut assembly_Outputs;
    [ObservableProperty][property: JsonIgnore] private byte[] assembly_OutputsData = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
   
    private EnIPTCPServerTransport Tcpserver;
    private static EnIPUDPTransport UdpCIPServer;

    private object LockTransaction = new();

    // A global packet for response frames
    private byte[] packet = new byte[1500];

    public ObservableCollection<EnIPClass> SupportedClassLists { get; private set; } = [];

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
    public EnIPProducer(IPEndPoint ep, int TcpTimeout, byte[] DataArray, Encapsulation_Packet encapsulation, ref int Offset)
    {
        this.ep = ep;
        IdentityEncapPacket = encapsulation;
        epUdp = new IPEndPoint(ep.Address, 2222);
        Tcpclient = new EnIPTCPClientTransport();
        FromListIdentityResponse(DataArray, ref Offset);
    }

    public EnIPProducer() => Tcpclient = new EnIPTCPClientTransport();

    public EnIPProducer(long localIP, CIP_Identity_instance cIP_Identity_Instance)
    {
        SocketAddress = new EnIPSocketAddress(new IPEndPoint(localIP, 44818));

        //Initialize encapsulated message listeners
        Tcpserver = new EnIPTCPServerTransport();

        UdpListener ??= new EnIPUDPTransport(epUdpEncap);
        epUdpBroadcast = new IPEndPoint(UdpListener.GetBroadcastAddress().Address, 2222);
        UdpListener?.JoinMulticastGroup(epUdpBroadcast.Address);

        UdpCIPServer ??= new EnIPUDPTransport(epUdpP2P);

        Identity_instance = cIP_Identity_Instance;
        CreateMessageRouterInstance();
        CreateAssemblyInstance();

        UdpListener.EncapMessageReceived += UdpListener_EncapMessageReceived;
        UdpCIPServer.ItemMessageReceived += UdpCIPServer_ItemMessageReceived;
        Tcpserver.MessageReceived += Tcpserver_MessageReceived;
    }

    #region Server

    private static readonly Random _rand = new();
    private static uint rnd32()
    {
        return (uint)(_rand.Next(1 << 30)) << 2 | (uint)(_rand.Next(1 << 2));
    }

    private void CreateMessageRouterInstance()
    {
        MessageRouter_instance = new CIP_MessageRouter_instance();

        MessageRouter_instance.SupportedObjects = new()
        {
            Number = 3,
            ClassesId =
            [
                (ushort)CIPObjectLibrary.Identity,
                (ushort)CIPObjectLibrary.MessageRouter,
                (ushort)CIPObjectLibrary.Assembly,
            ]
        };
    }

    private void CreateAssemblyInstance()
    {
        var @class = new EnIPClass(this, 0x04);
        var input_Instance = new EnIPInstance(@class, 0x64);

        var output_Instance = new EnIPInstance(@class, 0xC6);

        Assembly_Inputs = new EnIPAttribut(input_Instance, 0x03);
        Assembly_Outputs = new EnIPAttribut(output_Instance, 0x03);
    }

    private bool CreateNewSession(object sender, IPEndPoint consumer, Encapsulation_Packet encap)
    {
        uint handle = rnd32();
        if (sender is EnIPTCPServerTransport transport)
        {
            byte[] bytes = new byte[4];
            bytes = BitConverter.GetBytes(handle);
            transport.Send(new Encapsulation_Packet(EncapsulationCommands.RegisterSession, handle, bytes).toByteArray(), 28, consumer);
            return true;
        }
        return false;
    }


    private void UdpListener_EncapMessageReceived(object sender, byte[] packet, Encapsulation_Packet EncapPacket, int offset, int msg_length, IPEndPoint remote_address)
    {
        if (EncapPacket.Command == EncapsulationCommands.ListIdentity)
        {
            //FromListIdentityResponse(packet, ref offset);
            NetworkStatusUpdate?.Invoke(EnIPNetworkStatus.OnLine, "Identity requested from " + remote_address.ToString());
            if (sender is EnIPUDPTransport transport)
            {

                List<byte> data =
                [
                    .. new EnIPSocketAddress(epUdpEncap).toByteArray().ToList(),
                    .. Identity_instance.GetRawBytes(),
                ];
                List<byte> data1 =
                [
                    1, 0,
                    .. BitConverter.GetBytes((int)CommonPacketItemIdNumbers.ListIdentityResponse).Take(2),
                    .. BitConverter.GetBytes(data.Count() + 3).Take(2),
                    1,0,
                    .. data,
                    .. BitConverter.GetBytes((int)IdentityObjectState.Operational).Take(1),
                ];

                Encapsulation_Packet ident = new(EncapsulationCommands.ListIdentity, 0, data1.ToArray());

                transport.Send(ident, remote_address);
            }
        }
        else
        {

        }
    }
    private void UdpCIPServer_ItemMessageReceived(object sender, byte[] packet, SequencedAddressItem ItemPacket, int offset, int msg_length, IPEndPoint remote_address)
    {

    }
    private void Tcpserver_MessageReceived(object sender, byte[] packet, Encapsulation_Packet EncapPacket, int offset, int msg_length, IPEndPoint remote_address)
    {
        if (EncapPacket.Command == EncapsulationCommands.SendRRData)
        {
            UCMM_RR_Packet m = new(packet, ref offset, msg_length, true);
            if (m.IsOK)
            {

                // GetAttributeSingle
                if (m.IsService(CIPServiceCodes.GetAttributeSingle) && m.IsQuery)
                {
                    if (m.Path[1] == (byte)CIPObjectLibrary.MessageRouter)
                    {
                        // Instance 1, Atribute 1 (Object List)
                        if (m.Path[3] == 1 && m.Path[5] == 1)
                        {
                            if (sender is EnIPTCPServerTransport transport)
                            {
                                var dat = new UCMM_RR_Packet(CIPServiceCodes.GetAttributeSingle, false, m.Path, [.. MessageRouter_instance.DecodeAttr(1)]);

                                Encapsulation_Packet ident = new(EncapsulationCommands.SendRRData, EncapPacket.Sessionhandle, dat.toByteArray(true));

                                // ident.Status = EncapsulationStatus.Success;
                                var byt = ident.toByteArray();
                                transport.Send(byt, byt.Length, remote_address);
                            }
                        }
                    }
                    else if (m.Path[1] == (byte)CIPObjectLibrary.Assembly)
                    {
                        if (m.Path[3] == Assembly_Inputs.Instance.Id)
                        {
                            if (sender is EnIPTCPServerTransport transport)
                            {
                                var dat = new UCMM_RR_Packet(CIPServiceCodes.GetAttributeSingle, false, m.Path, Assembly_InputsData, CIPGeneralSatusCode.Success);
                                Encapsulation_Packet ident = new(EncapsulationCommands.SendRRData, EncapPacket.Sessionhandle, dat.toByteArray(true));
                                var byt = ident.toByteArray();
                                transport.Send(byt, byt.Length, remote_address);
                            }
                        }
                        else if (m.Path[3] == Assembly_Outputs.Instance.Id)
                        {
                            if (sender is EnIPTCPServerTransport transport)
                            {
                                var dat = new UCMM_RR_Packet(CIPServiceCodes.GetAttributeSingle, false, m.Path, Assembly_OutputsData, CIPGeneralSatusCode.Success);
                                Encapsulation_Packet ident = new(EncapsulationCommands.SendRRData, EncapPacket.Sessionhandle, dat.toByteArray(true));
                                var byt = ident.toByteArray();
                                transport.Send(byt, byt.Length, remote_address);
                            }
                        }
                    }

                }
                // GetAttributesAll
                else if (m.IsService(CIPServiceCodes.GetAttributesAll) && m.IsQuery)
                {
                    if (m.Path[1] == (byte)CIPObjectLibrary.Identity)
                    {

                        if (m.Path[3] == 0)
                        {
                            if (sender is EnIPTCPServerTransport transport)
                            {
                                byte[] data = [1, 0, 1, 0, 7, 0, 7, 0];
                                var dat = new UCMM_RR_Packet(CIPServiceCodes.GetAttributesAll, false, m.Path, [.. data]);
                                Encapsulation_Packet ident = new(EncapsulationCommands.SendRRData, EncapPacket.Sessionhandle, dat.toByteArray(true));
                                var byt = ident.toByteArray();
                                transport.Send(byt, byt.Length, remote_address);
                            }
                        }
                        else if (m.Path[3] == 1)
                            if (sender is EnIPTCPServerTransport transport)
                            {
                                var data = Identity_instance.EncodeInstance();
                                var dat = new UCMM_RR_Packet(CIPServiceCodes.GetAttributesAll, false, m.Path, [.. data]);
                                Encapsulation_Packet ident = new(EncapsulationCommands.SendRRData, EncapPacket.Sessionhandle, dat.toByteArray(true));

                                var byt = ident.toByteArray();
                                transport.Send(byt, byt.Length, remote_address);
                            }
                    }
                    // MessageRouter
                    else if (m.Path[1] == (byte)CIPObjectLibrary.MessageRouter)
                    {
                        if (m.Path[3] == 0)
                        {
                            //if (sender is EnIPTCPServerTransport transport)
                            //{
                            //    byte[] data = [1, 0, 1, 0, 7, 0, 7, 0];
                            //    var dat = new UCMM_RR_Packet(CIPServiceCodes.GetAttributesAll, false, m.Path, [.. data]);
                            //    Encapsulation_Packet ident = new(EncapsulationCommands.SendRRData, EncapPacket.Sessionhandle, dat.toByteArray(true));
                            //    var byt = ident.toByteArray();
                            //    transport.Send(byt, byt.Length, remote_address);
                            //}
                        }
                        else if (m.Path[3] == 1)
                            if (sender is EnIPTCPServerTransport transport)
                            {
                                var data = MessageRouter_instance.EncodeInstance();
                                var dat = new UCMM_RR_Packet(CIPServiceCodes.GetAttributesAll, false, m.Path, [.. data]);
                                Encapsulation_Packet ident = new(EncapsulationCommands.SendRRData, EncapPacket.Sessionhandle, dat.toByteArray(true));
                                var byt = ident.toByteArray();
                                transport.Send(byt, byt.Length, remote_address);
                            }
                    }
                    // Assembly
                    else if (m.Path[1] == (byte)CIPObjectLibrary.Assembly)
                    {
                        if (m.Path[3] == 0)
                        {
                            if (sender is EnIPTCPServerTransport transport)
                            {
                                var dat = new UCMM_RR_Packet(CIPServiceCodes.GetAttributesAll, false, m.Path, [], CIPGeneralSatusCode.Service_not_supported);
                                Encapsulation_Packet ident = new(EncapsulationCommands.SendRRData, EncapPacket.Sessionhandle, dat.toByteArray(true));
                                var byt = ident.toByteArray();
                                transport.Send(byt, byt.Length, remote_address);
                            }
                        }
                    }
                }
                // Forward Open
                else if (m.IsService(CIPServiceCodes.ForwardOpen) && m.IsQuery)
                {
                    if (m.Path[1] == (byte)CIPObjectLibrary.MessageRouter)
                    {

                    }
                    else if (m.Path[1] == (byte)CIPObjectLibrary.ConnectionManager)
                    {
                        if (m.Path[3] == 1)
                        {
                            var fopen = new ForwardOpen_Packet(EncapPacket.Encapsulateddata);

                            Class1AttributEnrolment(Assembly_Inputs);
                            Class1AttributEnrolment(Assembly_Outputs);

                            Assembly_Outputs.O2TEvent -= Assembly_Outputs_O2TEvent;
                            Assembly_Outputs.O2TEvent += Assembly_Outputs_O2TEvent;

                            if (sender is EnIPTCPServerTransport transport)
                            {
                                var dat = new UCMM_RR_Packet(CIPServiceCodes.ForwardOpen, false, m.Path, fopen.toReplyByteArray(), CIPGeneralSatusCode.Success);
                                Encapsulation_Packet ident = new(EncapsulationCommands.SendRRData, EncapPacket.Sessionhandle, dat.toByteArray(true));
                                var byt = ident.toByteArray();
                                transport.Send(byt, byt.Length, remote_address);
                            }
                        }
                    }
                }
                //Forward Close
                else if (m.IsService(CIPServiceCodes.ForwardClose) && m.IsQuery)
                {
                    Class1AttributUnEnrolment(Assembly_Inputs);
                    Class1AttributUnEnrolment(Assembly_Outputs);

                    Assembly_Outputs.O2TEvent -= Assembly_Outputs_O2TEvent;

                    if (sender is EnIPTCPServerTransport transport)
                    {
                        var dat = new UCMM_RR_Packet(CIPServiceCodes.ForwardClose, false, m.Path, [], CIPGeneralSatusCode.Success);
                        Encapsulation_Packet ident = new(EncapsulationCommands.SendRRData, EncapPacket.Sessionhandle, dat.toByteArray(true));
                        var byt = ident.toByteArray();
                        transport.Send(byt, byt.Length, remote_address);
                    }
                }
                else
                {

                }
            }
            else
            {

            }
        }
        else if (EncapPacket.Command == EncapsulationCommands.RegisterSession)
        {
            if (EncapPacket.IsOK)
            {
                CreateNewSession(sender, remote_address, EncapPacket);
            }
        }
        else if (EncapPacket.Command == EncapsulationCommands.UnRegisterSession)
        {
            if (EncapPacket.IsOK)
            {
                SessionHandle = 0;
            }
        }
        else
        {

        }
    }

    private void Assembly_Outputs_O2TEvent(EnIPAttribut sender)
    {

    }

    #endregion

    public void Dispose()
    {
        if (IsConnected)
            Disconnect();
    }

    public void Class1Activate(IPEndPoint ep) => UdpListener ??= new EnIPUDPTransport(ep.Address.ToString(), ep.Port);

    public void Class1AddMulticast(string IP) => UdpListener?.JoinMulticastGroup(IP);

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

    public void Class1SendO2T(SequencedAddressItem Item) => UdpListener?.Send(Item, epUdp);

    public void CopyData(EnIPProducer newset)
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
    public bool Equals(EnIPProducer other) => ep.Equals(other.ep);

    public bool IsConnected => Tcpclient.IsConnected;

    public bool Connect()
    {
        if (Tcpclient.IsConnected == true) 
            return true;

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
    public async Task<bool> DiscoverServer()
    {
        if (autoConnect) _ = Connect();

        try
        {
            if (Tcpclient.IsConnected)
            {
                Encapsulation_Packet p = new(EncapsulationCommands.ListIdentity)
                {
                    Command = EncapsulationCommands.ListIdentity
                };

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
                    DeviceArrival?.Invoke(this);
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
        if (autoConnect) _ = Connect();

        if (Tcpclient.IsConnected == true && SessionHandle == 0)
        {
            byte[] b = new byte[] { 1, 0, 0, 0 };
            Encapsulation_Packet p = new(EncapsulationCommands.RegisterSession, 0, b);

            int ret;
            Encapsulation_Packet rep;
            lock (LockTransaction)
                ret = Tcpclient.SendReceive(p, out rep, out int Offset, ref packet);

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
            UCMM_RR_Packet m = new(Service, true, DataPath, data);
            Encapsulation_Packet p = new(EncapsulationCommands.SendRRData, SessionHandle, m.toByteArray());

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

            string o = Service.ToString() + " : " + ErrorMsg + " - Node " + EnIPPath.GetPath(DataPath) + " - Endpoint " + ep.ToString();

            return ErrorMsg == "TCP Error"
                ? UpdateStatus(EnIPNetworkStatus.OffLine, o)
                : Service == CIPServiceCodes.SetAttributeSingle
                ? UpdateStatus(EnIPNetworkStatus.OnLineWriteRejected, o)
                : UpdateStatus(EnIPNetworkStatus.OnLineReadRejected, o);
        }
        catch (Exception ex)
        {
            _ = UpdateStatus(EnIPNetworkStatus.OffLine, "Error while sending request to endpoint. " + ep.ToString());
            return UpdateStatus(EnIPNetworkStatus.OffLine, ex);
        }
    }

    public EnIPNetworkStatus SetClassInstanceAttribut_Data(byte[] DataPath, CIPServiceCodes Service, byte[] data, ref int Offset, ref int Lenght, out byte[] packet) => SendUCMM_RR_Packet(DataPath, Service, data, ref Offset, ref Lenght, out packet);
    public EnIPNetworkStatus GetClassInstanceAttribut_Data(byte[] ClassDataPath, CIPServiceCodes Service, ref int Offset, ref int Lenght, out byte[] packet) => SendUCMM_RR_Packet(ClassDataPath, Service, null, ref Offset, ref Lenght, out packet);

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
            Encapsulation_Packet p = new(EncapsulationCommands.RegisterSession, SessionHandle);

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

                Cid = att.Instance.myClass.Id;
                if (previousAtt != null && Cid == previousAtt.Instance.myClass.Id)
                    Cid = null;

                Aid = att.Id == 3 && att.Instance.myClass.Id == 4 ? null : att.Id;

                DataPath = EnIPPath.GetPath(Cid, att.Instance.Id, Aid, att != Config);
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
        ForwardOpen_Config conf = new(O2T, T2O, P2P, CycleTime);
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

        ForwardOpen_Packet FwPkt = new(DataPath, conf);

        int Offset = 0;
        int Lenght = 0;

        EnIPNetworkStatus Status = FwPkt.IsLargeForwardOpen
            ? SendUCMM_RR_Packet(EnIPPath.GetPath(6, 1), CIPServiceCodes.LargeForwardOpen, FwPkt.toRequestByteArray(), ref Offset, ref Lenght, out byte[] packet)
            : SendUCMM_RR_Packet(EnIPPath.GetPath(6, 1), CIPServiceCodes.ForwardOpen, FwPkt.toRequestByteArray(), ref Offset, ref Lenght, out packet);
        if (Status == EnIPNetworkStatus.OnLine)
        {
            if (O2T != null)
            {
                O2T.O2T_ConnectionId = BitConverter.ToUInt32(packet, Offset); // badly made
                O2T.O2TSequenceItem = new SequencedAddressItem(O2T.O2T_ConnectionId, 0, O2T.RawData); // ready to send
            }

            if (T2O != null)
            {
                T2O.Class1Enrolment();
                T2O.T2O_ConnectionId = BitConverter.ToUInt32(packet, Offset + 4);
            }
            ClosePacket = new ForwardClose_Packet(FwPkt, T2O);
        }

        _ = Task.Run(() => ForwardOpenStatusUpdate?.Invoke(Status == EnIPNetworkStatus.OnLine ? EnIPForwardOpenStatus.ForwardOpen : EnIPForwardOpenStatus.ForwardClose));
        return UpdateStatus(Status);
    }

    [Obsolete("use ForwardClose(ClosePacket) instead")]
    public EnIPNetworkStatus ForwardClose(EnIPAttribut T2O, ForwardClose_Packet ClosePacket)
    {
        int Offset = 0;
        int Lenght = 0;

        T2O?.Class1UnEnrolment();

        _ = Task.Run(() => ForwardOpenStatusUpdate?.Invoke(EnIPForwardOpenStatus.ForwardClose));
        return UpdateStatus(SendUCMM_RR_Packet(EnIPPath.GetPath(6, 1), CIPServiceCodes.ForwardClose, ClosePacket.toByteArray(), ref Offset, ref Lenght, out byte[] packet));
    }

    public EnIPNetworkStatus ForwardClose(ForwardClose_Packet ClosePacket)
    {
        int Offset = 0;
        int Lenght = 0;

        ClosePacket.T2O?.Class1UnEnrolment();

        _ = Task.Run(() => ForwardOpenStatusUpdate?.Invoke(EnIPForwardOpenStatus.ForwardClose));
        return UpdateStatus(SendUCMM_RR_Packet(EnIPPath.GetPath(6, 1), CIPServiceCodes.ForwardClose, ClosePacket.toByteArray(), ref Offset, ref Lenght, out byte[] packet));
    }

    private EnIPNetworkStatus UpdateStatus(EnIPNetworkStatus state, string msg = "")
    {
        if (state != EnIPNetworkStatus.OnLine)
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
