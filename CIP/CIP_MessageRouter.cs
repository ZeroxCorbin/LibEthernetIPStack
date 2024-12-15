/**************************************************************************
*                           MIT License
* 
* Copyright (C) 2017 Frederic Chaxel <fchaxel@free.fr>
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
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Linq;

namespace LibEthernetIPStack.CIP;

// CIP_MessageRouter_class not required, nothing new than in CIPObjectBaseClass
public class CIP_MessageRouter_class : CIPObjectBaseClass
{
    public CIP_MessageRouter_class() => AttIdMax = 4;
    //public override string ToString()
    //{
    //    return "class Identity";
    //}
    public override bool DecodeAttr(int AttrNum, ref int Idx, byte[] b) =>
        // base decoding, but should be used only for attribut 1 to 7 and 
        // other decoding for attribut 8 and more
        base.DecodeAttr(AttrNum, ref Idx, b);
}
[JsonObject(MemberSerialization.OptOut)]
public class CIP_MessageRouter_instance : CIPObject
{
    public string Serialized => JsonConvert.SerializeObject(this, new CIPAttributeIdSerializer());

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class MessageRouterObjectList
    {
        public ushort? Number { get; set; }
        public ushort[] ClassesId { get; set; }
        public override string ToString() => "";
    }

    [CIPAttributId(1, "Supported Objects")]
    public MessageRouterObjectList SupportedObjects { get; set; }
    [CIPAttributId(2, "Max Connections")]
    public ushort? MaxConnectionsSupported { get; set; }
    [CIPAttributId(3, "Concurrent Connections")]
    public ushort? NumberOfCurrentConnections { get; set; }
    [CIPAttributId(4, "Active Connections")]
    public ushort[] ActiveConnections { get; set; }

    public CIP_MessageRouter_instance() => AttIdMax = 4;

    //public override string ToString()
    //{
    //    if (FilteredAttribut == -1)
    //        return "MessageRouter instance";
    //    else
    //        return "MessageRouter instance attribute #" + FilteredAttribut.ToString();
    //}

    public override bool DecodeAttr(int AttrNum, ref int Idx, byte[] b)
    {
        switch (AttrNum)
        {
            case 1:
                SupportedObjects = new MessageRouterObjectList
                {
                    Number = GetUInt16(ref Idx, b)
                };
                SupportedObjects.ClassesId = new ushort[SupportedObjects.Number.Value];
                for (int i = 0; i < SupportedObjects.Number.Value; i++)
                    SupportedObjects.ClassesId[i] = GetUInt16(ref Idx, b).Value;

                return true;
            case 2:
                MaxConnectionsSupported = GetUInt16(ref Idx, b);
                return true;
            case 3:
                NumberOfCurrentConnections = GetUInt16(ref Idx, b);
                return true;
            case 4:
                if (NumberOfCurrentConnections == null) return false;

                ActiveConnections = new ushort[NumberOfCurrentConnections.Value];
                for (int i = 0; i < ActiveConnections.Length; i++)
                {
                    ActiveConnections[i] = GetUInt16(ref Idx, b).Value;
                }
                return true;

        }
        return false;
    }

    public byte[] DecodeAttr(int AttrNum)
    {
        switch (AttrNum)
        {
            case 1:
                if (SupportedObjects == null) return null;
                byte[] b = new byte[2 + SupportedObjects.Number.Value * 2];
                int Idx = 0;
                SetUInt16(ref Idx, b, SupportedObjects.Number);
                for (int i = 0; i < SupportedObjects.Number.Value; i++)
                    SetUInt16(ref Idx, b, SupportedObjects.ClassesId[i]);
                return b;
            case 2:
                if (MaxConnectionsSupported == null) return null;
                return BitConverter.GetBytes(MaxConnectionsSupported.Value);
            case 3:
                if (NumberOfCurrentConnections == null) return null;
                return BitConverter.GetBytes(NumberOfCurrentConnections.Value);
            case 4:
                if (ActiveConnections == null) return null;
                byte[] b2 = new byte[ActiveConnections.Length * 2];
                Idx = 0;
                for (int i = 0; i < ActiveConnections.Length; i++)
                    SetUInt16(ref Idx, b2, ActiveConnections[i]);
                return b2;
        }
        return null;
    }

    public override bool EncodeAttr(int AttrNum, ref int Idx, byte[] b)
    {
        switch (AttrNum)
        {
            case 1:
                if (SupportedObjects == null) return false;
                SetUInt16(ref Idx, b, SupportedObjects.Number);
                for (int i = 0; i < SupportedObjects.Number.Value; i++)
                    SetUInt16(ref Idx, b, SupportedObjects.ClassesId[i]);
                return true;
            case 2:
                if (MaxConnectionsSupported == null) return false;
                SetUInt16(ref Idx, b, MaxConnectionsSupported);
                return true;
            case 3:
                if (NumberOfCurrentConnections == null) return false;
                SetUInt16(ref Idx, b, NumberOfCurrentConnections);
                return true;
            case 4:
                if (ActiveConnections == null) return false;
                for (int i = 0; i < ActiveConnections.Length; i++)
                    SetUInt16(ref Idx, b, ActiveConnections[i]);
                return true;
        }
        return false;
    }

    public override byte[] EncodeInstance()
    {
        var b = new byte[512];
        int Idx = 0;
        foreach (var prop in GetType().GetProperties().Where(p => p.GetCustomAttributes(typeof(CIPAttributId), false).Length > 0))
        {
            CIPAttributId attr = (CIPAttributId)prop.GetCustomAttributes(typeof(CIPAttributId), false)[0];
            if (attr.Id != 0)
                EncodeAttr(attr.Id, ref Idx, b);
        }
        return b.Take(Idx).ToArray();
    }
}
