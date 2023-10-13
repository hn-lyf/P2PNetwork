using System;
using System.Linq;

namespace P2PNetwork
{
    public class ICMPPacket : IPv4Packet
    {
        public ICMPPacket(byte[] Payload)
        {
            Type = 8;
            Code = 0;
            Header.Protocol = EProtocolType.ICMP;
            Header.DSCP = 0;
            this.Data = Payload;
            Header.TotalLength = 8 + Header.IHL * 4 + Payload.Length;
        }

        public ICMPPacket(IPv4Packet Packet) : base(Packet)
        {
            Type = payload[0];
            Code = payload[1];
            Checksum = (payload[2] << 8) | payload[3];
            PingId = (payload[4] << 8) | payload[5];
            PingSeq = (payload[6] << 8) | payload[7];
            Data = new byte[payload.Length - 8];
            Buffer.BlockCopy(payload, 8, Data, 0, payload.Length - 8);
        }

        public int Type;
        public int Code;
        public int Checksum { get; private set; }
        public int PingId; // Seprate process / fixed for windows
        public int PingSeq; // Increase with same process

        public byte[] Data { get; private set; }

        public override byte[] ToBytes()
        {
            payload = new byte[8 + Data.Length];
            Checksum = 0;
            payload[0] = (byte)Type;
            payload[1] = (byte)Code;
            payload[2] = (byte)(Checksum >> 8);
            payload[3] = (byte)(Checksum & 0xFF);
            payload[4] = (byte)(PingId >> 8);
            payload[5] = (byte)(PingId & 0xFF);
            payload[6] = (byte)(PingSeq >> 8);
            payload[7] = (byte)(PingSeq & 0xFF);
            Buffer.BlockCopy(Data, 0, payload, 8, payload.Length - 8);

            long sum = 0;
            int remainder = payload.Length % 2;
            for (int i = 0; i < payload.Length - remainder; i += 2)
                sum += ((payload[i] << 8) & 0xFF00) + (payload[i + 1] & 0xFF);
            if (remainder!=0)
                sum += ((payload.Last() << 8) & 0xFF00) + (0 & 0xFF);

            while ((sum >> 16) != 0)
                sum = (sum & 0xFFFF) + (sum >> 16);
            Checksum = (ushort)(~sum);
            payload[2] = (byte)(Checksum >> 8);
            payload[3] = (byte)(Checksum & 0xFF);
            return base.ToBytes();
        }
    }

}
