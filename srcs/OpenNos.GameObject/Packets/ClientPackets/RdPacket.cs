﻿////<auto-generated <- Codemaid exclusion for now (PacketIndex Order is important for maintenance)

using OpenNos.Core.Serializing;

namespace OpenNos.GameObject.Packets.ClientPackets
{
    [PacketHeader("rd")]
    public class RdPacket : PacketDefinition
    {
        #region Properties        

        [PacketIndex(0)] public short Type { get; set; }

        [PacketIndex(1)] public long CharacterId { get; set; }

        [PacketIndex(2)] public short? Parameter { get; set; }

        #endregion
    }
}