using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using NosSharp.Enums;
using OpenNos.Core;
using OpenNos.GameObject.Helpers;
using OpenNos.GameObject.Map;
using OpenNos.GameObject.Networking;

namespace OpenNos.GameObject.Event.ICEBREAKER
{
    public class IceBreaker
    {
        public const int MaxAllowedPlayers = 50;

        private static readonly int[] GoldRewards =
        {
            100,
            1000,
            3000,
            5000,
            10000,
            20000
        };

        private static readonly Tuple<int, int>[] LevelBrackets =
        {
            new Tuple<int, int>(1, 25),
            new Tuple<int, int>(20, 40),
            new Tuple<int, int>(35, 55),
            new Tuple<int, int>(50, 70),
            new Tuple<int, int>(65, 85),
            new Tuple<int, int>(80, 99)
        };


        private static int _currentBracket;

        private static List<Group> _groups { get; set; }


        public static List<ClientSession> AlreadyFrozenPlayers { get; set; }

        public static List<ClientSession> FrozenPlayers { get; set; }

        public static MapInstance Map { get; private set; }


        public static void AddGroup(Group group)
        {
            _groups.Add(group);
        }

        public static Group GetGroupByClientSession(ClientSession session)
        {
            return _groups.FirstOrDefault(x => x.IsMemberOfGroup(session));
        }

        public static void MergeGroups(IEnumerable<Group> groups)
        {
            var newGroup = new Group(GroupType.IceBreaker);
            foreach (Group group in groups)
            {
                foreach (ClientSession character in group.Characters)
                {
                    newGroup.Characters.Add(character);
                    group.Characters.ToList().Remove(character);
                }

                RemoveGroup(group);
            }

            AddGroup(newGroup);
        }

        public static void RemoveGroup(Group group)
        {
            _groups.Remove(group);
        }

        public static bool SessionsHaveSameGroup(ClientSession session1, ClientSession session2)
        {
            return _groups != null && _groups.Any(x => x.IsMemberOfGroup(session1) && x.IsMemberOfGroup(session2));
        }


        public static void GenerateIceBreaker(bool useTimer = true)
        {
            Initialize();
            if (useTimer)
            {
                ServerManager.Instance.Broadcast(UserInterfaceHelper.Instance.GenerateMsg(
                    string.Format(Language.Instance.GetMessageFromKey("ICEBREAKER_MINUTES"), 5, LevelBrackets[_currentBracket].Item1, LevelBrackets[_currentBracket].Item2), 1));
                Thread.Sleep(5 * 60 * 1000);
                ServerManager.Instance.Broadcast(UserInterfaceHelper.Instance.GenerateMsg(
                    string.Format(Language.Instance.GetMessageFromKey("ICEBREAKER_MINUTES"), 1, LevelBrackets[_currentBracket].Item1, LevelBrackets[_currentBracket].Item2), 1));
                Thread.Sleep(1 * 60 * 1000);
                ServerManager.Instance.Broadcast(UserInterfaceHelper.Instance.GenerateMsg(
                    string.Format(Language.Instance.GetMessageFromKey("ICEBREAKER_SECONDS"), 30, LevelBrackets[_currentBracket].Item1, LevelBrackets[_currentBracket].Item2), 1));
                Thread.Sleep(30 * 1000);
                ServerManager.Instance.Broadcast(UserInterfaceHelper.Instance.GenerateMsg(
                    string.Format(Language.Instance.GetMessageFromKey("ICEBREAKER_SECONDS"), 10, LevelBrackets[_currentBracket].Item1, LevelBrackets[_currentBracket].Item2), 1));
                Thread.Sleep(10 * 1000);
            }
            
            ServerManager.Instance.Broadcast(
                UserInterfaceHelper.Instance.GenerateMsg(Language.Instance.GetMessageFromKey("ICEBREAKER_STARTED"),
                    1));
            ServerManager.Instance.Broadcast(
                $"qnaml 2 #guri^501 {Language.Instance.GetMessageFromKey("ICEBREAKER_QUESTION")}");
            ServerManager.Instance.Broadcast(UserInterfaceHelper.Instance.GenerateMsg(Language.Instance.GetMessageFromKey("ICEBREAKER_STARTED"), 1));
            ServerManager.Instance.IceBreakerInWaiting = true;
            ServerManager.Instance.Sessions.Where(x => x.Character.Level >= LevelBrackets[_currentBracket].Item1 && x.Character.Level <= LevelBrackets[_currentBracket].Item2 && x.CurrentMapInstance.MapInstanceType == MapInstanceType.BaseMapInstance).ToList().ForEach(x => x.SendPacket($"qnaml 2 #guri^501 {string.Format(Language.Instance.GetMessageFromKey("ICEBREAKER_ASK"), 500)}"));
            _currentBracket++;
            if (_currentBracket > 5)
            {
                _currentBracket = 0;
            }

            Observable.Timer(TimeSpan.FromSeconds(10)).Subscribe(c =>
            {
                ServerManager.Instance.StartedEvents.Remove(EventType.ICEBREAKER);
                ServerManager.Instance.IceBreakerInWaiting = false;
                if (Map.Sessions.Count() <= 1)
                {
                    Map.Broadcast(
                        UserInterfaceHelper.Instance.GenerateMsg(Language.Instance.GetMessageFromKey("ICEBREAKER_WIN"),
                            0));
                    Map.Sessions.ToList().ForEach(x =>
                    {
                        x.Character.GetReput(x.Character.Level * 10, true);
                        if (x.Character.Dignity < 100)
                        {
                            x.Character.Dignity = 100;
                        }

                        x.Character.Gold += GoldRewards[_currentBracket];
                        x.Character.Gold = x.Character.Gold > ServerManager.Instance.MaxGold
                            ? ServerManager.Instance.MaxGold
                            : x.Character.Gold;
                        x.SendPacket(x.Character.GenerateFd());
                        x.SendPacket(x.Character.GenerateGold());
                        x.SendPacket(x.Character.GenerateSay(
                            string.Format(Language.Instance.GetMessageFromKey("WIN_MONEY"),
                                GoldRewards[_currentBracket]), 10));
                        x.SendPacket(x.Character.GenerateSay(
                            string.Format(Language.Instance.GetMessageFromKey("WIN_REPUT"), x.Character.Level * 10),
                            10));
                        x.SendPacket(x.Character.GenerateSay(
                            string.Format(Language.Instance.GetMessageFromKey("DIGNITY_RESTORED"),
                                x.Character.Level * 10), 10));
                    });
                    Thread.Sleep(5000);
                    EventHelper.Instance.ScheduleEvent(TimeSpan.FromSeconds(10),
                        new EventContainer(Map, EventActionType.DISPOSEMAP, null));
                }
                else
                {
                    Map.Broadcast(
                        UserInterfaceHelper.Instance.GenerateMsg(
                            Language.Instance.GetMessageFromKey("ICEBREAKER_FIGHT_WARN"), 0));
                    Observable.Timer(TimeSpan.FromSeconds(6)).Subscribe(s =>
                    {
                        Map.Broadcast(UserInterfaceHelper.Instance.GenerateMsg(
                            Language.Instance.GetMessageFromKey("ICEBREAKER_FIGHT_WARN"), 0));
                    });
                    Observable.Timer(TimeSpan.FromSeconds(13)).Subscribe(s =>
                    {
                        Map.Broadcast(UserInterfaceHelper.Instance.GenerateMsg(
                            Language.Instance.GetMessageFromKey("ICEBREAKER_FIGHT_WARN"), 0));
                    });
                    Observable.Timer(TimeSpan.FromSeconds(14)).Subscribe(s =>
                    {
                        Map.Broadcast(UserInterfaceHelper.Instance.GenerateMsg(
                            Language.Instance.GetMessageFromKey("ICEBREAKER_FIGHT_WARN"), 0));
                    });

                    Observable.Timer(TimeSpan.FromSeconds(15)).Subscribe(s =>
                    {
                        Map.Broadcast(
                            UserInterfaceHelper.Instance.GenerateMsg(
                                Language.Instance.GetMessageFromKey("ICEBREAKER_FIGHT_START"), 0));
                        Map.IsPvp = true;
                    });
                    IDisposable obs = null;
                    obs = Observable.Interval(TimeSpan.FromSeconds(1)).Subscribe(b =>
                    {
                        if (Map.Sessions.Count() > 1 && AlreadyFrozenPlayers.Count != Map.Sessions.Count() &&
                            _groups.Count > 1)
                        {
                            return;
                        }

                        Map.Broadcast(
                            UserInterfaceHelper.Instance.GenerateMsg(
                                Language.Instance.GetMessageFromKey("ICEBREAKER_WIN"), 0));
                        Map.Sessions.ToList().ForEach(x =>
                        {
                            x.Character.GetReput(x.Character.Level * 10, true);
                            if (x.Character.Dignity < 100)
                            {
                                x.Character.Dignity = 100;
                            }

                            x.Character.Gold += GoldRewards[_currentBracket];
                            x.Character.Gold = x.Character.Gold > ServerManager.Instance.MaxGold
                                ? ServerManager.Instance.MaxGold
                                : x.Character.Gold;
                            x.SendPacket(x.Character.GenerateFd());
                            x.SendPacket(x.Character.GenerateGold());
                            x.SendPacket(x.Character.GenerateSay(
                                string.Format(Language.Instance.GetMessageFromKey("WIN_MONEY"),
                                    GoldRewards[_currentBracket]), 10));
                            x.SendPacket(x.Character.GenerateSay(
                                string.Format(Language.Instance.GetMessageFromKey("WIN_REPUT"), x.Character.Level * 10),
                                10));
                            x.SendPacket(x.Character.GenerateSay(
                                string.Format(Language.Instance.GetMessageFromKey("DIGNITY_RESTORED"),
                                    x.Character.Level * 10), 10));
                            obs.Dispose();
                        });
                        EventHelper.Instance.ScheduleEvent(TimeSpan.FromSeconds(10),
                            new EventContainer(Map, EventActionType.DISPOSEMAP, null));
                    });
                }
            });
        }


        private static void Initialize()
        {
            AlreadyFrozenPlayers = new List<ClientSession>();
            FrozenPlayers = new List<ClientSession>();
            Map = ServerManager.Instance.GenerateMapInstance(2005, MapInstanceType.IceBreakerInstance,
                new InstanceBag());
            _groups = new List<Group>();
        }
    }
}
