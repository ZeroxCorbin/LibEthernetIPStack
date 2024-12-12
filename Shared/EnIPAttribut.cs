using System;
using System.Net;

namespace LibEthernetIPStack.Shared;
public class EnIPAttribut : EnIPCIPObject
{
    public EnIPInstance myInstance;
    // Forward Open
    public uint T2O_ConnectionId, O2T_ConnectionId;

    // It got the required data to close the previous ForwardOpen
    private ForwardClose_Packet closePkt;
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

    public override string GetStrPath() => myInstance.myClass.Id.ToString() + '.' + myInstance.Id.ToString() + "." + Id.ToString();

    public override EnIPNetworkStatus ReadDataFromNetwork()
    {
        byte[] DataPath = EnIPPath.GetPath(myInstance.myClass.Id, myInstance.Id, Id);
        EnIPNetworkStatus ret = ReadDataFromNetwork(DataPath, CIPServiceCodes.GetAttributeSingle);
        if (ret == EnIPNetworkStatus.OnLine)
        {
            _ = (CIPObjectLibrary)myInstance.myClass.Id;
            try
            {
                if (DecodedMembers == null) // No decoder
                {
                    if (myInstance.DecodedMembers == null)
                        _ = myInstance.AttachDecoderClass();

                    DecodedMembers = myInstance.DecodedMembers; // get the same object as the associated Instance
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
                _ = DecodedMembers.DecodeAttr(Id, ref Idx, RawData);
            }
            catch { }
        }

        T2OEvent?.Invoke(this);
    }

    [Obsolete("See Class1SampleClient2 sample : use ForwardOpen() on the EnIPRemoteDevice object")]
    public EnIPNetworkStatus ForwardOpen(bool p2p, bool T2O, bool O2T, uint CycleTime, int DurationSecond) => EnIPNetworkStatus.OffLine;
    [Obsolete("See Class1SampleClient2 sample : use ForwardClose() on the EnIPRemoteDevice object")]
    public EnIPNetworkStatus ForwardClose() => EnIPNetworkStatus.OffLine;
}
