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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using Newtonsoft.Json;

namespace LibEthernetIPStack.ObjectsLibrary
{
    // CIP_Identity_class not required, nothing new than in CIPObjectBaseClass
    // but implemented here to show how it should be done if additional attribut are present
    [JsonObject(MemberSerialization.OptOut)]
    public class CIP_Identity_class : CIPObjectBaseClass
    {
        public CIP_Identity_class() { AttIdMax = 7; }
        //public override string ToString()
        //{
        //    return "class Identity";
        //}
        public override bool DecodeAttr(int AttrNum, ref int Idx, byte[] b)
        {
            // base decoding, but should be used only for attribut 1 to 7 and 
            // other decoding for attribut 8 and more
            return base.DecodeAttr(AttrNum, ref Idx, b);
        }
    }
    [JsonObject(MemberSerialization.OptOut)]
    public class CIP_Identity_instance : CIPObject
    {
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public class IdentityRevision
        {
            public byte? Major_Revision { get; set; }
            public byte? Minor_Revision { get; set; }
            public override string ToString() { return ""; }
        }

        [CIPAttributId(1)]
        public ushort? Vendor_ID { get; set; }
        [CIPAttributId(2)]
        public ushort? Device_Type { get; set; }
        [CIPAttributId(3)]
        public ushort? Product_Code { get; set; }
        [CIPAttributId(4)]
        public IdentityRevision Revision { get; set; }
        [CIPAttributId(5)]
        public ushort? Status { get; set; }
        [CIPAttributId(6)]
        public uint? Serial_Number { get; set; }
        [CIPAttributId(7)]
        public string Product_Name { get; set; }


        public CIP_Identity_instance() { AttIdMax = 7; }

        //public override string ToString()
        //{
        //    if (FilteredAttribut == -1)
        //        return "Identity instance";
        //    else
        //        return "Identity instance attribute #" + FilteredAttribut.ToString();
        //}
        public override bool DecodeAttr(int AttrNum, ref int Idx, byte[] b)
        {
            switch (AttrNum)
            {
                case 1:
                    Vendor_ID = GetUInt16(ref Idx, b);
                    return true;
                case 2:
                    Device_Type = GetUInt16(ref Idx, b);
                    return true;
                case 3:
                    Product_Code = GetUInt16(ref Idx, b);
                    return true;
                case 4:
                    Revision = new IdentityRevision
                    {
                        Major_Revision = Getbyte(ref Idx, b),
                        Minor_Revision = Getbyte(ref Idx, b)
                    };
                    return true;
                case 5:
                    Status = GetUInt16(ref Idx, b);
                    return true;
                case 6:
                    Serial_Number = GetUInt32(ref Idx, b);
                    return true;
                case 7:
                    Product_Name = GetShortString(ref Idx, b);
                    return true;
            }

            return false;
        }
    }
}
