/*
 * This file is part of the OpenNos Emulator Project. See AUTHORS file for Copyright information
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 */

using OpenNos.Core;
using OpenNos.DAL;
using OpenNos.Data;
using OpenNos.Domain;
using OpenNos.GameObject;
using OpenNos.GameObject.CommandPackets;
using OpenNos.GameObject.Helpers;
using OpenNos.Master.Library.Client;
using OpenNos.GameObject.Networking;
using OpenNos.Master.Library.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using OpenNos.GameObject.Packets.CommandPackets;

namespace OpenNos.Handler
{
    public class CommandPacketHandler : IPacketHandler
    {
        #region Instantiation

        public CommandPacketHandler(ClientSession session) => Session = session;

        #endregion

        #region Properties

        private ClientSession Session { get; }

        #endregion

        #region Methods

        public void Benchmark(BenchmarkPacket benchmarkPacket)
        {
            if (benchmarkPacket != null)
            {
                double totalMiliseconds = 0;
                switch (benchmarkPacket.Test)
                {
                    case 1:
                        {
                            Session.SendPacket(Session.Character.GenerateSay($"=== TEST: Receive Object from MS ===", 12));
                            Stopwatch sw = Stopwatch.StartNew();
                            for (int i = 0; i < benchmarkPacket.Iterations; i++)
                            {
                                ConfigurationServiceClient.Instance.GetConfigurationObject();
                            }
                            sw.Stop();
                            totalMiliseconds = sw.Elapsed.TotalMilliseconds;
                        }
                        break;

                    case 2:
                        {
                            ConfigurationObject conf = ConfigurationServiceClient.Instance.GetConfigurationObject();
                            Session.SendPacket(Session.Character.GenerateSay($"=== TEST: Send Object to MS ===", 12));
                            Stopwatch sw = Stopwatch.StartNew();
                            for (int i = 0; i < benchmarkPacket.Iterations; i++)
                            {
                                ConfigurationServiceClient.Instance.UpdateConfigurationObject(conf);
                            }
                            sw.Stop();
                            totalMiliseconds = sw.Elapsed.TotalMilliseconds;
                        }
                        break;

                    default:
                        Session.SendPacket(Session.Character.GenerateSay(BenchmarkPacket.ReturnHelp(), 10));
                        return;
                }

                Session.SendPacket(Session.Character.GenerateSay($"The test with {benchmarkPacket.Iterations} iterations took {totalMiliseconds} ms", 12));
                Session.SendPacket(Session.Character.GenerateSay($"The each iteration took {((totalMiliseconds * 1000000) / benchmarkPacket.Iterations).ToString("0.00 ns")}", 12));
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(BenchmarkPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Buy Packet
        /// </summary>
        /// <param name="BuyPacket"></param>
        public void Buy(GameObject.CommandPackets.BuyPacket buypacket)
        {
            try //Not needed but safe is safe
            {
                if (buypacket != null)
                {
                    if (buypacket.Amount <= 1020)
                    {
                        if (buypacket.Item != null && buypacket.Amount != 0)
                        {

                            int Leftover = buypacket.Amount % 255;
                            int FulLStacks = buypacket.Amount / 255;
                            short BuyVNum = 0;

                            switch (buypacket.Item.ToUpper())
                            {
                                case "CELLA":
                                    BuyVNum = 1014;
                                    break;
                                case "FULLI":
                                    BuyVNum = 1244;
                                    break;
                                default:
                                    return;
                            }

                            Item iteminfo = ServerManager.GetItem(BuyVNum);
                            if (Session.Character.Gold >= buypacket.Amount * iteminfo.Price)
                            {
                                for (int i = 1; i <= FulLStacks; i++)
                                {
                                    ItemInstance inv = Session.Character.Inventory.AddNewToInventory(BuyVNum, 255).FirstOrDefault();
                                    if (inv == null)
                                    {
                                        Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("NOT_ENOUGH_PLACE"), 0));
                                    }
                                    else
                                    {
                                        Session.SendPacket(Session.Character.GenerateSay($"{Language.Instance.GetMessageFromKey("ITEM_ACQUIRED")}: {iteminfo.Name} x {255}", 12));
                                        Session.Character.Gold -= 255 * inv.Item.Price;
                                    }
                                }

                                if (Leftover > 0)
                                {
                                    ItemInstance inv = Session.Character.Inventory.AddNewToInventory(BuyVNum, (byte)Leftover).FirstOrDefault();
                                    if (inv == null)
                                    {
                                        Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("NOT_ENOUGH_PLACE"), 0));
                                    }
                                    else
                                    {
                                        Session.SendPacket(Session.Character.GenerateSay($"{Language.Instance.GetMessageFromKey("ITEM_ACQUIRED")}: {iteminfo.Name} x {Leftover}", 12));
                                        Session.Character.Gold -= Leftover * inv.Item.Price;
                                    }
                                }
                            }
                            else
                            {
                                Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("NOT_ENOUGH_MONEY"), 0));
                            }
                            Session.SendPacket(Session.Character.GenerateGold());
                        }
                    }
                }
            }
            catch
            {
                //Put Errorlog here
                return;
            }

        }

        /// $Prestige
        /// </summary>
        /// <param name="prestigePacket"></param>
        public void Prestige(PrestigePacket PrestigePacket)
        {
            if (PrestigePacket != null)
            {
                if (Session.Character.Level == 99
                    && Session.Character.HeroLevel == 50
                    && Session.Character.Inventory.CountItemInAnInventory(InventoryType.Wear) == 0)
                {
                    Session.Character.prestigeLevel += 1;
                    Session.Character.Save();
                    CommunicationServiceClient.Instance.SendMessageToCharacter(new SCSCharacterMessage
                    {
                        DestinationCharacterId = null,
                        SourceCharacterId = Session.Character.CharacterId,
                        SourceWorldId = ServerManager.Instance.WorldId,
                        Message = $"Player {Session.Character.Name} Prestigio to + {Session.Character.PrestigeLevl}",
                        Type = MessageType.Shout
                    });
                    Session.Character.GiftAdd(5422, 5);
                    Session.Character.Level = 1;
                    Session.Character.JobLevel = 1;
                    Session.Character.HeroLevel = 1;
                    Session.Character.GenerateLev();
                    ServerManager.Instance.ChangeMapInstance(Session.Character.CharacterId,
                        Session.Character.MapInstanceId, Session.Character.PositionX, Session.Character.PositionY,
                        true);
                    Session.SendPacket(StaticPacketHelper.Cancel(2));
                }
                else
                {
                    Session.SendPacket(Session.Character.GenerateSay("Debes desequipar todos tus item y ser levl 99 H50", 15));
                }
            }
        }

        /// <summary>
        /// $GetPrestige
        /// </summary>
        /// <param name="GetExpPacket"></param>
       /* public void GetPrestige(GetPrestigePacket packet)
        {
            Session.SendPacket(Session.Character.GenerateSay("=======Level Exp=======", 15));
            Session.SendPacket(Session.Character.GenerateSay("Current XP: " + Session.Character.PrestigeXp, 15));
            Session.SendPacket(Session.Character.GenerateSay("Required XP: " + Session.Character.PrestigeXPLoad(), 15));
            Session.SendPacket(Session.Character.GenerateSay("Current Percentage: " + String.Format("{0:0.00}", Session.Character.prestigeLevel / Session.Character.PrestigeXPLoad() * 100D), 15));
            Session.SendPacket(Session.Character.GenerateSay("=======================", 15));

        }*/


        /// <summary>
        /// $GetExp
        /// </summary>
        /// <param name="GetExpPacket"></param>
        public void GetExp(GetExpPacket packet)
        {
            Session.SendPacket(Session.Character.GenerateSay("=======Level Exp=======", 15));
            Session.SendPacket(Session.Character.GenerateSay("Current XP: " + Session.Character.LevelXp, 15));
            Session.SendPacket(Session.Character.GenerateSay("Required XP: " + Session.Character.XpLoad(), 15));         
            Session.SendPacket(Session.Character.GenerateSay("Current Percentage: " + String.Format("{0:0.00}", Session.Character.LevelXp / Session.Character.XpLoad() * 100D), 15));         
            Session.SendPacket(Session.Character.GenerateSay("=======================", 15));

        }

        /// <summary>
        /// $CreateRaid
        /// </summary>
        /// <param name="createRaidPacket"></param>
        public void CreateRaid(CreateRaidPacket createRaidPacket)
        {
            if (createRaidPacket != null)
            {
                if (!int.TryParse(createRaidPacket.FactionType, out int faction))
                {
                    return;
                }
                if (!int.TryParse(createRaidPacket.RaidType, out int raidType))
                {
                    return;
                }
                switch (faction)
                {
                    case 1:
                        ServerManager.Instance.Act4AngelStat.Percentage = 10001;
                        ServerManager.Instance.Act4AngelStat.Mode = 1;
                        break;
                    case 2:
                        ServerManager.Instance.Act4DemonStat.Percentage = 10001;
                        ServerManager.Instance.Act4DemonStat.Mode = 1;
                        break;
                    default:
                        break;
                }
                if (createRaidPacket.RaidType != null)
                {
                    ServerManager.Instance.RaidType = raidType;
                }
            }
        }

        /// <summary>
        /// $Act6Percent
        /// </summary>
        /// <param name="packet"></param>
       /* public void Act6Percent(Act6RaidPacket packet)
        {
            if (string.IsNullOrEmpty(packet?.Name))
            {
                Session.SendPacket(Session.Character.GenerateSay("$Act6Percent Name [Percent]", 11));
                Session.SendPacket(Session.Character.GenerateSay("(Percent is optionnal)", 11));
                return;
            }
            switch (packet.Name)
            {
                case "Erenia":
                case "erenia":
                    ServerManager.Instance.Act6Erenia.Percentage = (short)(packet.Percent.HasValue ? packet.Percent * 10 : 1000);
                    ServerManager.Instance.Act6Process();
                    Session.SendPacket(Session.Character.GenerateSay("Done !", 11));
                    break;
                case "Zenas":
                case "zenas":
                    ServerManager.Instance.Act6Zenas.Percentage = (short)(packet.Percent.HasValue ? packet.Percent * 10 : 1000);
                    ServerManager.Instance.Act6Process();
                    Session.SendPacket(Session.Character.GenerateSay("Done !", 11));
                    break;
            }
        }*/


        /// <summary>
        /// $MapStat Command
        /// </summary>
        /// <param name="mapStatPacket"></param>
        public void MapStats(MapStatisticsPacket mapStatPacket)
        {
            // lower the boilerplate
            void SendMapStats(MapDTO map, GameObject.MapInstance mapInstance)
            {
                if (map != null && mapInstance != null)
                {
                    Session.SendPacket(Session.Character.GenerateSay("-------------MapData-------------", 10));
                    Session.SendPacket(Session.Character.GenerateSay(
                        $"MapId: {map.MapId}\n" +
                        $"MapMusic: {map.Music}\n" +
                        $"MapName: {map.Name}\n" +
                        $"MapShopAllowed: {map.ShopAllowed}", 10));
                    Session.SendPacket(Session.Character.GenerateSay("---------------------------------", 10));
                    Session.SendPacket(Session.Character.GenerateSay("---------MapInstanceData---------", 10));
                    Session.SendPacket(Session.Character.GenerateSay(
                        $"MapInstanceId: {mapInstance.MapInstanceId}\n" +
                        $"MapInstanceType: {mapInstance.MapInstanceType}\n" +
                        $"MapMonsterCount: {mapInstance.Monsters.Count}\n" +
                        $"MapNpcCount: {mapInstance.Npcs.Count}\n" +
                        $"MapPortalsCount: {mapInstance.Portals.Count}\n" +
                        $"MapInstanceUserShopCount: {mapInstance.UserShops.Count}\n" +
                        $"SessionCount: {mapInstance.Sessions.Count()}\n" +
                        $"MapInstanceXpRate: {mapInstance.XpRate}\n" +
                        $"MapInstanceDropRate: {mapInstance.DropRate}\n" +
                        $"MapInstanceMusic: {mapInstance.InstanceMusic}\n" +
                        $"ShopsAllowed: {mapInstance.ShopAllowed}\n" +
                        $"IsPVP: {mapInstance.IsPVP}\n" +
                        $"IsSleeping: {mapInstance.IsSleeping}\n" +
                        $"Dance: {mapInstance.IsDancing}", 10));
                    Session.SendPacket(Session.Character.GenerateSay("---------------------------------", 10));
                }
            }

            if (mapStatPacket != null)
            {
                if (mapStatPacket.MapId.HasValue)
                {
                    MapDTO map = DAOFactory.MapDAO.LoadById(mapStatPacket.MapId.Value);
                    GameObject.MapInstance mapInstance = ServerManager.GetMapInstanceByMapId(mapStatPacket.MapId.Value);
                    if (map != null && mapInstance != null)
                    {
                        SendMapStats(map, mapInstance);
                    }
                }
                else if (Session.HasCurrentMapInstance)
                {
                    MapDTO map = DAOFactory.MapDAO.LoadById(Session.CurrentMapInstance.Map.MapId);
                    if (map != null)
                    {
                        SendMapStats(map, Session.CurrentMapInstance);
                    }
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(MapStatisticsPacket.ReturnHelp(), 10));
            }
        }

        public void Act4Stat(Act4StatPacket packet)
        {
            if (packet != null && ServerManager.Instance.ChannelId == 51)
            {
                switch (packet.Faction)
                {
                    case 1:
                        ServerManager.Instance.Act4AngelStat.Percentage = packet.Value;
                        break;

                    case 2:
                        ServerManager.Instance.Act4DemonStat.Percentage = packet.Value;
                        break;
                }

                Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(AddMonsterPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Nosville Command
        /// </summary>
        /// <param name="WarpPacket"></param>
        public void Warp(WarpPacket warppacket)
        {
            if (warppacket.Data != null)
            {
                if (Session.Character.MapId > 153 || Session.Character.MapId < 130)
                {
                    string Location = warppacket.Data;
                    if (Session.Character.HasShopOpened || Session.Character.InExchangeOrTrade)
                    {
                        Session.Character.Dispose();
                    }
                    if (Session.Character.IsChangingMapInstance)
                    {
                        return;
                    }

                    switch (Location.ToUpper())
                    {
                        case "ICE":
                            ServerManager.Instance.ChangeMap(Session.Character.CharacterId, 145, 51, 41);
                            break;
                        case "HOME":
                            ServerManager.Instance.ChangeMap(Session.Character.CharacterId, 1, 60, 60);
                            break;
                        case "LDT":
                            ServerManager.Instance.ChangeMap(Session.Character.CharacterId, 98, 6, 35);
                            break;
                        case "Act5":
                            ServerManager.Instance.ChangeMap(Session.Character.CharacterId, 205, 5, 5);
                            break;
                    }
                }
            }
        }


        /// <summary>
        /// $StuffPack
        /// </summary>
        /// <param name="stuffPackPacket"></param>
        public void StuffPack(StuffPackPacket stuffPackPacket)
        {
            if (stuffPackPacket != null)
            {
                switch (stuffPackPacket.Type)
                {
                    case "Archer":
                    case "archer":
                        Session.Character.GiftAdd(4986, 1, rare: 8, upgrade: 10); // HERO 50 ARMOR
                        Session.Character.GiftAdd(4983, 1, rare: 8, upgrade: 10); // HERO 50 WEAPON
                        Session.Character.GiftAdd(4980, 1, rare: 8, upgrade: 10); // HERO 50 SECOND WEAPON
                        Session.Character.GiftAdd(903, 1, 5, 0, 15);
                        Session.Character.GiftAdd(904, 1, 0, 0, 15);
                        Session.Character.GiftAdd(911, 1, 0, 0, 15);
                        Session.Character.GiftAdd(912, 1, 0, 0, 15);
                        Session.Character.GiftAdd(4501, 1, 0, 0, 15);
                        Session.Character.GiftAdd(4498, 1, 0, 0, 15);
                        Session.Character.GiftAdd(4492, 1, 0, 0, 15);
                        Session.Character.GiftAdd(4488, 1, 0, 0, 15);
                        break;
                    case "Mage":
                    case "mage":
                        Session.Character.GiftAdd(4985, 1, rare: 8, upgrade: 10); // HERO 50 ARMOR
                        Session.Character.GiftAdd(4982, 1, rare: 8, upgrade: 10); // HERO 50 WEAPON
                        Session.Character.GiftAdd(4979, 1, rare: 8, upgrade: 10); // HERO 50 SECOND WEAPON
                        Session.Character.GiftAdd(905, 1, 0, 15, 15);
                        Session.Character.GiftAdd(906, 1, 0, 15, 15);
                        Session.Character.GiftAdd(913, 1, 0, 15, 15);
                        Session.Character.GiftAdd(914, 1, 0, 15, 15);
                        Session.Character.GiftAdd(4502, 1, 0, 15, 15);
                        Session.Character.GiftAdd(4499, 1, 0, 15, 15);
                        Session.Character.GiftAdd(4491, 1, 0, 15, 15);
                        Session.Character.GiftAdd(4487, 1, 0, 15, 15);
                        break;
                    case "Sword":
                    case "sword":
                        Session.Character.GiftAdd(4984, 1, 0, 10, 8); // HERO 50 ARMOR
                        Session.Character.GiftAdd(4981, 1, 0, 10, 8); // HERO 50 WEAPON
                        Session.Character.GiftAdd(4978, 1, 0, 10, 8); // HERO 50 SECOND WEAPON
                        Session.Character.GiftAdd(901, 1, 0, 15, 15);
                        Session.Character.GiftAdd(902, 1, 0, 15, 15);
                        Session.Character.GiftAdd(909, 1, 0, 15, 15);
                        Session.Character.GiftAdd(910, 1, 0, 15, 15);
                        Session.Character.GiftAdd(4500, 1, 0, 15, 15);
                        Session.Character.GiftAdd(4497, 1, 0, 15, 15);
                        Session.Character.GiftAdd(4493, 1, 0, 15, 15);
                        Session.Character.GiftAdd(4489, 1, 0, 15, 15);
                        break;
                    case "Mount":
                    case "mount":
                        Session.Character.GiftAdd(5196, 1); // Nossi
                        Session.Character.GiftAdd(5330, 1); // Soucoupe
                        Session.Character.GiftAdd(5360, 1); // Planche à voile
                        Session.Character.GiftAdd(5997, 1); // dragon of bones
                        Session.Character.GiftAdd(5834, 1); // Magic Jaguar
                        break;
                    case "Resists":
                    case "resists":
                        break;
                    case "Fairy":
                    case "fairy":
                        Session.Character.GiftAdd(4713, 1);
                        Session.Character.GiftAdd(4714, 1);
                        Session.Character.GiftAdd(4715, 1);
                        Session.Character.GiftAdd(4716, 1);
                        break;
                    default:
                        Session.SendPacket(Session.Character.GenerateSay("Use : \"Archer\", \"Sword\" \"Mage\" or \"Mount\"", 10));
                        break;
                }
            }
        }

        /// <summary>
        /// $Character
        /// </summary>
        /// <param name="characterPacket"></param>
        public void CharacterUpdater(CharacterPacket characterPacket)
        {
            Logger.Debug("CharacterPacket Command", Session.Character.GenerateIdentity());
            if (characterPacket != null)
            {
                ClientSession session = Session;
                if (ServerManager.Instance.GetSessionByCharacterName(characterPacket.Name) != null)
                {
                    session = ServerManager.Instance.GetSessionByCharacterName(characterPacket.Name);
                }
                switch (characterPacket.Property)
                {
                    case "-h":
                        Session.SendPacket("$Character [Type] [Value] <PlayerName>");
                        Session.SendPacket("[Types] : Name Level JobLevel HeroLevel");
                        Session.SendPacket("[Types] : Gold Compliment Reputation Gender");
                        Session.SendPacket("[Types] : Dignity Stats Class");
                        break;
                    case "Name":
                        if (DAOFactory.CharacterDAO.LoadByName(characterPacket.Value) != null) return;
                        CharacterDTO character = session.Character;
                        character.Name = characterPacket.Value;
                        DAOFactory.CharacterDAO.InsertOrUpdate(ref character);
                        ServerManager.Instance.ChangeMap(session.Character.CharacterId);
                        break;
                    case "Level":
                        ChangeCharacterLevel(session, byte.Parse(characterPacket.Value), 1);
                        break;
                    case "JobLevel":
                        ChangeCharacterLevel(session, byte.Parse(characterPacket.Value), 2);
                        break;
                    case "HeroLevel":
                        ChangeCharacterLevel(session, byte.Parse(characterPacket.Value), 3);
                        break;
                    case "Gold":
                        ChangeGold(session, long.Parse(characterPacket.Value));
                        break;
                    case "Compliment":
                        ChangeCompliment(session, short.Parse(characterPacket.Value));
                        Session.SendPacket(
                            UserInterfaceHelper.GenerateMsg(
                                Language.Instance.GetMessageFromKey("COMPLIMENT_CHANGED"), 0));
                        break;
                    case "Reputation":
                        ChangeCharacterReputation(session, long.Parse(characterPacket.Value));
                        Session.SendPacket(
                            UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("REP_CHANGED"),
                                0));
                        break;
                    case "Gender":
                        session.Character.ChangeSex();
                        Session.SendPacket(
                            UserInterfaceHelper.GenerateMsg(
                                Language.Instance.GetMessageFromKey("GENDER_CHANGED"), 12));
                        break;
                    case "Dignity":
                        session.Character.Dignity = long.Parse(characterPacket.Value) > 100 ? 100 : long.Parse(characterPacket.Value);
                        session.SendPacket(Session.Character.GenerateFd());
                        session.CurrentMapInstance?.Broadcast(session, session.Character.GenerateIn(),
                            ReceiverType.AllExceptMe);
                        session.CurrentMapInstance?.Broadcast(session, session.Character.GenerateGidx(),
                            ReceiverType.AllExceptMe);
                        Session.SendPacket(
                            UserInterfaceHelper.GenerateMsg(
                                Language.Instance.GetMessageFromKey("DIGNITY_CHANGED"), 12));
                        break;
                    case "Stats":
                        if (DAOFactory.CharacterDAO.LoadByName(characterPacket.Name) != null)
                        {
                            CharacterDTO characterDto = DAOFactory.CharacterDAO.LoadByName(characterPacket.Name);
                            sendStats(characterDto);
                        }
                        break;
                    case "Class":
                        switch (characterPacket.Value)
                        {
                            case "0":
                            case "Adventurer":
                                session.Character.ChangeClass(ClassType.Adventurer);
                                break;
                            case "1":
                            case "Swordman":
                                session.Character.ChangeClass(ClassType.Swordman);
                                break;
                            case "2":
                            case "Archer":
                                session.Character.ChangeClass(ClassType.Archer);
                                break;
                            case "3":
                            case "Mage":
                                session.Character.ChangeClass(ClassType.Magician);
                                break;
                        }
                        break;
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay("$Character [Property] [Value] <PlayerName>", 10));
            }
        }

        private void ChangeCharacterReputation(ClientSession session, long reput)
        {
            if (session == null || reput < 0) return;
            session.Character.Reputation = reput;
            session.SendPacket(session.Character.GenerateFd());
            session.CurrentMapInstance?.Broadcast(session, session.Character.GenerateIn(), ReceiverType.AllExceptMe);
            session.CurrentMapInstance?.Broadcast(session, session.Character.GenerateGidx(), ReceiverType.AllExceptMe);
        }

        private void ChangeCompliment(ClientSession session, short compliment)
        {
            if (session == null || compliment < 0) return;
            session.Character.Compliment = compliment;
            ServerManager.Instance.ChangeMap(session.Character.CharacterId);

        }

        private void ChangeGold(ClientSession session, long gold)
        {
            if (session == null || gold < 0) return;
            long maxGold = ServerManager.Instance.MaxGold;
            gold = gold > maxGold ? maxGold : gold;
            if (gold >= 0)
            {
                session.Character.Gold = gold;
                Session.SendPacket(
                    UserInterfaceHelper.GenerateMsg(
                        Language.Instance.GetMessageFromKey("GOLD_SET"), 0));
                session.SendPacket(Session.Character.GenerateGold());
            }
            else
            {
                Session.SendPacket(
                    UserInterfaceHelper.GenerateMsg(
                        Language.Instance.GetMessageFromKey("WRONG_VALUE"), 0));
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="session"></param>
        /// <param name="level"></param>
        /// <param name="type"></param>
        private void ChangeCharacterLevel(ClientSession session, byte level, byte type)
        {
            if (session == null || level <= 0) return;
            switch (type)
            {
                case 1:
                    session.Character.Level = level;
                    session.Character.LevelXp = 0;
                    Session.SendPacket(
                        UserInterfaceHelper.GenerateMsg(
                            Language.Instance.GetMessageFromKey("LEVEL_CHANGED"), 0));
                    session.SendPacket(session.Character.GenerateLev());
                    session.SendPacket(session.Character.GenerateStatInfo());
                    session.SendPacket(session.Character.GenerateStatChar());
                    session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateIn(),
                        ReceiverType.AllExceptMe);
                    session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateGidx(),
                        ReceiverType.AllExceptMe);
                    session.CurrentMapInstance?.Broadcast(StaticPacketHelper.GenerateEff(UserType.Player, session.Character.CharacterId, 6), session.Character.MapX, session.Character.MapY);
                    session.CurrentMapInstance?.Broadcast(StaticPacketHelper.GenerateEff(UserType.Player, session.Character.CharacterId, 198), session.Character.MapX, session.Character.MapY);
                    break;
                case 2:
                    if ((session.Character.Class == 0 && level <= 20 || session.Character.Class != 0 && level <= 255) &&
                        level > 0)
                    {
                        session.Character.JobLevel = level;
                        session.Character.JobLevelXp = 0;
                        session.Character.Skills.GetAndClearAllItems();
                        session.SendPacket(Session.Character.GenerateLev());
                        session.SendPacket(
                            UserInterfaceHelper.GenerateMsg(
                                Language.Instance.GetMessageFromKey("JOBLEVEL_CHANGED"), 0));
                        session.CurrentMapInstance?.Broadcast(session, session.Character.GenerateIn(),
                            ReceiverType.AllExceptMe);
                        session.CurrentMapInstance?.Broadcast(session, session.Character.GenerateGidx(),
                            ReceiverType.AllExceptMe);
                        session.CurrentMapInstance?.Broadcast(StaticPacketHelper.GenerateEff(UserType.Player, session.Character.CharacterId, 8), session.Character.MapX, session.Character.MapY);

                        session.Character.Skills[(short)(200 + 20 * (byte)session.Character.Class)] =
                            new CharacterSkill
                            {
                                SkillVNum = (short)(200 + 20 * (byte)session.Character.Class),
                                CharacterId = session.Character.CharacterId
                            };
                        session.Character.Skills[(short)(201 + 20 * (byte)session.Character.Class)] =
                            new CharacterSkill
                            {
                                SkillVNum = (short)(201 + 20 * (byte)session.Character.Class),
                                CharacterId = session.Character.CharacterId
                            };
                        session.Character.Skills[236] = new CharacterSkill
                        {
                            SkillVNum = 236,
                            CharacterId = session.Character.CharacterId
                        };
                        if (!session.Character.UseSp)
                        {
                            session.SendPacket(session.Character.GenerateSki());
                        }
                        session.Character.LearnAdventurerSkill();
                    }
                    break;
                case 3:
                    {
                        session.Character.HeroLevel = level;
                        session.Character.HeroXp = 0;
                        Session.SendPacket(
                            UserInterfaceHelper.GenerateMsg(
                                Language.Instance.GetMessageFromKey("HEROLEVEL_CHANGED"), 0));
                        session.SendPacket(session.Character.GenerateLev());
                        session.SendPacket(session.Character.GenerateStatInfo());
                        session.SendPacket(session.Character.GenerateStatChar());
                        session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateIn(),
                            ReceiverType.AllExceptMe);
                        session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateGidx(),
                            ReceiverType.AllExceptMe);
                        session.CurrentMapInstance?.Broadcast(StaticPacketHelper.GenerateEff(UserType.Player, session.Character.CharacterId, 6), session.Character.MapX, session.Character.MapY);
                        session.CurrentMapInstance?.Broadcast(StaticPacketHelper.GenerateEff(UserType.Player, session.Character.CharacterId, 198), session.Character.MapX, session.Character.MapY);

                    }
                    break;
                case 4:
                    {
                        session.Character.prestigeLevel = level;
                        session.Character.PrestigeXp = 0;
                        Session.SendPacket(
                            UserInterfaceHelper.GenerateMsg(
                                Language.Instance.GetMessageFromKey("PRESTIGELEVEL_CHANGED"), 0));
                        session.SendPacket(session.Character.GenerateLev());
                        session.SendPacket(session.Character.GenerateStatInfo());
                        session.SendPacket(session.Character.GenerateStatChar());
                        session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateIn(),
                            ReceiverType.AllExceptMe);
                        session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateGidx(),
                            ReceiverType.AllExceptMe);
                        session.CurrentMapInstance?.Broadcast(StaticPacketHelper.GenerateEff(UserType.Player, session.Character.CharacterId, 6), session.Character.MapX, session.Character.MapY);
                        session.CurrentMapInstance?.Broadcast(StaticPacketHelper.GenerateEff(UserType.Player, session.Character.CharacterId, 198), session.Character.MapX, session.Character.MapY);

                    }
                    break;
            }
        }

        /// <summary>
        /// $AddMonster Command
        /// </summary>
        /// <param name="targetInfoPacket"></param>
        public void TargetInfo(TargetInfoPacket targetInfoPacket)
        {
            long id = Session.Character.LastTargetId;
            long type = Session.Character.LastTargetType;

            Session.SendPacket(Session.Character.GenerateSay("Id: " + id, 11));
            Session.SendPacket(Session.Character.GenerateSay("Type: " + type, 11));

            switch (type)
            {
                case 2:
                    MapNpcDTO mapNpc = DAOFactory.MapNpcDAO.LoadById((int)id);
                    if (mapNpc == null)
                    {
                        Session.SendPacket(Session.Character.GenerateSay("NPC not found.", 10));
                    }

                    Session.SendPacket(Session.Character.GenerateSay("VNum: " + mapNpc.NpcVNum, 11));
                    break;
                default:
                    Session.SendPacket(Session.Character.GenerateSay("Unhandled Type!!", 10));
                    break;
            }
        }

        /// <summary>
        /// $AddMonster Command
        /// </summary>
        /// <param name="addMonsterPacket"></param>
        public void AddMonster(AddMonsterPacket addMonsterPacket)
        {
            if (addMonsterPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[AddMonster]NpcMonsterVNum: {addMonsterPacket.MonsterVNum} IsMoving: {addMonsterPacket.IsMoving}");

                if (!Session.HasCurrentMapInstance)
                {
                    return;
                }
                NpcMonster npcmonster = ServerManager.GetNpc(addMonsterPacket.MonsterVNum);
                if (npcmonster == null)
                {
                    return;
                }
                MapMonsterDTO monst = new MapMonsterDTO
                {
                    MonsterVNum = addMonsterPacket.MonsterVNum,
                    MapY = Session.Character.PositionY,
                    MapX = Session.Character.PositionX,
                    MapId = Session.Character.MapInstance.Map.MapId,
                    Position = Session.Character.Direction,
                    IsMoving = addMonsterPacket.IsMoving,
                    MapMonsterId = Session.CurrentMapInstance.GetNextMonsterId()
                };

                if (!DAOFactory.MapMonsterDAO.DoesMonsterExist(monst.MapMonsterId))
                {
                    if (DAOFactory.MapMonsterDAO.Insert(monst) != null)
                    {
                        MapMonster monster = new MapMonster();
                        monster.IsDisabled = monst.IsDisabled;
                        monster.IsMoving = monst.IsMoving;
                        monster.MapId = monst.MapId;
                        monster.MapMonsterId = monst.MapMonsterId;
                        monster.MapX = monst.MapX;
                        monster.MapY = monst.MapY;
                        monster.MonsterVNum = monst.MonsterVNum;
                        monster.Position = monst.Position;

                        monster.Initialize(Session.CurrentMapInstance);
                        Session.CurrentMapInstance?.AddMonster(monster);
                        Session.CurrentMapInstance?.Broadcast(monster.GenerateIn());
                    }
                }
                Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(AddMonsterPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $AddPartner Command
        /// </summary>
        /// <param name="addPartnerPacket"></param>
        public void AddPartner(AddPartnerPacket addPartnerPacket)
        {
            if (addPartnerPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[AddPartner]NpcMonsterVNum: {addPartnerPacket.MonsterVNum} Level: {addPartnerPacket.Level}");

                addMate(addPartnerPacket.MonsterVNum, addPartnerPacket.Level, MateType.Partner);
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(AddPartnerPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $AddPet Command
        /// </summary>
        /// <param name="addPetPacket"></param>
        public void AddPet(AddPetPacket addPetPacket)
        {
            if (addPetPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[AddPet]NpcMonsterVNum: {addPetPacket.MonsterVNum} Level: {addPetPacket.Level}");

                addMate(addPetPacket.MonsterVNum, addPetPacket.Level, MateType.Pet);
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(AddPartnerPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $AddPortal Command
        /// </summary>
        /// <param name="addPortalPacket"></param>
        public void AddPortal(AddPortalPacket addPortalPacket)
        {
            if (addPortalPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[AddPortal]DestinationMapId: {addPortalPacket.DestinationMapId} DestinationMapX: {addPortalPacket.DestinationX} DestinationY: {addPortalPacket.DestinationY}");

                addPortal(addPortalPacket.DestinationMapId, addPortalPacket.DestinationX, addPortalPacket.DestinationY, addPortalPacket.PortalType == null ? (short)-1 : (short)addPortalPacket.PortalType, true);
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(AddPortalPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Add
        /// 
        /// 
        /// 
        /// Effect Command
        /// </summary>
        /// <param name="addShellEffectPacket"></param>
        public void AddShellEffect(AddShellEffectPacket addShellEffectPacket)
        {
            if (addShellEffectPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[AddShellEffect]Slot: {addShellEffectPacket.Slot} EffectLevel: {addShellEffectPacket.EffectLevel} Effect: {addShellEffectPacket.Effect} Value: {addShellEffectPacket.Value}");
                try
                {
                    ItemInstance instance = Session.Character.Inventory.LoadBySlotAndType(addShellEffectPacket.Slot, InventoryType.Equipment);
                    if (instance != null)
                    {
                        instance.ShellEffects.Add(new ShellEffectDTO() { EffectLevel = (ShellEffectLevelType)addShellEffectPacket.EffectLevel, Effect = addShellEffectPacket.Effect, Value = addShellEffectPacket.Value, EquipmentSerialId = instance.EquipmentSerialId });
                    }
                }
                catch
                {
                    Session.SendPacket(Session.Character.GenerateSay(AddShellEffectPacket.ReturnHelp(), 10));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(AddShellEffectPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $AddSkill Command
        /// </summary>
        /// <param name="addSkillPacket"></param>
        public void AddSkill(AddSkillPacket addSkillPacket)
        {
            if (addSkillPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(),
                    $"[AddSkill]SkillVNum: {addSkillPacket.SkillVnum}");

                short skillVNum = addSkillPacket.SkillVnum;
                Skill skillinfo = ServerManager.GetSkill(skillVNum);
                if (skillinfo == null)
                {
                    Session.SendPacket(
                        Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("SKILL_DOES_NOT_EXIST"), 11));
                    return;
                }

                if (skillinfo.SkillVNum < 200)
                {
                    foreach (CharacterSkill skill in Session.Character.Skills.GetAllItems())
                    {
                        if (skillinfo.CastId == skill.Skill.CastId && skill.Skill.SkillVNum < 200)
                        {
                            Session.Character.Skills.Remove(skill.SkillVNum);
                        }
                    }
                }
                else
                {
                    if (Session.Character.Skills.ContainsKey(skillVNum))
                    {
                        Session.SendPacket(
                            Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("SKILL_ALREADY_EXIST"),
                                11));
                        return;
                    }

                    if (skillinfo.UpgradeSkill != 0)
                    {
                        CharacterSkill oldupgrade = Session.Character.Skills.FirstOrDefault(s =>
                            s.Skill.UpgradeSkill == skillinfo.UpgradeSkill
                            && s.Skill.UpgradeType == skillinfo.UpgradeType && s.Skill.UpgradeSkill != 0);
                        if (oldupgrade != null)
                        {
                            Session.Character.Skills.Remove(oldupgrade.SkillVNum);
                        }
                    }
                }

                Session.Character.Skills[skillVNum] = new CharacterSkill
                {
                    SkillVNum = skillVNum,
                    CharacterId = Session.Character.CharacterId
                };
                Session.SendPacket(Session.Character.GenerateSki());
                Session.SendPackets(Session.Character.GenerateQuicklist());
                Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("SKILL_LEARNED"),
                    0));
                Session.SendPacket(Session.Character.GenerateLev());
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(AddSkillPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $ArenaWinner Command
        /// </summary>
        /// <param name="arenaWinner"></param>
        public void ArenaWinner(ArenaWinnerPacket arenaWinner)
        {
            Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[ArenaWinner]");
            if (Session.Account.Authority == AuthorityType.GameMaster)
            { 

            Session.Character.ArenaWinner = Session.Character.ArenaWinner == 0 ? 1 : 0;
            Session.CurrentMapInstance?.Broadcast(Session.Character.GenerateCMode());
            Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
            }

            else           
            {
                if (ServerManager.Instance.TopComplimented.Take(10).Any(s => s.CharacterId.Equals(Session.Character.CharacterId)) || Session.Character.ArenaWinner==1)
                {
                    Session.Character.ArenaWinnerTemp = Session.Character.ArenaWinnerTemp == 0 ? (byte)1 : (byte)0;
                    Session.CurrentMapInstance?.Broadcast(Session.Character.GenerateCMode());
                    Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
                     }
                 }
             }


/// <summary>
/// $Ban Command
/// </summary>
/// <param name="banPacket"></param>
public void Ban(BanPacket banPacket)
        {
            if (banPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Ban]CharacterName: {banPacket.CharacterName} Reason: {banPacket.Reason} Until: {(banPacket.Duration == 0 ? DateTime.Now.AddYears(15) : DateTime.Now.AddDays(banPacket.Duration))}");
                BanMethod(banPacket.CharacterName, banPacket.Duration, banPacket.Reason);
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(BanPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Ban Command
        /// </summary>
        /// <param name="bankPacket"></param>
       /* public void BankManagement(BankPacket bankPacket)
        {
            if (bankPacket != null && !Session.Character.InExchangeOrTrade)
            {
                switch (bankPacket.Mode?.ToLower())
                {
                    case "balance":
                        {
                            Logger.LogEvent("BANK", $"[{Session.GenerateIdentity()}][Balance]Balance: {Session.Character.GoldBank}");
                            Session.SendPacket(Session.Character.GenerateSay($"Current Balance: {Session.Character.GoldBank} Gold.", 10));
                            return;
                        }
                    case "deposit":
                        {
                            if (bankPacket.Param1 != null && (long.TryParse(bankPacket.Param1, out long amount) || string.Equals(bankPacket.Param1, "all", StringComparison.OrdinalIgnoreCase)))
                            {
                                if (string.Equals(bankPacket.Param1, "all", StringComparison.OrdinalIgnoreCase) && Session.Character.Gold > 0)
                                {
                                    Logger.LogEvent("BANK", $"[{Session.GenerateIdentity()}][Deposit]Amount: {Session.Character.Gold} OldBank: {Session.Character.GoldBank} NewBank: {Session.Character.GoldBank + Session.Character.Gold}");
                                    Session.SendPacket(Session.Character.GenerateSay($"Deposited ALL({Session.Character.Gold}) Gold.", 10));
                                    Session.Character.GoldBank += Session.Character.Gold;
                                    Session.Character.Gold = 0;
                                    Session.SendPacket(Session.Character.GenerateGold());
                                    Session.SendPacket(Session.Character.GenerateSay($"New Balance: {Session.Character.GoldBank} Gold.", 10));
                                }
                                else if (amount <= Session.Character.Gold && Session.Character.Gold > 0)
                                {
                                    if (amount < 1)
                                    {
                                        Logger.LogEvent("BANK", $"[{Session.GenerateIdentity()}][Illegal]Mode: {bankPacket.Mode} Param1: {bankPacket.Param1} Param2: {bankPacket.Param2}");
                                        Session.SendPacket(Session.Character.GenerateSay("I'm afraid I can't let you do that. This incident has been logged.", 10));
                                    }
                                    else
                                    {
                                        Logger.LogEvent("BANK", $"[{Session.GenerateIdentity()}][Deposit]Amount: {amount} OldBank: {Session.Character.GoldBank} NewBank: {Session.Character.GoldBank + amount}");
                                        Session.SendPacket(Session.Character.GenerateSay($"Deposited {amount} Gold.", 10));
                                        Session.Character.GoldBank += amount;
                                        Session.Character.Gold -= amount;
                                        Session.SendPacket(Session.Character.GenerateGold());
                                        Session.SendPacket(Session.Character.GenerateSay($"New Balance: {Session.Character.GoldBank} Gold.", 10));
                                    }
                                }
                            }
                            return;
                        }
                    case "withdraw":
                        {
                            if (bankPacket.Param1 != null && long.TryParse(bankPacket.Param1, out long amount) && amount <= Session.Character.GoldBank && Session.Character.GoldBank > 0 && (Session.Character.Gold + amount) <= ServerManager.Instance.Configuration.MaxGold)
                            {
                                if (amount < 1)
                                {
                                    Logger.LogEvent("BANK", $"[{Session.GenerateIdentity()}][Illegal]Mode: {bankPacket.Mode} Param1: {bankPacket.Param1} Param2: {bankPacket.Param2}");

                                    Session.SendPacket(Session.Character.GenerateSay("I'm afraid I can't let you do that. This incident has been logged.", 10));
                                }
                                else
                                {
                                    Logger.LogEvent("BANK", $"[{Session.GenerateIdentity()}][Withdraw]Amount: {amount} OldBank: {Session.Character.GoldBank} NewBank: {Session.Character.GoldBank - amount}");

                                    Session.SendPacket(Session.Character.GenerateSay($"Withdrawn {amount} Gold.", 10));
                                    Session.Character.GoldBank -= amount;
                                    Session.Character.Gold += amount;
                                    Session.SendPacket(Session.Character.GenerateGold());
                                    Session.SendPacket(Session.Character.GenerateSay($"New Balance: {Session.Character.GoldBank} Gold.", 10));
                                }
                            }
                            return;
                        }
                    case "send":
                        {
                            if (bankPacket.Param1 != null)
                            {
                                long amount = bankPacket.Param2;
                                ClientSession receiver = ServerManager.Instance.GetSessionByCharacterName(bankPacket.Param1);
                                if (amount <= Session.Character.GoldBank && Session.Character.GoldBank > 0 && receiver != null)
                                {
                                    if (amount < 1)
                                    {
                                        Logger.LogEvent("BANK", $"[{Session.GenerateIdentity()}][Illegal]Mode: {bankPacket.Mode} Param1: {bankPacket.Param1} Param2: {bankPacket.Param2}");

                                        Session.SendPacket(Session.Character.GenerateSay("I'm afraid I can't let you do that. This incident has been logged.", 10));
                                    }
                                    else
                                    {
                                        Logger.LogEvent("BANK", $"[{Session.GenerateIdentity()}][Send]Amount: {amount} OldBankSender: {Session.Character.GoldBank} NewBankSender: {Session.Character.GoldBank - amount} OldBankReceiver: {receiver.Character.GoldBank} NewBankReceiver: {receiver.Character.GoldBank + amount}");

                                        Session.SendPacket(Session.Character.GenerateSay($"Sent {amount} Gold to {receiver.Character.Name}", 10));
                                        receiver.SendPacket(Session.Character.GenerateSay($"Received {amount} Gold from {Session.Character.Name}", 10));
                                        Session.Character.GoldBank -= amount;
                                        receiver.Character.GoldBank += amount;
                                        Session.SendPacket(Session.Character.GenerateSay($"New Balance: {Session.Character.GoldBank} Gold.", 10));
                                        receiver.SendPacket(Session.Character.GenerateSay($"New Balance: {receiver.Character.GoldBank} Gold.", 10));
                                    }
                                }
                            }
                            return;
                        }
                    default:
                        {
                            Session.SendPacket(Session.Character.GenerateSay(BankPacket.ReturnHelp(), 10));
                            return;
                        }
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(BankPacket.ReturnHelp(), 10));
            }
        }*/

        /// <summary>
        /// $BlockExp Command
        /// </summary>
        /// <param name="blockExpPacket"></param>
        public void BlockExp(BlockExpPacket blockExpPacket)
        {
            if (blockExpPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(),
                    $"[BlockExp]CharacterName: {blockExpPacket.CharacterName} Reason: {blockExpPacket.Reason} Until: {DateTime.Now.AddMinutes(blockExpPacket.Duration)}");

                if (blockExpPacket.Duration == 0)
                {
                    blockExpPacket.Duration = 60;
                }

                blockExpPacket.Reason = blockExpPacket.Reason?.Trim();
                CharacterDTO character = DAOFactory.CharacterDAO.LoadByName(blockExpPacket.CharacterName);
                if (character != null)
                {
                    ClientSession session =
                        ServerManager.Instance.Sessions.FirstOrDefault(s =>
                            s.Character?.Name == blockExpPacket.CharacterName);
                    session?.SendPacket(blockExpPacket.Duration == 1
                        ? UserInterfaceHelper.GenerateInfo(
                            string.Format(Language.Instance.GetMessageFromKey("MUTED_SINGULAR"), blockExpPacket.Reason))
                        : UserInterfaceHelper.GenerateInfo(string.Format(
                            Language.Instance.GetMessageFromKey("MUTED_PLURAL"), blockExpPacket.Reason,
                            blockExpPacket.Duration)));
                    PenaltyLogDTO log = new PenaltyLogDTO
                    {
                        AccountId = character.AccountId,
                        Reason = blockExpPacket.Reason,
                        Penalty = PenaltyType.BlockExp,
                        DateStart = DateTime.Now,
                        DateEnd = DateTime.Now.AddMinutes(blockExpPacket.Duration),
                        AdminName = Session.Character.Name
                    };
                    Character.InsertOrUpdatePenalty(log);
                    Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
                }
                else
                {
                    Session.SendPacket(
                        Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("USER_NOT_FOUND"), 10));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(BlockExpPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $BlockFExp Command
        /// </summary>
        /// <param name="blockFExpPacket"></param>
        public void BlockFExp(BlockFExpPacket blockFExpPacket)
        {
            if (blockFExpPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(),
                    $"[BlockFExp]CharacterName: {blockFExpPacket.CharacterName} Reason: {blockFExpPacket.Reason} Until: {DateTime.Now.AddMinutes(blockFExpPacket.Duration)}");

                if (blockFExpPacket.Duration == 0)
                {
                    blockFExpPacket.Duration = 60;
                }

                blockFExpPacket.Reason = blockFExpPacket.Reason?.Trim();
                CharacterDTO character = DAOFactory.CharacterDAO.LoadByName(blockFExpPacket.CharacterName);
                if (character != null)
                {
                    ClientSession session =
                        ServerManager.Instance.Sessions.FirstOrDefault(s =>
                            s.Character?.Name == blockFExpPacket.CharacterName);
                    session?.SendPacket(blockFExpPacket.Duration == 1
                        ? UserInterfaceHelper.GenerateInfo(
                            string.Format(Language.Instance.GetMessageFromKey("MUTED_SINGULAR"),
                                blockFExpPacket.Reason))
                        : UserInterfaceHelper.GenerateInfo(string.Format(
                            Language.Instance.GetMessageFromKey("MUTED_PLURAL"), blockFExpPacket.Reason,
                            blockFExpPacket.Duration)));
                    PenaltyLogDTO log = new PenaltyLogDTO
                    {
                        AccountId = character.AccountId,
                        Reason = blockFExpPacket.Reason,
                        Penalty = PenaltyType.BlockFExp,
                        DateStart = DateTime.Now,
                        DateEnd = DateTime.Now.AddMinutes(blockFExpPacket.Duration),
                        AdminName = Session.Character.Name
                    };
                    Character.InsertOrUpdatePenalty(log);
                    Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
                }
                else
                {
                    Session.SendPacket(
                        Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("USER_NOT_FOUND"), 10));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(BlockFExpPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $BlockPM Command
        /// </summary>
        /// <param name="blockPmPacket"></param>
        public void BlockPm(BlockPMPacket blockPmPacket)
        {
            Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), "[BlockPM]");

            if (!Session.Character.GmPvtBlock)
            {
                Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("GM_BLOCK_ENABLE"),
                    10));
                Session.Character.GmPvtBlock = true;
            }
            else
            {
                Session.SendPacket(
                    Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("GM_BLOCK_DISABLE"), 10));
                Session.Character.GmPvtBlock = false;
            }
        }

        /// <summary>
        /// $BlockRep Command
        /// </summary>
        /// <param name="blockRepPacket"></param>
        public void BlockRep(BlockRepPacket blockRepPacket)
        {
            if (blockRepPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(),
                    $"[BlockRep]CharacterName: {blockRepPacket.CharacterName} Reason: {blockRepPacket.Reason} Until: {DateTime.Now.AddMinutes(blockRepPacket.Duration)}");

                if (blockRepPacket.Duration == 0)
                {
                    blockRepPacket.Duration = 60;
                }

                blockRepPacket.Reason = blockRepPacket.Reason?.Trim();
                CharacterDTO character = DAOFactory.CharacterDAO.LoadByName(blockRepPacket.CharacterName);
                if (character != null)
                {
                    ClientSession session =
                        ServerManager.Instance.Sessions.FirstOrDefault(s =>
                            s.Character?.Name == blockRepPacket.CharacterName);
                    session?.SendPacket(blockRepPacket.Duration == 1
                        ? UserInterfaceHelper.GenerateInfo(
                            string.Format(Language.Instance.GetMessageFromKey("MUTED_SINGULAR"), blockRepPacket.Reason))
                        : UserInterfaceHelper.GenerateInfo(string.Format(
                            Language.Instance.GetMessageFromKey("MUTED_PLURAL"), blockRepPacket.Reason,
                            blockRepPacket.Duration)));
                    PenaltyLogDTO log = new PenaltyLogDTO
                    {
                        AccountId = character.AccountId,
                        Reason = blockRepPacket.Reason,
                        Penalty = PenaltyType.BlockRep,
                        DateStart = DateTime.Now,
                        DateEnd = DateTime.Now.AddMinutes(blockRepPacket.Duration),
                        AdminName = Session.Character.Name
                    };
                    Character.InsertOrUpdatePenalty(log);
                    Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
                }
                else
                {
                    Session.SendPacket(
                        Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("USER_NOT_FOUND"), 10));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(BlockRepPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        ///     $MuteMap Command
        /// </summary>
        /// <param name="muteMapPacket"></param>
        public void MuteMap(MuteMapPacket muteMapPacket)
        {
            if (Session.CurrentMapInstance == null)
            {
                return;
            }

            ServerManager.Shout($"!Todo el mapa ha sido muteado, gracias!");
            Session.CurrentMapInstance.IsMute = !Session.CurrentMapInstance.IsMute;
        }

        /// <summary>
        /// $Buff packet
        /// </summary>
        /// <param name="buffPacket"></param>
        public void Buff(BuffPacket buffPacket)
        {
            if (buffPacket != null)
            {
                Buff buff = new Buff(buffPacket.CardId, buffPacket.Level ?? (byte)1);
                Session.Character.AddBuff(buff);
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(BuffPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $ChangeClass Command
        /// </summary>
        /// <param name="changeClassPacket"></param>
        public void ChangeClass(ChangeClassPacket changeClassPacket)
        {
            if (changeClassPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[ChangeClass]Class: {changeClassPacket.ClassType}");

                Session.Character.ChangeClass(changeClassPacket.ClassType);
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(ChangeClassPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $ChangeDignity Command
        /// </summary>
        /// <param name="changeDignityPacket"></param>
        public void ChangeDignity(ChangeDignityPacket changeDignityPacket)
        {
            if (changeDignityPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[ChangeDignity]Dignity: {changeDignityPacket.Dignity}");

                if (changeDignityPacket.Dignity >= -1000 && changeDignityPacket.Dignity <= 100)
                {
                    Session.Character.Dignity = changeDignityPacket.Dignity;
                    Session.SendPacket(Session.Character.GenerateFd());
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("DIGNITY_CHANGED"), 12));
                    Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateIn(), ReceiverType.AllExceptMe);
                    Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateGidx(), ReceiverType.AllExceptMe);
                }
                else
                {
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("BAD_DIGNITY"), 11));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(ChangeDignityPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $FLvl Command
        /// </summary>
        /// <param name="changeFairyLevelPacket"></param>
        public void ChangeFairyLevel(ChangeFairyLevelPacket changeFairyLevelPacket)
        {
            ItemInstance fairy = Session.Character.Inventory.LoadBySlotAndType((byte)EquipmentType.Fairy, InventoryType.Wear);
            if (changeFairyLevelPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[FLvl]FairyLevel: {changeFairyLevelPacket.FairyLevel}");

                if (fairy != null)
                {
                    short fairylevel = changeFairyLevelPacket.FairyLevel;
                    fairylevel -= fairy.Item.ElementRate;
                    fairy.ElementRate = fairylevel;
                    fairy.XP = 0;
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(string.Format(Language.Instance.GetMessageFromKey("FAIRY_LEVEL_CHANGED"), fairy.Item.Name), 10));
                    Session.SendPacket(Session.Character.GeneratePairy());
                }
                else
                {
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("NO_FAIRY"), 10));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(ChangeFairyLevelPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $ChangeSex Command
        /// </summary>
        /// <param name="changeSexPacket"></param>
        public void ChangeGender(ChangeSexPacket changeSexPacket)
        {
            Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), "[ChangeSex]");
            Session.Character.ChangeSex();
        }

        /// <summary>
        /// $HeroLvl Command
        /// </summary>
        /// <param name="changeHeroLevelPacket"></param>
        public void ChangeHeroLevel(ChangeHeroLevelPacket changeHeroLevelPacket)
        {
            if (changeHeroLevelPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[HeroLvl]HeroLevel: {changeHeroLevelPacket.HeroLevel}");

                if (changeHeroLevelPacket.HeroLevel <= 255)
                {
                    Session.Character.HeroLevel = changeHeroLevelPacket.HeroLevel;
                    Session.Character.HeroXp = 0;
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("HEROLEVEL_CHANGED"), 0));
                    Session.SendPacket(Session.Character.GenerateLev());
                    Session.SendPacket(Session.Character.GenerateStatChar());
                    Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateIn(), ReceiverType.AllExceptMe);
                    Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateGidx(), ReceiverType.AllExceptMe);
                    Session.CurrentMapInstance?.Broadcast(StaticPacketHelper.GenerateEff(UserType.Player, Session.Character.CharacterId, 6), Session.Character.PositionX, Session.Character.PositionY);
                    Session.CurrentMapInstance?.Broadcast(StaticPacketHelper.GenerateEff(UserType.Player, Session.Character.CharacterId, 198), Session.Character.PositionX, Session.Character.PositionY);
                }
                else
                {
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("WRONG_VALUE"), 0));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(ChangeHeroLevelPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $PrestigeLvl Command
        /// </summary>
        /// <param name="changeprestigeLevelPacket"></param>
        public void ChangeprestigeLevel(ChangeprestigeLevelPacket changeprestigeLevelPacket)
        {
            if (changeprestigeLevelPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[PrestigeLvl]PrestigeLevel: {changeprestigeLevelPacket.prestigeLevel}");

                if (changeprestigeLevelPacket.prestigeLevel <= 255)
                {
                    Session.Character.prestigeLevel = changeprestigeLevelPacket.prestigeLevel;
                    Session.Character.PrestigeXp = 0;
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("PRESTIGELEVEL_CHANGED"), 0));
                    Session.SendPacket(Session.Character.GenerateLev());
                    Session.SendPacket(Session.Character.GenerateStatChar());
                    Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateIn(), ReceiverType.AllExceptMe);
                    Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateGidx(), ReceiverType.AllExceptMe);
                    Session.CurrentMapInstance?.Broadcast(StaticPacketHelper.GenerateEff(UserType.Player, Session.Character.CharacterId, 6), Session.Character.PositionX, Session.Character.PositionY);
                    Session.CurrentMapInstance?.Broadcast(StaticPacketHelper.GenerateEff(UserType.Player, Session.Character.CharacterId, 198), Session.Character.PositionX, Session.Character.PositionY);
                }
                else
                {
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("WRONG_VALUE"), 0));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(ChangeprestigeLevelPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $JLvl Command
        /// </summary>
        /// <param name="changeJobLevelPacket"></param>
        public void ChangeJobLevel(ChangeJobLevelPacket changeJobLevelPacket)
        {
            if (changeJobLevelPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[JLvl]JobLevel: {changeJobLevelPacket.JobLevel}");

                if (((Session.Character.Class == 0 && changeJobLevelPacket.JobLevel <= 20) || (Session.Character.Class != 0 && changeJobLevelPacket.JobLevel <= 255)) && changeJobLevelPacket.JobLevel > 0)
                {
                    Session.Character.JobLevel = changeJobLevelPacket.JobLevel;
                    Session.Character.JobLevelXp = 0;
                    Session.Character.Skills.ClearAll();
                    Session.SendPacket(Session.Character.GenerateLev());
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("JOBLEVEL_CHANGED"), 0));
                    Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateIn(), ReceiverType.AllExceptMe);
                    Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateGidx(), ReceiverType.AllExceptMe);
                    Session.CurrentMapInstance?.Broadcast(StaticPacketHelper.GenerateEff(UserType.Player, Session.Character.CharacterId, 8), Session.Character.PositionX, Session.Character.PositionY);
                    Session.Character.Skills[(short)(200 + (20 * (byte)Session.Character.Class))] = new CharacterSkill
                    {
                        SkillVNum = (short)(200 + (20 * (byte)Session.Character.Class)),
                        CharacterId = Session.Character.CharacterId
                    };
                    Session.Character.Skills[(short)(201 + (20 * (byte)Session.Character.Class))] = new CharacterSkill
                    {
                        SkillVNum = (short)(201 + (20 * (byte)Session.Character.Class)),
                        CharacterId = Session.Character.CharacterId
                    };
                    Session.Character.Skills[236] = new CharacterSkill
                    {
                        SkillVNum = 236,
                        CharacterId = Session.Character.CharacterId
                    };
                    if (!Session.Character.UseSp)
                    {
                        Session.SendPacket(Session.Character.GenerateSki());
                    }
                    Session.Character.LearnAdventurerSkill();
                }
                else
                {
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("WRONG_VALUE"), 0));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(ChangeJobLevelPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Lvl Command
        /// </summary>
        /// <param name="changeLevelPacket"></param>
        public void ChangeLevel(ChangeLevelPacket changeLevelPacket)
        {
            if (changeLevelPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Lvl]Level: {changeLevelPacket.Level}");

                if (changeLevelPacket.Level > 0)
                {
                    Session.Character.Level = changeLevelPacket.Level;
                    Session.Character.LevelXp = 0;
                    Session.Character.Hp = (int)Session.Character.HPLoad();
                    Session.Character.Mp = (int)Session.Character.MPLoad();
                    Session.SendPacket(Session.Character.GenerateStat());
                    Session.SendPacket(Session.Character.GenerateStatChar());
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("LEVEL_CHANGED"), 0));
                    Session.SendPacket(Session.Character.GenerateLev());
                    Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateIn(), ReceiverType.AllExceptMe);
                    Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateGidx(), ReceiverType.AllExceptMe);
                    Session.CurrentMapInstance?.Broadcast(StaticPacketHelper.GenerateEff(UserType.Player, Session.Character.CharacterId, 6), Session.Character.PositionX, Session.Character.PositionY);
                    Session.CurrentMapInstance?.Broadcast(StaticPacketHelper.GenerateEff(UserType.Player, Session.Character.CharacterId, 198), Session.Character.PositionX, Session.Character.PositionY);
                    ServerManager.Instance.UpdateGroup(Session.Character.CharacterId);
                    if (Session.Character.Family != null)
                    {
                        ServerManager.Instance.FamilyRefresh(Session.Character.Family.FamilyId);
                        CommunicationServiceClient.Instance.SendMessageToCharacter(new SCSCharacterMessage()
                        {
                            DestinationCharacterId = Session.Character.Family.FamilyId,
                            SourceCharacterId = Session.Character.CharacterId,
                            SourceWorldId = ServerManager.Instance.WorldId,
                            Message = "fhis_stc",
                            Type = MessageType.Family
                        });
                    }
                }
                else
                {
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("WRONG_VALUE"), 0));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(ChangeLevelPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $ChangeRep Command
        /// </summary>
        /// <param name="changeReputationPacket"></param>
        public void ChangeReputation(ChangeReputationPacket changeReputationPacket)
        {
            if (changeReputationPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[ChangeRep]Reputation: {changeReputationPacket.Reputation}");

                if (changeReputationPacket.Reputation > 0)
                {
                    Session.Character.Reputation = changeReputationPacket.Reputation;
                    Session.SendPacket(Session.Character.GenerateFd());
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("REP_CHANGED"), 0));
                    Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateIn(), ReceiverType.AllExceptMe);
                    Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateGidx(), ReceiverType.AllExceptMe);
                }
                else
                {
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("WRONG_VALUE"), 0));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(ChangeReputationPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $SPLvl Command
        /// </summary>
        /// <param name="changeSpecialistLevelPacket"></param>
        public void ChangeSpecialistLevel(ChangeSpecialistLevelPacket changeSpecialistLevelPacket)
        {
            if (changeSpecialistLevelPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[SPLvl]SpecialistLevel: {changeSpecialistLevelPacket.SpecialistLevel}");

                ItemInstance sp = Session.Character.Inventory.LoadBySlotAndType((byte)EquipmentType.Sp, InventoryType.Wear);
                if (sp != null && Session.Character.UseSp)
                {
                    if (changeSpecialistLevelPacket.SpecialistLevel <= 255 && changeSpecialistLevelPacket.SpecialistLevel > 0)
                    {
                        sp.SpLevel = changeSpecialistLevelPacket.SpecialistLevel;
                        sp.XP = 0;
                        Session.SendPacket(Session.Character.GenerateLev());
                        Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("SPLEVEL_CHANGED"), 0));
                        Session.Character.LearnSPSkill();
                        Session.SendPacket(Session.Character.GenerateSki());
                        Session.SendPackets(Session.Character.GenerateQuicklist());
                        Session.Character.Skills.ForEach(s => s.LastUse = DateTime.Now.AddDays(-1));
                        Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateIn(), ReceiverType.AllExceptMe);
                        Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateGidx(), ReceiverType.AllExceptMe);
                        Session.CurrentMapInstance?.Broadcast(StaticPacketHelper.GenerateEff(UserType.Player, Session.Character.CharacterId, 8), Session.Character.PositionX, Session.Character.PositionY);
                    }
                    else
                    {
                        Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("WRONG_VALUE"), 0));
                    }
                }
                else
                {
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("NO_SP"), 0));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(ChangeSpecialistLevelPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $ChannelInfo Command
        /// </summary>
        /// <param name="channelInfoPacket"></param>
        public void ChannelInfo(ChannelInfoPacket channelInfoPacket)
        {
            Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), "[ChannelInfo]");

            Session.SendPacket(Session.Character.GenerateSay($"-----------Channel Info-----------\n-------------Channel:{ServerManager.Instance.ChannelId}-------------", 11));
            foreach (ClientSession session in ServerManager.Instance.Sessions)
            {
                Session.SendPacket(Session.Character.GenerateSay($"CharacterName: {session.Character.Name} SessionId: {session.SessionId}", 12));
            }
            Session.SendPacket(Session.Character.GenerateSay("----------------------------------------", 11));
        }

        /// <summary>
        /// $CharEdit Command
        /// </summary>
        /// <param name="characterEditPacket"></param>
        public void CharacterEdit(CharacterEditPacket characterEditPacket)
        {
            if (characterEditPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[CharEdit]Property: {characterEditPacket.Property} Value: {characterEditPacket.Data}");

                if (characterEditPacket.Property != null && !string.IsNullOrEmpty(characterEditPacket.Data))
                {
                    PropertyInfo propertyInfo = Session.Character.GetType().GetProperty(characterEditPacket.Property);
                    if (propertyInfo != null)
                    {
                        propertyInfo.SetValue(Session.Character, Convert.ChangeType(characterEditPacket.Data, propertyInfo.PropertyType));
                        ServerManager.Instance.ChangeMap(Session.Character.CharacterId);
                        Session.Character.Save();
                        Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
                    }
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(CharacterEditPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $CharStat Command
        /// </summary>
        /// <param name="characterStatsPacket"></param>
        public void CharStat(CharacterStatsPacket characterStatsPacket)
        {
            string returnHelp = CharacterStatsPacket.ReturnHelp();
            if (characterStatsPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[CharStat]CharacterName: {characterStatsPacket.CharacterName}");

                string name = characterStatsPacket.CharacterName;
                if (int.TryParse(characterStatsPacket.CharacterName, out int sessionId))
                {
                    if (ServerManager.Instance.GetSessionBySessionId(sessionId) != null)
                    {
                        Character character = ServerManager.Instance.GetSessionBySessionId(sessionId).Character;
                        sendStats(character);
                    }
                    else
                    {
                        Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("USER_NOT_FOUND"), 10));
                    }
                }
                else if (!string.IsNullOrEmpty(name))
                {
                    if (ServerManager.Instance.GetSessionByCharacterName(name) != null)
                    {
                        Character character = ServerManager.Instance.GetSessionByCharacterName(name).Character;
                        sendStats(character);
                    }
                    else if (DAOFactory.CharacterDAO.LoadByName(name) != null)
                    {
                        CharacterDTO characterDto = DAOFactory.CharacterDAO.LoadByName(name);
                        sendStats(characterDto);
                    }
                    else
                    {
                        Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("USER_NOT_FOUND"), 10));
                    }
                }
                else
                {
                    Session.SendPacket(Session.Character.GenerateSay(returnHelp, 10));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(returnHelp, 10));
            }
        }

        /// <summary>
        /// $Clear Command
        /// </summary>
        /// <param name="clearInventoryPacket"></param>
        public void ClearInventory(ClearInventoryPacket clearInventoryPacket)
        {
            if (clearInventoryPacket != null && clearInventoryPacket.InventoryType != InventoryType.Wear)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Clear]InventoryType: {clearInventoryPacket.InventoryType}");

                Parallel.ForEach(Session.Character.Inventory.Where(s => s.Type == clearInventoryPacket.InventoryType), inv =>
                {
                    Session.Character.Inventory.DeleteById(inv.Id);
                    Session.SendPacket(UserInterfaceHelper.Instance.GenerateInventoryRemove(inv.Type, inv.Slot));
                });
                Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(ClearInventoryPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $ClearMap packet
        /// </summary>
        /// <param name="clearMapPacket"></param>
        public void ClearMap(ClearMapPacket clearMapPacket)
        {
            if (clearMapPacket != null && Session.HasCurrentMapInstance)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[ClearMap]MapId: {Session.CurrentMapInstance.MapInstanceId}");

                Parallel.ForEach(Session.CurrentMapInstance.Monsters.Where(s => s.ShouldRespawn != true), monster =>
                {
                    Session.CurrentMapInstance.Broadcast(StaticPacketHelper.Out(UserType.Monster, monster.MapMonsterId));
                    Session.CurrentMapInstance.RemoveMonster(monster);
                });
                Parallel.ForEach(Session.CurrentMapInstance.DroppedList.GetAllItems(), drop =>
                {
                        Session.CurrentMapInstance.Broadcast(StaticPacketHelper.Out(UserType.Object, drop.TransportId));
                        Session.CurrentMapInstance.DroppedList.Remove(drop.TransportId);
                });
                Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(ClearMapPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Clone Command
        /// </summary>
        /// <param name="cloneItemPacket"></param>
        public void CloneItem(CloneItemPacket cloneItemPacket)
        {
            if (cloneItemPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Clone]Slot: {cloneItemPacket.Slot}");
                ItemInstance item = Session.Character.Inventory.LoadBySlotAndType(cloneItemPacket.Slot, InventoryType.Equipment);
                if (item != null)
                {
                    item = item.DeepCopy();
                    item.Id = Guid.NewGuid();
                    Session.Character.Inventory.AddToInventory(item);
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(CloneItemPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Help Command
        /// </summary>
        /// <param name="helpPacket"></param>
        public void Command(HelpPacket helpPacket)
        {
            Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), "[Help]");

            // get commands
            List<Type> classes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(t => t.GetTypes()).Where(t => t.IsClass && t.Namespace == "OpenNos.GameObject.CommandPackets" && (((PacketHeaderAttribute)Array.Find(t.GetCustomAttributes(true), ca => ca.GetType().Equals(typeof(PacketHeaderAttribute))))?.Authority ?? AuthorityType.User) <= Session.Account.Authority).ToList();
            List<string> messages = new List<string>();
            foreach (Type type in classes)
            {
                object classInstance = Activator.CreateInstance(type);
                Type classType = classInstance.GetType();
                MethodInfo method = classType.GetMethod("ReturnHelp");
                if (method != null)
                {
                    messages.Add(method.Invoke(classInstance, null).ToString());
                }
            }

            // send messages
            if (messages != null)
            {
                messages.Sort();
                if (helpPacket.Contents == "*" || string.IsNullOrEmpty(helpPacket.Contents))
                {
                    Session.SendPacket(Session.Character.GenerateSay("-------------Commands Info-------------", 11));
                    foreach (string message in messages)
                    {
                        Session.SendPacket(Session.Character.GenerateSay(message, 15));
                    }
                }
                else
                {
                    Session.SendPacket(Session.Character.GenerateSay("-------------Command Info-------------", 11));
                    foreach (string message in messages.Where(s => s.IndexOf(helpPacket.Contents, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        Session.SendPacket(Session.Character.GenerateSay(message, 15));
                    }
                }
                Session.SendPacket(Session.Character.GenerateSay("-----------------------------------------------", 11));
            }
        }

        /// <summary>
        /// $CreateItem Packet
        /// </summary>
        /// <param name="createItemPacket"></param>
        public void CreateItem(CreateItemPacket createItemPacket)
        {
            if (createItemPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[CreateItem]ItemVNum: {createItemPacket.VNum} Amount/Design: {createItemPacket.Design} Upgrade: {createItemPacket.Upgrade}");

                short vnum = createItemPacket.VNum;
                sbyte rare = 0;
                byte upgrade = 0;
                byte design = 0;
                short amount = 1;
                if (vnum == 1046)
                {
                    return; // cannot create gold as item, use $Gold instead
                }
                Item iteminfo = ServerManager.GetItem(vnum);
                if (iteminfo != null)
                {
                    if (iteminfo.IsColored || (iteminfo.ItemType == ItemType.Box && iteminfo.ItemSubType == 3))
                    {
                        if (createItemPacket.Design.HasValue)
                        {
                            design = createItemPacket.Design.Value;
                        }
                    }
                    else if (iteminfo.Type == 0)
                    {
                        if (createItemPacket.Upgrade.HasValue)
                        {
                            if (iteminfo.EquipmentSlot != EquipmentType.Sp)
                            {
                                upgrade = createItemPacket.Upgrade.Value;
                            }
                            else
                            {
                                design = createItemPacket.Upgrade.Value;
                            }
                            if (iteminfo.EquipmentSlot != EquipmentType.Sp && upgrade == 0 && iteminfo.BasicUpgrade != 0)
                            {
                                upgrade = iteminfo.BasicUpgrade;
                            }
                        }
                        if (createItemPacket.Design.HasValue)
                        {
                            if (iteminfo.EquipmentSlot == EquipmentType.Sp)
                            {
                                upgrade = createItemPacket.Design.Value;
                            }
                            else
                            {
                                rare = (sbyte)createItemPacket.Design.Value;
                            }
                        }
                    }
                    if (createItemPacket.Design.HasValue && !createItemPacket.Upgrade.HasValue)
                    {
                        amount = createItemPacket.Design.Value > 255 ? (short)255 : createItemPacket.Design.Value;
                    }
                    ItemInstance inv = Session.Character.Inventory.AddNewToInventory(vnum, amount, Rare: rare, Upgrade: upgrade, Design: design).FirstOrDefault();
                    if (inv != null)
                    {
                        ItemInstance wearable = Session.Character.Inventory.LoadBySlotAndType(inv.Slot, inv.Type);
                        if (wearable != null)
                        {
                            switch (wearable.Item.EquipmentSlot)
                            {
                                case EquipmentType.Armor:
                                case EquipmentType.MainWeapon:
                                case EquipmentType.SecondaryWeapon:
                                    wearable.SetRarityPoint();
                                    break;

                                case EquipmentType.Boots:
                                case EquipmentType.Gloves:
                                    wearable.FireResistance = (short)(wearable.Item.FireResistance * upgrade);
                                    wearable.DarkResistance = (short)(wearable.Item.DarkResistance * upgrade);
                                    wearable.LightResistance = (short)(wearable.Item.LightResistance * upgrade);
                                    wearable.WaterResistance = (short)(wearable.Item.WaterResistance * upgrade);
                                    break;
                            }
                        }
                        Session.SendPacket(Session.Character.GenerateSay($"{Language.Instance.GetMessageFromKey("ITEM_ACQUIRED")}: {iteminfo.Name} x {amount}", 12));
                    }
                    else
                    {
                        Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("NOT_ENOUGH_PLACE"), 0));
                    }
                }
                else
                {
                    UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("NO_ITEM"), 0);
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(CreateItemPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Demote Command
        /// </summary>
        /// <param name="demotePacket"></param>
        public void Demote(DemotePacket demotePacket)
        {
            if (demotePacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Demote]CharacterName: {demotePacket.CharacterName}");

                string name = demotePacket.CharacterName;
                AccountDTO account = DAOFactory.AccountDAO.LoadById(DAOFactory.CharacterDAO.LoadByName(name).AccountId);
                if (account?.Authority > AuthorityType.User)
                {
                    account.Authority--;
                    DAOFactory.AccountDAO.InsertOrUpdate(ref account);
                    ClientSession session = ServerManager.Instance.Sessions.FirstOrDefault(s => s.Character?.Name == name);
                    if (session != null)
                    {
                        session.Account.Authority--;
                        session.Character.Authority--;
                        ServerManager.Instance.ChangeMap(session.Character.CharacterId);
                        DAOFactory.AccountDAO.WriteGeneralLog(session.Account.AccountId, session.IpAddress, session.Character.CharacterId, GeneralLogType.Demotion, $"by: {Session.Character.Name}");
                    }
                    else
                    {
                        DAOFactory.AccountDAO.WriteGeneralLog(account.AccountId, "127.0.0.1", null, GeneralLogType.Demotion, $"by: {Session.Character.Name}");
                    }
                    ServerManager.Shout($"Adios traidor y no vuelvas por aqui ");
                    Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
                }
                else
                {
                    Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("USER_NOT_FOUND"), 10));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(DemotePacket.ReturnHelp(), 10));
            }
        }

        public void DirectConnect(DirectConnectPacket directConnectPacket) => Session.Character.ChangeChannel(directConnectPacket.IPAddress, directConnectPacket.Port, 3);

        /// <summary>
        /// $DropRate Command
        /// </summary>
        /// <param name="dropRatePacket"></param>
        public void DropRate(DropRatePacket dropRatePacket)
        {
            if (dropRatePacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[DropRate]Value: {dropRatePacket.Value}");

                if (dropRatePacket.Value <= 1000)
                {
                    ServerManager.Instance.Configuration.RateDrop = dropRatePacket.Value;
                    ServerManager.Shout($"El DROP RATE CAMBIO, DISFRUTEN TODOS! ");
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("DROP_RATE_CHANGED"), 0));
                }
                else
                {
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("WRONG_VALUE"), 0));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(DropRatePacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Effect Command
        /// </summary>
        /// <param name="effectCommandpacket"></param>
        public void Effect(EffectCommandPacket effectCommandpacket)
        {
            if (effectCommandpacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Effect]EffectId: {effectCommandpacket.EffectId}");

                Session.CurrentMapInstance?.Broadcast(StaticPacketHelper.GenerateEff(UserType.Player, Session.Character.CharacterId, effectCommandpacket.EffectId), Session.Character.PositionX, Session.Character.PositionY);
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(EffectCommandPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Faction Command
        /// </summary>
        /// <param name="factionPacket"></param>
        public void Faction(FactionPacket factionPacket)
        {
            if (factionPacket != null)
            {
                Session.SendPacket("scr 0 0 0 0 0 0 0");
                Session.SendPacket(Session.Character.GenerateFaction());
                if (Session.Character.Faction == FactionType.Angel)
                {
                    Session.Character.Faction = FactionType.Demon;
                    Session.SendPacket(StaticPacketHelper.GenerateEff(UserType.Player, Session.Character.CharacterId, 4803));
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey($"GET_PROTECTION_POWER_2"), 0));
                }
                else
                {
                    Session.Character.Faction = FactionType.Angel;
                    Session.SendPacket(StaticPacketHelper.GenerateEff(UserType.Player, Session.Character.CharacterId, 4800));
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey($"GET_PROTECTION_POWER_1"), 0));
                }
            }
        }

        /// <summary>
        /// $FairyXPRate Command
        /// </summary>
        /// <param name="fairyXpRatePacket"></param>
        public void FairyXpRate(FairyXpRatePacket fairyXpRatePacket)
        {
            if (fairyXpRatePacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[FairyXPRate]Value: {fairyXpRatePacket.Value}");

                if (fairyXpRatePacket.Value <= 1000)
                {
                    ServerManager.Instance.Configuration.RateFairyXP = fairyXpRatePacket.Value;
                    ServerManager.Shout($"El FAIRYXP RATE CAMBIO, DISFRUTEN TODOS! ");
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("FAIRYXP_RATE_CHANGED"), 0));
                }
                else
                {
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("WRONG_VALUE"), 0));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(FairyXpRatePacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Gift Command
        /// </summary>
        /// <param name="giftPacket"></param>
        public void Gift(GiftPacket giftPacket)
        {
            if (giftPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Gift]CharacterName: {giftPacket.CharacterName} ItemVNum: {giftPacket.VNum} Amount: {giftPacket.Amount} Rare: {giftPacket.Rare} Upgrade: {giftPacket.Upgrade}");

                if (giftPacket.CharacterName == "*")
                {
                    if (Session.HasCurrentMapInstance)
                    {
                        Parallel.ForEach(Session.CurrentMapInstance.Sessions, session => Session.Character.SendGift(session.Character.CharacterId, giftPacket.VNum, giftPacket.Amount, giftPacket.Rare, giftPacket.Upgrade, false));
                        Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("GIFT_SENT"), 10));
                    }
                }
                else
                {
                    CharacterDTO chara = DAOFactory.CharacterDAO.LoadByName(giftPacket.CharacterName);
                    if (chara != null)
                    {
                        Session.Character.SendGift(chara.CharacterId, giftPacket.VNum, giftPacket.Amount, giftPacket.Rare, giftPacket.Upgrade, false);
                        Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("GIFT_SENT"), 10));
                    }
                    else
                    {
                        Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("USER_NOT_CONNECTED"), 0));
                    }
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(GiftPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $GodMode Command
        /// </summary>
        /// <param name="godModePacket"></param>
        public void GodMode(GodModePacket godModePacket)
        {
            Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), "[GodMode]");

            Session.Character.HasGodMode = !Session.Character.HasGodMode;
            Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
        }

        /// <summary>
        /// $Gold Command
        /// </summary>
        /// <param name="goldPacket"></param>
        public void Gold(GoldPacket goldPacket)
        {
            if (goldPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Gold]Amount: {goldPacket.Amount}");

                long gold = goldPacket.Amount;
                long maxGold = ServerManager.Instance.Configuration.MaxGold;
                gold = gold > maxGold ? maxGold : gold;
                if (gold >= 0)
                {
                    Session.Character.Gold = gold;
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("GOLD_SET"), 0));
                    Session.SendPacket(Session.Character.GenerateGold());
                }
                else
                {
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("WRONG_VALUE"), 0));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(GoldPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $GoldDropRate Command
        /// </summary>
        /// <param name="goldDropRatePacket"></param>
        public void GoldDropRate(GoldDropRatePacket goldDropRatePacket)
        {
            if (goldDropRatePacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[GoldDropRate]Value: {goldDropRatePacket.Value}");

                if (goldDropRatePacket.Value <= 1000)
                {
                    ServerManager.Instance.Configuration.RateGoldDrop = goldDropRatePacket.Value;
                    ServerManager.Shout($"El  GOLD DROP RATE CAMBIO, DISFRUTEN TODOS! ");
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("GOLD_DROP_RATE_CHANGED"), 0));
                }
                else
                {
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("WRONG_VALUE"), 0));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(GoldDropRatePacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $GoldRate Command
        /// </summary>
        /// <param name="goldRatePacket"></param>
        public void GoldRate(GoldRatePacket goldRatePacket)
        {
            if (goldRatePacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[GoldRate]Value: {goldRatePacket.Value}");

                if (goldRatePacket.Value <= 1000)
                {
                    ServerManager.Instance.Configuration.RateGold = goldRatePacket.Value;

                    ServerManager.Shout($"El GOLD RATE CAMBIO, DISFRUTEN TODOS! ");

                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("GOLD_RATE_CHANGED"), 0));
                }
                else
                {
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("WRONG_VALUE"), 0));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(GoldRatePacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Guri Command
        /// </summary>
        /// <param name="guriCommandPacket"></param>
        public void Guri(GuriCommandPacket guriCommandPacket)
        {
            if (guriCommandPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Guri]Type: {guriCommandPacket.Type} Value: {guriCommandPacket.Value} Arguments: {guriCommandPacket.Argument}");

                Session.SendPacket(UserInterfaceHelper.GenerateGuri(guriCommandPacket.Type, guriCommandPacket.Argument, Session.Character.CharacterId, guriCommandPacket.Value));
            }
            Session.Character.GenerateSay(GuriCommandPacket.ReturnHelp(), 10);
        }

        /// <summary>
        /// $HairColor Command
        /// </summary>
        /// <param name="hairColorPacket"></param>
        public void Haircolor(HairColorPacket hairColorPacket)
        {
            if (hairColorPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[HairColor]HairColor: {hairColorPacket.HairColor}");

                Session.Character.HairColor = hairColorPacket.HairColor;
                Session.SendPacket(Session.Character.GenerateEq());
                Session.CurrentMapInstance?.Broadcast(Session.Character.GenerateIn());
                Session.CurrentMapInstance?.Broadcast(Session.Character.GenerateGidx());
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(HairColorPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $HairStyle Command
        /// </summary>
        /// <param name="hairStylePacket"></param>
        public void Hairstyle(HairStylePacket hairStylePacket)
        {
            if (hairStylePacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[HairStyle]HairStyle: {hairStylePacket.HairStyle}");

                Session.Character.HairStyle = hairStylePacket.HairStyle;
                Session.SendPacket(Session.Character.GenerateEq());
                Session.CurrentMapInstance?.Broadcast(Session.Character.GenerateIn());
                Session.CurrentMapInstance?.Broadcast(Session.Character.GenerateGidx());
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(HairStylePacket.ReturnHelp(), 10));
            }
        }

        public void HelpMe(HelpMePacket packet)
        {
            int count = 0;
            foreach (ClientSession team in ServerManager.Instance.Sessions.Where(s => s.Account.Authority == AuthorityType.GameMaster || s.Account.Authority == AuthorityType.Moderator))
            {
                if (team.HasSelectedCharacter)
                {
                    count++;

                    // TODO: move that to resx soo we follow i18n
                    team.SendPacket(team.Character.GenerateSay($"User {Session.Character.Name} needs your help!", 12));
                    team.SendPacket(team.Character.GenerateSay("Please inform the family chat when you take care of!", 12));
                    team.SendPacket(Session.Character.GenerateSpk("Click this message to start chatting.", 5));
                    team.SendPacket(UserInterfaceHelper.GenerateMsg($"User {Session.Character.Name} needs your help!", 0));
                    ServerManager.Shout($"Un User nesecita la ayuda de un GM O SUPPORT O UN AMIGO 7n7 ");
                }
            }
            if (count != 0)
            {
                Session.SendPacket(Session.Character.GenerateSay($"{count} Team members were informed! You should get a message shortly.", 10));
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay("Sadly, there are no online team member right now. Please ask for help on our Discord Server at:", 10));
                Session.SendPacket(Session.Character.GenerateSay("https://discord.gg/NSfm2uw", 10));
            }
        }

        /// <summary>
        /// $HeroXPRate Command
        /// </summary>
        /// <param name="heroXpRatePacket"></param>
        public void HeroXpRate(HeroXpRatePacket heroXpRatePacket)
        {
            if (heroXpRatePacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[HeroXPRate]Value: {heroXpRatePacket.Value}");

                if (heroXpRatePacket.Value <= 1000)
                {
                    ServerManager.Instance.Configuration.RatePrestigeXP = heroXpRatePacket.Value;
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("HEROXP_RATE_CHANGED"), 0));
                }
                else
                {
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("WRONG_VALUE"), 0));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(HeroXpRatePacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $PrestigeXPRate Command
        /// </summary>
        /// <param name="PrestigeXpRatePacket"></param>
        public void PrestigeXpRate(PrestigeXpRatePacket PrestigeXpRatePacket)
        {
            if (PrestigeXpRatePacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[PrestigeXPRate]Value: {PrestigeXpRatePacket.Value}");

                if (PrestigeXpRatePacket.Value <= 1000)
                {
                    ServerManager.Instance.Configuration.RatePrestigeXP = PrestigeXpRatePacket.Value;
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("PRESTIGEXP_RATE_CHANGED"), 0));
                }
                else
                {
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("WRONG_VALUE"), 0));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(PrestigeXpRatePacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $InstanceMusic Command
        /// </summary>
        /// <param name="instanceMusicPacket"></param>
        public void InstanceMusic(InstanceMusicPacket instanceMusicPacket)
        {
            if (instanceMusicPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[InstanceMusic]SongId: {instanceMusicPacket.Music} Mode: {instanceMusicPacket.Maps}");

                void changeMusic(bool isRevert)
                {
                    try
                    {
                        foreach (GameObject.MapInstance instance in ServerManager.GetAllMapInstances())
                        {
                            if (!isRevert && int.TryParse(instanceMusicPacket.Music, out int mapMusic))
                            {
                                instance.InstanceMusic = mapMusic;
                            }
                            else
                            {
                                instance.InstanceMusic = instance.Map.Music;
                            }
                            instance.Broadcast($"bgm {instance.InstanceMusic}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex);
                    }
                }

                if (instanceMusicPacket.Maps == "*")
                {
                    if (instanceMusicPacket.Music == "?")
                    {
                        changeMusic(true);
                    }
                    else
                    {
                        changeMusic(false);
                    }
                }
                else if (Session.CurrentMapInstance != null)
                {
                    if (instanceMusicPacket.Music == "?")
                    {
                        Session.CurrentMapInstance.InstanceMusic = Session.CurrentMapInstance.Map.Music;
                        Session.CurrentMapInstance.Broadcast($"bgm {Session.CurrentMapInstance.Map.Music}");
                        return;
                    }
                    if (int.TryParse(instanceMusicPacket.Music, out int mapMusic))
                    {
                        Session.CurrentMapInstance.InstanceMusic = mapMusic;
                        Session.CurrentMapInstance.Broadcast($"bgm {instanceMusicPacket.Music}");
                    }
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(InstanceMusicPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Invisible Command
        /// </summary>
        /// <param name="invisiblePacket"></param>
        public void Invisible(InvisiblePacket invisiblePacket)
        {
            Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), "[Invisible]");

            Session.Character.Invisible = !Session.Character.Invisible;
            Session.Character.InvisibleGm = !Session.Character.InvisibleGm;
            Session.CurrentMapInstance?.Broadcast(Session.Character.GenerateInvisible());
            this.Session.SendPacket(Session.Character.GenerateEq());
            if (Session.Character.InvisibleGm)
            {
                Session.Character.Mates.Where(s => s.IsTeamMember).ToList().ForEach(s => Session.CurrentMapInstance?.Broadcast(s.GenerateOut()));
                Session.CurrentMapInstance?.Broadcast(Session, StaticPacketHelper.Out(UserType.Player, Session.Character.CharacterId), ReceiverType.AllExceptMe);
            }
            else
            {
                Session.Character.Mates.Where(m => m.IsTeamMember).ToList().ForEach(m => Session.CurrentMapInstance?.Broadcast(m.GenerateIn(), ReceiverType.AllExceptMe));
                Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateIn(), ReceiverType.AllExceptMe);
                Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateGidx(), ReceiverType.AllExceptMe);
            }
        }

        /// <summary>
        /// $ItemRain Command
        /// </summary>
        /// <param name="itemRainPacket"></param>
        public void ItemRain(ItemRainPacket itemRainPacket)
        {
            if (itemRainPacket != null)
            {
                short vnum = itemRainPacket.VNum;
                byte amount = itemRainPacket.Amount;
                int count = itemRainPacket.Count;
                int time = itemRainPacket.Time;
                GameObject.MapInstance instance = Session.CurrentMapInstance;

                Observable.Timer(TimeSpan.FromSeconds(0)).Subscribe(observer =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        MapCell cell = instance.Map.GetRandomPosition();
                        MonsterMapItem droppedItem = new MonsterMapItem(cell.X, cell.Y, vnum, amount);
                        instance.DroppedList[droppedItem.TransportId] = droppedItem;
                        instance.Broadcast($"drop {droppedItem.ItemVNum} {droppedItem.TransportId} {droppedItem.PositionX} {droppedItem.PositionY} {(droppedItem.GoldAmount > 1 ? droppedItem.GoldAmount : droppedItem.Amount)} 0 0 -1");

                        System.Threading.Thread.Sleep(time * 1000 / count);
                    }
                });
            }
        }

        /// <summary>
        /// $Kick Command
        /// </summary>
        /// <param name="kickPacket"></param>
        public void Kick(KickPacket kickPacket)
        {
            if (kickPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Kick]CharacterName: {kickPacket.CharacterName}");

                if (kickPacket.CharacterName == "*")
                {
                    ServerManager.Shout($"ADIOS!");
                    Parallel.ForEach(ServerManager.Instance.Sessions, session => session.Disconnect());
                }
                ServerManager.Instance.Kick(kickPacket.CharacterName);
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(KickPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $KickSession Command
        /// </summary>
        /// <param name="kickSessionPacket"></param>
        public void KickSession(KickSessionPacket kickSessionPacket)
        {
            if (kickSessionPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Kick]AccountName: {kickSessionPacket.AccountName} SessionId: {kickSessionPacket.SessionId}");

                if (kickSessionPacket.SessionId.HasValue) //if you set the sessionId, remove account verification
                {
                    kickSessionPacket.AccountName = string.Empty;
                }
                ServerManager.Shout($"FUERA DE ESTA SESSION! ");
                Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
                AccountDTO account = DAOFactory.AccountDAO.LoadByName(kickSessionPacket.AccountName);
                CommunicationServiceClient.Instance.KickSession(account?.AccountId, kickSessionPacket.SessionId);
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(KickSessionPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Kill Command
        /// </summary>
        /// <param name="killPacket"></param>
        public void Kill(KillPacket killPacket)
        {
            if (killPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(),
                    $"[Kill]CharacterName: {killPacket.CharacterName}");

                ClientSession sess = ServerManager.Instance.GetSessionByCharacterName(killPacket.CharacterName);
                if (sess != null)
                {
                    if (sess.Character.HasGodMode)
                    {
                        return;
                    }

                    if (sess.Character.Hp < 1)
                    {
                        return;
                    }

                    sess.Character.Hp = 0;
                    sess.Character.LastDefence = DateTime.Now;
                    Session.CurrentMapInstance?.Broadcast(StaticPacketHelper.SkillUsed(UserType.Player,
                        Session.Character.CharacterId, 1, sess.Character.CharacterId, 1114, 4, 11, 4260, 0, 0, false, 0, 60000, 3, 0));
                    sess.SendPacket(sess.Character.GenerateStat());
                    ServerManager.Instance.AskRevive(sess.Character.CharacterId);
                    Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
                }
                else
                {
                    Session.SendPacket(
                        UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("USER_NOT_CONNECTED"), 0));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(KillPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $PenaltyLog Command
        /// </summary>
        /// <param name="penaltyLogPacket"></param>
        public void ListPenalties(PenaltyLogPacket penaltyLogPacket)
        {
            string returnHelp = CharacterStatsPacket.ReturnHelp();
            if (penaltyLogPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[PenaltyLog]CharacterName: {penaltyLogPacket.CharacterName}");

                string name = penaltyLogPacket.CharacterName;
                if (!string.IsNullOrEmpty(name))
                {
                    CharacterDTO character = DAOFactory.CharacterDAO.LoadByName(name);
                    if (character != null)
                    {
                        bool separatorSent = false;
                        void WritePenalty(PenaltyLogDTO penalty)
                        {
                            Session.SendPacket(Session.Character.GenerateSay($"Type: {penalty.Penalty}", 13));
                            Session.SendPacket(Session.Character.GenerateSay($"AdminName: {penalty.AdminName}", 13));
                            Session.SendPacket(Session.Character.GenerateSay($"Reason: {penalty.Reason}", 13));
                            Session.SendPacket(Session.Character.GenerateSay($"DateStart: {penalty.DateStart}", 13));
                            Session.SendPacket(Session.Character.GenerateSay($"DateEnd: {penalty.DateEnd}", 13));
                            Session.SendPacket(Session.Character.GenerateSay("----- ------- -----", 13));
                            separatorSent = true;
                        }
                        IEnumerable<PenaltyLogDTO> penaltyLogs = ServerManager.Instance.PenaltyLogs.Where(s => s.AccountId == character.AccountId).ToList();

                        //PenaltyLogDTO penalty = penaltyLogs.LastOrDefault(s => s.DateEnd > DateTime.Now);
                        Session.SendPacket(Session.Character.GenerateSay("----- PENALTIES -----", 13));

                        #region Warnings

                        Session.SendPacket(Session.Character.GenerateSay("----- WARNINGS -----", 13));
                        foreach (PenaltyLogDTO penaltyLog in penaltyLogs.Where(s => s.Penalty == PenaltyType.Warning).OrderBy(s => s.DateStart))
                        {
                            WritePenalty(penaltyLog);
                        }
                        if (!separatorSent)
                        {
                            Session.SendPacket(Session.Character.GenerateSay("----- ------- -----", 13));
                        }
                        separatorSent = false;

                        #endregion

                        #region Mutes

                        Session.SendPacket(Session.Character.GenerateSay("----- MUTES -----", 13));
                        foreach (PenaltyLogDTO penaltyLog in penaltyLogs.Where(s => s.Penalty == PenaltyType.Muted).OrderBy(s => s.DateStart))
                        {
                            WritePenalty(penaltyLog);
                        }
                        if (!separatorSent)
                        {
                            Session.SendPacket(Session.Character.GenerateSay("----- ------- -----", 13));
                        }
                        separatorSent = false;

                        #endregion

                        #region Bans

                        Session.SendPacket(Session.Character.GenerateSay("----- BANS -----", 13));
                        foreach (PenaltyLogDTO penaltyLog in penaltyLogs.Where(s => s.Penalty == PenaltyType.Banned).OrderBy(s => s.DateStart))
                        {
                            WritePenalty(penaltyLog);
                        }
                        if (!separatorSent)
                        {
                            Session.SendPacket(Session.Character.GenerateSay("----- ------- -----", 13));
                        }
                        separatorSent = false;

                        #endregion

                        Session.SendPacket(Session.Character.GenerateSay("----- SUMMARY -----", 13));
                        Session.SendPacket(Session.Character.GenerateSay($"Warnings: {penaltyLogs.Count(s => s.Penalty == PenaltyType.Warning)}", 13));
                        Session.SendPacket(Session.Character.GenerateSay($"Mutes: {penaltyLogs.Count(s => s.Penalty == PenaltyType.Muted)}", 13));
                        Session.SendPacket(Session.Character.GenerateSay($"Bans: {penaltyLogs.Count(s => s.Penalty == PenaltyType.Banned)}", 13));
                        Session.SendPacket(Session.Character.GenerateSay("----- ------- -----", 13));
                    }
                    else
                    {
                        Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("USER_NOT_FOUND"), 10));
                    }
                }
                else
                {
                    Session.SendPacket(Session.Character.GenerateSay(returnHelp, 10));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(returnHelp, 10));
            }
        }

        /// <summary>
        /// $MapDance Command
        /// </summary>
        /// <param name="mapDancePacket"></param>
        public void MapDance(MapDancePacket mapDancePacket)
        {
            Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[MapDance]");

            if (Session.HasCurrentMapInstance)
            {
                Session.CurrentMapInstance.IsDancing = !Session.CurrentMapInstance.IsDancing;
                if (Session.CurrentMapInstance.IsDancing)
                {
                    Session.Character.Dance();
                    Session.CurrentMapInstance?.Broadcast("dance 2");
                }
                else
                {
                    Session.Character.Dance();
                    Session.CurrentMapInstance?.Broadcast("dance");
                }
                ServerManager.Shout($"TODOS A BAILAR ESTO ES UNA FIESTA EEEEEAAAA! ");
                Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
            }
        }

        /// <summary>
        /// $MapPVP Command
        /// </summary>
        /// <param name="mapPVPPacket"></param>
        public void MapPVP(MapPVPPacket mapPVPPacket)
        {
            Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[MapPVP]");

            Session.CurrentMapInstance.IsPVP = !Session.CurrentMapInstance.IsPVP;
            ServerManager.Shout($"ESTE MAPA AHORA ESTA EN MODO PVP ");
            Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
        }

        /// <summary>
        /// $Morph Command
        /// </summary>
        /// <param name="morphPacket"></param>
        public void Morph(MorphPacket morphPacket)
        {
            if (morphPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Morph]MorphId: {morphPacket.MorphId} MorphDesign: {morphPacket.MorphDesign} Upgrade: {morphPacket.Upgrade} MorphId: {morphPacket.ArenaWinner}");

                if (morphPacket.MorphId < 32 && morphPacket.MorphId > 0)
                {
                    Session.Character.UseSp = true;
                    Session.Character.Morph = morphPacket.MorphId;
                    Session.Character.MorphUpgrade = morphPacket.Upgrade;
                    Session.Character.MorphUpgrade2 = morphPacket.MorphDesign;
                    Session.Character.ArenaWinner = morphPacket.ArenaWinner;
                    Session.CurrentMapInstance?.Broadcast(Session.Character.GenerateCMode());
                }
                else if (morphPacket.MorphId > 32)
                {
                    Session.Character.IsVehicled = true;
                    Session.Character.Morph = morphPacket.MorphId;
                    Session.Character.ArenaWinner = morphPacket.ArenaWinner;
                    Session.CurrentMapInstance?.Broadcast(Session.Character.GenerateCMode());
                }
                else
                {
                    Session.Character.IsVehicled = false;
                    Session.Character.UseSp = false;
                    Session.Character.ArenaWinner = 0;
                    Session.SendPacket(Session.Character.GenerateCond());
                    Session.SendPacket(Session.Character.GenerateLev());
                    Session.CurrentMapInstance?.Broadcast(Session.Character.GenerateCMode());
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(MorphPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Mute Command
        /// </summary>
        /// <param name="mutePacket"></param>
        public void Mute(MutePacket mutePacket)
        {
            if (mutePacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(),
                    $"[Mute]CharacterName: {mutePacket.CharacterName} Reason: {mutePacket.Reason} Until: {DateTime.Now.AddMinutes(mutePacket.Duration)}");

                if (mutePacket.Duration == 0)
                {
                    mutePacket.Duration = 60;
                }

                mutePacket.Reason = mutePacket.Reason?.Trim();
                MuteMethod(mutePacket.CharacterName, mutePacket.Reason, mutePacket.Duration);
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(MutePacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Packet Command
        /// </summary>
        /// <param name="packetCallbackPacket"></param>
        public void PacketCallBack(PacketCallbackPacket packetCallbackPacket)
        {
            if (packetCallbackPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Packet]Packet: {packetCallbackPacket.Packet}");

                Session.SendPacket(packetCallbackPacket.Packet);
                Session.SendPacket(Session.Character.GenerateSay(packetCallbackPacket.Packet, 10));
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(PacketCallbackPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Maintenance Command
        /// </summary>
        /// <param name="maintenancePacket"></param>
        public void PlanMaintenance(MaintenancePacket maintenancePacket)
        {
            if (maintenancePacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Maintenance]Delay: {maintenancePacket.Delay} Duration: {maintenancePacket.Duration} Reason: {maintenancePacket.Reason}");
                DateTime dateStart = DateTime.Now.AddMinutes(maintenancePacket.Delay);
                MaintenanceLogDTO maintenance = new MaintenanceLogDTO()
                {
                    DateEnd = dateStart.AddMinutes(maintenancePacket.Duration),
                    DateStart = dateStart,
                    Reason = maintenancePacket.Reason
                };
                DAOFactory.MaintenanceLogDAO.Insert(maintenance);
                ServerManager.Shout($"El Servidor se pondra en modo Maintenance ");
                Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(MaintenancePacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $PortalTo Command
        /// </summary>
        /// <param name="portalToPacket"></param>
        public void PortalTo(PortalToPacket portalToPacket)
        {
            if (portalToPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[PortalTo]DestinationMapId: {portalToPacket.DestinationMapId} DestinationMapX: {portalToPacket.DestinationX} DestinationY: {portalToPacket.DestinationY}");

                addPortal(portalToPacket.DestinationMapId, portalToPacket.DestinationX, portalToPacket.DestinationY, portalToPacket.PortalType == null ? (short)-1 : (short)portalToPacket.PortalType, false);
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(PortalToPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Position Command
        /// </summary>
        /// <param name="positionPacket"></param>
        public void Position(PositionPacket positionPacket)
        {
            Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), "[Position]");

            Session.SendPacket(Session.Character.GenerateSay(
                $"Map:{Session.Character.MapInstance.Map.MapId} - X:{Session.Character.PositionX} - Y:{Session.Character.PositionY} - Dir:{Session.Character.Direction} - Cell:{Session.CurrentMapInstance.Map.JaggedGrid[Session.Character.PositionX][Session.Character.PositionY]?.Value}",
                12));
        }

        /// <summary>
        /// $Promote Command
        /// </summary>
        /// <param name="promotePacket"></param>
        public void Promote(PromotePacket promotePacket)
        {
            if (promotePacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Promote]CharacterName: {promotePacket.CharacterName}");

                string name = promotePacket.CharacterName;
                AccountDTO account = DAOFactory.AccountDAO.LoadById(DAOFactory.CharacterDAO.LoadByName(name).AccountId);
                if (account?.Authority >= AuthorityType.User && account.Authority < AuthorityType.GameMaster)
                {
                    account.Authority++;
                    DAOFactory.AccountDAO.InsertOrUpdate(ref account);
                    ClientSession session = ServerManager.Instance.Sessions.FirstOrDefault(s => s.Character?.Name == name);
                    if (session != null)
                    {
                        session.Account.Authority++;
                        session.Character.Authority++;
                        ServerManager.Instance.ChangeMap(session.Character.CharacterId);
                        DAOFactory.AccountDAO.WriteGeneralLog(session.Account.AccountId, session.IpAddress, session.Character.CharacterId, GeneralLogType.Promotion, $"by: {Session.Character.Name}");
                    }
                    else
                    {
                        DAOFactory.AccountDAO.WriteGeneralLog(account.AccountId, "127.0.0.1", null, GeneralLogType.Promotion, $"by: {Session.Character.Name}");
                    }
                    Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
                }
                else
                {
                    Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("USER_NOT_FOUND"), 10));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(PromotePacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Msg Command
        /// </summary>
        /// <param name="newAdminMessage"></param>
        public void Msg(MsgPacket msgPacket)
        {
            if (msgPacket != null && !string.IsNullOrWhiteSpace(msgPacket.Message))
            {
                ServerManager.Instance.Broadcast(Session, $"msg 3 [{Session.Character.Name}]: {msgPacket.Message}",
                ReceiverType.All);
            }
        }


        /// <summary>
        /// Schattenjajamaru Packet
        /// </summary>
        /// <param name="morphSchattenjajamaruPacket"></param>
        public void Schattenjajamaru(MorphSchattenjajamaruPacket morphSchattenjajamaruPacket)
        {
            Session.SendPacket(Session.Character.GenerateSay($"===============", 11));
            Session.SendPacket(Session.Character.GenerateSay($"Du hast dich verwandelt!", 12));
            Session.SendPacket(Session.Character.GenerateSay($"Drücke 'g' für eine Rückverwandlung.", 10));
            Session.SendPacket(Session.Character.GenerateSay($"===============", 11));
            Session.Character.UseSp = true;
            Session.Character.Morph = 1412;
            Session.Character.MorphUpgrade = 15;
            Session.Character.MorphUpgrade2 = 12;
            Session.Character.ArenaWinner = 0;
            Session.Character.Speed = 25;
            Session.CurrentMapInstance?.Broadcast(Session.Character.GenerateCMode());
            Buff buff = new Buff(411, 1);
            Session.Character.AddBuff(buff, true);
            Buff buff2 = new Buff(648, 1);
            Session.Character.AddBuff(buff2, true);
            Session.CurrentMapInstance?.Broadcast(
                    StaticPacketHelper.GenerateEff(UserType.Player, Session.Character.CharacterId,
                        1011), Session.Character.PositionX, Session.Character.PositionY);
            Session.SendPacket(
                           Session.Character.GenerateSay(
                               Language.Instance.GetMessageFromKey("SHADOWJAJAMARU_MESSAGE_ON_MORPH"), 0));
        }

        /// <summary>
        /// A RankDT!
        /// </summary>
        /// <param name="RankDTPacket"></param>
        public void RankDT(RankDTPacket RankDTPacket)
        {
            if (RankDTPacket != null)
            {
                CharacterDTO character = DAOFactory.CharacterDAO.LoadByName(RankDTPacket.CharacterName);
                if (character != null)
                {
                    ClientSession session = ServerManager.Instance.Sessions.FirstOrDefault(s => s.Character?.Name == RankDTPacket.CharacterName);
                    if (session != null)
                    {
                        session.Character.Authority = AuthorityType.Donador;
                        session.Account.Authority = AuthorityType.Donador;
                        session.Character.Compliment = 100;
                        ServerManager.Instance.ChangeMap(session.Character.CharacterId);
                    }
                    ServerManager.Shout($"Gracias A {RankDTPacket.CharacterName} por la Donation!");
                    AccountDTO account = DAOFactory.AccountDAO.LoadById(character.AccountId);
                    account.Authority = AuthorityType.Donador;
                    DAOFactory.AccountDAO.InsertOrUpdate(ref account);
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(RankDTPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Rarify Command
        /// </summary>
        /// <param name="rarifyPacket"></param>
        public void Rarify(RarifyPacket rarifyPacket)
        {
            if (rarifyPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Rarify]Slot: {rarifyPacket.Slot} Mode: {rarifyPacket.Mode} Protection: {rarifyPacket.Protection}");

                if (rarifyPacket.Slot >= 0)
                {
                    ItemInstance wearableInstance = Session.Character.Inventory.LoadBySlotAndType(rarifyPacket.Slot, 0);
                    wearableInstance?.RarifyItem(Session, rarifyPacket.Mode, rarifyPacket.Protection);
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(RarifyPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $RemoveNpcMonster Packet
        /// </summary>
        /// <param name="removeNpcMonsterPacket"></param>
        public void RemoveNpcMonster(RemoveNpcMonsterPacket removeNpcMonsterPacket)
        {
            if (Session.HasCurrentMapInstance)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[RemoveNpcMonster]NpcMonsterId: {Session.Character.LastNpcMonsterId}");

                MapMonster monster = Session.CurrentMapInstance.GetMonster(Session.Character.LastNpcMonsterId);
                MapNpc npc = Session.CurrentMapInstance.GetNpc(Session.Character.LastNpcMonsterId);
                if (monster != null)
                {
                    if (monster.IsAlive)
                    {
                        Session.CurrentMapInstance.Broadcast(StaticPacketHelper.Out(UserType.Monster, monster.MapMonsterId));
                        Session.SendPacket(Session.Character.GenerateSay(string.Format(Language.Instance.GetMessageFromKey("MONSTER_REMOVED"), monster.MapMonsterId, monster.Monster.Name, monster.MapId, monster.MapX, monster.MapY), 12));
                        Session.CurrentMapInstance.RemoveMonster(monster);
                        if (DAOFactory.MapMonsterDAO.LoadById(monster.MapMonsterId) != null)
                        {
                            DAOFactory.MapMonsterDAO.DeleteById(monster.MapMonsterId);
                        }
                    }
                    else
                    {
                        Session.SendPacket(Session.Character.GenerateSay(string.Format(Language.Instance.GetMessageFromKey("MONSTER_NOT_ALIVE")), 11));
                    }
                }
                else if (npc != null)
                {
                    if (!npc.IsMate && !npc.IsDisabled && !npc.IsProtected)
                    {
                        Session.CurrentMapInstance.Broadcast(StaticPacketHelper.Out(UserType.Npc, npc.MapNpcId));
                        Session.SendPacket(Session.Character.GenerateSay(string.Format(Language.Instance.GetMessageFromKey("NPCMONSTER_REMOVED"), npc.MapNpcId, npc.Npc.Name, npc.MapId, npc.MapX, npc.MapY), 12));
                        Session.CurrentMapInstance.RemoveNpc(npc);
                        if (DAOFactory.ShopDAO.LoadByNpc(npc.MapNpcId) != null)
                        {
                            DAOFactory.ShopDAO.DeleteById(npc.MapNpcId);
                        }
                        if (DAOFactory.MapNpcDAO.LoadById(npc.MapNpcId) != null)
                        {
                            DAOFactory.MapNpcDAO.DeleteById(npc.MapNpcId);
                        }
                    }
                    else
                    {
                        Session.SendPacket(Session.Character.GenerateSay(string.Format(Language.Instance.GetMessageFromKey("NPC_CANNOT_BE_REMOVED")), 11));
                    }
                }
                else
                {
                    Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("NPCMONSTER_NOT_FOUND"), 11));
                }
            }
        }

        /// <summary>
        /// $RemovePortal Command
        /// </summary>
        /// <param name="removePortalPacket"></param>
        public void RemovePortal(RemovePortalPacket removePortalPacket)
        {
            if (Session.HasCurrentMapInstance)
            {
                Portal portal = Session.CurrentMapInstance.Portals.Find(s => s.SourceMapInstanceId == Session.Character.MapInstanceId && Map.GetDistance(new MapCell { X = s.SourceX, Y = s.SourceY }, new MapCell { X = Session.Character.PositionX, Y = Session.Character.PositionY }) < 10);
                if (portal != null)
                {
                    Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[RemovePortal]MapId: {portal.SourceMapId} MapX: {portal.SourceX} MapY: {portal.SourceY}");
                    Session.SendPacket(Session.Character.GenerateSay(string.Format(Language.Instance.GetMessageFromKey("NEAREST_PORTAL"), portal.SourceMapId, portal.SourceX, portal.SourceY), 12));
                    Session.CurrentMapInstance.Portals.Remove(portal);
                    Session.CurrentMapInstance?.Broadcast(portal.GenerateGp());
                }
                else
                {
                    Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("NO_PORTAL_FOUND"), 11));
                }
            }
        }

        /// <summary>
        /// $Resize Command
        /// </summary>
        /// <param name="resizePacket"></param>
        public void Resize(ResizePacket resizePacket)
        {
            if (resizePacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Resize]Size: {resizePacket.Value}");

                if (resizePacket.Value >= 0)
                {
                    Session.Character.Size = resizePacket.Value;
                    Session.CurrentMapInstance?.Broadcast(Session.Character.GenerateScal());
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(ResizePacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $SearchItem Command
        /// </summary>
        /// <param name="searchItemPacket"></param>
        public void SearchItem(SearchItemPacket searchItemPacket)
        {
            if (searchItemPacket != null)
            {
                string contents = searchItemPacket.Contents;
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[SearchItem]Contents: {(string.IsNullOrEmpty(contents) ? "none" : contents)}");

                string name = string.Empty;
                byte page = 0;
                if (!string.IsNullOrEmpty(contents))
                {
                    string[] packetsplit = contents.Split(' ');
                    bool withPage = byte.TryParse(packetsplit[0], out page);
                    name = packetsplit.Length == 1 && withPage ? string.Empty : packetsplit.Skip(withPage ? 1 : 0).Aggregate((a, b) => a + ' ' + b);
                }
                IEnumerable<ItemDTO> itemlist = DAOFactory.ItemDAO.FindByName(name).OrderBy(s => s.VNum).Skip(page * 200).Take(200).ToList();
                if (itemlist.Any())
                {
                    foreach (ItemDTO item in itemlist)
                    {
                        Session.SendPacket(Session.Character.GenerateSay($"[SearchItem:{page}]Item: {(string.IsNullOrEmpty(item.Name) ? "none" : item.Name)} VNum: {item.VNum}", 12));
                    }
                }
                else
                {
                    Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("ITEM_NOT_FOUND"), 11));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(SearchItemPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $SearchMonster Command
        /// </summary>
        /// <param name="searchMonsterPacket"></param>
        public void SearchMonster(SearchMonsterPacket searchMonsterPacket)
        {
            if (searchMonsterPacket != null)
            {
                string contents = searchMonsterPacket.Contents;
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[SearchMonster]Contents: {(string.IsNullOrEmpty(contents) ? "none" : contents)}");

                string name = string.Empty;
                byte page = 0;
                if (!string.IsNullOrEmpty(contents))
                {
                    string[] packetsplit = contents.Split(' ');
                    bool withPage = byte.TryParse(packetsplit[0], out page);
                    name = packetsplit.Length == 1 && withPage ? string.Empty : packetsplit.Skip(withPage ? 1 : 0).Aggregate((a, b) => a + ' ' + b);
                }
                IEnumerable<NpcMonsterDTO> monsterlist = DAOFactory.NpcMonsterDAO.FindByName(name).OrderBy(s => s.NpcMonsterVNum).Skip(page * 200).Take(200).ToList();
                if (monsterlist.Any())
                {
                    foreach (NpcMonsterDTO npcMonster in monsterlist)
                    {
                        Session.SendPacket(Session.Character.GenerateSay($"[SearchMonster:{page}]Monster: {(string.IsNullOrEmpty(npcMonster.Name) ? "none" : npcMonster.Name)} VNum: {npcMonster.NpcMonsterVNum}", 12));
                    }
                }
                else
                {
                    Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("MONSTER_NOT_FOUND"), 11));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(SearchMonsterPacket.ReturnHelp(), 10));
            }
        }

     

        /// <summary>
        /// $SetPerfection Command
        /// </summary>
        /// <param name="setPerfectionPacket"></param>
        public void SetPerfection(SetPerfectionPacket setPerfectionPacket)
        {
            if (setPerfectionPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[SetPerfection]Slot: {setPerfectionPacket.Slot} Type: {setPerfectionPacket.Type} Value: {setPerfectionPacket.Value}");

                if (setPerfectionPacket.Slot >= 0)
                {
                    ItemInstance specialistInstance = Session.Character.Inventory.LoadBySlotAndType(setPerfectionPacket.Slot, 0);

                    if (specialistInstance != null)
                    {
                        switch (setPerfectionPacket.Type)
                        {
                            case 0:
                                specialistInstance.SpStoneUpgrade = setPerfectionPacket.Value;
                                break;

                            case 1:
                                specialistInstance.SpDamage = setPerfectionPacket.Value;
                                break;

                            case 2:
                                specialistInstance.SpDefence = setPerfectionPacket.Value;
                                break;

                            case 3:
                                specialistInstance.SpElement = setPerfectionPacket.Value;
                                break;

                            case 4:
                                specialistInstance.SpHP = setPerfectionPacket.Value;
                                break;

                            case 5:
                                specialistInstance.SpFire = setPerfectionPacket.Value;
                                break;

                            case 6:
                                specialistInstance.SpWater = setPerfectionPacket.Value;
                                break;

                            case 7:
                                specialistInstance.SpLight = setPerfectionPacket.Value;
                                break;

                            case 8:
                                specialistInstance.SpDark = setPerfectionPacket.Value;
                                break;

                            default:
                                Session.SendPacket(Session.Character.GenerateSay(UpgradeCommandPacket.ReturnHelp(), 10));
                                break;
                        }
                    }
                    else
                    {
                        Session.SendPacket(Session.Character.GenerateSay(UpgradeCommandPacket.ReturnHelp(), 10));
                    }
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(UpgradeCommandPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Shout Command
        /// </summary>
        /// <param name="shoutPacket"></param>
        public void Shout(ShoutPacket shoutPacket)
        {
            if (shoutPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Shout]Message: {shoutPacket.Message}");

                CommunicationServiceClient.Instance.SendMessageToCharacter(new SCSCharacterMessage()
                {
                    DestinationCharacterId = null,
                    SourceCharacterId = Session.Character.CharacterId,
                    SourceWorldId = ServerManager.Instance.WorldId,
                    Message = shoutPacket.Message,
                    Type = MessageType.Shout
                });
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(ShoutPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $DonatorShout Command
        /// </summary>
        /// <param name="donatorShoutPacket"></param>
        public void DonatorShout(DonatorShout donatorShoutPacket)
        {
            if (donatorShoutPacket != null && !string.IsNullOrWhiteSpace(donatorShoutPacket.Message))
            {
                ServerManager.Instance.Broadcast(Session, $"msg 4 [{Session.Character.Name}]: {donatorShoutPacket.Message}",
                ReceiverType.All);
                Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("SPAM_PROTECTION_MESSAGE"), 11));
            }
        }

        /// <summary>
        /// $DonatorMessage Command
        /// </summary>
        /// <param name="donatorChatPacket"></param>
        public void DonatorChat(DonatorChat donatorChatPacket)
        {
            if (donatorChatPacket != null && !string.IsNullOrWhiteSpace(donatorChatPacket.Message))
            {
                ServerManager.Instance.Broadcast(Session, $"msg 3 [{Session.Character.Name}]: {donatorChatPacket.Message}",
                ReceiverType.All);
                Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("SPAM_PROTECTION_MESSAGE"), 11));
            }
        }

        /// <summary>
        /// $ShoutHere Command
        /// </summary>
        /// <param name="shoutHerePacket"></param>
        public void ShoutHere(ShoutHerePacket shoutHerePacket)
        {
            if (shoutHerePacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[ShoutHere]Message: {shoutHerePacket.Message}");

                ServerManager.Shout(shoutHerePacket.Message);
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(ShoutHerePacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Shutdown Command
        /// </summary>
        /// <param name="shutdownPacket"></param>
        public void Shutdown(ShutdownPacket shutdownPacket)
        {
            Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Shutdown]");

            if (ServerManager.Instance.TaskShutdown != null)
            {
                ServerManager.Instance.ShutdownStop = true;
                ServerManager.Instance.TaskShutdown = null;
            }
            else
            {
                ServerManager.Instance.TaskShutdown = ServerManager.Instance.ShutdownTaskAsync();
                ServerManager.Instance.TaskShutdown.Start();
            }
        }

        /// <summary>
        /// $ShutdownAll Command
        /// </summary>
        /// <param name="shutdownAllPacket"></param>
        public void ShutdownAll(ShutdownAllPacket shutdownAllPacket)
        {
            if (shutdownAllPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[ShutdownAll]");

                if (!string.IsNullOrEmpty(shutdownAllPacket.WorldGroup))
                {
                    CommunicationServiceClient.Instance.Shutdown(shutdownAllPacket.WorldGroup);
                }
                else
                {
                    CommunicationServiceClient.Instance.Shutdown(ServerManager.Instance.ServerGroup);
                }
                Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(ShutdownAllPacket.ReturnHelp(), 10));
            }
        }

        public void Restart(RestartPacket restartPacket)
        {
            Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Restart]");

            if (ServerManager.Instance.TaskShutdown != null)
            {
                ServerManager.Instance.ShutdownStop = true;
                ServerManager.Instance.TaskShutdown = null;
            }
            else
            {
                ServerManager.Instance.IsReboot = true;
                ServerManager.Instance.TaskShutdown = ServerManager.Instance.ShutdownTaskAsync();
                ServerManager.Instance.TaskShutdown.Start();
            }
        }

        /// <summary>
        /// $ShutdownAll Command
        /// </summary>
        /// <param name="restartAllPacket"></param>
        public void RestartAll(RestartAllPacket restartAllPacket)
        {
            if (restartAllPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[RestartAll]");

                if (!string.IsNullOrEmpty(restartAllPacket.WorldGroup))
                {
                    CommunicationServiceClient.Instance.Restart(restartAllPacket.WorldGroup);
                }
                else
                {
                    CommunicationServiceClient.Instance.Restart(ServerManager.Instance.ServerGroup);
                }
                Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(RestartAllPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Sort Command
        /// </summary>
        /// <param name="sortPacket"></param>
        public void Sort(SortPacket sortPacket)
        {
            if (sortPacket?.InventoryType.HasValue == true)
            {
                Logger.LogUserEvent("USERCOMMAND", Session.GenerateIdentity(), $"[Sort]InventoryType: {sortPacket.InventoryType}");
                if (sortPacket.InventoryType == InventoryType.Equipment || sortPacket.InventoryType == InventoryType.Etc || sortPacket.InventoryType == InventoryType.Main)
                {
                    Session.Character.Inventory.Reorder(Session, sortPacket.InventoryType.Value);
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(SortPacket.ReturnHelp(), 10));
                foreach (string str in SortPacket.MoreHelp())
                {
                    Session.SendPacket(Session.Character.GenerateSay(str, 10));
                }
            }
        }

        /// <summary>
        /// $Speed Command
        /// </summary>
        /// <param name="speedPacket"></param>
        public void Speed(SpeedPacket speedPacket)
        {
            if (speedPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Speed]Value: {speedPacket.Value}");

                if (speedPacket.Value < 60)
                {
                    Session.Character.Speed = speedPacket.Value;
                    Session.Character.IsCustomSpeed = true;
                    Session.SendPacket(Session.Character.GenerateCond());
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(SpeedPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $SPRefill Command
        /// </summary>
        /// <param name="spRefillPacket"></param>
        public void SPRefill(SPRefillPacket spRefillPacket)
        {
            Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[SPRefill]");

            Session.Character.SpPoint = 10000;
            Session.Character.SpAdditionPoint = 1000000;
            Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("SP_REFILL"), 0));
            Session.SendPacket(Session.Character.GenerateSpPoint());
        }

        /// <summary>
        /// $Event Command
        /// </summary>
        /// <param name="eventPacket"></param>
        public void StartEvent(EventPacket eventPacket)
        {
            if (eventPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Event]EventType: {eventPacket.EventType.ToString()}");

                EventHelper.GenerateEvent(eventPacket.EventType);
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(EventPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $GlobalEvent Command
        /// </summary>
        /// <param name="globalEventPacket"></param>
        public void StartGlobalEvent(GlobalEventPacket globalEventPacket)
        {
            if (globalEventPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[GlobalEvent]EventType: {globalEventPacket.EventType.ToString()}");

                CommunicationServiceClient.Instance.RunGlobalEvent(globalEventPacket.EventType);
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(EventPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Stat Command
        /// </summary>
        /// <param name="statCommandPacket"></param>
        public void Stat(StatCommandPacket statCommandPacket)
        {
            Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Stat]");

            Session.SendPacket(Session.Character.GenerateSay($"{Language.Instance.GetMessageFromKey("XP_RATE_NOW")}: {ServerManager.Instance.Configuration.RateXP} ", 13));
            Session.SendPacket(Session.Character.GenerateSay($"{Language.Instance.GetMessageFromKey("DROP_RATE_NOW")}: {ServerManager.Instance.Configuration.RateDrop} ", 13));
            Session.SendPacket(Session.Character.GenerateSay($"{Language.Instance.GetMessageFromKey("GOLD_RATE_NOW")}: {ServerManager.Instance.Configuration.RateGold} ", 13));
            Session.SendPacket(Session.Character.GenerateSay($"{Language.Instance.GetMessageFromKey("GOLD_DROPRATE_NOW")}: {ServerManager.Instance.Configuration.RateGoldDrop} ", 13));
            Session.SendPacket(Session.Character.GenerateSay($"{Language.Instance.GetMessageFromKey("HERO_XPRATE_NOW")}: {ServerManager.Instance.Configuration.RatePrestigeXP} ", 13));
            Session.SendPacket(Session.Character.GenerateSay($"{Language.Instance.GetMessageFromKey("PRESTIGE_XPRATE_NOW")}: {ServerManager.Instance.Configuration.RatePrestigeXP} ", 13));
            Session.SendPacket(Session.Character.GenerateSay($"{Language.Instance.GetMessageFromKey("FAIRYXP_RATE_NOW")}: {ServerManager.Instance.Configuration.RateFairyXP} ", 13));
            Session.SendPacket(Session.Character.GenerateSay($"{Language.Instance.GetMessageFromKey("SERVER_WORKING_TIME")}: {(Process.GetCurrentProcess().StartTime - DateTime.Now).ToString(@"d\ hh\:mm\:ss")} ", 13));

            foreach (string message in CommunicationServiceClient.Instance.RetrieveServerStatistics())
            {
                Session.SendPacket(Session.Character.GenerateSay(message, 13));
            }
        }
    
        /// <summary>
        /// $Sudo Command
        /// </summary>
        /// <param name="sudoPacket"></param>
        public void SudoCommand(SudoPacket sudoPacket)
        {
            if (sudoPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Sudo]CharacterName: {sudoPacket.CharacterName} CommandContents:{sudoPacket.CommandContents}");

                if (sudoPacket.CharacterName == "*")
                {
                    foreach (ClientSession sess in Session.CurrentMapInstance.Sessions)
                    {
                        sess.ReceivePacket(sudoPacket.CommandContents, true);
                    }
                }
                else
                {
                    ClientSession session = ServerManager.Instance.GetSessionByCharacterName(sudoPacket.CharacterName);

                    if (session != null && !string.IsNullOrWhiteSpace(sudoPacket.CommandContents))
                    {
                        session.ReceivePacket(sudoPacket.CommandContents, true);
                    }
                    else
                    {
                        Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("USER_NOT_CONNECTED"), 0));
                    }
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(SudoPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Summon Command
        /// </summary>
        /// <param name="summonPacket"></param>
        public void Summon(SummonPacket summonPacket)
        {
            if (summonPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Summon]NpcMonsterVNum: {summonPacket.NpcMonsterVNum} Amount: {summonPacket.Amount} IsMoving: {summonPacket.IsMoving}");

                if (Session.IsOnMap && Session.HasCurrentMapInstance)
                {
                    NpcMonster npcmonster = ServerManager.GetNpc(summonPacket.NpcMonsterVNum);
                    if (npcmonster == null)
                    {
                        return;
                    }
                    Random random = new Random();
                    for (int i = 0; i < summonPacket.Amount; i++)
                    {
                        List<MapCell> possibilities = new List<MapCell>();
                        for (short x = -4; x < 5; x++)
                        {
                            for (short y = -4; y < 5; y++)
                            {
                                possibilities.Add(new MapCell { X = x, Y = y });
                            }
                        }
                        foreach (MapCell possibilitie in possibilities.OrderBy(s => random.Next()))
                        {
                            short mapx = (short)(Session.Character.PositionX + possibilitie.X);
                            short mapy = (short)(Session.Character.PositionY + possibilitie.Y);
                            if (!Session.CurrentMapInstance?.Map.IsBlockedZone(mapx, mapy) ?? false)
                            {
                                break;
                            }
                        }

                        if (Session.HasCurrentMapInstance)
                        {
                            MapMonster monster = new MapMonster
                            {
                                MonsterVNum = summonPacket.NpcMonsterVNum,
                                MapY = Session.Character.PositionY,
                                MapX = Session.Character.PositionX,
                                MapId = Session.Character.MapInstance.Map.MapId,
                                Position = (byte)Session.Character.Direction,
                                IsMoving = summonPacket.IsMoving,
                                MapMonsterId = Session.CurrentMapInstance.GetNextMonsterId(),
                                ShouldRespawn = false
                            };
                            monster.Initialize(Session.CurrentMapInstance);
                            Session.CurrentMapInstance.AddMonster(monster);
                            Session.CurrentMapInstance.Broadcast(monster.GenerateIn());
                        }
                    }
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(SummonPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $SummonNPC Command
        /// </summary>
        /// <param name="summonNPCPacket"></param>
        public void SummonNPC(SummonNPCPacket summonNPCPacket)
        {
            if (summonNPCPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[SummonNPC]NpcMonsterVNum: {summonNPCPacket.NpcMonsterVNum} Amount: {summonNPCPacket.Amount} IsMoving: {summonNPCPacket.IsMoving}");

                if (Session.IsOnMap && Session.HasCurrentMapInstance)
                {
                    NpcMonster npcmonster = ServerManager.GetNpc(summonNPCPacket.NpcMonsterVNum);
                    if (npcmonster == null)
                    {
                        return;
                    }
                    Random random = new Random();
                    for (int i = 0; i < summonNPCPacket.Amount; i++)
                    {
                        List<MapCell> possibilities = new List<MapCell>();
                        for (short x = -4; x < 5; x++)
                        {
                            for (short y = -4; y < 5; y++)
                            {
                                possibilities.Add(new MapCell { X = x, Y = y });
                            }
                        }
                        foreach (MapCell possibilitie in possibilities.OrderBy(s => random.Next()))
                        {
                            short mapx = (short)(Session.Character.PositionX + possibilitie.X);
                            short mapy = (short)(Session.Character.PositionY + possibilitie.Y);
                            if (!Session.CurrentMapInstance?.Map.IsBlockedZone(mapx, mapy) ?? false)
                            {
                                break;
                            }
                        }
                        if (Session.HasCurrentMapInstance)
                        {
                            MapNpc npc = new MapNpc
                            {
                                NpcVNum = summonNPCPacket.NpcMonsterVNum,
                                MapY = Session.Character.PositionY,
                                MapX = Session.Character.PositionX,
                                MapId = Session.Character.MapInstance.Map.MapId,
                                Position = Session.Character.Direction,
                                IsMoving = summonNPCPacket.IsMoving,
                                MapNpcId = Session.CurrentMapInstance.GetNextMonsterId()
                            };
                            npc.Initialize(Session.CurrentMapInstance);
                            Session.CurrentMapInstance.AddNPC(npc);
                            Session.CurrentMapInstance.Broadcast(npc.GenerateIn());
                        }
                    }
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(SummonNPCPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Teleport Command
        /// </summary>
        /// <param name="teleportPacket"></param>
        public void Teleport(TeleportPacket teleportPacket)
        {
            if (teleportPacket != null)
            {
                if (Session.Character.HasShopOpened || Session.Character.InExchangeOrTrade)
                {
                    Session.Character.Dispose();
                }
                if (Session.Character.IsChangingMapInstance)
                {
                    return;
                }
                if (short.TryParse(teleportPacket.Data, out short mapId))
                {
                    Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Teleport]MapId: {teleportPacket.Data} MapX: {teleportPacket.X} MapY: {teleportPacket.Y}");
                    ServerManager.Instance.ChangeMap(Session.Character.CharacterId, mapId, teleportPacket.X, teleportPacket.Y);
                }
                else
                {
                    Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Teleport]CharacterName: {teleportPacket.Data}");

                    ClientSession session = ServerManager.Instance.GetSessionByCharacterName(teleportPacket.Data);
                    if (session != null)
                    {
                        short mapX = session.Character.PositionX;
                        short mapY = session.Character.PositionY;
                        if (session.Character.Miniland == session.Character.MapInstance)
                        {
                            ServerManager.Instance.JoinMiniland(Session, session);
                        }
                        else
                        {
                            ServerManager.Instance.ChangeMapInstance(Session.Character.CharacterId, session.Character.MapInstanceId, mapX, mapY);
                        }
                    }
                    else
                    {
                        Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("USER_NOT_CONNECTED"), 0));
                    }
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(TeleportPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $TeleportToMe Command
        /// </summary>
        /// <param name="teleportToMePacket"></param>
        public void TeleportToMe(TeleportToMePacket teleportToMePacket)
        {
            Random random = new Random();
            if (teleportToMePacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[TeleportToMe]CharacterName: {teleportToMePacket.CharacterName}");

                if (teleportToMePacket.CharacterName == "*")
                {
                    Parallel.ForEach(ServerManager.Instance.Sessions.Where(s => s.Character != null && s.Character.CharacterId != Session.Character.CharacterId), session =>
                    {
                        // clear any shop or trade on target character
                        session.Character.Dispose();
                        if (!session.Character.IsChangingMapInstance && Session.HasCurrentMapInstance)
                        {
                            List<MapCell> possibilities = new List<MapCell>();
                            for (short x = -6, y = -6; x < 6 && y < 6; x++, y++)
                            {
                                possibilities.Add(new MapCell { X = x, Y = y });
                            }

                            short mapXPossibility = Session.Character.PositionX;
                            short mapYPossibility = Session.Character.PositionY;
                            foreach (MapCell possibility in possibilities.OrderBy(s => random.Next()))
                            {
                                mapXPossibility = (short)(Session.Character.PositionX + possibility.X);
                                mapYPossibility = (short)(Session.Character.PositionY + possibility.Y);
                                if (!Session.CurrentMapInstance.Map.IsBlockedZone(mapXPossibility, mapYPossibility))
                                {
                                    break;
                                }
                            }
                            if (Session.Character.Miniland == Session.Character.MapInstance)
                            {
                                ServerManager.Instance.JoinMiniland(session, Session);
                            }
                            else
                            {
                                ServerManager.Instance.ChangeMapInstance(session.Character.CharacterId, Session.Character.MapInstanceId, mapXPossibility, mapYPossibility);
                            }
                        }
                    });
                }
                else
                {
                    ClientSession targetSession = ServerManager.Instance.GetSessionByCharacterName(teleportToMePacket.CharacterName);
                    if (targetSession?.Character.IsChangingMapInstance == false)
                    {
                        targetSession.Character.Dispose();
                        ServerManager.Instance.ChangeMapInstance(targetSession.Character.CharacterId, Session.Character.MapInstanceId, (short)(Session.Character.PositionX + 1), (short)(Session.Character.PositionY + 1));
                    }
                    else
                    {
                        Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("USER_NOT_CONNECTED"), 0));
                    }
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(TeleportToMePacket.ReturnHelp(), 10));
            }
        }

        public void ReturnPoint(ReturnPointPacket packet)
        {
            if (packet != null)
            {
                if (!Session.Character.StaticBonusList.Any(s => s.StaticBonusType == StaticBonusType.MultipleReturns))
                {
                    Session.SendPacket(
                        Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("RETURNPOINT_NOT_PERMITTED"),
                            10));
                    return;
                }

                if (Session.Character.MapInstance.MapInstanceType != MapInstanceType.BaseMapInstance)
                {
                    Session.SendPacket(
                        Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("CANT_USE_THAT"), 10));
                    return;
                }

                if (packet.ReturnPointId < 1 || packet.ReturnPointId > 5)
                {
                    return;
                }

                RespawnDTO resp = Session.Character.Respawns.Find(s => s.RespawnMapTypeId == packet.ReturnPointId + 50);
                if (resp == null)
                {
                    Session.SendPacket(
                        Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("RETURNPOINT_NOT_SET"), 10));
                    return;
                }

                if (ServerManager.Instance.ChannelId == 51 || ServerManager
                        .GetMapInstance(ServerManager.GetBaseMapInstanceIdByMapId(resp.MapId)).Map.MapTypes
                        .Any(s => s.MapTypeId == (short)MapTypeEnum.Act4))
                {
                    Session.SendPacket(
                        Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("CANT_USE_THAT"), 10));
                    return;
                }

                ServerManager.Instance.LeaveMap(Session.Character.CharacterId);
                ServerManager.Instance.ChangeMap(Session.Character.CharacterId, resp.MapId, resp.X, resp.Y);
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(ReturnPointPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Unban Command
        /// </summary>
        /// <param name="unbanPacket"></param>
        public void Unban(UnbanPacket unbanPacket)
        {
            if (unbanPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(),
                    $"[Unban]CharacterName: {unbanPacket.CharacterName}");

                string name = unbanPacket.CharacterName;
                CharacterDTO chara = DAOFactory.CharacterDAO.LoadByName(name);
                if (chara != null)
                {
                    PenaltyLogDTO log = ServerManager.Instance.PenaltyLogs.Find(s =>
                        s.AccountId == chara.AccountId && s.Penalty == PenaltyType.Banned && s.DateEnd > DateTime.Now);
                    if (log != null)
                    {
                        log.DateEnd = DateTime.Now.AddSeconds(-1);
                        Character.InsertOrUpdatePenalty(log);
                        Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"),
                            10));
                    }
                    else
                    {
                        Session.SendPacket(
                            Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("USER_NOT_BANNED"), 10));
                    }
                }
                else
                {
                    Session.SendPacket(
                        Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("USER_NOT_FOUND"), 10));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(UnbanPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Undercover Command
        /// </summary>
        /// <param name="undercoverPacket"></param>
        public void Undercover(UndercoverPacket undercoverPacket)
        {
            Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Undercover]");

            Session.Character.Undercover = !Session.Character.Undercover;
            Session.SendPacket(Session.Character.GenerateEq());
            Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateIn(), ReceiverType.AllExceptMe);
            Session.CurrentMapInstance?.Broadcast(Session, Session.Character.GenerateGidx(), ReceiverType.AllExceptMe);
        }

        /// <summary>
        /// $Unmute Command
        /// </summary>
        /// <param name="unmutePacket"></param>
        public void Unmute(UnmutePacket unmutePacket)
        {
            if (unmutePacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(),
                    $"[Unmute]CharacterName: {unmutePacket.CharacterName}");

                string name = unmutePacket.CharacterName;
                CharacterDTO chara = DAOFactory.CharacterDAO.LoadByName(name);
                if (chara != null)
                {
                    if (ServerManager.Instance.PenaltyLogs.Any(s =>
                        s.AccountId == chara.AccountId && s.Penalty == (byte)PenaltyType.Muted
                        && s.DateEnd > DateTime.Now))
                    {
                        PenaltyLogDTO log = ServerManager.Instance.PenaltyLogs.Find(s =>
                            s.AccountId == chara.AccountId && s.Penalty == (byte)PenaltyType.Muted
                            && s.DateEnd > DateTime.Now);
                        if (log != null)
                        {
                            log.DateEnd = DateTime.Now.AddSeconds(-1);
                            Character.InsertOrUpdatePenalty(log);
                        }

                        Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"),
                            10));
                    }
                    else
                    {
                        Session.SendPacket(
                            Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("USER_NOT_MUTED"), 10));
                    }
                }
                else
                {
                    Session.SendPacket(
                        Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("USER_NOT_FOUND"), 10));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(UnmutePacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Unstuck Command
        /// </summary>
        /// <param name="unstuckPacket"></param>
        public void Unstuck(UnstuckPacket unstuckPacket)
        {
            if (Session?.Character != null)
            {
                if (Session.Character.Miniland == Session.Character.MapInstance)
                {
                    ServerManager.Instance.JoinMiniland(Session, Session);
                }
                else
                {
                    
                    ServerManager.Instance.ChangeMapInstance(Session.Character.CharacterId, Session.Character.MapInstanceId, Session.Character.PositionX, Session.Character.PositionY);
                    Session.SendPacket(StaticPacketHelper.Cancel(2));
                }
            }
        }

        /// <summary>
        /// $Upgrade Command
        /// </summary>
        /// <param name="upgradePacket"></param>
        public void Upgrade(UpgradeCommandPacket upgradePacket)
        {
            if (upgradePacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Upgrade]Slot: {upgradePacket.Slot} Mode: {upgradePacket.Mode} Protection: {upgradePacket.Protection}");

                if (upgradePacket.Slot >= 0)
                {
                    ItemInstance wearableInstance = Session.Character.Inventory.LoadBySlotAndType(upgradePacket.Slot, 0);
                    wearableInstance?.UpgradeItem(Session, upgradePacket.Mode, upgradePacket.Protection, true);
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(UpgradeCommandPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Warn Command
        /// </summary>
        /// <param name="warningPacket"></param>
        public void Warn(WarningPacket warningPacket)
        {
            if (warningPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(),
                    $"[Warn]CharacterName: {warningPacket.CharacterName} Reason: {warningPacket.Reason}");

                string characterName = warningPacket.CharacterName;
                CharacterDTO character = DAOFactory.CharacterDAO.LoadByName(characterName);
                if (character != null)
                {
                    ClientSession session = ServerManager.Instance.GetSessionByCharacterName(characterName);
                    session?.SendPacket(UserInterfaceHelper.GenerateInfo(
                        string.Format(Language.Instance.GetMessageFromKey("WARNING"), warningPacket.Reason)));
                    Character.InsertOrUpdatePenalty(new PenaltyLogDTO
                    {
                        AccountId = character.AccountId,
                        Reason = warningPacket.Reason,
                        Penalty = PenaltyType.Warning,
                        DateStart = DateTime.Now,
                        DateEnd = DateTime.Now,
                        AdminName = Session.Character.Name
                    });
                    switch (DAOFactory.PenaltyLogDAO.LoadByAccount(character.AccountId)
                        .Count(p => p.Penalty == PenaltyType.Warning))
                    {
                        case 1:
                            break;

                        case 2:
                            MuteMethod(characterName, "Auto-Warning mute: 2 strikes", 30);
                            break;

                        case 3:
                            MuteMethod(characterName, "Auto-Warning mute: 3 strikes", 60);
                            break;

                        case 4:
                            MuteMethod(characterName, "Auto-Warning mute: 4 strikes", 720);
                            break;

                        case 5:
                            MuteMethod(characterName, "Auto-Warning mute: 5 strikes", 1440);
                            break;

                        case 69:
                            BanMethod(characterName, 7, "LOL SIXTY NINE AMIRITE?");
                            break;

                        default:
                            MuteMethod(characterName, "You've been THUNDERSTRUCK",
                                6969); // imagined number as for I = √(-1), complex z = a + bi
                            break;
                    }
                }
                else
                {
                    Session.SendPacket(
                        Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("USER_NOT_FOUND"), 10));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(WarningPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $WigColor Command
        /// </summary>
        /// <param name="wigColorPacket"></param>
        public void WigColor(WigColorPacket wigColorPacket)
        {
            if (wigColorPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[WigColor]Color: {wigColorPacket.Color}");

                ItemInstance wig = Session.Character.Inventory.LoadBySlotAndType((byte)EquipmentType.Hat, InventoryType.Wear);
                if (wig != null)
                {
                    wig.Design = wigColorPacket.Color;
                    Session.SendPacket(Session.Character.GenerateEq());
                    Session.SendPacket(Session.Character.GenerateEquipment());
                    Session.CurrentMapInstance?.Broadcast(Session.Character.GenerateIn());
                    Session.CurrentMapInstance?.Broadcast(Session.Character.GenerateGidx());
                }
                else
                {
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("NO_WIG"), 0));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(WigColorPacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $XpRate Command
        /// </summary>
        /// <param name="xpRatePacket"></param>
        public void XpRate(XpRatePacket xpRatePacket)
        {
            if (xpRatePacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[XpRate]Value: {xpRatePacket.Value}");

                if (xpRatePacket.Value <= 1000)
                {
                    ServerManager.Instance.Configuration.RateXP = xpRatePacket.Value;
                    ServerManager.Shout($"LA EXP CAMBIO, DISFRUTEN TODOS! ");
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("XP_RATE_CHANGED"), 0));
                }
                else
                {
                    Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("WRONG_VALUE"), 0));
                }
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(XpRatePacket.ReturnHelp(), 10));
            }
        }

        /// <summary>
        /// $Zoom Command
        /// </summary>
        /// <param name="zoomPacket"></param>
        public void Zoom(ZoomPacket zoomPacket)
        {
            if (zoomPacket != null)
            {
                Logger.LogUserEvent("GMCOMMAND", Session.GenerateIdentity(), $"[Zoom]Value: {zoomPacket.Value}");

                Session.SendPacket(UserInterfaceHelper.GenerateGuri(15, zoomPacket.Value, Session.Character.CharacterId));
            }
            Session.Character.GenerateSay(ZoomPacket.ReturnHelp(), 10);
        }

        /// <summary>
        /// private addMate method
        /// </summary>
        /// <param name="vnum"></param>
        /// <param name="level"></param>
        /// <param name="mateType"></param>
        private void addMate(short vnum, byte level, MateType mateType)
        {
            NpcMonster mateNpc = ServerManager.GetNpc(vnum);
            if (Session.CurrentMapInstance == Session.Character.Miniland && mateNpc != null)
            {
                level = level == 0 ? (byte)1 : level;
                Mate mate = new Mate(Session.Character, mateNpc, level, mateType);
                Session.Character.AddPet(mate);
            }
            else
            {
                Session.SendPacket(UserInterfaceHelper.GenerateMsg(Language.Instance.GetMessageFromKey("NOT_IN_MINILAND"), 0));
            }
        }

        /// <summary>
        /// private add portal command
        /// </summary>
        /// <param name="destinationMapId"></param>
        /// <param name="destinationX"></param>
        /// <param name="destinationY"></param>
        /// <param name="type"></param>
        /// <param name="insertToDatabase"></param>
        private void addPortal(short destinationMapId, short destinationX, short destinationY, short type, bool insertToDatabase)
        {
            if (Session.HasCurrentMapInstance)
            {
                Portal portal = new Portal
                {
                    SourceMapId = Session.Character.MapId,
                    SourceX = Session.Character.PositionX,
                    SourceY = Session.Character.PositionY,
                    DestinationMapId = destinationMapId,
                    DestinationX = destinationX,
                    DestinationY = destinationY,
                    DestinationMapInstanceId = insertToDatabase ? Guid.Empty : destinationMapId == 20000 ? Session.Character.Miniland.MapInstanceId : Guid.Empty,
                    Type = type
                };
                if (insertToDatabase)
                {
                    DAOFactory.PortalDAO.Insert(portal);
                }
                Session.CurrentMapInstance.Portals.Add(portal);
                Session.CurrentMapInstance?.Broadcast(portal.GenerateGp());
            }
        }

        /// <summary>
        /// private ban method
        /// </summary>
        /// <param name="characterName"></param>
        /// <param name="duration"></param>
        /// <param name="reason"></param>
        private void BanMethod(string characterName, int duration, string reason)
        {
            CharacterDTO character = DAOFactory.CharacterDAO.LoadByName(characterName);
            if (character != null)
            {
                ServerManager.Instance.Kick(characterName);
                PenaltyLogDTO log = new PenaltyLogDTO
                {
                    AccountId = character.AccountId,
                    Reason = reason?.Trim(),
                    Penalty = PenaltyType.Banned,
                    DateStart = DateTime.Now,
                    DateEnd = duration == 0 ? DateTime.Now.AddYears(15) : DateTime.Now.AddDays(duration),
                    AdminName = Session.Character.Name
                };
                Character.InsertOrUpdatePenalty(log);
                Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("USER_NOT_FOUND"),
                    10));
            }
        }

        /// <summary>
        /// private mute method
        /// </summary>
        /// <param name="characterName"></param>
        /// <param name="reason"></param>
        /// <param name="duration"></param>
        private void MuteMethod(string characterName, string reason, int duration)
        {
            CharacterDTO characterToMute = DAOFactory.CharacterDAO.LoadByName(characterName);
            if (characterToMute != null)
            {
                ClientSession session = ServerManager.Instance.GetSessionByCharacterName(characterName);
                if (session?.Character.IsMuted() == false)
                {
                    session.SendPacket(UserInterfaceHelper.GenerateInfo(
                        string.Format(Language.Instance.GetMessageFromKey("MUTED_PLURAL"), reason, duration)));
                }

                PenaltyLogDTO log = new PenaltyLogDTO
                {
                    AccountId = characterToMute.AccountId,
                    Reason = reason,
                    Penalty = PenaltyType.Muted,
                    DateStart = DateTime.Now,
                    DateEnd = DateTime.Now.AddMinutes(duration),
                    AdminName = Session.Character.Name
                };
                Character.InsertOrUpdatePenalty(log);
                Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("DONE"), 10));
            }
            else
            {
                Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("USER_NOT_FOUND"),
                    10));
            }
        }

        /// <summary>
        /// $DeactivateZoom Command
        /// </summary>
        /// <param name="deactivateZoomPacket"></param>
        public void DeactivateZoom(DeactivateZoomPacket deactivateZoomPacket)
        {
            Session.SendPacket("guri 15 0 1");
            Session.SendPacket(Session.Character.GenerateSay(Language.Instance.GetMessageFromKey("ZOOM_DEACTIVATED"), 12));
        }

        /// <summary>
        /// Helper method used for sending stats of desired character
        /// </summary>
        /// <param name="character"></param>
        private void sendStats(CharacterDTO character)
        {
            Session.SendPacket(Session.Character.GenerateSay("----- CHARACTER -----", 13));
            Session.SendPacket(Session.Character.GenerateSay($"Name: {character.Name}", 13));
            Session.SendPacket(Session.Character.GenerateSay($"Id: {character.CharacterId}", 13));
            Session.SendPacket(Session.Character.GenerateSay($"State: {character.State}", 13));
            Session.SendPacket(Session.Character.GenerateSay($"Gender: {character.Gender}", 13));
            Session.SendPacket(Session.Character.GenerateSay($"Class: {character.Class}", 13));
            Session.SendPacket(Session.Character.GenerateSay($"Level: {character.Level}", 13));
            Session.SendPacket(Session.Character.GenerateSay($"JobLevel: {character.JobLevel}", 13));
            Session.SendPacket(Session.Character.GenerateSay($"HeroLevel: {character.HeroLevel}", 13));
            Session.SendPacket(Session.Character.GenerateSay($"Prestige: {character.prestigeLevel}", 13));
            Session.SendPacket(Session.Character.GenerateSay($"Gold: {character.Gold}", 13));
            Session.SendPacket(Session.Character.GenerateSay($"Bio: {character.Biography}", 13));
            Session.SendPacket(Session.Character.GenerateSay($"MapId: {Session.CurrentMapInstance.Map.MapId}", 13));
            Session.SendPacket(Session.Character.GenerateSay($"MapX: {Session.Character.PositionX}", 13));
            Session.SendPacket(Session.Character.GenerateSay($"MapY: {Session.Character.PositionY}", 13));
            Session.SendPacket(Session.Character.GenerateSay($"Reputation: {character.Reputation}", 13));
            Session.SendPacket(Session.Character.GenerateSay($"Dignity: {character.Dignity}", 13));
            Session.SendPacket(Session.Character.GenerateSay($"Rage: {character.RagePoint}", 13));
            Session.SendPacket(Session.Character.GenerateSay($"Compliment: {character.Compliment}", 13));
            Session.SendPacket(Session.Character.GenerateSay($"Fraction: {(character.Faction == FactionType.Demon ? Language.Instance.GetMessageFromKey("DEMON") : Language.Instance.GetMessageFromKey("ANGEL"))}", 13));
            Session.SendPacket(Session.Character.GenerateSay("----- --------- -----", 13));
            AccountDTO account = DAOFactory.AccountDAO.LoadById(character.AccountId);
            if (account != null)
            {
                Session.SendPacket(Session.Character.GenerateSay("----- ACCOUNT -----", 13));
                Session.SendPacket(Session.Character.GenerateSay($"Id: {account.AccountId}", 13));
                Session.SendPacket(Session.Character.GenerateSay($"Name: {account.Name}", 13));
                Session.SendPacket(Session.Character.GenerateSay($"Authority: {account.Authority}", 13));
                Session.SendPacket(Session.Character.GenerateSay($"RegistrationIP: {account.RegistrationIP}", 13));
                Session.SendPacket(Session.Character.GenerateSay($"Email: {account.Email}", 13));
                Session.SendPacket(Session.Character.GenerateSay("----- ------- -----", 13));
                IEnumerable<PenaltyLogDTO> penaltyLogs = ServerManager.Instance.PenaltyLogs.Where(s => s.AccountId == account.AccountId).ToList();
                PenaltyLogDTO penalty = penaltyLogs.LastOrDefault(s => s.DateEnd > DateTime.Now);
                Session.SendPacket(Session.Character.GenerateSay("----- PENALTY -----", 13));
                if (penalty != null)
                {
                    Session.SendPacket(Session.Character.GenerateSay($"Type: {penalty.Penalty}", 13));
                    Session.SendPacket(Session.Character.GenerateSay($"AdminName: {penalty.AdminName}", 13));
                    Session.SendPacket(Session.Character.GenerateSay($"Reason: {penalty.Reason}", 13));
                    Session.SendPacket(Session.Character.GenerateSay($"DateStart: {penalty.DateStart}", 13));
                    Session.SendPacket(Session.Character.GenerateSay($"DateEnd: {penalty.DateEnd}", 13));
                }
                Session.SendPacket(Session.Character.GenerateSay($"Bans: {penaltyLogs.Count(s => s.Penalty == PenaltyType.Banned)}", 13));
                Session.SendPacket(Session.Character.GenerateSay($"Mutes: {penaltyLogs.Count(s => s.Penalty == PenaltyType.Muted)}", 13));
                Session.SendPacket(Session.Character.GenerateSay($"Warnings: {penaltyLogs.Count(s => s.Penalty == PenaltyType.Warning)}", 13));
                Session.SendPacket(Session.Character.GenerateSay("----- ------- -----", 13));
            }

            Session.SendPacket(Session.Character.GenerateSay("----- SESSION -----", 13));
            foreach (long[] connection in CommunicationServiceClient.Instance.RetrieveOnlineCharacters(character.CharacterId))
            {
                if (connection != null)
                {
                    CharacterDTO _character = DAOFactory.CharacterDAO.LoadById(connection[0]);
                    if (_character != null)
                    {
                        Session.SendPacket(Session.Character.GenerateSay($"Character Name: {_character.Name}", 13));
                        Session.SendPacket(Session.Character.GenerateSay($"ChannelId: {connection[1]}", 13));
                        Session.SendPacket(Session.Character.GenerateSay($"Current IP: {Session.IpAddress}", 13));
                        Session.SendPacket(Session.Character.GenerateSay($"Session: {Session.SessionId}", 13));
                        Session.SendPacket(Session.Character.GenerateSay($"TimeRegi: {Session.RegisterTime}", 13));
                        Session.SendPacket(Session.Character.GenerateSay($"Connected: {Session.IsConnected}", 13));
                        Session.SendPacket(Session.Character.GenerateSay($"Disposing: {Session.IsDisposing}", 13));
                        Session.SendPacket(Session.Character.GenerateSay($"Client: {Session.ClientId}", 13));
                        Session.SendPacket(Session.Character.GenerateSay($"HasSession: {Session.HasSession}", 13));
                        Session.SendPacket(Session.Character.GenerateSay("-----", 13));
                    }
                }
            }
            Session.SendPacket(Session.Character.GenerateSay("----- ------------ -----", 13));
        }

        #endregion
    }
}
          