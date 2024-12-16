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
using System.ComponentModel;
using System.Linq;
using System.Net.NetworkInformation;

namespace LibEthernetIPStack.CIP;

// CIP_EtherNetLink_class not required, nothing new than in CIPObjectBaseClass
public class CIP_EtherNetLink_class : CIPObjectBaseClass
{
    public CIP_EtherNetLink_class() => AttIdMax = 10;
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
public class CIP_EtherNetLink_instance : CIPObject
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class EtherNetLinkInterfaceCounters
    {
        public uint? In_Octets { get; set; }
        public uint? In_Ucast_Packets { get; set; }
        public uint? In_NUcast_Packets { get; set; }
        public uint? In_Discards { get; set; }
        public uint? In_Errors { get; set; }
        public uint? In_Unknown_Protos { get; set; }
        public uint? Out_Octets { get; set; }
        public uint? Out_Ucast_Packets { get; set; }
        public uint? Out_NUcast_Packets { get; set; }
        public uint? Out_Discards { get; set; }
        public uint? Out_Errors { get; set; }
        public override string ToString() => "";
    }
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class EtherNetLinkMediaCounters
    {
        public uint? Alignment_Errors { get; set; }
        public uint? FCS_Errors { get; set; }
        public uint? Single_Collisions { get; set; }
        public uint? Multiple_Collisions { get; set; }
        public uint? SQE_Test_Errors { get; set; }
        public uint? Deferred_Transmissions { get; set; }
        public uint? Late_Collisions { get; set; }
        public uint? Excessive_Collisions { get; set; }
        public uint? MAC_Transmit_Errors { get; set; }
        public uint? Carrier_Sense_Errors { get; set; }
        public uint? Frame_Too_Long { get; set; }
        public uint? MAC_Receive_Errors { get; set; }
        public override string ToString() => "";
    }
    [CIPAttributId(1)]
    public uint? Interface_Speed { get; set; }
    [CIPAttributId(2)]
    public uint? Interface_Flags { get; set; }
    [CIPAttributId(3)]
    public string Physical_Address { get; set; }
    [CIPAttributId(4)]
    public EtherNetLinkInterfaceCounters InterfaceCounters { get; set; }
    [CIPAttributId(5)]
    public EtherNetLinkMediaCounters MediaCounters { get; set; }
    [CIPAttributId(6)]
    public ushort? Control_Bits { get; set; }
    [CIPAttributId(6)]
    public ushort? Forced_Interface_Speed { get; set; }
    [CIPAttributId(7)]
    public byte? Interface_Type { get; set; }
    [CIPAttributId(8)]
    public byte? Interface_State { get; set; }
    [CIPAttributId(9)]
    public byte? Admin_State { get; set; }
    [CIPAttributId(10)]
    public string Interface_Label { get; set; }

    public CIP_EtherNetLink_instance() => AttIdMax = 10;

    //public override string ToString()
    //{
    //    if (FilteredAttribut == -1)
    //        return "EtherNetLink instance";
    //    else
    //        return "EtherNetLink instance attribute #" + FilteredAttribut.ToString();
    //}

    public override bool DecodeAttr(int AttrNum, ref int Idx, byte[] b)
    {
        switch (AttrNum)
        {
            case 1:
                Interface_Speed = GetUInt32(ref Idx, b);
                return true;
            case 2:
                Interface_Flags = GetUInt32(ref Idx, b);
                return true;
            case 3:
                Physical_Address = GetPhysicalAddress(ref Idx, b).ToString();
                return true;
            case 4:
                InterfaceCounters = new EtherNetLinkInterfaceCounters
                {
                    In_Octets = GetUInt32(ref Idx, b),
                    In_Ucast_Packets = GetUInt32(ref Idx, b),
                    In_NUcast_Packets = GetUInt32(ref Idx, b),
                    In_Discards = GetUInt32(ref Idx, b),
                    In_Errors = GetUInt32(ref Idx, b),
                    In_Unknown_Protos = GetUInt32(ref Idx, b),

                    Out_Octets = GetUInt32(ref Idx, b),
                    Out_Ucast_Packets = GetUInt32(ref Idx, b),
                    Out_NUcast_Packets = GetUInt32(ref Idx, b),
                    Out_Discards = GetUInt32(ref Idx, b),
                    Out_Errors = GetUInt32(ref Idx, b)
                };
                return true;
            case 5:
                MediaCounters = new EtherNetLinkMediaCounters
                {
                    Alignment_Errors = GetUInt32(ref Idx, b),
                    FCS_Errors = GetUInt32(ref Idx, b),
                    Single_Collisions = GetUInt32(ref Idx, b),
                    Multiple_Collisions = GetUInt32(ref Idx, b),
                    SQE_Test_Errors = GetUInt32(ref Idx, b),
                    Deferred_Transmissions = GetUInt32(ref Idx, b),
                    Late_Collisions = GetUInt32(ref Idx, b),
                    Excessive_Collisions = GetUInt32(ref Idx, b),
                    MAC_Transmit_Errors = GetUInt32(ref Idx, b),
                    Carrier_Sense_Errors = GetUInt32(ref Idx, b),
                    Frame_Too_Long = GetUInt32(ref Idx, b),
                    MAC_Receive_Errors = GetUInt32(ref Idx, b)
                };
                return true;
            case 6:
                Control_Bits = GetUInt16(ref Idx, b);
                Forced_Interface_Speed = GetUInt16(ref Idx, b);
                return true;
            case 7:
                Interface_Type = Getbyte(ref Idx, b);
                return true;
            case 8:
                Interface_State = Getbyte(ref Idx, b);
                return true;
            case 9:
                Admin_State = Getbyte(ref Idx, b);
                return true;
            case 10:
                Interface_Label = GetShortString(ref Idx, b);
                return true;
        }

        return false;
    }

    public override bool EncodeAttr(int AttrNum, ref int Idx, byte[] b)
    {
        switch (AttrNum)
        {
            case 1:
                if (Interface_Speed == null) return false;
                SetUInt32(ref Idx, b, Interface_Speed);
                return true;
            case 2:
                if (Interface_Flags == null) return false;
                SetUInt32(ref Idx, b, Interface_Flags);
                return true;
            case 3:
                if (Physical_Address == null) return false;
                SetPhysicalAddress(ref Idx, b, PhysicalAddress.Parse(Physical_Address));
                return true;
            case 4:
                if (InterfaceCounters == null) return false;
                SetUInt32(ref Idx, b, InterfaceCounters.In_Octets);
                SetUInt32(ref Idx, b, InterfaceCounters.In_Ucast_Packets);
                SetUInt32(ref Idx, b, InterfaceCounters.In_NUcast_Packets);
                SetUInt32(ref Idx, b, InterfaceCounters.In_Discards);
                SetUInt32(ref Idx, b, InterfaceCounters.In_Errors);
                SetUInt32(ref Idx, b, InterfaceCounters.In_Unknown_Protos);
                SetUInt32(ref Idx, b, InterfaceCounters.Out_Octets);
                SetUInt32(ref Idx, b, InterfaceCounters.Out_Ucast_Packets);
                SetUInt32(ref Idx, b, InterfaceCounters.Out_NUcast_Packets);
                SetUInt32(ref Idx, b, InterfaceCounters.Out_Discards);
                SetUInt32(ref Idx, b, InterfaceCounters.Out_Errors);
                return true;
            case 5:
                if (MediaCounters == null) return false;
                SetUInt32(ref Idx, b, MediaCounters.Alignment_Errors);
                SetUInt32(ref Idx, b, MediaCounters.FCS_Errors);
                SetUInt32(ref Idx, b, MediaCounters.Single_Collisions);
                SetUInt32(ref Idx, b, MediaCounters.Multiple_Collisions);
                SetUInt32(ref Idx, b, MediaCounters.SQE_Test_Errors);
                SetUInt32(ref Idx, b, MediaCounters.Deferred_Transmissions);
                SetUInt32(ref Idx, b, MediaCounters.Late_Collisions);
                SetUInt32(ref Idx, b, MediaCounters.Excessive_Collisions);
                SetUInt32(ref Idx, b, MediaCounters.MAC_Transmit_Errors);
                SetUInt32(ref Idx, b, MediaCounters.Carrier_Sense_Errors);
                SetUInt32(ref Idx, b, MediaCounters.Frame_Too_Long);
                SetUInt32(ref Idx, b, MediaCounters.MAC_Receive_Errors);
                return true;
            case 6:
                if (Control_Bits == null) return false;
                SetUInt16(ref Idx, b, Control_Bits);
                SetUInt16(ref Idx, b, Forced_Interface_Speed);
                return true;
            case 7:
                if (Interface_Type == null) return false;
                Setbyte(ref Idx, b, Interface_Type);
                return true;
            case 8:
                if (Interface_State == null) return false;
                Setbyte(ref Idx, b, Interface_State);
                return true;
            case 9:
                if (Admin_State == null) return false;
                Setbyte(ref Idx, b, Admin_State);
                return true;
            case 10:
                if (Interface_Label == null) return false;
                SetShortString(ref Idx, b, Interface_Label);
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
