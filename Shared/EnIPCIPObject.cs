using LibEthernetIPStack.CIP;
using Newtonsoft.Json;
using System;

namespace LibEthernetIPStack.Shared;
public abstract class EnIPCIPObject
{
    // set is present to shows not greyed in the property grid
    public ushort Id { get; set; }
    public CIPObjectLibrary IdEnum => Enum.Parse<CIPObjectLibrary>(Id.ToString());
    public EnIPNetworkStatus Status { get; set; }
    public CIPObject DecodedMembers { get; set; }
    public byte[] RawData { get; set; }

    public abstract EnIPNetworkStatus ReadDataFromNetwork();
    public virtual bool EncodeFromDecodedMembers() => false;  // Encode the existing RawData with the decoded membrer (maybe modified)
    public abstract EnIPNetworkStatus WriteDataToNetwork();

    public abstract string GetStrPath();
    [JsonIgnore]
    public EnIPProducerDevice RemoteDevice { get; set; }

    protected EnIPNetworkStatus ReadDataFromNetwork(byte[] Path, CIPServiceCodes Service)
    {
        int Offset = 0;
        int Lenght = 0;
        Status = RemoteDevice.GetClassInstanceAttribut_Data(Path, Service, ref Offset, ref Lenght, out byte[] packet);

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
        Status = RemoteDevice.SetClassInstanceAttribut_Data(Path, Service, RawData, ref Offset, ref Lenght, out _);

        return Status;
    }
}
