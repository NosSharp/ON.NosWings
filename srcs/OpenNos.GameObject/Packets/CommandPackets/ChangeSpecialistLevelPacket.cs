﻿////<auto-generated <- Codemaid exclusion for now (PacketIndex Order is important for maintenance)

using NosSharp.Enums;
using OpenNos.Core.Serializing;

namespace OpenNos.GameObject.Packets.CommandPackets
{
    [PacketHeader("$SPLvl", PassNonParseablePacket = true, Authority = AuthorityType.GameMaster)]
    public class ChangeSpecialistLevelPacket : PacketDefinition
    {
        #region Properties

        [PacketIndex(0)] public byte SpecialistLevel { get; set; }

        public static string ReturnHelp()
        {
            return "$SPLvl SPLEVEL";
        }

        #endregion
    }
}