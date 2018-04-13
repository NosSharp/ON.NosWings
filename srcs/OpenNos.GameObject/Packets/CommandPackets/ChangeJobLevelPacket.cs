﻿////<auto-generated <- Codemaid exclusion for now (PacketIndex Order is important for maintenance)

using NosSharp.Enums;
using OpenNos.Core.Serializing;

namespace OpenNos.GameObject.Packets.CommandPackets
{
    [PacketHeader("$JLvl", PassNonParseablePacket = true, Authority = AuthorityType.GameMaster)]
    public class ChangeJobLevelPacket : PacketDefinition
    {
        #region Properties

        [PacketIndex(0)] public byte JobLevel { get; set; }

        public static string ReturnHelp()
        {
            return "$JLvl JOBLEVEL";
        }

        #endregion
    }
}