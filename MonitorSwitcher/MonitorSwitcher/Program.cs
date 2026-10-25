/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.IO;

namespace MonitorSwitcherGUI
{
    public class MonitorSwitcher
    {
        private static Boolean debug;
        private static Boolean noIDMatch;
        private static Boolean validateOnly;

        public static void DebugOutput(String text)
        {
            if (debug)
            {
                Console.WriteLine(text);
            }
        }

        public static Boolean LoadDisplaySettings(String fileName)
        {
            DebugOutput("Loading display settings from file: " + fileName);
            if (!File.Exists(fileName))
            {
                Console.WriteLine("Failed to load display settings because file does not exist: " + fileName);

                return false;
            }

            // Objects for DeSerialization of pathInfo and modeInfo classes
            DebugOutput("Initializing objects for Serialization");
            System.Xml.Serialization.XmlSerializer readerAdditionalInfo = new System.Xml.Serialization.XmlSerializer(typeof(CCDWrapper.MonitorAdditionalInfo));
            System.Xml.Serialization.XmlSerializer readerPath = new System.Xml.Serialization.XmlSerializer(typeof(CCDWrapper.DisplayConfigPathInfo));
            System.Xml.Serialization.XmlSerializer readerModeTarget = new System.Xml.Serialization.XmlSerializer(typeof(CCDWrapper.DisplayConfigTargetMode));
            System.Xml.Serialization.XmlSerializer readerModeSource = new System.Xml.Serialization.XmlSerializer(typeof(CCDWrapper.DisplayConfigSourceMode));
            System.Xml.Serialization.XmlSerializer readerModeInfoType = new System.Xml.Serialization.XmlSerializer(typeof(CCDWrapper.DisplayConfigModeInfoType));
            System.Xml.Serialization.XmlSerializer readerModeAdapterID = new System.Xml.Serialization.XmlSerializer(typeof(CCDWrapper.LUID));

            // Lists for storing the results
            List<CCDWrapper.DisplayConfigPathInfo> pathInfoList = new List<CCDWrapper.DisplayConfigPathInfo>();
            List<CCDWrapper.DisplayConfigModeInfo> modeInfoList = new List<CCDWrapper.DisplayConfigModeInfo>();
            List<CCDWrapper.MonitorAdditionalInfo> additionalInfoList = new List<CCDWrapper.MonitorAdditionalInfo>();

            // Loading the xml file
            DebugOutput("Parsing XML file");
            XmlReader xml = XmlReader.Create(fileName);
            xml.Read();
            while (true)
            {
                DebugOutput("\tXML Element: " + xml.Name);
                if ((xml.Name.CompareTo("DisplayConfigPathInfo") == 0) && (xml.IsStartElement()))
                {
                    CCDWrapper.DisplayConfigPathInfo pathInfo = (CCDWrapper.DisplayConfigPathInfo)readerPath.Deserialize(xml);
                    pathInfoList.Add(pathInfo);
                    continue;
                }
                else if ((xml.Name.CompareTo("modeInfo") == 0) && (xml.IsStartElement()))
                {
                    DebugOutput("\t\tReading modeInfo");
                    CCDWrapper.DisplayConfigModeInfo modeInfo = new CCDWrapper.DisplayConfigModeInfo();
                    xml.Read();
                    xml.Read();
                    modeInfo.id = Convert.ToUInt32(xml.Value);                    
                    xml.Read();
                    xml.Read();
                    modeInfo.adapterId = (CCDWrapper.LUID)readerModeAdapterID.Deserialize(xml);
                    modeInfo.infoType = (CCDWrapper.DisplayConfigModeInfoType)readerModeInfoType.Deserialize(xml);
                    if (modeInfo.infoType == CCDWrapper.DisplayConfigModeInfoType.Target)
                    {
                        modeInfo.targetMode = (CCDWrapper.DisplayConfigTargetMode)readerModeTarget.Deserialize(xml);
                    }
                    else
                    {
                        modeInfo.sourceMode = (CCDWrapper.DisplayConfigSourceMode)readerModeSource.Deserialize(xml);
                    }
                    DebugOutput("\t\t\tmodeInfo.id = " + modeInfo.id);
                    DebugOutput("\t\t\tmodeInfo.adapterId (High Part) = " + modeInfo.adapterId.HighPart);
                    DebugOutput("\t\t\tmodeInfo.adapterId (Low Part) = " + modeInfo.adapterId.LowPart);
                    DebugOutput("\t\t\tmodeInfo.infoType = " + modeInfo.infoType);

                    modeInfoList.Add(modeInfo);
                    continue;
                } else if ((xml.Name.CompareTo("MonitorAdditionalInfo") == 0) && (xml.IsStartElement()))
                {
                    DebugOutput("\t\tReading additional informations");
                    CCDWrapper.MonitorAdditionalInfo additionalInfo = (CCDWrapper.MonitorAdditionalInfo)readerAdditionalInfo.Deserialize(xml);
                    additionalInfoList.Add(additionalInfo);
                    continue;
                }

                    if (!xml.Read())
                {
                    break;
                }
            }
            xml.Close();
            DebugOutput("Parsing of XML file successful");

            // Convert C# lists to simply array
            DebugOutput("Converting to simple arrays for API compatibility");
            var pathInfoArray = new CCDWrapper.DisplayConfigPathInfo[pathInfoList.Count];
            for (int iPathInfo = 0; iPathInfo < pathInfoList.Count; iPathInfo++)
            {
                pathInfoArray[iPathInfo] = pathInfoList[iPathInfo];
            }

            var modeInfoArray = new CCDWrapper.DisplayConfigModeInfo[modeInfoList.Count];
            for (int iModeInfo = 0; iModeInfo < modeInfoList.Count; iModeInfo++)
            {
                modeInfoArray[iModeInfo] = modeInfoList[iModeInfo];
            }

            // Get current display settings
            DebugOutput("Getting current display settings");
            CCDWrapper.DisplayConfigPathInfo[] pathInfoArrayCurrent = new CCDWrapper.DisplayConfigPathInfo[0];
            CCDWrapper.DisplayConfigModeInfo[] modeInfoArrayCurrent = new CCDWrapper.DisplayConfigModeInfo[0];
            CCDWrapper.MonitorAdditionalInfo[] additionalInfoCurrent = new CCDWrapper.MonitorAdditionalInfo[0];

            Boolean statusCurrent = GetDisplaySettings(ref pathInfoArrayCurrent, ref modeInfoArrayCurrent, ref additionalInfoCurrent, false);
            if (statusCurrent)
            {
                if (!noIDMatch)
                {
                    MatchToCurrentDisplays(pathInfoArray, modeInfoArray, additionalInfoList, pathInfoArrayCurrent);
                }

                // Set loaded display settings
                DebugOutput("Setting up final display settings to load");
                if (debug)
                {
                    // debug output complete display settings
                    Console.WriteLine("\nDisplay settings to be loaded: ");
                    Console.WriteLine(PrintDisplaySettings(pathInfoArray, modeInfoArray));
                }
                uint numPathArrayElements = (uint)pathInfoArray.Length;
                uint numModeInfoArrayElements = (uint)modeInfoArray.Length;

                if (validateOnly)
                {
                    // Only ask Windows whether the matched profile could be applied, without changing anything
                    long validateStatus = CCDWrapper.SetDisplayConfig(numPathArrayElements, pathInfoArray, numModeInfoArrayElements, modeInfoArray,
                                                                      CCDWrapper.SdcFlags.Validate | CCDWrapper.SdcFlags.UseSuppliedDisplayConfig);
                    if (validateStatus != 0)
                    {
                        validateStatus = CCDWrapper.SetDisplayConfig(numPathArrayElements, pathInfoArray, numModeInfoArrayElements, modeInfoArray,
                                                                     CCDWrapper.SdcFlags.Validate | CCDWrapper.SdcFlags.UseSuppliedDisplayConfig | CCDWrapper.SdcFlags.AllowChanges);
                    }
                    Console.WriteLine(validateStatus == 0 ? "Profile is valid and can be applied: " + fileName
                                                          : "Profile can not be applied, ERROR: " + validateStatus.ToString() + ": " + fileName);
                    return validateStatus == 0;
                }

                // First let's try without SdcFlags.AllowChanges
                long status = CCDWrapper.SetDisplayConfig(numPathArrayElements, pathInfoArray, numModeInfoArrayElements, modeInfoArray,
                                                          CCDWrapper.SdcFlags.Apply | CCDWrapper.SdcFlags.UseSuppliedDisplayConfig | CCDWrapper.SdcFlags.SaveToDatabase | CCDWrapper.SdcFlags.NoOptimization);

                if (status != 0)
                {// try again with SdcFlags.AllowChanges
                    Console.WriteLine("Failed to set display settings without SdcFlags.AllowChanges, ERROR: " + status.ToString());
                    Console.WriteLine("Trying again with additional SdcFlags.AllowChanges flag");
                    status = CCDWrapper.SetDisplayConfig(numPathArrayElements, pathInfoArray, numModeInfoArrayElements, modeInfoArray,
                                                          CCDWrapper.SdcFlags.Apply | CCDWrapper.SdcFlags.UseSuppliedDisplayConfig | CCDWrapper.SdcFlags.SaveToDatabase | CCDWrapper.SdcFlags.NoOptimization | CCDWrapper.SdcFlags.AllowChanges);
                }

                if (status != 0)
                {
                    Console.WriteLine("Failed to set display settings using default method, ERROR: " + status.ToString());

                    if ((additionalInfoCurrent.Length > 0) && (additionalInfoList.Count > 0)) // only if present, e.g. new profile
                    {
                        Console.WriteLine("Trying alternative method");
                        // Restore original settings and adapter IDs
                        DebugOutput("Converting again to simple arrays for API compatibility");
                        for (int iPathInfo = 0; iPathInfo < pathInfoList.Count; iPathInfo++)
                        {
                            pathInfoArray[iPathInfo] = pathInfoList[iPathInfo];
                        }

                        for (int iModeInfo = 0; iModeInfo < modeInfoList.Count; iModeInfo++)
                        {
                            modeInfoArray[iModeInfo] = modeInfoList[iModeInfo];
                        }

                        DebugOutput("Alternative matching mode");
                        // For each modeInfo iterate over the current additional informations, i.e. monitor names and paths, and find the one matching in the current setup
                        for (int iModeInfo = 0; iModeInfo < modeInfoArray.Length; iModeInfo++)
                        {
                            for (int iAdditionalInfoCurrent = 0; iAdditionalInfoCurrent < additionalInfoCurrent.Length; iAdditionalInfoCurrent++)
                            {
                                if ((additionalInfoCurrent[iAdditionalInfoCurrent].monitorFriendlyDevice != null) && (additionalInfoList[iModeInfo].monitorFriendlyDevice != null))
                                {
                                    if (additionalInfoCurrent[iAdditionalInfoCurrent].monitorFriendlyDevice.Equals(additionalInfoList[iModeInfo].monitorFriendlyDevice))
                                    {
                                        CCDWrapper.LUID originalID = modeInfoArray[iModeInfo].adapterId;
                                        // now also find all other matching pathInfo modeInfos with that ID and change it
                                        for (int iPathInfo = 0; iPathInfo < pathInfoArray.Length; iPathInfo++)
                                        {
                                            if ((pathInfoArray[iPathInfo].targetInfo.adapterId.LowPart == originalID.LowPart) &&
                                               (pathInfoArray[iPathInfo].targetInfo.adapterId.HighPart == originalID.HighPart))
                                            {
                                                pathInfoArray[iPathInfo].targetInfo.adapterId = modeInfoArrayCurrent[iAdditionalInfoCurrent].adapterId;
                                                pathInfoArray[iPathInfo].sourceInfo.adapterId = modeInfoArrayCurrent[iAdditionalInfoCurrent].adapterId;
                                                pathInfoArray[iPathInfo].targetInfo.id = modeInfoArrayCurrent[iAdditionalInfoCurrent].id;
                                            }
                                        }
                                        for (int iModeInfoFix = 0; iModeInfoFix < modeInfoArray.Length; iModeInfoFix++)
                                        {
                                            if ((modeInfoArray[iModeInfoFix].adapterId.LowPart == originalID.LowPart) &&
                                                (modeInfoArray[iModeInfoFix].adapterId.HighPart == originalID.HighPart))
                                            {
                                                modeInfoArray[iModeInfoFix].adapterId = modeInfoArrayCurrent[iAdditionalInfoCurrent].adapterId;
                                            }
                                        }
                                        modeInfoArray[iModeInfo].adapterId = modeInfoArrayCurrent[iAdditionalInfoCurrent].adapterId;
                                        modeInfoArray[iModeInfo].id = modeInfoArrayCurrent[iAdditionalInfoCurrent].id;

                                        break;
                                    }
                                }
                            }
                        }

                        if (debug)
                        {
                            // debug output complete display settings
                            Console.WriteLine("\nDisplay settings to be loaded: ");
                            Console.WriteLine(PrintDisplaySettings(pathInfoArray, modeInfoArray));
                        }


                        // First let's try without SdcFlags.AllowChanges
                        status = CCDWrapper.SetDisplayConfig(numPathArrayElements, pathInfoArray, numModeInfoArrayElements, modeInfoArray,
                                                             CCDWrapper.SdcFlags.Apply | CCDWrapper.SdcFlags.UseSuppliedDisplayConfig | CCDWrapper.SdcFlags.NoOptimization | CCDWrapper.SdcFlags.SaveToDatabase);

                        if (status != 0)
                        {   // again with SdcFlags.AllowChanges
                            status = CCDWrapper.SetDisplayConfig(numPathArrayElements, pathInfoArray, numModeInfoArrayElements, modeInfoArray,
                                                                 CCDWrapper.SdcFlags.Apply | CCDWrapper.SdcFlags.UseSuppliedDisplayConfig | CCDWrapper.SdcFlags.NoOptimization | CCDWrapper.SdcFlags.SaveToDatabase | CCDWrapper.SdcFlags.AllowChanges);
                        }
                    }

                    if (status != 0)
                    {
                        Console.WriteLine("Failed to set display settings using alternative method, ERROR: " + status.ToString());


                        Console.WriteLine("\nTrying yet another method for arapter ID maching:");

                        // Restore original settings and adapter IDs
                        DebugOutput("Converting again to simple arrays for API compatibility");
                        for (int iPathInfo = 0; iPathInfo < pathInfoList.Count; iPathInfo++)
                        {
                            pathInfoArray[iPathInfo] = pathInfoList[iPathInfo];
                        }

                        for (int iModeInfo = 0; iModeInfo < modeInfoList.Count; iModeInfo++)
                        {
                            modeInfoArray[iModeInfo] = modeInfoList[iModeInfo];
                        }

                        // The next method is identical to the first one but uses a more radical adapter ID assignment
                        for (int iPathInfo = 0; iPathInfo < pathInfoArray.Length; iPathInfo++)
                        {
                            for (int iPathInfoCurrent = 0; iPathInfoCurrent < pathInfoArrayCurrent.Length; iPathInfoCurrent++)
                            {
                                if ((pathInfoArray[iPathInfo].sourceInfo.id == pathInfoArrayCurrent[iPathInfoCurrent].sourceInfo.id) &&
                                    (pathInfoArray[iPathInfo].targetInfo.id == pathInfoArrayCurrent[iPathInfoCurrent].targetInfo.id))
                                {
                                    DebugOutput("\t!!! Both IDs are a match, getting new Adapter ID and replacing all other IDs !!!");
                                    // Compare and replace the whole LUID, the high part can change as well
                                    ulong oldID = LuidKey(pathInfoArray[iPathInfo].sourceInfo.adapterId);
                                    CCDWrapper.LUID newID = pathInfoArrayCurrent[iPathInfoCurrent].sourceInfo.adapterId;
                                    for (int iPathInfoReplace = 0; iPathInfoReplace < pathInfoArray.Length; iPathInfoReplace++)
                                    {
                                        if (LuidKey(pathInfoArray[iPathInfoReplace].sourceInfo.adapterId) == oldID)
                                            pathInfoArray[iPathInfoReplace].sourceInfo.adapterId = newID;
                                        if (LuidKey(pathInfoArray[iPathInfoReplace].targetInfo.adapterId) == oldID)
                                            pathInfoArray[iPathInfoReplace].targetInfo.adapterId = newID;
                                    }

                                    for (int iModeInfoReplace = 0; iModeInfoReplace < modeInfoArray.Length; iModeInfoReplace++)
                                    {
                                        if (LuidKey(modeInfoArray[iModeInfoReplace].adapterId) == oldID)
                                        {
                                            modeInfoArray[iModeInfoReplace].adapterId = newID;
                                        }
                                    }
                                    break;
                                }
                                DebugOutput("\t---");
                            }
                        }

                        // Set loaded display settings
                        DebugOutput("Setting up final display settings to load");
                        if (debug)
                        {
                            // debug output complete display settings
                            Console.WriteLine("\nDisplay settings to be loaded: ");
                            Console.WriteLine(PrintDisplaySettings(pathInfoArray, modeInfoArray));
                        }

                        // First let's try without SdcFlags.AllowChanges
                        status = CCDWrapper.SetDisplayConfig(numPathArrayElements, pathInfoArray, numModeInfoArrayElements, modeInfoArray,
                                                                CCDWrapper.SdcFlags.Apply | CCDWrapper.SdcFlags.UseSuppliedDisplayConfig | CCDWrapper.SdcFlags.SaveToDatabase | CCDWrapper.SdcFlags.NoOptimization | CCDWrapper.SdcFlags.AllowChanges);

                        if (status != 0)
                        {   // again with SdcFlags.AllowChanges
                            status = CCDWrapper.SetDisplayConfig(numPathArrayElements, pathInfoArray, numModeInfoArrayElements, modeInfoArray,
                                                                                        CCDWrapper.SdcFlags.Apply | CCDWrapper.SdcFlags.UseSuppliedDisplayConfig | CCDWrapper.SdcFlags.SaveToDatabase | CCDWrapper.SdcFlags.NoOptimization | CCDWrapper.SdcFlags.AllowChanges);
                        }
                    }

                    if (status != 0)
                    {
                        Console.WriteLine("Failed to set display settings using the other alternative method, ERROR: " + status.ToString());
                        return false;
                    }
                }
                
                return true;
            }

            DebugOutput("Failed to get current display settings");
            return false;
        }

        private static ulong LuidKey(CCDWrapper.LUID luid)
        {
            return ((ulong)luid.HighPart << 32) | luid.LowPart;
        }

        private static String LuidText(CCDWrapper.LUID luid)
        {
            return luid.HighPart + ":" + luid.LowPart;
        }

        private static String TargetKey(CCDWrapper.LUID adapterId, uint targetId)
        {
            return LuidKey(adapterId) + "/" + targetId;
        }

        private struct CurrentTarget
        {
            public CCDWrapper.LUID adapterId;
            public uint id;
            public CCDWrapper.MonitorAdditionalInfo info;
        }

        /// <summary>
        /// The adapter ID (LUID) is reassigned whenever the graphics driver reloads (restart, driver update, driver reset)
        /// and the whole 64 bit value can change, not only the low part. Target IDs can also change when the driver
        /// re-enumerates its outputs. The monitor device path and EDID IDs stay the same, so we use them to find where
        /// each saved monitor is connected now, including monitors that are currently disabled, and rewrite the
        /// adapter and target IDs of the loaded profile accordingly.
        /// </summary>
        private static void MatchToCurrentDisplays(CCDWrapper.DisplayConfigPathInfo[] pathInfoArray, CCDWrapper.DisplayConfigModeInfo[] modeInfoArray,
                                                   List<CCDWrapper.MonitorAdditionalInfo> additionalInfoList, CCDWrapper.DisplayConfigPathInfo[] pathInfoArrayCurrent)
        {
            // Collect all currently connected targets. The current paths were queried with AllPaths, so this
            // includes targets which are connected but not active.
            DebugOutput("Collecting currently connected targets");
            List<CurrentTarget> currentTargets = new List<CurrentTarget>();
            HashSet<String> seenTargets = new HashSet<String>();
            foreach (CCDWrapper.DisplayConfigPathInfo pathInfo in pathInfoArrayCurrent)
            {
                if (!pathInfo.targetInfo.targetAvailable || !seenTargets.Add(TargetKey(pathInfo.targetInfo.adapterId, pathInfo.targetInfo.id)))
                {
                    continue;
                }

                CurrentTarget target = new CurrentTarget();
                target.adapterId = pathInfo.targetInfo.adapterId;
                target.id = pathInfo.targetInfo.id;
                try
                {
                    target.info = CCDWrapper.GetMonitorAdditionalInfo(target.adapterId, target.id);
                }
                catch (Exception)
                {
                    target.info.valid = false;
                }
                DebugOutput("\tadapter " + LuidText(target.adapterId) + " target " + target.id + " = " + target.info.monitorFriendlyDevice + " " + target.info.monitorDevicePath);
                currentTargets.Add(target);
            }

            // Find the current location of each monitor saved in the profile
            DebugOutput("Matching saved monitors to connected targets");
            Dictionary<String, CurrentTarget> targetMap = new Dictionary<String, CurrentTarget>();
            Dictionary<ulong, CCDWrapper.LUID> adapterMap = new Dictionary<ulong, CCDWrapper.LUID>();
            for (int iModeInfo = 0; iModeInfo < modeInfoArray.Length; iModeInfo++)
            {
                if ((modeInfoArray[iModeInfo].infoType != CCDWrapper.DisplayConfigModeInfoType.Target) ||
                    (iModeInfo >= additionalInfoList.Count) || !additionalInfoList[iModeInfo].valid)
                {
                    continue;
                }

                CCDWrapper.MonitorAdditionalInfo savedInfo = additionalInfoList[iModeInfo];
                int match = -1;

                // First by device path, it identifies the monitor and the connector it is plugged into
                if (!String.IsNullOrEmpty(savedInfo.monitorDevicePath))
                {
                    match = currentTargets.FindIndex(t => t.info.valid && String.Equals(t.info.monitorDevicePath, savedInfo.monitorDevicePath, StringComparison.OrdinalIgnoreCase));
                }

                // Otherwise by EDID manufacturer and product code, but only if exactly one connected monitor has them
                if ((match < 0) && (savedInfo.manufactureId != 0 || savedInfo.productCodeId != 0))
                {
                    List<int> candidates = new List<int>();
                    for (int iTarget = 0; iTarget < currentTargets.Count; iTarget++)
                    {
                        if (currentTargets[iTarget].info.valid &&
                            (currentTargets[iTarget].info.manufactureId == savedInfo.manufactureId) &&
                            (currentTargets[iTarget].info.productCodeId == savedInfo.productCodeId))
                        {
                            candidates.Add(iTarget);
                        }
                    }
                    if (candidates.Count == 1)
                    {
                        match = candidates[0];
                    }
                }

                if (match < 0)
                {
                    DebugOutput("\t" + savedInfo.monitorFriendlyDevice + " (target " + modeInfoArray[iModeInfo].id + ") not found");
                    continue;
                }

                CurrentTarget current = currentTargets[match];
                DebugOutput("\t" + savedInfo.monitorFriendlyDevice + ": adapter " + LuidText(modeInfoArray[iModeInfo].adapterId) + " target " + modeInfoArray[iModeInfo].id +
                            " -> adapter " + LuidText(current.adapterId) + " target " + current.id);
                targetMap[TargetKey(modeInfoArray[iModeInfo].adapterId, modeInfoArray[iModeInfo].id)] = current;
                if (!adapterMap.ContainsKey(LuidKey(modeInfoArray[iModeInfo].adapterId)))
                {
                    adapterMap[LuidKey(modeInfoArray[iModeInfo].adapterId)] = current.adapterId;
                }
            }

            // Profiles without monitor information: fall back to matching source and target IDs, as before
            foreach (CCDWrapper.DisplayConfigPathInfo pathInfo in pathInfoArray)
            {
                if (adapterMap.ContainsKey(LuidKey(pathInfo.targetInfo.adapterId)))
                {
                    continue;
                }
                foreach (CCDWrapper.DisplayConfigPathInfo pathInfoCurrent in pathInfoArrayCurrent)
                {
                    if ((pathInfo.sourceInfo.id == pathInfoCurrent.sourceInfo.id) && (pathInfo.targetInfo.id == pathInfoCurrent.targetInfo.id))
                    {
                        DebugOutput("\tadapter " + LuidText(pathInfo.targetInfo.adapterId) + " -> " + LuidText(pathInfoCurrent.targetInfo.adapterId) + " (by source and target ID)");
                        adapterMap[LuidKey(pathInfo.targetInfo.adapterId)] = pathInfoCurrent.targetInfo.adapterId;
                        break;
                    }
                }
            }

            // Rewrite paths and modes
            CurrentTarget mapped;
            CCDWrapper.LUID mappedAdapter;
            for (int iPathInfo = 0; iPathInfo < pathInfoArray.Length; iPathInfo++)
            {
                if (targetMap.TryGetValue(TargetKey(pathInfoArray[iPathInfo].targetInfo.adapterId, pathInfoArray[iPathInfo].targetInfo.id), out mapped))
                {
                    pathInfoArray[iPathInfo].targetInfo.adapterId = mapped.adapterId;
                    pathInfoArray[iPathInfo].targetInfo.id = mapped.id;
                }
                else if (adapterMap.TryGetValue(LuidKey(pathInfoArray[iPathInfo].targetInfo.adapterId), out mappedAdapter))
                {
                    pathInfoArray[iPathInfo].targetInfo.adapterId = mappedAdapter;
                }

                if (adapterMap.TryGetValue(LuidKey(pathInfoArray[iPathInfo].sourceInfo.adapterId), out mappedAdapter))
                {
                    pathInfoArray[iPathInfo].sourceInfo.adapterId = mappedAdapter;
                }
            }

            for (int iModeInfo = 0; iModeInfo < modeInfoArray.Length; iModeInfo++)
            {
                if ((modeInfoArray[iModeInfo].infoType == CCDWrapper.DisplayConfigModeInfoType.Target) &&
                    targetMap.TryGetValue(TargetKey(modeInfoArray[iModeInfo].adapterId, modeInfoArray[iModeInfo].id), out mapped))
                {
                    modeInfoArray[iModeInfo].adapterId = mapped.adapterId;
                    modeInfoArray[iModeInfo].id = mapped.id;
                }
                else if (adapterMap.TryGetValue(LuidKey(modeInfoArray[iModeInfo].adapterId), out mappedAdapter))
                {
                    modeInfoArray[iModeInfo].adapterId = mappedAdapter;
                }
            }
            DebugOutput("Done matching of adapter IDs");
        }

        public static Boolean GetDisplaySettings(ref CCDWrapper.DisplayConfigPathInfo[] pathInfoArray, ref CCDWrapper.DisplayConfigModeInfo[] modeInfoArray, ref CCDWrapper.MonitorAdditionalInfo[] additionalInfo, Boolean ActiveOnly)
        {
            uint numPathArrayElements;
            uint numModeInfoArrayElements;

            // query active paths from the current computer.
            DebugOutput("Getting display settings");
            CCDWrapper.QueryDisplayFlags queryFlags = CCDWrapper.QueryDisplayFlags.AllPaths;
            if (ActiveOnly)
            {
                queryFlags = CCDWrapper.QueryDisplayFlags.OnlyActivePaths;
            }

            DebugOutput("Getting buffer size");
            var status = CCDWrapper.GetDisplayConfigBufferSizes(queryFlags, out numPathArrayElements, out numModeInfoArrayElements);
            if (status == 0)
            {
                pathInfoArray = new CCDWrapper.DisplayConfigPathInfo[numPathArrayElements];
                modeInfoArray = new CCDWrapper.DisplayConfigModeInfo[numModeInfoArrayElements];
                additionalInfo = new CCDWrapper.MonitorAdditionalInfo[numModeInfoArrayElements];

                DebugOutput("Querying display config");
                status = CCDWrapper.QueryDisplayConfig(queryFlags,
                                                       ref numPathArrayElements, pathInfoArray, ref numModeInfoArrayElements,
                                                       modeInfoArray, IntPtr.Zero);                    

                if (status == 0)
                {
                    // cleanup of modeInfo bad elements 
                    int validCount = 0;
                    foreach (CCDWrapper.DisplayConfigModeInfo modeInfo in modeInfoArray)
                    {
                        if (modeInfo.infoType != CCDWrapper.DisplayConfigModeInfoType.Zero)
                        {   // count number of valid mode Infos
                            validCount++;
                        }
                    }
                    if (validCount > 0)
                    {   // only cleanup if there is at least one valid element found
                        CCDWrapper.DisplayConfigModeInfo[] tempInfoArray = new CCDWrapper.DisplayConfigModeInfo[modeInfoArray.Count()];
                        modeInfoArray.CopyTo(tempInfoArray, 0);
                        modeInfoArray = new CCDWrapper.DisplayConfigModeInfo[validCount];
                        int index = 0;
                        foreach (CCDWrapper.DisplayConfigModeInfo modeInfo in tempInfoArray)
                        {
                            if (modeInfo.infoType != CCDWrapper.DisplayConfigModeInfoType.Zero)
                            {
                                modeInfoArray[index] = modeInfo;
                                index++;
                            }
                        }
                    }

                    // cleanup of currently not available pathInfo elements
                    validCount = 0;
                    foreach (CCDWrapper.DisplayConfigPathInfo pathInfo in pathInfoArray)
                    {
                        if (pathInfo.targetInfo.targetAvailable)
                        {
                            validCount++;
                        }
                    }
                    if (validCount > 0)
                    {   // only cleanup if there is at least one valid element found
                        CCDWrapper.DisplayConfigPathInfo[] tempInfoArray = new CCDWrapper.DisplayConfigPathInfo[pathInfoArray.Count()];
                        pathInfoArray.CopyTo(tempInfoArray, 0);
                        pathInfoArray = new CCDWrapper.DisplayConfigPathInfo[validCount];
                        int index = 0;
                        foreach (CCDWrapper.DisplayConfigPathInfo pathInfo in tempInfoArray)
                        {
                            if (pathInfo.targetInfo.targetAvailable)
                            {
                                pathInfoArray[index] = pathInfo;
                                index++;
                            }
                        }
                    }

                    // get the display names for all modes
                    for (var iMode = 0; iMode < modeInfoArray.Count(); iMode++)
                    {
                        if (modeInfoArray[iMode].infoType == CCDWrapper.DisplayConfigModeInfoType.Target)
                        {
                            try
                            {
                                additionalInfo[iMode] = CCDWrapper.GetMonitorAdditionalInfo(modeInfoArray[iMode].adapterId, modeInfoArray[iMode].id);
                            }
                            catch (Exception e)
                            {
                                additionalInfo[iMode].valid = false;
                            }
                        }
                    }
                    return true;
                } else
                {
                    DebugOutput("Querying display config failed");
                }
            } else
            {
                DebugOutput("Getting Buffer Size Failed");
            }

            return false;
        }

        public static String PrintDisplaySettings(CCDWrapper.DisplayConfigPathInfo[] pathInfoArray, CCDWrapper.DisplayConfigModeInfo[] modeInfoArray)
        {
            // initialize result
            String output = "";

            // initialize text writer
            StringWriter textWriter = new StringWriter();

            
            // initialize xml serializer
            System.Xml.Serialization.XmlSerializer writerPath = new System.Xml.Serialization.XmlSerializer(typeof(CCDWrapper.DisplayConfigPathInfo));
            System.Xml.Serialization.XmlSerializer writerModeTarget = new System.Xml.Serialization.XmlSerializer(typeof(CCDWrapper.DisplayConfigTargetMode));
            System.Xml.Serialization.XmlSerializer writerModeSource = new System.Xml.Serialization.XmlSerializer(typeof(CCDWrapper.DisplayConfigSourceMode));
            System.Xml.Serialization.XmlSerializer writerModeInfoType = new System.Xml.Serialization.XmlSerializer(typeof(CCDWrapper.DisplayConfigModeInfoType));
            System.Xml.Serialization.XmlSerializer writerModeAdapterID = new System.Xml.Serialization.XmlSerializer(typeof(CCDWrapper.LUID));

            // write content to string
            textWriter.WriteLine("<displaySettings>");
            textWriter.WriteLine("<pathInfoArray>");
            foreach (CCDWrapper.DisplayConfigPathInfo pathInfo in pathInfoArray)
            {
                writerPath.Serialize(textWriter, pathInfo);
            }
            textWriter.WriteLine("</pathInfoArray>");

            textWriter.WriteLine("<modeInfoArray>");
            for (int iModeInfo = 0; iModeInfo < modeInfoArray.Length; iModeInfo++)
            {
                textWriter.WriteLine("<modeInfo>");
                CCDWrapper.DisplayConfigModeInfo modeInfo = modeInfoArray[iModeInfo];
                textWriter.WriteLine("<id>" + modeInfo.id.ToString() + "</id>");
                writerModeAdapterID.Serialize(textWriter, modeInfo.adapterId);
                writerModeInfoType.Serialize(textWriter, modeInfo.infoType);
                if (modeInfo.infoType == CCDWrapper.DisplayConfigModeInfoType.Target)
                {
                    writerModeTarget.Serialize(textWriter, modeInfo.targetMode);
                }
                else
                {
                    writerModeSource.Serialize(textWriter, modeInfo.sourceMode);
                }
                textWriter.WriteLine("</modeInfo>");
            }
            textWriter.WriteLine("</modeInfoArray>");

            output = textWriter.ToString();
            return output;
        }

        public static Boolean SaveDisplaySettings(String fileName)
        {
            CCDWrapper.DisplayConfigPathInfo[] pathInfoArray = new CCDWrapper.DisplayConfigPathInfo[0];
            CCDWrapper.DisplayConfigModeInfo[] modeInfoArray = new CCDWrapper.DisplayConfigModeInfo[0];
            CCDWrapper.MonitorAdditionalInfo[] additionalInfo = new CCDWrapper.MonitorAdditionalInfo[0];

            DebugOutput("Getting display config");
            Boolean status = GetDisplaySettings(ref pathInfoArray, ref modeInfoArray, ref additionalInfo, true);
            if (status) 
            {
                if (debug)
                {
                    // debug output complete display settings
                    DebugOutput("Display settings to write:");
                    Console.WriteLine(PrintDisplaySettings(pathInfoArray, modeInfoArray));
                }

                DebugOutput("Initializing objects for Serialization");
                System.Xml.Serialization.XmlSerializer writerAdditionalInfo = new System.Xml.Serialization.XmlSerializer(typeof(CCDWrapper.MonitorAdditionalInfo));
                System.Xml.Serialization.XmlSerializer writerPath = new System.Xml.Serialization.XmlSerializer(typeof(CCDWrapper.DisplayConfigPathInfo));
                System.Xml.Serialization.XmlSerializer writerModeTarget = new System.Xml.Serialization.XmlSerializer(typeof(CCDWrapper.DisplayConfigTargetMode));
                System.Xml.Serialization.XmlSerializer writerModeSource = new System.Xml.Serialization.XmlSerializer(typeof(CCDWrapper.DisplayConfigSourceMode));
                System.Xml.Serialization.XmlSerializer writerModeInfoType = new System.Xml.Serialization.XmlSerializer(typeof(CCDWrapper.DisplayConfigModeInfoType));
                System.Xml.Serialization.XmlSerializer writerModeAdapterID = new System.Xml.Serialization.XmlSerializer(typeof(CCDWrapper.LUID));
                XmlWriter xml = XmlWriter.Create(fileName);

                xml.WriteStartDocument();
                xml.WriteStartElement("displaySettings");
                xml.WriteStartElement("pathInfoArray");
                foreach (CCDWrapper.DisplayConfigPathInfo pathInfo in pathInfoArray)
                {
                    writerPath.Serialize(xml, pathInfo);
                }
                xml.WriteEndElement();

                xml.WriteStartElement("modeInfoArray");
                for (int iModeInfo = 0; iModeInfo < modeInfoArray.Length; iModeInfo++)
                {
                    xml.WriteStartElement("modeInfo");
                    CCDWrapper.DisplayConfigModeInfo modeInfo = modeInfoArray[iModeInfo];
                    xml.WriteElementString("id", modeInfo.id.ToString());
                    writerModeAdapterID.Serialize(xml, modeInfo.adapterId);
                    writerModeInfoType.Serialize(xml, modeInfo.infoType);
                    if (modeInfo.infoType == CCDWrapper.DisplayConfigModeInfoType.Target)
                    {
                        writerModeTarget.Serialize(xml, modeInfo.targetMode);
                    }
                    else
                    {
                        writerModeSource.Serialize(xml, modeInfo.sourceMode);
                    }
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();

                xml.WriteStartElement("additionalInfo");
                for (int iAdditionalInfo = 0; iAdditionalInfo < additionalInfo.Length; iAdditionalInfo++)
                {
                    writerAdditionalInfo.Serialize(xml, additionalInfo[iAdditionalInfo]);
                }
                xml.WriteEndElement();
                xml.WriteEndDocument();
                xml.Flush();
                xml.Close();

                return true;
            }
            else
            {                    
                Console.WriteLine("Failed to get display settings, ERROR: " + status.ToString());
            }                

            return false;
        }

        static void Main(string[] args)
        {
            debug = false;
            noIDMatch = false;

            Boolean validCommand = false;
            foreach (string iArg in args)
            {
                string[] argElements = iArg.Split(new char[] { ':' }, 2);

                switch (argElements[0].ToLower())
                {
                    case "-debug":
                        debug = true;
                        DebugOutput("\nDebug output enabled");
                        break;
                    case "-noidmatch":
                        noIDMatch = true;
                        DebugOutput("\nDisabled matching of adapter IDs");
                        break;
                    case "-save":
                        SaveDisplaySettings(argElements[1]);
                        validCommand = true;
                        break;
                    case "-load":
                        LoadDisplaySettings(argElements[1]);
                        validCommand = true;
                        break;
                    case "-validate":
                        validateOnly = true;
                        LoadDisplaySettings(argElements[1]);
                        validateOnly = false;
                        validCommand = true;
                        break;
                    case "-print":
                        CCDWrapper.DisplayConfigPathInfo[] pathInfoArray = new CCDWrapper.DisplayConfigPathInfo[0];
                        CCDWrapper.DisplayConfigModeInfo[] modeInfoArray = new CCDWrapper.DisplayConfigModeInfo[0];
                        CCDWrapper.MonitorAdditionalInfo[] additionalInfo = new CCDWrapper.MonitorAdditionalInfo[0];

                        Boolean status = GetDisplaySettings(ref pathInfoArray, ref modeInfoArray, ref additionalInfo, true);
                        if (status)
                        {
                            Console.WriteLine(PrintDisplaySettings(pathInfoArray, modeInfoArray));
                        }
                        else
                        {
                            Console.WriteLine("Failed to get display settings");
                        }
                        validCommand = true;
                        break;
                }
            }

            if (!validCommand)
            {
                Console.WriteLine("Monitor Profile Switcher command line utlility (version 0.8.1.0):\n");
                Console.WriteLine("Paremeters to MonitorSwitcher.exe:");
                Console.WriteLine("\t -save:{xmlfile} \t save the current monitor configuration to file (full path)");
                Console.WriteLine("\t -load:{xmlfile} \t load and apply monitor configuration from file (full path)");
                Console.WriteLine("\t -validate:{xmlfile} \t check if a monitor configuration file can be applied, without applying it");
                Console.WriteLine("\t -debug \t\t enable debug output (parameter must come before -load or -save)");
                Console.WriteLine("\t -noidmatch \t\t disable matching of adapter IDs");
                Console.WriteLine("\t -print \t\t print current monitor configuration to console");
                Console.WriteLine("");
                Console.WriteLine("Examples:");
                Console.WriteLine("\tMonitorSwitcher.exe -save:MyProfile.xml");
                Console.WriteLine("\tMonitorSwitcher.exe -load:MyProfile.xml");
                Console.WriteLine("\tMonitorSwitcher.exe -debug -load:MyProfile.xml");
                Console.ReadKey();
            }
        }
    }
}
