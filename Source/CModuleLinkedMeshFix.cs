/*
 * Fix for struts and fuel lines connected in the EVA construction mode
 * https://github.com/xmnovotny/KSP-CModuleLinkedMeshFix
 * License: GPL v2.
 * 
 */

using UnityEngine;
using System.Collections.Generic;
using System;

namespace CModuleLinkedMeshFix
{
	[KSPAddon(KSPAddon.Startup.PSystemSpawn, false)]
	public class CModuleLinkedMeshFix : MonoBehaviour
	{
        public enum LogLevel {
            WARNING = 0,
            INFO = 1,
            DEBUG = 2
        };

#if DEBUG
        public static LogLevel logLevel = LogLevel.DEBUG;
#else
        public static LogLevel logLevel = LogLevel.WARNING;
#endif
        const uint DUPLICATE_CRAFTID = uint.MinValue;

        private class CraftMissionDictionary: Dictionary<uint, uint>
        {
            private Dictionary<uint, List<uint>> duplCraftMissionIDs = new Dictionary<uint, List<uint>>();

            public void AddCraftID(uint CraftID, uint MissionId)
            {
                if (ContainsKey(CraftID))
                {
                    this[CraftID] = DUPLICATE_CRAFTID;
                    if (!duplCraftMissionIDs.ContainsKey(CraftID))
                    {
                        duplCraftMissionIDs.Add(CraftID, new List<uint>());
                    }
                    duplCraftMissionIDs[CraftID].Add(MissionId);
                }
                else
                {
                    Add(CraftID, MissionId);
                }
            }

            public bool ExistsCraftMissionEntry(uint craftID, uint missionID)
            {
                if (ContainsKey(craftID))
                {
                    if (this[craftID] == missionID)
                    {
                        return true;
                    }

                    return duplCraftMissionIDs.ContainsKey(craftID) && duplCraftMissionIDs[craftID].Contains(missionID);
                }

                return false;
            }
        }

        public void Start()
        {
            PSystemManager.Instance.OnPSystemReady.Add(PSystemReady);
            GameEvents.OnFlightGlobalsReady.Add(FlightGlobalsReady);
            GameEvents.OnFlightCompoundPartLinked.Add(PartLinked);
        }

        private void PSystemReady()
        {
            ConfigNode[] cfgNodes = GameDatabase.Instance.GetConfigNodes("CMODULELINKEDMESHFIX");
            Log("Loading CModuleLinkedMeshFix configuration, cfgNodes: " + cfgNodes.Length, LogLevel.DEBUG);

            if (cfgNodes.Length>0)
            {
                try
                {
                    if (cfgNodes[0].HasValue("logLevel"))
                    {
                        logLevel = (LogLevel)Enum.Parse(typeof(LogLevel), cfgNodes[0].GetValue("logLevel"), true);
                        Log("Loaded CModuleLinkedMeshFix configuration", LogLevel.DEBUG);
                    } 
                } catch (Exception e)
                {
                    Log("Error loading CModuleLinkedMeshFix configuration: " + e.Message, LogLevel.WARNING);
                }
            }
        }

        private void FlightGlobalsReady(bool ready)
        {
            if (ready)
            {
                Log("FlightGlobals ready, vessels: " + FlightGlobals.Vessels.Count);
                foreach (Vessel v in FlightGlobals.Vessels)
                {
                    if (!v.loaded)
                    {
                        //fixing only unloaded vessels
                        List<ProtoPartSnapshot> partsToCheck = new List<ProtoPartSnapshot>();
                        CraftMissionDictionary craftMissionDict = new CraftMissionDictionary();

                        Log("Unloaded Vessel: " + v.GetDisplayName() + " part snapshots: " + v.protoVessel.protoPartSnapshots.Count);
                        foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
                        {
                            if (p.FindModule("CModuleLinkedMesh") != null)
                            {
                                Log("CModuleLinkedMesh: " + p.partName);
                                ConfigNode cn = p.partData;
                                Log(cn.ToString());
                                partsToCheck.Add(p);
                            }
                            else
                            {
                                craftMissionDict.AddCraftID(p.craftID, p.missionID);
                                Log("Another part: " + p.partName);
                            }
                        }

                        foreach (ProtoPartSnapshot p in partsToCheck)
                        {
                            if (p.partData != null)
                            {
                                uint tgt = 0;
                                if (p.partData.TryGetValue("tgt", ref tgt))
                                {
                                    Log("Checking part " + p.partName + " and target cid: " + tgt);
                                    if (tgt > 0 && !craftMissionDict.ExistsCraftMissionEntry(tgt, p.missionID))
                                    {
                                        //not found part with target CraftID and missionID of this part = wrong linkage in the savefile
                                        if (craftMissionDict.TryGetValue(tgt, out uint newMissionID))
                                        {
                                            if (newMissionID != DUPLICATE_CRAFTID)
                                            {
                                                p.missionID = newMissionID;
                                                Log("Fixed mission ID for vessel: " + v.GetDisplayName() + " and part: " + p.partName, LogLevel.INFO);
                                            } else
                                            {
                                                Log("Did not fixed mission ID due to ambiguous craftID. Vessel: " + v.GetDisplayName() + ", part: " + p.partName + ", craftID " + tgt, LogLevel.WARNING);
                                            }
                                        }
                                        else
                                        {
                                            Log("Target not found. Vessel: " + v.GetDisplayName() + ", part: " + p.partName + ", craftID " + tgt, LogLevel.DEBUG);
                                        }
                                    }
                                } else
                                {
                                    Log("Part data not found: " + p.partName);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void PartLinked(CompoundPart cpart)
        {
            if (cpart.target != null)
            {
                Log("Compound part target: " + cpart.target.name + ", mission ID: " + cpart.target.missionID.ToString() + ", part mid: " + cpart.missionID);
                if (cpart.target.missionID != cpart.missionID)
                {
                    cpart.missionID = cpart.target.missionID;
                    Log("Fixed mission ID for vessel: " + cpart.vessel.GetDisplayName() + " and part: " + cpart.name, LogLevel.INFO);
                }
            }
            else
            {
                Log("CompoundPart Linked: " + cpart.name + " - no target");
            }
        }

        private void Log(object msg, LogLevel level = LogLevel.DEBUG)
        {
            if (level <= logLevel)
            {
                const string LogPrefix = "[CModuleLinkedMeshFix] ";
                switch (level)
                {
                    case LogLevel.DEBUG:
                        Debug.Log(LogPrefix + msg);
                        break;
                    case LogLevel.INFO:
                        Debug.Log(LogPrefix + msg);
                        break;
                    case LogLevel.WARNING:
                        Debug.LogWarning(LogPrefix + msg);
                        break;
                }
            }
        }
    }

} // namespace CModuleLinkedMeshFix
