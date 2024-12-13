/**************************************************************************
*                           MIT License
* 
* Copyright (C) 2016 Frederic Chaxel <fchaxel@free.fr>
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
using System;
using System.Text;

namespace LibEthernetIPStack.Base;

// Volume 1 : C-1.4.2 Logical Segment
// Remember for 16 bits address : (0x21 or 0x25 or 0x31) - 0x00 - 0xPF - 0xpf
// also a pad 0x00 must be set for 32 bits address. No supported here.
public static class EnIPPath
{
    private static void Fit(byte[] path, ref int offset, ushort value, byte code)
    {
        if (value > 255)
        {
            path[offset] = (byte)(code | 0x1);
            path[offset + 2] = (byte)(value & 0xFF);
            path[offset + 3] = (byte)((value & 0xFF00) >> 8);
            offset += 4;
        }
        else
        {
            path[offset] = code;
            path[offset + 1] = (byte)(value & 0xFF);
            offset += 2;
        }
    }

    public static byte[] GetPath(ushort? Class, ushort Instance, ushort? Attribut = null, bool IsConnectionPoint = false)
    {

        byte[] path = new byte[12];

        int size = 0;

        if (Class != null)
            Fit(path, ref size, Class.Value, 0x20);

        // It seems that this Instance value is always required : 0 is used to access class data
        // Volume 1 : Figure 1-2.5 Instance #0 Example
        if (IsConnectionPoint)
            Fit(path, ref size, Instance, 0x2C); // sure it's not the good way to encode 2C instead of 24            
        else
            Fit(path, ref size, Instance, 0x24);

        if (Attribut != null)
            Fit(path, ref size, Attribut.Value, 0x30);

        byte[] Ret = new byte[size];
        Array.Copy(path, Ret, size);

        return Ret;
    }

    // Given in the form Class.Instance or Class.Instance.Attribut
    // for Class data should be Class.0
    public static byte[] GetPath(string path)
    {
        string[] s = path.Split('.');
        return s.Length == 3
            ? GetPath(Convert.ToUInt16(s[0]), Convert.ToUInt16(s[1]), Convert.ToUInt16(s[2]))
            : s.Length == 2 ? GetPath(Convert.ToUInt16(s[0]), Convert.ToUInt16(s[1]), null) : null;
    }

    // Base on Volume 1 : Figure C-1.3 Port Segment Encoding
    // & Table C-1.2 Port Segment Examples
    // & Volume 2 : 3-3.7 Connection Path
    // IPendPoint in the format x.x.x.x:x, port is optional
    private static byte[] GetExtendedPath(string IPendPoint, byte[] LogicalSeg)
    {
        byte[] PortSegment = Encoding.ASCII.GetBytes(IPendPoint);

        int IPlenght = PortSegment.Length;
        if (IPlenght % 2 != 0) IPlenght++;

        byte[] FullPath = new byte[LogicalSeg.Length + IPlenght + 2];

        // to be FIXED : Port number !
        FullPath[0] = 0x15;
        FullPath[1] = (byte)IPendPoint.Length;
        Array.Copy(PortSegment, 0, FullPath, 2, PortSegment.Length);
        Array.Copy(LogicalSeg, 0, FullPath, 2 + IPlenght, LogicalSeg.Length);

        return FullPath;
    }

    // Add a Data Member to the Path
    public static byte[] AddDataSegment(byte[] ExtendedPath, byte[] Data)
    {
        byte[] FullPath = new byte[Data.Length + ExtendedPath.Length + 2];
        Array.Copy(ExtendedPath, FullPath, ExtendedPath.Length);
        FullPath[ExtendedPath.Length] = 0x80;
        FullPath[ExtendedPath.Length + 1] = (byte)((Data.Length / 2) + (Data.Length % 2));
        Array.Copy(Data, 0, FullPath, ExtendedPath.Length + 2, Data.Length);

        return FullPath;
    }
    public static byte[] GetExtendedPath(string IPendPoint, string LogicalSegment)
    {
        byte[] LogicalSeg = GetPath(LogicalSegment);
        byte[] ExtendedPath = GetExtendedPath(IPendPoint, LogicalSeg);
        return ExtendedPath;
    }
    public static byte[] GetExtendedPath(string IPAdress, ushort Class, ushort Instance, ushort? Attribut = null)
    {
        byte[] LogicalSeg = GetPath(Class, Instance, Attribut);
        byte[] ExtendedPath = GetExtendedPath(IPAdress, LogicalSeg);
        return ExtendedPath;
    }

    public static string GetPath(byte[] path)
    {
        StringBuilder sb = new();

        int i = 0;
        do
        {
            if (i != 0) _ = sb.Append('.');
            // Missing 32 bits elements
            if ((path[i] & 3) == 1)
            {

                sb = sb.Append(((path[i + 2] << 8) | path[i + 3]).ToString());
                i += 4;
            }
            else
            {
                sb = sb.Append(path[i + 1].ToString());
                i += 2;
            }
        } while (i < path.Length);

        return sb.ToString();
    }
}
