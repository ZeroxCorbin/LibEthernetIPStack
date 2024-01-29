using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibEthernetIPStack
{
    public class Helpers
    {
        public static List<byte> GetPath(UInt16 Class, UInt32 Instance, UInt16? Attribute = null) // EnIPBase.cs:GetPath is wrong
        { // see Apendix C: Data Management: 
            List<byte> lb = new List<byte>();
            lb.AddRange(ItemPath(CipLogicalType.ClassID, Class));
            lb.AddRange(ItemPath(CipLogicalType.InstanceID, Instance)); // instance = 0 -> class level
            if (Attribute != null)
                lb.AddRange(ItemPath(CipLogicalType.AttributeId, Attribute.Value));
            return lb;
        }
        public static List<byte> ItemPath(CipLogicalType lt, UInt32 value)
        {
            List<byte> lb = new List<byte>();
            byte temp = CalcBytes(value); // maximal 32 bytes = 4 -> UInt32 value
            lb.Add((byte)(((byte)CipSegmentTypes.LogicalSegment) | ((byte)(((byte)lt) | ((byte)(temp / 2)))))); // 1,2,4 => 0,1,2   or  LogicalType  or LogicalSegment
            if (temp > 1) // padbyte
                lb.Add(0);
            byte[] xy = BitConverter.GetBytes(value);
            for (int i = 0; i < temp; i++) // add possible smallest representation
                lb.Add(xy[i]);
            return lb;
        }

        //should only be called with positive numbers: https://stackoverflow.com/questions/11650222/minimum-number-of-bytes-that-can-contain-an-integer-value
        public static byte CalcBytes(long value)
        {
            if (value == 0)
                return 1;
            UInt32 bitLength = 0;
            while (value > 0)
            {
                bitLength++;
                value >>= 1;
            }
            return (byte)(Math.Ceiling(bitLength * 1.0 / 8));
        }
        public static string ByteArrayToString(byte[] ba)
        {
            string hex = BitConverter.ToString(ba);
            return hex.Replace("-", " ");
        }
        public static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                try { bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16); }
                catch { }
            return bytes;
        }
        public static byte[] SubArray(byte[] data, int index, int length)
        {
            byte[] result = new byte[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }
    }
}
