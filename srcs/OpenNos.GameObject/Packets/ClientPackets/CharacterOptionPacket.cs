﻿////<auto-generated <- Codemaid exclusion for now (PacketIndex Order is important for maintenance)

using NosSharp.Enums;
using OpenNos.Core.Serializing;

namespace OpenNos.GameObject.Packets.ClientPackets
{
    [PacketHeader("gop")]
    public class CharacterOptionPacket : PacketDefinition
    {
        [PacketIndex(0)] public CharacterOption Option { get; set; }

        [PacketIndex(1)] public bool IsActive { get; set; }
    }
}