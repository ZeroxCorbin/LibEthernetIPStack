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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using Newtonsoft.Json;

namespace LibEthernetIPStack.ObjectsLibrary
{
    // CIP_MessageRouter_class not required, nothing new than in CIPObjectBaseClass
    public class CIP_MessageRouter_class : CIPObjectBaseClass
    {
        public CIP_MessageRouter_class() { AttIdMax = 4; }
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
    public class CIP_MessageRouter_instance : CIPObject
    {
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public class MessageRouterObjectList
        {
            public ushort? Number { get; set; }
            public ushort[] ClassesId { get; set; }
            public override string ToString() { return ""; }
        }

        [CIPAttributId(1)]
        public MessageRouterObjectList SupportedObjects { get; set; }
        [CIPAttributId(2)]
        public ushort? MaxConnectionsSupported { get; set; }
        [CIPAttributId(3)]
        public ushort? NumberOfCurrentConnections { get; set; }
        [CIPAttributId(4)]
        public ushort[] ActiveConnections { get; set; }

        public CIP_MessageRouter_instance() { AttIdMax = 4; }

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
                    SupportedObjects = new MessageRouterObjectList();
                    SupportedObjects.Number = GetUInt16(ref Idx, b);
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

    }
}
