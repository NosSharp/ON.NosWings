﻿////<auto-generated <- Codemaid exclusion for now (PacketIndex Order is important for maintenance)

using OpenNos.Core.Serializing;

namespace OpenNos.GameObject.Packets.ClientPackets
{
    [PacketHeader("mkraid")]
    public class MkraidPacket : PacketDefinition
    {
        #region Properties        

        [PacketIndex(0)] public byte Type { get; set; }

        [PacketIndex(1)] public short Parameter { get; set; }

        #endregion
    }
}