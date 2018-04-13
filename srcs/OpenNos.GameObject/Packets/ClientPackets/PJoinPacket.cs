﻿////<auto-generated <- Codemaid exclusion for now (PacketIndex Order is important for maintenance)

using NosSharp.Enums;
using OpenNos.Core.Serializing;

namespace OpenNos.GameObject.Packets.ClientPackets
{
    [PacketHeader("pjoin")]
    public class PJoinPacket : PacketDefinition
    {
        #region Properties

        [PacketIndex(0)] public GroupRequestType RequestType { get; set; }

        [PacketIndex(1)] public long CharacterId { get; set; }

        #endregion
    }
}