﻿////<auto-generated <- Codemaid exclusion for now (PacketIndex Order is important for maintenance)

using OpenNos.Core;
using OpenNos.Core.Serializing;

namespace OpenNos.GameObject.Packets.ClientPackets
{
    [PacketHeader("req_info")]
    public class ReqInfoPacket : PacketDefinition
    {
        #region Properties

        [PacketIndex(0)] public byte Type { get; set; }

        [PacketIndex(1)] public long TargetVNum { get; set; }

        [PacketIndex(2)] public int? MateVNum { get; set; }

        #endregion
    }
}