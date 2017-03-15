﻿/*
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
using OpenNos.Domain;
using OpenNos.GameObject.Helpers;
using System;
using System.Reactive.Linq;
using System.Threading;

namespace OpenNos.GameObject.Event
{
    public class LOD
    {
        #region Methods

        public static void GenerateLod(int lodtime = 120)
        {
            const int HornTime = 30;
            const int HornRepawn = 4;
            const int HornStay = 1;
            ServerManager.Instance.EnableMapEffect(98, true);
            LODThread lodThread = new LODThread();
            Thread thread = new Thread(() => lodThread.Run(lodtime * 60, HornTime * 60, HornRepawn * 60, HornStay * 60));
            thread.Start();
        }
        #endregion
    }

    public class LODThread
    {
        public void Run(int LODTime, int HornTime, int HornRespawn, int HornStay)
        {
            const int interval = 30;
            int dhspawns = 0;

            while (LODTime > 0)
            {
                RefreshLOD(LODTime);

                if (LODTime == HornTime)
                {
                    SpinWait.SpinUntil(() => !ServerManager.Instance.inFamilyRefreshMode);
                    foreach (Family fam in ServerManager.Instance.FamilyList.ToArray())
                    {
                        if (fam.LandOfDeath != null)
                        {
                            fam.LandOfDeath.Data.RunMapEvent(EventActionType.XPRATE, 3);
                            fam.LandOfDeath.Data.RunMapEvent(EventActionType.DROPRATE, 3);
                            SpawnDH(fam.LandOfDeath);
                        }
                    }
                }
                else if (LODTime == HornTime - (HornRespawn * dhspawns))
                {
                    SpinWait.SpinUntil(() => !ServerManager.Instance.inFamilyRefreshMode);
                    foreach (Family fam in ServerManager.Instance.FamilyList.ToArray())
                    {
                        if (fam.LandOfDeath != null)
                        {
                            fam.LandOfDeath.Data.RunMapEvent(EventActionType.XPRATE, 3);
                            fam.LandOfDeath.Data.RunMapEvent(EventActionType.DROPRATE, 3);
                            SpawnDH(fam.LandOfDeath);
                        }
                    }
                }
                else if (LODTime == HornTime - (HornRespawn * dhspawns) - HornStay)
                {
                    SpinWait.SpinUntil(() => !ServerManager.Instance.inFamilyRefreshMode);
                    foreach (Family fam in ServerManager.Instance.FamilyList.ToArray())
                    {
                        if (fam.LandOfDeath != null)
                        {
                            DespawnDH(fam.LandOfDeath);
                            dhspawns++;
                        }
                    }
                }

                LODTime -= interval;
                Thread.Sleep(interval * 1000);
            }
            EndLOD();
        }

        private void RefreshLOD(double remaining)
        {
            SpinWait.SpinUntil(() => !ServerManager.Instance.inFamilyRefreshMode);
            foreach (Family fam in ServerManager.Instance.FamilyList.ToArray())
            {
                if (fam.LandOfDeath == null)
                {
                    fam.LandOfDeath = ServerManager.GenerateMapInstanceNode(150, MapInstanceType.LodInstance);
                }
                fam.LandOfDeath.RunMapTreeEvent(EventActionType.CLOCK, remaining);
            }
        }

        private void EndLOD()
        {
            SpinWait.SpinUntil(() => !ServerManager.Instance.inFamilyRefreshMode);
            foreach (Family fam in ServerManager.Instance.FamilyList.ToArray())
            {
                if (fam.LandOfDeath != null)
                {
                    fam.LandOfDeath.Data.RunMapEvent(EventActionType.DISPOSE, null);
                    fam.LandOfDeath = null;
                }
            }
            ServerManager.Instance.StartedEvents.Remove(EventType.LOD);
            ServerManager.Instance.StartedEvents.Remove(EventType.LODDH);
            ServerManager.Instance.EnableMapEffect(98, false);
        }

        private void SpawnDH(MapInstanceNode LandOfDeath)
        {
            LandOfDeath.Data.RunMapEvent(EventActionType.SPAWNONLASTENTRY, 443);
            LandOfDeath.Data.RunMapEvent(EventActionType.MESSAGE, "df 2");
            LandOfDeath.Data.RunMapEvent(EventActionType.MESSAGE, UserInterfaceHelper.Instance.GenerateMsg(Language.Instance.GetMessageFromKey("HORN_APPEAR"), 0));
        }
        private void DespawnDH(MapInstanceNode LandOfDeath)
        {
            LandOfDeath.Data.RunMapEvent(EventActionType.MESSAGE, UserInterfaceHelper.Instance.GenerateMsg(Language.Instance.GetMessageFromKey("HORN_DISAPEAR"), 0));
            LandOfDeath.Data.RunMapEvent(EventActionType.LOCK, true);
            LandOfDeath.Data.RunMapEvent(EventActionType.UNSPAWN, 443);
        }
    }
}