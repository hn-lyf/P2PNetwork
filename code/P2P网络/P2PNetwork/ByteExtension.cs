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
        public unsafe static int ToInt32(this ArraySegment<byte> bytes, int index = 0)
        {
            return (bytes[index] << 24) | (bytes[index + 1] << 16) | (bytes[index + 2] << 8) | bytes[index + 3];
        }
        public static ulong ToUInt64(this ArraySegment<byte> bytes, int index = 0)
        {
            return ((ulong)bytes[index++] << 56) | ((ulong)bytes[index++] << 48) | ((ulong)bytes[index++] << 40) | ((ulong)bytes[index++] << 32) | ((ulong)bytes[index++] << 24) | ((ulong)bytes[index++] << 16) | ((ulong)bytes[index++] << 8) | bytes[index++];
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
        public static byte[] ToBytes(this long value)
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
        public static string GetString(this byte[] bytes, Encoding encoding = null)
        {
            return (encoding ?? Encoding.UTF8).GetString(bytes);
        }
    }
}
