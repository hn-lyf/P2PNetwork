using System;
using System.Linq;

namespace P2PNetwork
{
    public enum EProtocolType
    {
        ICMP = 1, //Internet Control Message Protocol
        IGMP = 2, //Internet Group Management Protocol
        TCP = 6, //Transmission Control Protocol
        UDP = 17, //User Datagram Protocol
        ENCAP = 41, //IPv6 encapsulation
        OSPF = 89, //Open Shortest Path First
        SCTP = 132      //Stream Control Transmission Protocol
    }
    public class IPv4Packet
    {
        public IPv4Packet(byte[] Raw) : this(new IPv4Header(Raw))
        {
            payload = new byte[Header.TotalLength - Header.IHL * 4];
            Buffer.BlockCopy(Raw, Header.IHL * 4, payload, 0, Header.TotalLength - Header.IHL * 4);
        }

        public IPv4Packet(IPv4Packet Packet) : this(Packet.Header, Packet.payload)
        {

        }

        protected IPv4Packet() : this(new IPv4Header())
        {

        }
        private IPv4Packet(IPv4Header Header, byte[] Payload) : this(Header)
        {
            this.payload = new byte[Payload.Length];
            Buffer.BlockCopy(Payload, 0, this.payload, 0, Payload.Length);
        }

        private IPv4Packet(IPv4Header Header)
        {
            this.Header = Header;
        }

        protected IPv4Header Header;

        protected byte[] payload;
        public int SourceIPMask
        {
            get { return Header.SourceIPMask; }
        }
        public int SourceIP
        {
            get { return Header.SourceIP; }
            set { Header.SourceIP = value; }
        }
        public string SourceIPText
        {
            get { return Header.SourceIP.ToIP(); }
        }
        public int DestIPMask
        {
            get { return Header.DestIPMask; }
        }
        public int DestIP
        {
            get { return Header.DestIP; }
            set { Header.DestIP = value; }
        }
        public string DestIPText
        {
            get { return Header.DestIP.ToIP(); }
        }
        public int Id
        {
            get { return Header.Id; }
            set { Header.Id = value; }
        }

        public EProtocolType Protocol { get { return Header.Protocol; } }

        public virtual byte[] ToBytes()
        {
            byte[] head = Header.ToBytes();
            byte[] packet = new byte[payload.Length + head.Length];
            Buffer.BlockCopy(head, 0, packet, 0, head.Length);
            Buffer.BlockCopy(payload, 0, packet, head.Length, payload.Length);
            return packet;
        }
    }

    public class IPv4Header
    {
        public IPv4Header()
        {

        }

        public IPv4Header(byte[] Raw)
        {
            DSCP = Raw[1] >> 2;
            ECN = Raw[1] & 0x03;
            TotalLength = (Raw[2] << 8) | Raw[3];
            Id = (Raw[4] << 8) | Raw[5];
            Flags = Raw[6] >> 5;
            FragmentOffset = (Raw[6] & 0x1F) | Raw[7];
            TTL = Raw[8];
            Protocol = (EProtocolType)Raw[9];
            Checksum = (Raw[10] << 8) | Raw[11];
            SourceIPMask = Raw[12] << 8 | Raw[13];
            DestIPMask = Raw[16] << 8 | Raw[17];

            SourceIP = Raw[12] << 24 | Raw[13] << 16 | Raw[14] << 8 | Raw[15];// string.Format("{0}.{1}.{2}.{3}", Raw[12], Raw[13], Raw[14], Raw[15]);
            DestIP = Raw[16] << 24 | Raw[17] << 16 | Raw[18] << 8 | Raw[19];
        }

        public byte[] ToBytes()
        {
            Checksum = 0;
            byte[] header = new byte[IHL * 4];
            header[0] = (byte)((Version << 4) | IHL);
            header[1] = (byte)((DSCP << 2) | ECN);
            header[2] = (byte)(TotalLength >> 8);
            header[3] = (byte)(TotalLength & 0xFF);
            header[4] = (byte)(Id >> 8);
            header[5] = (byte)(Id & 0xFF);
            header[6] = (byte)((Flags << 5) | (FragmentOffset >> 8));
            header[7] = (byte)(FragmentOffset & 0xFF);
            header[8] = (byte)TTL;
            header[9] = (byte)Protocol;
            header[10] = (byte)(Checksum >> 8);
            header[11] = (byte)(Checksum & 0xFF);
            header[12] = (byte)(SourceIP >> 24);
            header[13] = (byte)(SourceIP >> 16 & 0xff);
            header[14] = (byte)(SourceIP >> 8 & 0xff);
            header[15] = (byte)(SourceIP & 255);
            //ip = DestIP.Split('.').Select(int.Parse).ToArray();
            header[16] = (byte)(DestIP >> 24);
            header[17] = (byte)(DestIP >> 16 & 0xff);
            header[18] = (byte)(DestIP >> 8 & 0xff);
            header[19] = (byte)(DestIP & 255);
            long sum = 0;
            for (int i = 0; i < IHL * 4; i += 2)
                sum += ((header[i] << 8) & 0xFF00) + (header[i + 1] & 0xFF);
            while ((sum >> 16) != 0)
                sum = (sum & 0xFFFF) + (sum >> 16);
            Checksum = (ushort)(~sum);
            header[10] = (byte)(Checksum >> 8);
            header[11] = (byte)(Checksum & 0xFF);
            return header;
        }

        public int Version { get { return 4; } }

        public int IHL
        {
            get { return 5; }
        }

        public int DSCP;

        public int ECN;

        public int TotalLength;

        public int Id;

        public int Flags;

        public int FragmentOffset;

        public int TTL = 128;

        public EProtocolType Protocol;

        public int Checksum { get; private set; }

        public int SourceIPMask { get; private set; }
        public int DestIPMask { get; private set; }

        public int SourceIP;

        public int DestIP;

    }
}
