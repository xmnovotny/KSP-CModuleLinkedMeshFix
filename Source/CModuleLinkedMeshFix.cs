/*
 * Fix for struts and fuel lines connected in the EVA construction mode
 * https://github.com/xmnovotny/
 * License: GPL v2.
 * 
 */

using UnityEngine;
using System.Collections.Generic;

namespace CModuleLinkedMeshFix
{
	[KSPAddon(KSPAddon.Startup.PSystemSpawn, false)]
	public class CModuleLinkedMeshFix : MonoBehaviour
	{
        enum LogLevel {WARNING, INFO, DEBUG};
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
            //GameEvents.onGameStatePostLoad.Add(GamePostLoad);
            GameEvents.OnFlightGlobalsReady.Add(FlightGlobalsReady);
            GameEvents.OnFlightCompoundPartLinked.Add(PartLinked);
        }

//        private void GamePostLoad(ConfigNode node)
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
                                Log("Unloaded CModuleLinkedMesh: " + p.partName);
                                ConfigNode cn = p.partData;
                                Log(cn.ToString());
                                partsToCheck.Add(p);
                            }
                            else
                            {
                                craftMissionDict.AddCraftID(p.craftID, p.missionID);
                                Log("Unloaded another part: " + p.partName);
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
            const string LogPrefix = "[CModuleLinkedMeshFix] ";
            switch (level) {
                case LogLevel.DEBUG:
#if DEBUG
                    Debug.Log(LogPrefix + msg);
#endif
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

} // namespace CModuleLinkedMeshFix
