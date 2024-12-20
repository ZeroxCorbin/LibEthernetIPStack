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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace LibEthernetIPStack.CIP;

public class CIPAttributId : Attribute
{
    public int Id;
    public string Name;
    public CIPAttributId(int Id, string name = "") { this.Id = Id; Name = name; }
}
// base class used into the propertyGrid container to displays decoded members
// also used to decode rawdata
[TypeConverter(typeof(ExpandableObjectConverter))]
[JsonObject(MemberSerialization.OptOut)]
public abstract class CIPObject
{
    [CIPAttributId(1, "Remain Undecoded Bytes")]
    public byte[] Remain_Undecoded_Bytes { get; set; } // if name changes, remember to modify GetProperties method also !

    protected int FilteredAttribut = -1;
    protected int AttIdMax;

    public void FilterAttribut(int AttId) => FilteredAttribut = AttId;

    public virtual bool DecodeAttr(int AttrNum, ref int Idx, byte[] b) => false;
    public virtual bool EncodeAttr(int AttrNum, ref int Idx, byte[] b) => false;

    public virtual byte[] EncodeInstance() => null;

    public bool SetRawBytes(byte[] b)
    {
        int Idx = 0;

        for (int i = 1; i < AttIdMax + 1; i++)
            _ = DecodeAttr(i, ref Idx, b);

        if (Idx < b.Length)
        {
            Remain_Undecoded_Bytes = new byte[b.Length - Idx];
            Array.Copy(b, Idx, Remain_Undecoded_Bytes, 0, Remain_Undecoded_Bytes.Length);
        }
        return true;
    }

    public bool GetRawBytes(byte[] b)
    {
        int Idx = 0;

        for (int i = 1; i < AttIdMax + 1; i++)
            _ = EncodeAttr(i, ref Idx, b);
        if (Remain_Undecoded_Bytes != null)
            Array.Copy(Remain_Undecoded_Bytes, 0, b, Idx, Remain_Undecoded_Bytes.Length);

        return true;
    }

    public bool GetRawBytes(byte[] b, ref int Idx)
    {
        for (int i = 1; i < AttIdMax + 1; i++)
            _ = EncodeAttr(i, ref Idx, b);
        if (Remain_Undecoded_Bytes != null)
            Array.Copy(Remain_Undecoded_Bytes, 0, b, Idx, Remain_Undecoded_Bytes.Length);
        return true;
    }

    public byte[] GetRawBytes()
    {
        var b = new byte[1024];
        int Idx = 0;

        for (int i = 1; i < AttIdMax + 1; i++)
            _ = EncodeAttr(i, ref Idx, b);
        if (Remain_Undecoded_Bytes != null)
        {
            Array.Copy(Remain_Undecoded_Bytes, 0, b, Idx, Remain_Undecoded_Bytes.Length);
            Idx += Remain_Undecoded_Bytes.Length;
        }
        return b.Take(Idx).ToArray();
    }

    public bool? Getbool(ref int Idx, byte[] buf)
    {
        if (buf.Length < Idx) return null;
        bool ret = buf[Idx] == 1;
        Idx += 1;
        return ret;
    }
    public void Setbool(ref int Idx, byte[] buf, bool? val)
    {
        if (val == null) return;
        Array.Copy(BitConverter.GetBytes(val.Value), 0, buf, Idx, 1);
        Idx++;
    }
    public byte? Getbyte(ref int Idx, byte[] buf)
    {
        if (buf.Length < Idx) return null;
        byte ret = buf[Idx];
        Idx += 1;
        return ret;
    }
    public void Setbyte(ref int Idx, byte[] buf, byte? val)
    {
        if (val == null) return;
        Array.Copy(BitConverter.GetBytes(val.Value), 0, buf, Idx, 1);
        Idx++;
    }
    public ushort? GetUInt16(ref int Idx, byte[] buf)
    {
        if (buf.Length < Idx + 1) return null;
        ushort ret = BitConverter.ToUInt16(buf, Idx);
        Idx += 2;
        return ret;
    }
    public void SetUInt16(ref int Idx, byte[] buf, ushort? val)
    {
        if (val == null) return;
        Array.Copy(BitConverter.GetBytes(val.Value), 0, buf, Idx, 2);
        Idx += 2;
    }
    public uint? GetUInt32(ref int Idx, byte[] buf)
    {
        if (buf.Length < Idx + 3) return null;
        uint ret = BitConverter.ToUInt32(buf, Idx);
        Idx += 4;
        return ret;
    }
    public void SetUInt32(ref int Idx, byte[] buf, uint? val)
    {
        if (val == null) return;
        Array.Copy(BitConverter.GetBytes(val.Value), 0, buf, Idx, 4);
        Idx += 4;
    }
    public ulong? GetUInt64(ref int Idx, byte[] buf)
    {
        if (buf.Length < Idx + 7) return null;
        ulong ret = BitConverter.ToUInt64(buf, Idx);
        Idx += 8;
        return ret;
    }
    public void SetUInt64(ref int Idx, byte[] buf, ulong? val)
    {
        if (val == null) return;
        Array.Copy(BitConverter.GetBytes(val.Value), 0, buf, Idx, 8);
        Idx += 8;
    }
    public sbyte? Getsbyte(ref int Idx, byte[] buf)
    {
        if (buf.Length < Idx) return null;
        sbyte ret = (sbyte)buf[Idx];
        Idx += 1;
        return ret;
    }
    public void Setsbyte(ref int Idx, byte[] buf, sbyte? val)
    {
        if (val == null) return;
        Array.Copy(BitConverter.GetBytes(val.Value), 0, buf, Idx, 1);
        Idx++;
    }
    public short? GetInt16(ref int Idx, byte[] buf)
    {
        if (buf.Length < Idx + 1) return null;
        short ret = BitConverter.ToInt16(buf, Idx);
        Idx += 2;
        return ret;
    }
    public void SetInt16(ref int Idx, byte[] buf, short? val)
    {
        if (val == null) return;
        Array.Copy(BitConverter.GetBytes(val.Value), 0, buf, Idx, 2);
        Idx += 2;
    }
    public int? GetInt32(ref int Idx, byte[] buf)
    {
        if (buf.Length < Idx + 3) return null;
        int ret = BitConverter.ToInt32(buf, Idx);
        Idx += 4;
        return ret;
    }
    public void SetInt32(ref int Idx, byte[] buf, int? val)
    {
        if (val == null) return;
        Array.Copy(BitConverter.GetBytes(val.Value), 0, buf, Idx, 4);
        Idx += 4;
    }
    public long? GetInt64(ref int Idx, byte[] buf)
    {
        if (buf.Length < Idx + 7) return null;
        long ret = BitConverter.ToInt64(buf, Idx);
        Idx += 8;
        return ret;
    }
    public void SetInt64(ref int Idx, byte[] buf, long? val)
    {
        if (val == null) return;
        Array.Copy(BitConverter.GetBytes(val.Value), 0, buf, Idx, 8);
        Idx += 8;
    }

    public float? GetSingle(ref int Idx, byte[] buf)
    {
        if (buf.Length < Idx + 3) return null;
        float ret = BitConverter.ToSingle(buf, Idx);
        Idx += 4;
        return ret;
    }
    public void SetSingle(ref int Idx, byte[] buf, float? val)
    {
        if (val == null) return;
        Array.Copy(BitConverter.GetBytes(val.Value), 0, buf, Idx, 4);
        Idx += 4;
    }
    public double? GetDouble(ref int Idx, byte[] buf)
    {
        if (buf.Length < Idx + 7) return null;
        double ret = BitConverter.ToDouble(buf, Idx);
        Idx += 8;
        return ret;
    }
    public void SetDouble(ref int Idx, byte[] buf, double? val)
    {
        if (val == null) return;
        Array.Copy(BitConverter.GetBytes(val.Value), 0, buf, Idx, 8);
        Idx += 8;
    }
    public string GetString(ref int Idx, byte[] buf)
    {
        ushort? t = GetUInt16(ref Idx, buf);
        if (t != null && buf.Length >= Idx + t.Value)
        {
            Encoding iso = Encoding.GetEncoding("ISO-8859-1");
            string s = iso.GetString(buf, Idx, t.Value);
            Idx += t.Value;
            return s;
        }
        return null;
    }
    public void SetString(ref int Idx, byte[] buf, string val)
    {
        if (val == null) return;
        Encoding iso = Encoding.GetEncoding("ISO-8859-1");
        byte[] b = iso.GetBytes(val);
        SetUInt16(ref Idx, buf, (ushort)b.Length);
        Array.Copy(b, 0, buf, Idx, b.Length);
        Idx += b.Length;
    }
    public string GetShortString(ref int Idx, byte[] buf)
    {
        byte? t = Getbyte(ref Idx, buf);
        if (t != null && buf.Length >= Idx + t.Value)
        {
            Encoding iso = Encoding.GetEncoding("ISO-8859-1");
            string s = iso.GetString(buf, Idx, t.Value);
            Idx += t.Value;
            return s;
        }
        return null;
    }
    public void SetShortString(ref int Idx, byte[] buf, string val)
    {
        if (val == null) return;
        Encoding iso = Encoding.GetEncoding("ISO-8859-1");
        byte[] b = iso.GetBytes(val);
        if (b.Length > 255) throw new Exception("String too long");
        Setbyte(ref Idx, buf, (byte)b.Length);
        Array.Copy(b, 0, buf, Idx, b.Length);
        Idx += b.Length;
    }
    public IPAddress GetIPAddress(ref int Idx, byte[] buf)
    {
        if (buf.Length < Idx + 3) return null;

        byte[] b_ip = new byte[4]; ;
        Array.Copy(buf, Idx, b_ip, 0, 4);
        Idx += 4;
        Array.Reverse(b_ip);
        return new IPAddress(b_ip);
    }
    public void SetIPAddress(ref int Idx, byte[] buf, IPAddress val)
    {
        if (val == null) return;
        byte[] b_ip = val.GetAddressBytes();
        Array.Reverse(b_ip);
        Array.Copy(b_ip, 0, buf, Idx, 4);
        Idx += 4;
    }

    public PhysicalAddress GetPhysicalAddress(ref int Idx, byte[] buf)
    {
        if (buf.Length < Idx + 5) return null;

        byte[] b_eth = new byte[6]; ;
        Array.Copy(buf, Idx, b_eth, 0, 6);
        Idx += 6;
        //Array.Reverse(b_ip);
        return new PhysicalAddress(b_eth);
    }
    public void SetPhysicalAddress(ref int Idx, byte[] buf, PhysicalAddress val)
    {
        if (val == null) return;
        byte[] b_eth = val.GetAddressBytes();
        //Array.Reverse(b_ip);
        Array.Copy(b_eth, 0, buf, Idx, 6);
        Idx += 6;
    }

    #region CustomTypeDescriptor
    public AttributeCollection GetAttributes() => TypeDescriptor.GetAttributes(this, true);

    public string GetClassName() => TypeDescriptor.GetClassName(this, true);

    public string GetComponentName() => TypeDescriptor.GetComponentName(this, true);

    public TypeConverter GetConverter() => TypeDescriptor.GetConverter(this, true);

    public EventDescriptor GetDefaultEvent() => TypeDescriptor.GetDefaultEvent(this, true);

    public PropertyDescriptor GetDefaultProperty() => TypeDescriptor.GetDefaultProperty(this, true);

    public object GetEditor(Type editorBaseType) => TypeDescriptor.GetEditor(this, editorBaseType, true);

    public EventDescriptorCollection GetEvents(Attribute[] attributes) => TypeDescriptor.GetEvents(this, attributes, true);

    public EventDescriptorCollection GetEvents() => TypeDescriptor.GetEvents(this, true);

    public PropertyDescriptorCollection GetProperties(Attribute[] attributes) => GetProperties();

    public PropertyDescriptorCollection GetProperties()
    {

        PropertyDescriptorCollection props = TypeDescriptor.GetProperties(this, true);

        // For CIP Attribut we get the class and hides all attributs with
        // the wrong Id
        PropertyDescriptor Remain_Undecoded_Bytes_Prop = null;
        if (FilteredAttribut == -1)
        {
            // Reorder the list to shows Remain_Undecoded_Bytes at the last index
            PropertyDescriptor[] reordered = new PropertyDescriptor[props.Count];
            int i = 0;
            foreach (PropertyDescriptor p in props)
                if (p.Name == "Remain_Undecoded_Bytes")
                    Remain_Undecoded_Bytes_Prop = p;
                else
                    reordered[i++] = p;
            reordered[i] = Remain_Undecoded_Bytes_Prop;

            return new PropertyDescriptorCollection(reordered);
        }

        List<PropertyDescriptor> propsfiltered = [];
        foreach (PropertyDescriptor p in props)
        {
            Attribute a = p.Attributes[typeof(CIPAttributId)];
            if (a != null)
                if ((a as CIPAttributId).Id == FilteredAttribut)
                    propsfiltered.Add(p);
                else
                    propsfiltered.Add(p); // leave also all not tagged properties
        }
        return new PropertyDescriptorCollection(propsfiltered.ToArray());
    }

    public object GetPropertyOwner(PropertyDescriptor pd) => this;
    #endregion

}
