﻿////<auto-generated <- Codemaid exclusion for now (PacketIndex Order is important for maintenance)
using OpenNos.Core;
using OpenNos.Domain;

namespace OpenNos.GameObject
{
    [PacketHeader("$Mute", PassNonParseablePacket = true, Authority = AuthorityType.GameMaster)]
    public class MutePacket : PacketDefinition
    {
        #region Properties

        [PacketIndex(0)]
        public string CharacterName { get; set; }

        [PacketIndex(1)]
        public int Duration { get; set; }

        [PacketIndex(2, SerializeToEnd = true)]
        public string Reason { get; set; }

        public override string ToString()
        {
            return $"Mute Command CharacterName: {CharacterName} Duration: {Duration} Reason: {Reason}";
        }

        #endregion
    }
}