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
using System.Linq;
using System.Net;

namespace LibEthernetIPStack;
public partial class EnIPConsumerDevice : ObservableObject, IDisposable
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    //public event DeviceArrivalHandler DeviceArrival;

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
            if (epTcpEncap == null || epTcpEncap.Address.ToString() != new IPAddress(value.sin_addr).ToString())
            {
                epTcpEncap = new IPEndPoint(System.Net.IPAddress.Parse(IPAddress), value.sin_port);
                epUdpCIP = new IPEndPoint(epTcpEncap.Address, 2222);
                epUdpEncap = new IPEndPoint(epTcpEncap.Address, value.sin_port);
            }
        }
    }

    // Data comming from the reply to ListIdentity query
    // get set are used by the property grid in EnIPExplorer
    [ObservableProperty][property: JsonIgnore] private ushort dataLength;
    [ObservableProperty][property: JsonIgnore] private ushort encapsulationVersion;

    [ObservableProperty] private CIP_Identity_instance identity_instance;
    [ObservableProperty] private CIP_MessageRouter_instance messageRouter_instance;

    [ObservableProperty] private EnIPAttribut assembly_Inputs;
    [ObservableProperty] private byte[] assembly_InputsData = new byte[300];

    [ObservableProperty] private EnIPAttribut assembly_Outputs;
    [ObservableProperty] private byte[] assembly_OutputsData = [0,0,0,0,0,0,0,0,0,0];

    private EnIPRemoteProducer _self;

    private IPEndPoint epTcpEncap;
    private IPEndPoint epUdpCIP;
    private IPEndPoint epUdpEncap;

    // Not a property to avoid browsable in propertyGrid, also [Browsable(false)] could be used
    public IPAddress IPAdd() => epTcpEncap.Address;

    [JsonIgnore] public bool autoConnect = true;
    [JsonIgnore] public bool autoRegisterSession = true;

    private uint SessionHandle = 0; // When Register Session is set

    private EnIPTCPServerTransport Tcpserver;
    private static EnIPUDPTransport UdpListener;

    private object LockTransaction = new();

    // A global packet for response frames
    private byte[] packet = new byte[1500];

    public ObservableCollection<EnIPClass> SupportedClassLists { get; private set; } = [];

    // The remote udp endpoint is given here, it's also the tcp one
    // This constuctor is used with the ListIdentity response buffer
    // No local endpoint given here, the TCP/IP stack should do the job
    // if more than one interface is present
    public EnIPConsumerDevice(long localIP, CIP_Identity_instance cIP_Identity_Instance)
    {
        SocketAddress = new EnIPSocketAddress(new IPEndPoint(localIP, 44818));

        //Initialize encapsulated message listeners
        Tcpserver = new EnIPTCPServerTransport();

        UdpListener ??= new EnIPUDPTransport(epUdpEncap);
        epUdpCIP = new IPEndPoint(UdpListener.GetBroadcastAddress().Address, 2222);
        UdpListener?.JoinMulticastGroup(epUdpCIP.Address);

        Identity_instance = cIP_Identity_Instance;
        CreateMessageRouterInstance();
        CreateAssemblyInstance();

        UdpListener.ItemMessageReceived += UdpListener_ItemMessageReceived;
        UdpListener.EncapMessageReceived += UdpListener_EncapMessageReceived;
        Tcpserver.MessageReceived += Tcpserver_MessageReceived;
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
        _self = new EnIPRemoteProducer
        {
            SocketAddress = SocketAddress,
            VendorId = VendorId,
            DeviceType = DeviceType,
            ProductCode = ProductCode,
            Revision = Revision,
            Status = Status,
            SerialNumber = SerialNumber,
            ProductName = ProductName,
            State = State
        };


        var @class = new EnIPClass(_self, 0x04);
        var input_Instance = new EnIPInstance(@class, 0x64);

        var output_Instance = new EnIPInstance(@class, 0xC6);

        Assembly_Inputs = new EnIPAttribut(input_Instance, 0x03);
        Assembly_Outputs = new EnIPAttribut(output_Instance, 0x03);
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
                else if (m.IsService(CIPServiceCodes.ForwardOpen) && m.IsQuery)
                {
                    if (m.Path[1] == (byte)CIPObjectLibrary.MessageRouter)
                    {

                    }
                    else if(m.Path[1] == (byte)CIPObjectLibrary.ConnectionManager)
                    {
                        if(m.Path[3] == 1)
                        {
                            var fopen = new ForwardOpen_Packet(EncapPacket.Encapsulateddata);

                            Class1AttributEnrolment(Assembly_Inputs);
                            Class1AttributEnrolment(Assembly_Outputs);

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

    private void UdpListener_ItemMessageReceived(object sender, byte[] packet, SequencedAddressItem ItemPacket, int offset, int msg_length, IPEndPoint remote_address)
    {

    }

    public void Dispose()
    {
        //if (IsConnected())
        //    Disconnect();
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

    public void Class1SendO2T(SequencedAddressItem Item) => UdpListener?.Send(Item, epUdpEncap);
    public void Class1SendT2O(SequencedAddressItem Item) => UdpListener?.Send(Item, epUdpEncap);
    
    public void CopyData(EnIPRemoteProducer newset)
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
    // public bool Equals(EnIPConsumerDevice other) => ep.Equals(other.ep);

    public bool IsListening() => Tcpserver.IsListening;

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

    private static readonly Random _rand = new();
    private static uint rnd32()
    {
        return (uint)(_rand.Next(1 << 30)) << 2 | (uint)(_rand.Next(1 << 2));
    }
}
