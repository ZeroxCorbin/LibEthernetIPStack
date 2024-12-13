using LibEthernetIPStack.Base;
using LibEthernetIPStack.ObjectsLibrary;
using System;
using System.Reflection;

namespace LibEthernetIPStack.Shared;
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

    public override EnIPNetworkStatus WriteDataToNetwork() => EnIPNetworkStatus.OnLineWriteRejected;

    public override string GetStrPath() => myClass.Id.ToString() + '.' + Id.ToString();

    public override EnIPNetworkStatus ReadDataFromNetwork()
    {
        byte[] DataPath = EnIPPath.GetPath(myClass.Id, Id, null);
        EnIPNetworkStatus ret = ReadDataFromNetwork(DataPath, CIPServiceCodes.GetAttributesAll);
        if (ret == EnIPNetworkStatus.OnLine)
        {
            if (DecodedMembers == null)
                _ = AttachDecoderClass();

            try
            {
                _ = DecodedMembers.SetRawBytes(RawData);
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
                DecodedMembers = (CIPObject)Activator.CreateInstance(Assembly.GetExecutingAssembly().GetType("LibEthernetIPStack.ObjectsLibrary.CIP_" + classid.ToString() + "_instance"));
                //DecodedMembers = (CIPObject)o.Unwrap();
                //DecodedMembers = new CIPObjectBaseClass(classid.ToString());
            }
            else
            {
                object o = Activator.CreateInstance(DecoderClass);
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
        Status = RemoteDevice.GetClassInstanceAttribut_Data(DataPath, CIPServiceCodes.GetAttributeList, ref Offset, ref Lenght, out _);

        return Status;
    }

    // Never tested, certainly not like this
    public bool CreateRemoteInstance()
    {
        byte[] ClassDataPath = EnIPPath.GetPath(myClass.Id, Id, null);

        int Offset = 0;
        int Lenght = 0;
        Status = RemoteDevice.SendUCMM_RR_Packet(ClassDataPath, CIPServiceCodes.Create, RawData, ref Offset, ref Lenght, out _);

        return Status == EnIPNetworkStatus.OnLine;
    }
}
