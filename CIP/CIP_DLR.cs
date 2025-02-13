﻿/**************************************************************************
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
using System.Linq;
using System.Net.NetworkInformation;

namespace LibEthernetIPStack.CIP;


// CIP_DLR_class not required, nothing new than in CIPObjectBaseClass
public class CIP_DLR_class : CIPObjectBaseClass
{
    public CIP_DLR_class() => AttIdMax = 4;
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
public class CIP_DLR_instance : CIPObject
{
    [CIPAttributId(1)]
    public byte? Network_Topology { get; set; }
    [CIPAttributId(2)]
    public byte? Network_Status { get; set; }
    [CIPAttributId(3)]
    public string Active_Supervisor_IPAddress { get; set; }
    [CIPAttributId(4)]
    public string Active_Supervisor_PhysicalAddress { get; set; }
    [CIPAttributId(5)]
    public uint? Capability_Flag { get; set; }

    public CIP_DLR_instance() => AttIdMax = 5;

    //public override string ToString()
    //{
    //    if (FilteredAttribut == -1)
    //        return "DLR instance";
    //    else
    //        return "DLR instance attribute #" + FilteredAttribut.ToString();
    //}

    public override bool DecodeAttr(int AttrNum, ref int Idx, byte[] b)
    {
        switch (AttrNum)
        {
            case 1:
                Network_Topology = Getbyte(ref Idx, b);
                return true;
            case 2:
                Network_Status = Getbyte(ref Idx, b);
                return true;
            case 3:
                Active_Supervisor_IPAddress = GetIPAddress(ref Idx, b).ToString();
                return true;
            case 4:
                Active_Supervisor_PhysicalAddress = GetPhysicalAddress(ref Idx, b).ToString();
                return true;
            case 5:
                Capability_Flag = GetUInt32(ref Idx, b);
                return true;
        }
        return false;
    }

    public override bool EncodeAttr(int AttrNum, ref int Idx, byte[] b)
    {
        switch (AttrNum)
        {
            case 1:
                if (Network_Topology == null) return false;
                Setbyte(ref Idx, b, Network_Topology);
                return true;
            case 2:
                if (Network_Status == null) return false;
                Setbyte(ref Idx, b, Network_Status);
                return true;
            case 3:
                if (Active_Supervisor_IPAddress == null) return false;
                SetIPAddress(ref Idx, b, System.Net.IPAddress.Parse(Active_Supervisor_IPAddress));
                return true;
            case 4:
                if (Active_Supervisor_PhysicalAddress == null) return false;
                SetPhysicalAddress(ref Idx, b, PhysicalAddress.Parse(Active_Supervisor_PhysicalAddress));
                return true;
            case 5:
                if (Capability_Flag == null) return false;
                SetUInt32(ref Idx, b, Capability_Flag);
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
