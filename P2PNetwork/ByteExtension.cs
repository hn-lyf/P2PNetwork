using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P2PNetwork
{
    public static class ByteExtension
    {
        public static int ToInt32(this byte[] bytes, int index = 0)
        {
            return (bytes[index] << 24) | (bytes[index + 1] << 16) | (bytes[index + 2] << 8) | bytes[index + 3];
        }
        public static uint ToUInt32(this byte[] bytes, int index = 0)
        {
            return (uint)((bytes[index] << 24) | (bytes[index + 1] << 16) | (bytes[index + 2] << 8) | bytes[index + 3]);
        }
        public unsafe static ulong ToUInt64(this byte[] bytes, int index = 0)
        {
            fixed (byte* ptr = &bytes[index])
            {
                return ((ulong)(*ptr) << 56) | ((ulong)ptr[1] << 48) | ((ulong)ptr[2] << 40) | ((ulong)ptr[3] << 32) | ((ulong)ptr[4] << 24) | ((ulong)ptr[5] << 16) | ((ulong)ptr[6] << 8) | ptr[7];
            }
        }
        public static byte[] ToBytes(this ulong value)
        {
            byte[] array = new byte[8];
            array[7] = (byte)(value & 0xFF);
            array[6] = (byte)((value >> 8) & 0xFF);
            array[5] = (byte)((value >> 16) & 0xFF);
            array[4] = (byte)((value >> 24) & 0xFF);
            array[3] = (byte)((value >> 32) & 0xFF);
            array[2] = (byte)((value >> 40) & 0xFF);
            array[1] = (byte)((value >> 48) & 0xFF);
            array[0] = (byte)((value >> 56) & 0xFF);
            return array;
        }
        public static byte[] ToBytes(this int value)
        {
            byte[] array = new byte[4];
            array[3] = (byte)(value & 0xFF);
            array[2] = (byte)((value >> 8) & 0xFF);
            array[1] = (byte)((value >> 16) & 0xFF);
            array[0] = (byte)((value >> 24) & 0xFF);
            return array;
        }
        public static string ToIP(this int ip)
        {
            return string.Join(".", ip >> 24 & 255, ip >> 16 & 255, ip >> 8 & 255, ip & 255);
        }
        public static string ToIP(this long ip)
        {
            return string.Join(".", ip >> 24 & 255, ip >> 16 & 255, ip >> 8 & 255, ip & 255);
        }
        public static string ToIP(this ulong ip)
        {
            return string.Join(".", ip >> 24 & 255, ip >> 16 & 255, ip >> 8 & 255, ip & 255);
        }
        public static int IPToInt(this string ip)
        {
            return ip.Split('.').Select(byte.Parse).ToArray().ToInt32(); ;
        }
        public static string GetString(this byte[] bytes, Encoding encoding = null)
        {
            return (encoding ?? Encoding.UTF8).GetString(bytes);
        }
    }
}
