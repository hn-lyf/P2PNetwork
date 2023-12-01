using System;
using System.Linq;

namespace P2PNetwork
{
    public class UDPPacket : IPv4Packet
    {
        public UDPPacket(byte[] Data)
        {
            Header.Protocol = EProtocolType.UDP;
            this.Data = new byte[Data.Length];
            Buffer.BlockCopy(Data, 0, this.Data, 0, Data.Length);
        }
        public UDPPacket(IPv4Packet Packet) : base(Packet)
        {
            SourcePort = (payload[0] << 8) | payload[1];
            DestPort = (payload[2] << 8) | payload[3];
            Length = (payload[4] << 8) | payload[5];
            Checksum = (payload[6] << 8) | payload[7];
            Data = new byte[Length - 8];
            Buffer.BlockCopy(payload, 8, Data, 0, Length - 8);
        }

        public int SourcePort;
        public int DestPort;
        public int Length { get; private set; }
        public int Checksum { get; private set; }

        public byte[] Data { get; private set; }

        public override byte[] ToBytes()
        {
            Checksum = 0;
            Header.TotalLength = 8 + Header.IHL * 4 + Data.Length;
            Length = 8 + Data.Length;
            payload = new byte[Length];
            payload[0] = (byte)(SourcePort >> 8);
            payload[1] = (byte)(SourcePort & 0xFF);
            payload[2] = (byte)(DestPort >> 8);
            payload[3] = (byte)(DestPort & 0xFF);
            payload[4] = (byte)(Length >> 8);
            payload[5] = (byte)(Length & 0xFF);
            payload[6] = (byte)(Checksum >> 8);
            payload[7] = (byte)(Checksum & 0xFF);
            Buffer.BlockCopy(Data, 0, payload, 8, payload.Length - 8);

            byte[] pseudo = new byte[12];
            pseudo[0] = (byte)(SourceIP >> 24);
            pseudo[1] = (byte)(SourceIP >> 16 & 0xff);
            pseudo[2] = (byte)(SourceIP >> 8 & 0xff);
            pseudo[3] = (byte)(SourceIP & 255);
            pseudo[4] = (byte)(DestIP >> 24);
            pseudo[5] = (byte)(DestIP >> 16 & 0xff);
            pseudo[6] = (byte)(DestIP >> 8 & 0xff);
            pseudo[7] = (byte)(DestIP & 255);
            pseudo[8] = 0;
            pseudo[9] = (byte)EProtocolType.UDP;
            pseudo[10] = (byte)(Length >> 8);
            pseudo[11] = (byte)(Length & 0xFF);

            long sum = 0;
            for (int i = 0; i < 12; i += 2)
                sum += ((pseudo[i] << 8) & 0xFF00) + (pseudo[i + 1] & 0xFF);
            int remainder = payload.Length % 2;
            for (int i = 0; i < payload.Length - remainder; i += 2)
                sum += ((payload[i] << 8) & 0xFF00) + (payload[i + 1] & 0xFF);
            if (remainder != 0)
                sum += ((payload.Last() << 8) & 0xFF00) + (0 & 0xFF);
            while ((sum >> 16) != 0)
                sum = (sum & 0xFFFF) + (sum >> 16);
            Checksum = (ushort)(~sum);
            payload[6] = (byte)(Checksum >> 8);
            payload[7] = (byte)(Checksum & 0xFF);

            return base.ToBytes();
        }
    }
}
