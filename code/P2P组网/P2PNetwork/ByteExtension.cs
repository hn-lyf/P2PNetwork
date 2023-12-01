using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P2PNetwork
{
    public static class ByteExtension
    {
        public static byte[] ToBytes(this int value)
        {
            byte[] array = new byte[4];
            array[3] = (byte)(value & 0xFF);
            array[2] = (byte)((value >> 8) & 0xFF);
            array[1] = (byte)((value >> 16) & 0xFF);
            array[0] = (byte)((value >> 24) & 0xFF);
            return array;
        }
        public static uint ToUInt32(this byte[] bytes, int index = 0)
        {
            return (uint)((bytes[index] << 24) | (bytes[index + 1] << 16) | (bytes[index + 2] << 8) | bytes[index + 3]);
        }
        public static int ToInt32(this byte[] bytes, int index = 0)
        {
            return (bytes[index] << 24) | (bytes[index + 1] << 16) | (bytes[index + 2] << 8) | bytes[index + 3];
        }
        public static string ToIP(this int ip)
        {
            return string.Join(".", ip >> 24 & 255, ip >> 16 & 255, ip >> 8 & 255, ip & 255);
        }
    }
}
