﻿////<auto-generated <- Codemaid exclusion for now (PacketIndex Order is important for maintenance)

using OpenNos.Core;
using OpenNos.Core.Serializing;

namespace OpenNos.GameObject.Packets.ClientPackets
{
    [PacketHeader("glist")]
    public class GListPacket : PacketDefinition
    {
        #region Properties

        [PacketIndex(1)] public byte Type { get; set; }

        #endregion
    }
}