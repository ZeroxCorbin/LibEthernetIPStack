using LibEthernetIPStack.Base;
using System;
using System.Net;

namespace LibEthernetIPStack.Shared;

// Device data dictionnary top hierarchy 
public delegate void T2OEventHandler(EnIPAttribut sender);
public delegate void O2TEventHandler(EnIPAttribut sender);

public class EnIPAttribut : EnIPCIPObject
{
    public EnIPInstance Instance { get; set; }
    // Forward Open
    public uint T2O_ConnectionId { get; set; }
    public uint O2T_ConnectionId { get; set; }

    // It got the required data to close the previous ForwardOpen
    private ForwardClose_Packet closePkt;
    // sequence for O->T
    public SequencedAddressItem O2TSequenceItem { get; set; }
    public SequencedAddressItem T2OSequenceItem { get; set; }

    public event T2OEventHandler T2OEvent;
    public event O2TEventHandler O2TEvent;
    public EnIPAttribut(EnIPInstance instance, ushort id)
    {
        Id = id;
        Instance = instance;
        RemoteDevice = instance.RemoteDevice;
        Status = EnIPNetworkStatus.OffLine;
    }

    public override bool EncodeFromDecodedMembers()
    {
        byte[] newData = new byte[RawData.Length];

        try
        {
            int Idx = 0;
            if (DecodedMembers.EncodeAttr(Id, ref Idx, newData) == true)
            {
                RawData = newData;
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
        byte[] dataPath = EnIPPath.GetPath(Instance.myClass.Id, Instance.Id, Id);
        return WriteDataToNetwork(dataPath, CIPServiceCodes.SetAttributeSingle);
    }

    public override string GetStrPath() => Instance.myClass.Id.ToString() + '.' + Instance.Id.ToString() + "." + Id.ToString();

    public override EnIPNetworkStatus ReadDataFromNetwork()
    {
        byte[] dataPath = EnIPPath.GetPath(Instance.myClass.Id, Instance.Id, Id);
        EnIPNetworkStatus ret = ReadDataFromNetwork(dataPath, CIPServiceCodes.GetAttributeSingle);
        if (ret == EnIPNetworkStatus.OnLine)
        {
            _ = (CIPObjectLibrary)Instance.myClass.Id;
            try
            {
                if (DecodedMembers == null) // No decoder
                {
                    if (Instance.DecodedMembers == null)
                        _ = Instance.AttachDecoderClass();

                    DecodedMembers = Instance.DecodedMembers; // get the same object as the associated Instance
                }
                int Idx = 0;
                _ = DecodedMembers?.DecodeAttr(Id, ref Idx, RawData);
            }
            catch { }
        }
        return ret;
    }

    public void Class1Enrolment() => RemoteDevice.Class1AttributEnrolment(this);

    public void Class1UnEnrolment() => RemoteDevice.Class1AttributUnEnrolment(this);

    public void Class1UpdateO2T()
    {
        O2TSequenceItem.data = RawData; // Normaly don't change between call
        RemoteDevice.Class1SendO2T(O2TSequenceItem);
    }

    public void Class1UpdateT2O(byte[] data)
    {
        T2OSequenceItem.data = data; // Normaly don't change between call
        RemoteDevice.Class1SendT2O(T2OSequenceItem);
    }

    // Coming from an udp class1 device, with a previous ForwardOpen action
    public void On_ItemMessageReceived(object sender, byte[] packet, SequencedAddressItem itemPacket, int offset, int msg_length, IPEndPoint remote_address)
    {
        if (itemPacket.ConnectionId == T2O_ConnectionId)
        {
            if (msg_length - offset == 0) return;

            RawData = new byte[msg_length - offset];
            Array.Copy(packet, offset, RawData, 0, RawData.Length);

            if (DecodedMembers != null)
            {
                int idx = 0;
                try
                {
                    _ = DecodedMembers.DecodeAttr(Id, ref idx, RawData);
                }
                catch { }
            }

            T2OEvent?.Invoke(this);
        }
        else if (itemPacket.ConnectionId == O2T_ConnectionId)
        {

            if (msg_length - offset == 0) return;

            RawData = new byte[msg_length - offset];
            Array.Copy(packet, offset, RawData, 0, RawData.Length);

            if (DecodedMembers != null)
            {
                int idx = 0;
                try
                {
                    _ = DecodedMembers.DecodeAttr(Id, ref idx, RawData);
                }
                catch { }
            }

            O2TEvent?.Invoke(this);
        }
        else
        {

        }


    }

    [Obsolete("See Class1SampleClient2 sample : use ForwardOpen() on the EnIPRemoteDevice object")]
    public EnIPNetworkStatus ForwardOpen(bool p2p, bool T2O, bool O2T, uint CycleTime, int DurationSecond) => EnIPNetworkStatus.OffLine;
    [Obsolete("See Class1SampleClient2 sample : use ForwardClose() on the EnIPRemoteDevice object")]
    public EnIPNetworkStatus ForwardClose() => EnIPNetworkStatus.OffLine;
}
