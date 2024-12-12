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

namespace LibEthernetIPStack.ObjectsLibrary;

[JsonObject(MemberSerialization.OptOut)]
// Common class attribut : 4-4.1 Class Attributes
public class CIPObjectBaseClass : CIPObject
{
    [CIPAttributId(1, "Revision")]
    public ushort? Revision { get; set; }
    [CIPAttributId(2)]
    public ushort? Max_Instance { get; set; }
    [CIPAttributId(3)]
    public ushort? Number_of_Instances { get; set; }
    [CIPAttributId(4)]
    public ushort? Number_of_Attributes { get; set; }
    [CIPAttributId(4)]
    public ushort[] Optional_Attributes { get; set; }
    [CIPAttributId(5)]
    public ushort? Number_of_Services { get; set; }
    [CIPAttributId(5)]
    public ushort[] Optional_Services { get; set; }
    [CIPAttributId(6)]
    public ushort? Maximum_ID_Number_Class_Attributes { get; set; }
    [CIPAttributId(7)]
    public ushort? Maximum_ID_Number_Instance_Attributes { get; set; }

    private string Name = "Base";
    public CIPObjectBaseClass() => AttIdMax = 7;

    public CIPObjectBaseClass(string Name)
    {
        this.Name = Name;
        AttIdMax = 7;
    }

    //public override string ToString()
    //{
    //    return "CIP_" + Name + "_class";
    //}

    public override bool DecodeAttr(int AttrNum, ref int Idx, byte[] b)
    {
        switch (AttrNum)
        {
            case 1:
                Revision = GetUInt16(ref Idx, b);
                return true;
            case 2:
                Max_Instance = GetUInt16(ref Idx, b);
                return true;
            case 3:
                Number_of_Instances = GetUInt16(ref Idx, b);
                return true;
            case 4:
                Number_of_Attributes = GetUInt16(ref Idx, b);
                if (Number_of_Attributes != null && Number_of_Attributes.Value > 0)
                {
                    Optional_Attributes = new ushort[Number_of_Attributes.Value];
                    for (int i = 0; i < Number_of_Attributes.Value; i++)
                        if (GetUInt16(ref Idx, b).HasValue)
                            Optional_Attributes[i] = GetUInt16(ref Idx, b).Value;
                }
                return true;
            case 5:
                Number_of_Services = GetUInt16(ref Idx, b);
                if (Number_of_Services != null && Number_of_Services.Value > 0)
                {
                    Optional_Services = new ushort[Number_of_Services.Value];
                    for (int i = 0; i < Number_of_Services.Value; i++)
                        if (GetUInt16(ref Idx, b).HasValue)
                            Optional_Services[i] = GetUInt16(ref Idx, b).Value;
                }
                return true;
            case 6:
                Maximum_ID_Number_Class_Attributes = GetUInt16(ref Idx, b);
                return true;
            case 7:
                Maximum_ID_Number_Instance_Attributes = GetUInt16(ref Idx, b);
                return true;
        }

        return false;
    }
}

// Only used to fill the Remain_Undecoded_Bytes
public class CIPBaseUserDecoder : CIPObject
{
    public override string ToString() => GetType().Name;
    protected void FinishDecode(int Idx, byte[] b)
    {
        if (b.Length - Idx > 0)
        {
            Remain_Undecoded_Bytes = new byte[b.Length - Idx];
            for (int i = 0; i < Remain_Undecoded_Bytes.Length; i++)
                Remain_Undecoded_Bytes[i] = b[Idx++];
        }
    }
    protected void FinishEncode(int Idx, byte[] b)
    {
        if (Idx < b.Length)
            Array.Copy(Remain_Undecoded_Bytes, 0, b, Idx, Remain_Undecoded_Bytes.Length);
    }
}

// Only used for Attribut decoding
public class CIPUInt16Array : CIPBaseUserDecoder
{
    public ushort[] UINT { get; set; }

    public override bool DecodeAttr(int AttrNum, ref int Idx, byte[] b)
    {
        UINT = new ushort[b.Length >> 1];

        for (int i = 0; i < UINT.Length; i++)
            UINT[i] = GetUInt16(ref Idx, b).Value;

        Idx = UINT.Length * 2;
        FinishDecode(Idx, b);
        return true;
    }
}
