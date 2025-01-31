using LibEthernetIPStack.Base;
using LibEthernetIPStack.CIP;
using System;
using System.IO;
using System.Reflection;

namespace LibEthernetIPStack.Shared;
public class EnIPClass : EnIPCIPObject
{
    private Type DecoderClass;

    public EnIPClass(EnIPProducer remoteDevice, ushort id, Type decoderClass = null)
    {
        Id = id;
        RemoteDevice = remoteDevice;
        Status = EnIPNetworkStatus.OffLine;

        if (decoderClass != null)
        {
            DecoderClass = decoderClass;
            if (!decoderClass.IsSubclassOf(typeof(CIPObject)))
                throw new ArgumentException("Wrong Decoder class, not subclass of CIPObject", "DecoderClass");
        }
    }

    public override EnIPNetworkStatus WriteDataToNetwork() => EnIPNetworkStatus.OnLineWriteRejected;

    public override string GetStrPath() => Id.ToString() + ".0";

    public override EnIPNetworkStatus ReadDataFromNetwork()
    {

        // Read all class static attributes
        byte[] classDataPath = EnIPPath.GetPath(Id, 0, null);
        EnIPNetworkStatus ret = ReadDataFromNetwork(classDataPath, CIPServiceCodes.GetAttributesAll);

        // If rejected try to read all attributes one by one
        if (ret == EnIPNetworkStatus.OnLineReadRejected)
        {

            MemoryStream rawbuffer = new();

            ushort attributeId = 1; // first static attribut number

            do
            {
                classDataPath = EnIPPath.GetPath(Id, 0, attributeId);
                ret = ReadDataFromNetwork(classDataPath, CIPServiceCodes.GetAttributeSingle);

                // push the buffer into the data stream
                if (ret == EnIPNetworkStatus.OnLine)
                    rawbuffer.Write(RawData, 0, RawData.Length);
                attributeId++;
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
                            DecodedMembers = (CIPObject)Activator.CreateInstance(Assembly.GetExecutingAssembly().GetType("LibEthernetIPStack.CIP.CIP_" + classid.ToString() + "_class"));
                            //DecodedMembers = new CIPObjectBaseClass(classid.ToString());
                        }
                        else
                        {
                            object o = Activator.CreateInstance(DecoderClass);
                            DecodedMembers = (CIPObject)o;

                        }
                    }
                    catch (Exception ex)
                    {
                        // echec, get the base class as described in Volume 1, §4-4.1 Class Attributes
                        DecodedMembers = new CIPObjectBaseClass(classid.ToString());
                    }
                }
                _ = DecodedMembers.SetRawBytes(RawData);
            }
            catch (Exception)
            { }
        }
        return ret;
    }
}

