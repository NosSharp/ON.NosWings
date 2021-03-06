﻿using OpenNos.Core;
using OpenNos.Core.Handling;
using OpenNos.GameObject.Networking;
using OpenNos.GameObject.Packets.HomePackets;

namespace OpenNos.Handler
{
    public class HomeSystemPacketHandler : IPacketHandler
    {
        private readonly ClientSession _session;
        public HomeSystemPacketHandler(ClientSession session) => _session = session;

        /// <summary>
        ///     This method will handle the
        /// </summary>
        public void SetHome(SetHomePacket packet)
        {
            if (packet == null)
            {
            }

            // if home already exist replace it
        }

        /// <summary>
        ///     This method will handle the unsethome packet
        /// </summary>
        public void UnsetHome(UnsetHomePacket packet)
        {
            if (packet == null)
            {
            }

            // remove home
        }


        /// <summary>
        /// </summary>
        public void Home(HomePacket packet)
        {
            if (packet == null)
            {
                return;
            }

            if (_session.Character.HasShopOpened)
            {
                _session.SendPacket(_session.Character.GenerateSay(Language.Instance.GetMessageFromKey("CLOSE_SHOP"), 11));
                return;
            }

            if (_session.Character.InExchangeOrTrade)
            {
                _session.SendPacket(_session.Character.GenerateSay(Language.Instance.GetMessageFromKey("CLOSE_EXCHANGE"), 11));
            }

            // X = delay to tp (FileConfiguration)
            // Set WaitingForTeleportation flag to true
            // new Task teleport in X milliseconds
        }
    }
}