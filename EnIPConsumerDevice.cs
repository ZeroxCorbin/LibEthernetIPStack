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

    private EnIPAttribut _assemblyClass;

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

        UdpListener.ItemMessageReceived += UdpListener_ItemMessageReceived;
        UdpListener.EncapMessageReceived += UdpListener_EncapMessageReceived;
        Tcpserver.MessageReceived += Tcpserver_MessageReceived;
    }

    private void Tcpserver_MessageReceived(object sender, byte[] packet, Encapsulation_Packet EncapPacket, int offset, int msg_length, IPEndPoint remote_address)
    {
        if (EncapPacket.Command == EncapsulationCommands.SendRRData)
        {
            UCMM_RR_Packet m = new(packet, ref offset, msg_length);
            if (m.IsOK)
            {
                if (m.IsService(CIPServiceCodes.GetAttributeSingle) || m.IsService(CIPServiceCodes.SetAttributeSingle))
                {
                    //EnIPAttribut att = new(m., this);
                    //att.On_ItemMessageReceived(sender, packet, m, offset, msg_length, remote_address);
                }
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
                    1, 0, 0, 0, 0, 0, 0, 0,
                    .. new EnIPSocketAddress(epUdpEncap).toByteArray().ToList(),
                    .. Identity_instance.EncodeAttr(),
                ];
                Encapsulation_Packet ident = new(EncapsulationCommands.ListIdentity, 0, data.ToArray());

                transport.Send(ident, remote_address);
            }
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

    public void CopyData(EnIPProducerDevice newset)
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
        uint handle = 123654;
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
}
