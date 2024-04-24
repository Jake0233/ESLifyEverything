﻿using ESLifyEverything.FormData;
using ESLifyEverything.PluginHandles;
using ESLifyEverything.XEdit;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Masters;
using System.Diagnostics;
using System.Text.Json;
using ESLifyEverythingGlobalDataLibrary;
using ESLifyEverythingGlobalDataLibrary.Properties.DataFileTypes;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace ESLifyEverything
{
    public static partial class ESLify
    {
        //Region for reading the xEdit log
        #region xEdit Log
        //Parses the xEdit log and readys it for output
        public static void XEditSession()
        {
            Directory.CreateDirectory(GF.CompactedFormsFolder);
            XEditLogReader.ReadLog(Path.Combine(GF.Settings.XEditFolderPath, GF.Settings.XEditLogFileName));
            int xEditSessionsCount = XEditLogReader.xEditLog.xEditSessions?.Length ?? 0;
            if (xEditSessionsCount <= 0)
            {
                GF.WriteLine(GF.stringLoggingData.NoxEditSessions);
            }
            else if (GF.Settings.AutoReadAllxEditSession)
            {
                string fileName = Path.Combine(GF.Settings.XEditFolderPath, GF.Settings.XEditLogFileName);
                FileInfo fi = new FileInfo(fileName);
                if (fi.Length > 10000000)//Path.Combine(GF.Settings.XEditFilePath, GF.Settings.XEditLogFileName)
                {
                    GF.WriteLine(GF.stringLoggingData.XEditLogFileSizeWarning);
                }
                XEditSessionAutoAll();
                if (GF.Settings.DeletexEditLogAfterRun_Requires_AutoReadAllxEditSession)
                {
                    File.Delete(Path.Combine(GF.Settings.XEditFolderPath, GF.Settings.XEditLogFileName));
                }
            }
            else if (GF.Settings.AutoReadNewestxEditSession)
            {
                XEditLogReader.xEditLog.xEditSessions![xEditSessionsCount - 1].GenerateCompactedModDatas();
            }
            else
            {
                XEditSessionMenu();
            }
        }

        //Menu to pick which sessions to output
        public static void XEditSessionMenu()
        {
            int xEditSessionsCount = XEditLogReader.xEditLog.xEditSessions?.Length ?? 0;
            Console.WriteLine("\n\n\n\n");
            GF.WriteLine(GF.stringLoggingData.SelectSession,true,false);
            for (int i = 0; i < xEditSessionsCount; i++)
            {
                GF.WriteLine($"{i}. " + XEditLogReader.xEditLog.xEditSessions![i].SessionTimeStamp);
            }
            GF.WriteLine(GF.stringLoggingData.InputSessionPromt, true, false);
            GF.WriteLine(GF.stringLoggingData.ExitCodeInput, true, false);
            int selectedMenuItem;
            while (GF.WhileMenuSelect(xEditSessionsCount - 1, out selectedMenuItem) == false);
            if (selectedMenuItem != -1) XEditLogReader.xEditLog.xEditSessions![selectedMenuItem].GenerateCompactedModDatas();
        }

        //Outputs all xEdit sessions to output to Compacted Mod Data
        public static void XEditSessionAutoAll()
        {
            foreach (XEditSession session in XEditLogReader.xEditLog.xEditSessions!)
            {
                session.GenerateCompactedModDatas();
            }
        }
        #endregion xEdit Log

        //Trys to locate and then build a zMerge cache
        public static void BuildMergedData()
        {
            IEnumerable<string> mergeFolders = Directory.EnumerateDirectories(
                GF.Settings.DataFolderPath,
                "merge - *",
                SearchOption.TopDirectoryOnly);
            foreach(string folder in mergeFolders)
            {
                string mergeJsonPath = Path.Combine(folder, "merge.json");
                if (File.Exists(mergeJsonPath))
                {
                    GF.WriteLine(GF.stringLoggingData.MergeFound + folder, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                    CompactedMergeData mergeData = new CompactedMergeData(mergeJsonPath, out bool success);
                    if (success)
                    {
                        string potentialMergeDataCachPath = Path.Combine(GF.CompactedFormsFolder, mergeData.MergeName + GF.MergeCacheExtension);

                        if (File.Exists(potentialMergeDataCachPath))
                        {
                            CompactedMergeData previouslyCachedMergeData = JsonSerializer.Deserialize<CompactedMergeData>(File.ReadAllText(potentialMergeDataCachPath))!;
                            if (previouslyCachedMergeData.AlreadyCached())
                            {
                                GF.WriteLine(mergeData.MergeName + GF.stringLoggingData.PluginCheckPrev, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                                GF.WriteLine(string.Format(GF.stringLoggingData.SkippingImport, mergeData.MergeName + GF.MergeCacheExtension), GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                                continue;
                            }
                        }

                        string mapPath = Path.Combine(folder, "map.json");
                        string fidCachePath = Path.Combine(folder, "fidCache.json");

                        if (File.Exists(mapPath) && File.Exists(fidCachePath))
                        {
                            if (mergeData.CompactedModDataD != null)
                            {
                                CompactedModData? outputtedCompactedModData = null;
                                string potentialCompactedModDataPath = Path.Combine(GF.CompactedFormsFolder, mergeData.MergeName + GF.CompactedFormExtension);
                                if (File.Exists(potentialCompactedModDataPath))
                                {
                                    GF.WriteLine(GF.stringLoggingData.ReadingCompDataLog + potentialCompactedModDataPath, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                                    outputtedCompactedModData = JsonSerializer.Deserialize<CompactedModData>(File.ReadAllText(potentialCompactedModDataPath))!;
                                }

                                IConfiguration fidCache = new ConfigurationBuilder()
                                    .AddJsonFile(fidCachePath)
                                    .AddEnvironmentVariables().Build();

                                IConfiguration mergeMap = new ConfigurationBuilder()
                                    .AddJsonFile(mapPath)
                                    .AddEnvironmentVariables().Build();
                                foreach (string key in mergeData.CompactedModDataD.Keys)
                                {
                                    if(mergeData.CompactedModDataD.TryGetValue(key, out CompactedModData? compactedModData))
                                    {
                                        try
                                        {
                                            foreach (KeyValuePair<string, string> mapping in mergeMap.GetRequiredSection(compactedModData.ModName).Get<Dictionary<string, string>>())
                                            {
                                                FormHandler form = new FormHandler(mergeData.MergeName, mapping.Key, mapping.Value);

                                                if (outputtedCompactedModData != null)
                                                {
                                                    foreach (FormHandler formHandler in outputtedCompactedModData.CompactedModFormList)
                                                    {
                                                        if (formHandler.OriginalFormID.Equals(mapping.Value))
                                                        {
                                                            form.ChangeCompactedID(formHandler.CompactedFormID);
                                                        }
                                                    }
                                                }

                                                compactedModData.CompactedModFormList.Add(form);
                                            }
                                        }
                                        catch(InvalidOperationException)
                                        {
                                            GF.WriteLine(String.Format(GF.stringLoggingData.NoChangedFormsFor, key, mergeData.MergeName), GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                                        }
                                        catch(Exception e)
                                        {
                                            GF.WriteLine(mapPath);
                                            GF.WriteLine(e.Message);
                                        }

                                        try
                                        {
                                            foreach (string fidCacheKP in fidCache.GetRequiredSection(compactedModData.ModName).Get<HashSet<string>>())
                                            {
                                                FormHandler form = new FormHandler(mergeData.MergeName, fidCacheKP, fidCacheKP);
                                                if (outputtedCompactedModData != null)
                                                {
                                                    foreach (FormHandler formHandler in outputtedCompactedModData.CompactedModFormList)
                                                    {
                                                        if (formHandler.OriginalFormID.Equals(fidCacheKP))
                                                        {
                                                            form.ChangeCompactedID(formHandler.CompactedFormID);
                                                        }
                                                    }
                                                }
                                                compactedModData.AddIfMissing(form);
                                                
                                            }
                                        }
                                        catch (InvalidOperationException)
                                        {
                                            GF.WriteLine(String.Format(GF.stringLoggingData.NoChangedFormsFor, key, mergeData.MergeName), GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                                        }

                                        mergeData.CompactedModDatas.Add(compactedModData);
                                    }
                                }

                                if (outputtedCompactedModData != null)
                                {
                                    GF.WriteLine(string.Format(GF.stringLoggingData.SetToIgnore, mergeData.MergeName + GF.CompactedFormExtension, mergeData.MergeName + GF.CompactedFormIgnoreExtension));
                                    File.Move(potentialCompactedModDataPath, Path.Combine(GF.CompactedFormsFolder, mergeData.MergeName + GF.CompactedFormIgnoreExtension), true);
                                }

                                if(CompactedMergeData.GetCompactedMergeDataFromMergeName(mergeData.MergeName, out var s))
                                {
                                    mergeData.Enabled = s!.Enabled;
                                }

                                mergeData.NewRecordCount = mergeData.CoundNewRecords();

                                mergeData.OutputModData(true);
                            }
                        }
                        else
                        {
                            GF.WriteLine(string.Format(GF.stringLoggingData.MapNotFound, mergeData.MergeName + GF.MergeCacheExtension), GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                            GF.WriteLine(string.Format(GF.stringLoggingData.SkippingImport, mergeData.MergeName + GF.MergeCacheExtension), GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                        }
                    }
                    else
                    {
                        GF.WriteLine(String.Format(GF.stringLoggingData.PluginNotFoundImport, mergeData.MergeName), GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                        GF.WriteLine(String.Format(GF.stringLoggingData.SkippingImport, mergeData.MergeName), GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                    }
                }
            }
        }

        //Region for importing Compacted Mod Data Files
        #region Import Mod Data
        //Imports all _CompactedModData.json files for ESLify Everything
        public static void ImportModData(string compactedFormsLocation)
        {
            if (!Directory.Exists(compactedFormsLocation))
            {
                GF.WriteLine(String.Format(GF.stringLoggingData.NoCMDinDataFolder, compactedFormsLocation));
                return;
            }

            IEnumerable<string> compactedFormsModFiles = Directory.EnumerateFiles(
                compactedFormsLocation,
                "*" + GF.CompactedFormExtension,
                SearchOption.AllDirectories);

            if (!compactedFormsModFiles.Any())
            {
                GF.WriteLine(String.Format(GF.stringLoggingData.NoCMDinDataFolder, compactedFormsLocation));
                return;
            }

            foreach (string compactedFormsModFile in compactedFormsModFiles)
            {
                GF.WriteLine(GF.stringLoggingData.ReadingCompDataLog + compactedFormsModFile);
                CompactedModData modData = JsonSerializer.Deserialize<CompactedModData>(File.ReadAllText(compactedFormsModFile))!;
                modData.Write();

                if (AlwaysIgnoreList.Contains(modData.ModName))
                {
                    GF.WriteLine(string.Format(GF.stringLoggingData.SkippingImport, modData.ModName + GF.CompactedFormExtension));
                    continue;
                }

                if (!File.Exists(Path.Combine(GF.Settings.DataFolderPath, modData.ModName)))
                {
                    GF.WriteLine(String.Format(GF.stringLoggingData.PluginNotFoundImport, compactedFormsModFile));
                    continue;
                }
                
                if (modData.Recheck == true)
                {
                    if (modData.PluginLastModifiedValidation is null)
                    {
                        modData = ValidateCompactedModDataJson(modData);
                    }
                    else
                    {
                        if (!modData.PluginLastModifiedValidation!.Value.Equals(File.GetLastWriteTime(Path.Combine(GF.Settings.DataFolderPath, modData.ModName))))
                        {
                            modData = ValidateCompactedModDataJson(modData);
                        }
                    }
                }

                if (modData.Enabled == true && !AlwaysIgnoreList.Contains(modData.ModName, StringComparer.OrdinalIgnoreCase))
                {
                    if (modData.PluginLastModifiedValidation is not null)
                    {
                        string splitModDataPath = Path.Combine(compactedFormsLocation, modData.ModName + GF.ModSplitDataExtension);
                        if (File.Exists(splitModDataPath))
                        {
                            CompactedModData splitModData = JsonSerializer.Deserialize<CompactedModData>(File.ReadAllText(splitModDataPath))!;
                            if (splitModData.PluginLastModifiedValidation.Equals(modData.PluginLastModifiedValidation))
                            {
                                foreach (FormHandler form in splitModData.CompactedModFormList)
                                {
                                    modData.CompactedModFormList.Add(form);
                                }
                            }
                        }

                        //if (!modData.PreviouslyESLified || GF.Settings.ImportAllCompactedModData || ImportModDataCheck(modData.ModName))
                        //{
                            GF.WriteLine(GF.stringLoggingData.ImportingCompDataLog + modData.ModName);
                            CompactedModDataD.TryAdd(modData.ModName, modData);
                        //}
                        //else
                        //{
                        //    GF.WriteLine(GF.stringLoggingData.ImportingCompDataLogOSP + modData.ModName);
                        //}
                        
                        //CompactedModDataDNoFaceVoice.TryAdd(modData.ModName, modData);

                    }
                }
                else
                {
                    GF.WriteLine(string.Format(GF.stringLoggingData.SkippingImport, modData.ModName + GF.CompactedFormExtension));
                }
                
            }

            
        }

        public static void ImportMergeData()
        {
            IEnumerable<string> compactedFormsModFiles = Directory.EnumerateFiles(
                GF.CompactedFormsFolder,
                "*" + GF.MergeCacheExtension,
                SearchOption.AllDirectories);

            if(compactedFormsModFiles.Any()) MergesFound = true;

            foreach(string file in compactedFormsModFiles)
            {
                CompactedMergeData mergeData = JsonSerializer.Deserialize<CompactedMergeData>(File.ReadAllText(file))!;

                if (!mergeData.Enabled)
                {
                    Console.WriteLine(mergeData.MergeName + " is disabled by variable. Skipping..");
                    continue;
                }

                if(mergeData.NewRecordCount != null)
                {
                    if (mergeData.NewRecordCount >= GF.LargeMergeCount)
                    {
                        if (!GF.Settings.EnableLargeMerges)
                        {
                            GF.WriteLine(GF.stringLoggingData.SkippingMergeCache + mergeData.MergeName);
                            continue;
                        }
                    }
                }
                else
                {
                    mergeData.NewRecordCount = mergeData.CoundNewRecords();
                    if(mergeData.NewRecordCount >= GF.LargeMergeCount)
                    {
                        if (!GF.Settings.EnableLargeMerges)
                        {
                            GF.WriteLine(GF.stringLoggingData.SkippingMergeCache + mergeData.MergeName);
                            continue;
                        }
                    }
                    mergeData.OutputModData(false);
                }

                string pluginPath = Path.Combine(GF.Settings.DataFolderPath, mergeData.MergeName);

                if (File.Exists(pluginPath) && ActiveLoadOrder.Contains(mergeData.MergeName))
                {
                    if (GF.Settings.AutoRunMergedPluginFixer)
                    {
                        mergeData.MergedPluginFixer();
                    }
                    else
                    {
                        GF.WriteLine("");
                        GF.WriteLine("");
                        GF.WriteLine(String.Format("Do you want to check over plugins added to the merge {0} for other CompactedModData?", mergeData.MergeName));
                        GF.WriteLine("Any input other then N will start the MergedPluginFixer.");
                        string? input = Console.ReadLine();
                        if(input != null)
                        {
                            GF.WriteLine("Input: " + input, false, true);
                            if (!input.Equals("N", StringComparison.OrdinalIgnoreCase))
                            {
                                mergeData.MergedPluginFixer();
                            }
                        }
                        else
                        {
                            GF.WriteLine("Input: " + "Empty.String", false, true);
                        }
                    }

                    if (mergeData.AlreadyCached() && !AlwaysIgnoreList.Contains(mergeData.MergeName, StringComparer.OrdinalIgnoreCase))
                    {
                        GF.WriteLine(GF.stringLoggingData.ImportingMergeCache + file);
                        foreach (CompactedModData compactedModData in mergeData.CompactedModDatas)
                        {
                            if (compactedModData.CompactedModFormList.Any())
                            {
                                compactedModData.FromMerge = true;
                                compactedModData.MergeName = mergeData.MergeName;
                                GF.IgnoredPlugins.Add(compactedModData.ModName);
                                GF.WriteLine(GF.stringLoggingData.ImportingMergeCompactedModData + compactedModData.ModName, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                                if(!CompactedModDataD.TryAdd(compactedModData.ModName, compactedModData))
                                {
                                    if(CompactedModDataD.ContainsKey(compactedModData.ModName))
                                    {
                                        CompactedModDataD[compactedModData.ModName] = compactedModData;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        GF.WriteLine(String.Format(GF.stringLoggingData.SkippingImport, mergeData.MergeName), GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                    }
                }
                else
                {
                    GF.WriteLine(String.Format(GF.stringLoggingData.PluginNotFoundImport, mergeData.MergeName), GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                    GF.WriteLine(String.Format(GF.stringLoggingData.SkippingImport, mergeData.MergeName), GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                }
            }
        }

        //Validates whether the CompactedModData is still valid compared to the Plugin
        public static CompactedModData ValidateCompactedModDataJson(CompactedModData modData)
        {
            if (modData.NotCompactedData.HasValue && modData.NotCompactedData.Value) 
            {
                modData.PluginLastModifiedValidation = File.GetLastWriteTime(Path.Combine(GF.Settings.DataFolderPath, modData.ModName));
                modData.Enabled = true;
                modData.Recheck = true;
            }
            else if (modData.IsCompacted(false))
            {
                modData.PluginLastModifiedValidation = File.GetLastWriteTime(Path.Combine(GF.Settings.DataFolderPath, modData.ModName));
                modData.Enabled = true;
                modData.Recheck = true;
            }
            else if (modData.IsCompacted(true))
            {
                modData.PluginLastModifiedValidation = File.GetLastWriteTime(Path.Combine(GF.Settings.DataFolderPath, modData.ModName));
                modData.Enabled = true;
                modData.Recheck = true;
            }
            else
            {
                modData.PreviouslyESLified = false;
                modData.PluginLastModifiedValidation = null;
                GF.WriteLine("");
                GF.WriteLine("", false, true);
                GF.WriteLine("", false, true);
                modData.Enabled = false;
                GF.WriteLine(String.Format(GF.stringLoggingData.OutOfDateCMData1, modData.ModName + GF.CompactedFormExtension));
                Console.WriteLine();

                bool notReCompactedFully = true;

                if (GF.Settings.RunSubPluginCompaction)
                {
                    GF.WriteLine(GF.stringLoggingData.RunPluginRecompactionMenu1);
                    GF.WriteLine(GF.stringLoggingData.RunPluginRecompactionMenu2);
                    GF.WriteLine(GF.stringLoggingData.RunPluginRecompactionMenu3);
                    GF.WriteLine(GF.stringLoggingData.RunPluginRecompactionEnterPrompt);

                    string input = Console.ReadLine()!;
                    GF.WriteLine("Input: " + input, false, true);

                    if (input != null)
                    {
                        if (input.Equals("Y", StringComparison.OrdinalIgnoreCase))
                        {
                            bool added = CompactedModDataD.TryAdd(modData.ModName, modData);
                            RunRecompact(modData.ModName);
                            if (added)
                            {
                                CompactedModDataD.Remove(modData.ModName);
                            }

                            if (modData.IsCompacted(true))
                            {
                                modData.PluginLastModifiedValidation = File.GetLastWriteTime(Path.Combine(GF.Settings.DataFolderPath, modData.ModName));
                                modData.Enabled = true;
                                modData.Recheck = true;
                                notReCompactedFully = false;
                            }
                        }
                    }
                }

                if(notReCompactedFully)
                {
                    GF.WriteLine(String.Format(GF.stringLoggingData.OutOfDateCMData2, modData.ModName));
                    GF.WriteLine(String.Format(GF.stringLoggingData.OutOfDateCMData3, modData.ModName));
                    GF.WriteLine(String.Format(GF.stringLoggingData.OutOfDateCMData4, modData.ModName));
                    GF.WriteLine(String.Format(GF.stringLoggingData.OutOfDateCMData5, modData.ModName));
                }

                GF.EnterToContinue();
                GF.WriteLine("", false, true);
                GF.WriteLine("", false, true);
                GF.WriteLine("");
            }
            modData.OutputModData(false, false);
            return modData;
        }

        //Recompacts the known Forms that relate to the CompactedModData
        //This does not change Forms that are not known inside the CompactedModData
        public static void RunRecompact(string pluginName)
        {
            Task<int>? handlePluginTask = null;
            try
            {
                handlePluginTask = HandleMod.HandleSkyrimMod(pluginName);
                handlePluginTask.Wait();
                switch (handlePluginTask.Result)
                {
                    case 0:
                        GF.WriteLine(pluginName + GF.stringLoggingData.PluginNotFound);
                        break;
                    case 1:
                        break;
                    case 2:
                        GF.WriteLine(pluginName + GF.stringLoggingData.PluginNotChanged);
                        break;
                    case 3:
                        GF.WriteLine(pluginName + GF.stringLoggingData.PluginMissingMasterFile);
                        break;
                    default:
                        GF.WriteLine(GF.stringLoggingData.PluginSwitchDefaultMessage);
                        break;
                }
                handlePluginTask.Dispose();
            }
            catch (Exception e)
            {
                if (handlePluginTask != null) handlePluginTask.Dispose();
                GF.WriteLine("Error reading " + pluginName);
                GF.WriteLine(e.Message);
                if (e.StackTrace != null) GF.WriteLine(e.StackTrace);

            }
        }
        #endregion Import Mod Data

        //Parses the BSA's in the Data folder
        public static async Task<int> LoadOrderBSAData()
        {
            string pluginsFilePath = Path.Combine(Environment.GetEnvironmentVariable("LocalAppData")!, "Skyrim Special Edition", "plugins.txt");
            if (!File.Exists(pluginsFilePath))
            {
                pluginsFilePath = "plugins.txt";
                if (File.Exists(pluginsFilePath))
                {
                    ActiveLoadOrder = GF.FilterForActiveLoadOrder(pluginsFilePath);
                }
                else
                {
                    GF.WriteLine(GF.stringLoggingData.LoadOrderNotDetectedError);
                    GF.WriteLine(GF.stringLoggingData.RunOrReport);
                    BSANotExtracted = true;
                    return await Task.FromResult(1);
                }
            }
            else
            {
                ActiveLoadOrder = GF.FilterForActiveLoadOrder(pluginsFilePath);
            }

            string loadOrderFilePath = Path.Combine(Environment.GetEnvironmentVariable("LocalAppData")!, "Skyrim Special Edition", "loadorder.txt");
            if (!File.Exists(pluginsFilePath))
            {
                pluginsFilePath = "loadorder.txt";
            }
            if (File.Exists(loadOrderFilePath))
            {
                string[] loadOrder = File.ReadAllLines(loadOrderFilePath);
                
                foreach (string plugin in loadOrder)
                {
                    string pluginNoExtension = Path.ChangeExtension(plugin, null);
                    if (File.Exists(Path.Combine(GF.Settings.DataFolderPath, pluginNoExtension + ".bsa")))
                    {
                        LoadOrderNoExtensions.Add(pluginNoExtension);
                    }
                }

                int loadorderCount = LoadOrderNoExtensions.Count;
                for (int i = 0; i < loadorderCount; i++)
                {
                    if (File.Exists(Path.Combine(GF.Settings.DataFolderPath, LoadOrderNoExtensions[i] + ".bsa")))
                    {
                        GF.WriteLine(String.Format(GF.stringLoggingData.BSACheckMod, LoadOrderNoExtensions[i]));
                        BSAData.AddNew(LoadOrderNoExtensions[i]);
                    }
                    Console.WriteLine();
                    GF.WriteLine(String.Format(GF.stringLoggingData.ProcessedBSAsLogCount, i + 1, loadorderCount));
                    Console.WriteLine();
                }
                BSAData.Output();
            }
            else
            {
                GF.WriteLine(GF.stringLoggingData.LoadOrderNotDetectedError);
                GF.WriteLine(GF.stringLoggingData.RunOrReport);
                BSANotExtracted = true;
                return await Task.FromResult(1);
            }
            return await Task.FromResult(0);
        }

        public static bool CheckModDataCheck(string modName)
        {
            if (CheckEverything)
            {
                return true;
            }
            return AlwaysCheckList.Contains(modName);
        }

        //Region for Voice Eslify
        #region Voice Eslify
        //Voice Eslify Main Menu
        public static void VoiceESlIfyMenu()
        {
            bool whileContinue = true;
            do
            {
                GF.WriteLine(GF.stringLoggingData.VoiceESLMenuHeader);
                GF.WriteLine($"1. {GF.stringLoggingData.ESLEveryMod}{GF.stringLoggingData.WithCompactedForms}", true, false);//with a _ESlEverything.json attached to it inside of the CompactedForms folders.
                GF.WriteLine($"2. {GF.stringLoggingData.SingleInputMod}{GF.stringLoggingData.WithCompactedForms}", true, false);
                GF.WriteLine(GF.stringLoggingData.CanLoop, true, false);
                GF.WriteLine(GF.stringLoggingData.ExitCodeInput, true, false);
                GF.WhileMenuSelect(2, out int selectedMenuItem, 1);
                switch (selectedMenuItem)
                {
                    case -1:
                        GF.WriteLine(GF.stringLoggingData.ExitCodeInputOutput);
                        whileContinue = false;
                        break;
                    case 1:
                        GF.WriteLine(GF.stringLoggingData.EslifingEverything);
                        whileContinue = VoiceESLifyEverything();
                        break;
                    case 2:
                        GF.WriteLine(GF.stringLoggingData.EslifingSingleMod);
                        VoiceESLifySingleMod();
                        break;
                }
            } while (whileContinue == true);
        }

        //Runs all Compacted Mod Data
        public static bool VoiceESLifyEverything()
        {
            foreach (CompactedModData modData in CompactedModDataD.Values)
            {
                if (!modData.PreviouslyESLified || GF.Settings.RunAllVoiceAndFaceGen || CheckModDataCheck(modData.ModName))
                {
                    VoiceESLifyMod(modData);
                }
            }
            return false;
        }

        //Voice Eslify menu to select which Compacted Mod Data to check
        public static void VoiceESLifySingleMod()
        {
            bool whileContinue = true;
            string input;
            do
            {
                GF.WriteLine($"{GF.stringLoggingData.SingleModInputHeader}{GF.stringLoggingData.ExamplePlugin}");
                GF.WriteLine(GF.stringLoggingData.ExitCodeInput, true, false);
                input = Console.ReadLine() ?? "";
                if (CompactedModDataD.TryGetValue(input, out CompactedModData? modData))
                {
                    modData.Write();
                    VoiceESLifyMod(modData);
                }
                else if (input.Equals("XXX", StringComparison.OrdinalIgnoreCase))
                {
                    whileContinue = false;
                }
            } while (whileContinue == true);
        }

        //Runs all needed methods to acuretly find Voice lines from the Compacted Mod Data
        public static void VoiceESLifyMod(CompactedModData modData)
        {
            Task v = ExtractBSAVoiceData(modData.ModName);
            v.Wait();
            v.Dispose();
            Task e = VoiceESLifyModData(modData, GF.ExtractedBSAModDataPath);
            e.Wait();
            e.Dispose();
            Task l = VoiceESLifyModData(modData, GF.Settings.DataFolderPath);
            l.Wait();
            l.Dispose();
        }

        //Extracts Voice Lines from BSA with Voice lines connected to the plugin
        public static async Task<int> ExtractBSAVoiceData(string pluginName)
        {
            foreach (string plugin in LoadOrderNoExtensions)
            {
                if (BSAData.BSAs.TryGetValue(plugin, out BSA? bsa))
                {
                    if (bsa.VoiceModConnections.Contains(pluginName, StringComparer.OrdinalIgnoreCase))
                    {
                        string line = "";
                        GF.WriteLine(String.Format(GF.stringLoggingData.BSAContainsData, bsa.BSAName_NoExtention, pluginName));
                        Process p = new Process();
                        p.StartInfo.FileName = ".\\BSABrowser\\bsab.exe";
                        p.StartInfo.Arguments = $"\"{Path.GetFullPath(Path.Combine(GF.Settings.DataFolderPath, bsa.BSAName_NoExtention + ".bsa"))}\" -f \"{pluginName}\"  -e -o \"{Path.GetFullPath(GF.ExtractedBSAModDataPath)}\"";
                        if (GF.DevSettings.DevLogging)
                        {
                            p.StartInfo.UseShellExecute = false;
                            p.StartInfo.RedirectStandardOutput = true;
                            p.StartInfo.RedirectStandardError = true;
                            p.StartInfo.CreateNoWindow = true;
                            p.Start();
                            while (!p.StandardOutput.EndOfStream)
                            {
                                string tempLine = p.StandardOutput.ReadLine()!;
                                if (tempLine != string.Empty)
                                {
                                    line = tempLine;
                                }
                            }
                        }
                        else
                        {
                            p.Start();
                        }
                        p.WaitForExit();
                        p.Dispose();
                        DevLog.Log(line);
                    }
                    
                }
            }
            return await Task.FromResult(0);
        }

        //Checks the given Compacted Mod Data for Voice lines and fixes them from targeted locations
        public static async Task<int> VoiceESLifyModData(CompactedModData modData, string dataStartPath)
        {
            DevLog.Log("Voice ESLify: " + modData.ModName);
            if (Directory.Exists(Path.Combine(dataStartPath, "sound\\voice", modData.ModName)))
            {
                DevLog.Log("Voice Lines Found: " + modData.ModName);
                foreach (FormHandler form in modData.CompactedModFormList)
                {
                    IEnumerable<string> voiceFilePaths = Directory.EnumerateFiles(
                        Path.Combine(dataStartPath, "sound\\voice", modData.ModName),
                        "*" + form.OriginalFormID + "*",
                        SearchOption.AllDirectories);
                    foreach (string voiceFilePath in voiceFilePaths)
                    {
                        GF.WriteLine(GF.stringLoggingData.OriganalPath + voiceFilePath, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                        string[] pathArr = voiceFilePath.Split('\\');

                        string newStartPath = Path.Combine(GF.Settings.OutputFolder, $"sound\\voice\\{form.ModName}\\{pathArr[pathArr.Length - 2]}");
                        Directory.CreateDirectory(newStartPath);
                        string newPath = Path.Combine(newStartPath, pathArr[pathArr.Length - 1].Replace(form.OriginalFormID, form.CompactedFormID, StringComparison.OrdinalIgnoreCase));

                        //File.Copy(voiceFilePath, newPath, true);
                        byte[] vfile = File.ReadAllBytes(voiceFilePath);
                        File.WriteAllBytes(newPath, vfile);


                        GF.WriteLine(GF.stringLoggingData.NewPath + newPath, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                    }
                }
            }
            return await Task.FromResult(1);
        }
        #endregion Voice Eslify

        //Region for FaceGen Eslify
        #region FaceGen Eslify
        //FaceGen Eslify Main Menu
        public static void FaceGenESlIfyMenu()
        {
            bool whileContinue = true;
            do
            {
                GF.WriteLine(GF.stringLoggingData.FaceGenESLMenuHeader);
                GF.WriteLine($"1. {GF.stringLoggingData.ESLEveryMod}{GF.stringLoggingData.WithCompactedForms}", true, false);//with a _ESlEverything.json attached to it inside of the CompactedForms folders.
                GF.WriteLine($"2. {GF.stringLoggingData.SingleInputMod}{GF.stringLoggingData.WithCompactedForms}", true, false);
                GF.WriteLine(GF.stringLoggingData.CanLoop, true, false);
                GF.WriteLine(GF.stringLoggingData.ExitCodeInput, true, false);
                GF.WhileMenuSelect(2, out int selectedMenuItem, 1);
                switch (selectedMenuItem)
                {
                    case -1:
                        GF.WriteLine(GF.stringLoggingData.ExitCodeInputOutput);
                        whileContinue = false;
                        break;
                    case 1:
                        GF.WriteLine(GF.stringLoggingData.EslifingEverything);
                        whileContinue = FaceGenESLifyEverything();
                        break;
                    case 2:
                        GF.WriteLine(GF.stringLoggingData.EslifingSingleMod);
                        FaceGenESLifySingleMod();
                        break;
                }
            } while (whileContinue == true);
        }

        //Runs all Compacted Mod Data
        public static bool FaceGenESLifyEverything()
        {
            foreach (CompactedModData modData in CompactedModDataD.Values)
            {
                if (!modData.PreviouslyESLified || GF.Settings.RunAllVoiceAndFaceGen || CheckModDataCheck(modData.ModName))
                {
                    FaceGenESLifyMod(modData);
                }
            }
            return false;
        }

        //FaceGen Eslify menu to select which Compacted Mod Data to check
        public static void FaceGenESLifySingleMod()
        {
            bool whileContinue = true;
            string input;
            CompactedModData modData;
            do
            {
                GF.WriteLine(GF.stringLoggingData.SingleModInputHeader + GF.stringLoggingData.ExamplePlugin, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                GF.WriteLine(GF.stringLoggingData.ExitCodeInput, true, false);
                input = Console.ReadLine() ?? "";
                if (CompactedModDataD.TryGetValue(input, out modData!))
                {
                    modData.Write();
                    FaceGenESLifyMod(modData);
                }
                else if (input.Equals("XXX", StringComparison.OrdinalIgnoreCase))
                {
                    whileContinue = false;
                }
            } while (whileContinue == true);
        }

        //Runs all needed methods to acuretly find FaceGen from the Compacted Mod Data
        public static void FaceGenESLifyMod(CompactedModData modData)
        {
            Task f = ExtractBSAFaceGenData(modData.ModName);
            f.Wait();
            f.Dispose();
            Task e = FaceGenEslifyModData(modData, GF.ExtractedBSAModDataPath);
            e.Wait();
            e.Dispose();
            Task l = FaceGenEslifyModData(modData, GF.Settings.DataFolderPath);
            l.Wait();
            l.Dispose();

        }

        //Extracts Voice Lines from BSA with FaceGen connected to the plugin
        public static async Task<int> ExtractBSAFaceGenData(string pluginName)
        {
            foreach (string plugin in LoadOrderNoExtensions)
            {
                if (BSAData.BSAs.TryGetValue(plugin, out BSA? bsa))
                {
                    if (bsa.FaceGenModConnections.Contains(pluginName, StringComparer.OrdinalIgnoreCase))
                    {
                        string line = "";
                        GF.WriteLine(String.Format(GF.stringLoggingData.BSAContainsData, bsa.BSAName_NoExtention, pluginName));
                        Process m = new Process();
                        m.StartInfo.FileName = ".\\BSABrowser\\bsab.exe";
                        m.StartInfo.Arguments = $"\"{Path.GetFullPath(Path.Combine(GF.Settings.DataFolderPath, bsa.BSAName_NoExtention + ".bsa"))}\" -f \"{pluginName}\"  -e -o \"{Path.GetFullPath(GF.ExtractedBSAModDataPath)}\"";
                        if (GF.DevSettings.DevLogging)
                        {
                            m.StartInfo.UseShellExecute = false;
                            m.StartInfo.RedirectStandardOutput = true;
                            m.StartInfo.RedirectStandardError = true;
                            m.StartInfo.CreateNoWindow = true;
                            m.Start();
                            while (!m.StandardOutput.EndOfStream)
                            {
                                string tempLine = m.StandardOutput.ReadLine()!;
                                if (tempLine != string.Empty)
                                {
                                    line = tempLine;
                                }
                            }
                        }
                        else
                        {
                            m.Start();
                        }
                        m.WaitForExit();
                        m.Dispose();
                        DevLog.Log(line);
                        if (bsa.HasTextureBSA)
                        {
                            line = "";
                            Process t = new Process();
                            t.StartInfo.FileName = ".\\BSABrowser\\bsab.exe";
                            t.StartInfo.Arguments = $"\"{Path.GetFullPath(Path.Combine(GF.Settings.DataFolderPath, bsa.BSAName_NoExtention + " - Textures.bsa"))}\" -f \"{pluginName}\"  -e -o \"{Path.GetFullPath(GF.ExtractedBSAModDataPath)}\"";
                            if (GF.DevSettings.DevLogging)
                            {
                                t.StartInfo.UseShellExecute = false;
                                t.StartInfo.RedirectStandardOutput = true;
                                t.StartInfo.RedirectStandardError = true;
                                t.StartInfo.CreateNoWindow = true;
                                t.Start();
                                while (!t.StandardOutput.EndOfStream)
                                {
                                    string tempLine = t.StandardOutput.ReadLine()!;
                                    if (tempLine != string.Empty)
                                    {
                                        line = tempLine;
                                    }
                                }
                            }
                            else
                            {
                                t.Start();
                            }
                            t.WaitForExit();
                            t.Dispose();
                            DevLog.Log(line);
                        }
                    }
                    
                }
            }
            return await Task.FromResult(0);
        }

        //Checks the given Compacted Mod Data for FaceGen and fixes them from targeted locations
        public static async Task<int> FaceGenEslifyModData(CompactedModData modData, string dataStartPath)
        {
            DevLog.Log("FaceGen ESLify: " + modData.ModName);
            if (Directory.Exists(Path.Combine(dataStartPath, "Meshes\\Actors\\Character\\FaceGenData\\FaceGeom\\", modData.ModName)))
            {
                DevLog.Log("FaceGen Lines Found: " + modData.ModName);
                foreach (FormHandler form in modData.CompactedModFormList)
                {
                    //IEnumerable<string> FaceGenTexFilePaths = Directory.EnumerateFiles(
                    //    Path.Combine(dataStartPath, "Textures\\Actors\\Character\\FaceGenData\\FaceTint\\", modData.ModName),
                    //    "*" + form.OriginalFormID + "*.dds",
                    //    SearchOption.AllDirectories);
                    string FaceTintFilePath = Path.Combine(dataStartPath, "Textures\\Actors\\Character\\FaceGenData\\FaceTint\\", modData.ModName + "\\00" + form.OriginalFormID + ".dds");
                    //foreach (string FaceTintFilePath in FaceGenTexFilePaths)
                    if(File.Exists(FaceTintFilePath))
                    {
                        GF.WriteLine(GF.stringLoggingData.OriganalPath + FaceTintFilePath, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);

                        string newStartPath = Path.Combine(GF.Settings.OutputFolder, "Textures\\Actors\\Character\\FaceGenData\\FaceTint\\" + form.ModName);
                        Directory.CreateDirectory(newStartPath);
                        string newPath = Path.Combine(newStartPath, "00" + form.CompactedFormID + ".dds");

                        File.Copy(FaceTintFilePath, newPath, true);
                        GF.WriteLine(GF.stringLoggingData.NewPath + newPath, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                    }

                    //IEnumerable<string> FaceGenFilePaths = Directory.EnumerateFiles(
                    //    Path.Combine(dataStartPath, "Meshes\\Actors\\Character\\FaceGenData\\FaceGeom\\", modData.ModName),
                    //    "*" + form.OriginalFormID + "*.nif",
                    //    SearchOption.AllDirectories);
                    string FaceGenFilePath = Path.Combine(dataStartPath, "Meshes\\Actors\\Character\\FaceGenData\\FaceGeom\\", modData.ModName + "\\00" + form.OriginalFormID + ".nif");
                    //foreach (string FaceGenFilePath in FaceGenFilePaths)
                    if(File.Exists(FaceGenFilePath))
                    {
                        GF.WriteLine(GF.stringLoggingData.OriganalPath + FaceGenFilePath, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);

                        NifFileWrapper OrigonalNifFile = new NifFileWrapper(FaceGenFilePath);
                        OrigonalNifFile = PatchNif(OrigonalNifFile, form.OriginalFormID, modData.ModName, form.CompactedFormID, form.ModName);

                        string newStartPath = Path.Combine(GF.Settings.OutputFolder, "Meshes\\Actors\\Character\\FaceGenData\\FaceGeom\\" + form.ModName);
                        Directory.CreateDirectory(newStartPath);
                        string newPath = Path.Combine(newStartPath, "00" + form.CompactedFormID + ".nif");

                        OrigonalNifFile.SaveAs(newPath, true);

                        GF.WriteLine(GF.stringLoggingData.NewPath + newPath, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);

                        //File.Copy(FaceGenFilePath, newPath, true);
                        //EditedFaceGen = true;
                        //using (StreamWriter stream = File.AppendText(GF.FaceGenFileFixPath))
                        //{
                        //    stream.WriteLine(Path.GetFullPath(newPath) + ";" + form.OriginalFormID + ";" + form.CompactedFormID);
                        //}
                    }

                }
            }
            return await Task.FromResult(1);
        }

        //Changes the OrigonalFormID to the CompactedFormID
        //mostly from https://github.com/Jampi0n/Skyrim-NifPatcher/blob/f71a5e5a532cf011790a978d20406b4a3208d856/NifPatcher/RuleParser.cs#L424
        public static NifFileWrapper PatchNif(NifFileWrapper nif, string OrgID, string OrgPluginName, string CompID, string CompPluginName)
        {
            for (var i = 0; i < nif.GetNumShapes(); ++i)
            {
                var shape = nif.GetShape(i);
                var subSurface = shape.SubsurfaceMap.ToLower();
                if (subSurface.Contains(OrgID, StringComparison.OrdinalIgnoreCase))
                {
                    subSurface = subSurface.Replace($"00{OrgID}.dds", $"00{CompID}.dds", StringComparison.OrdinalIgnoreCase);
                    subSurface = subSurface.Replace(OrgPluginName, CompPluginName);
                    shape.SubsurfaceMap = subSurface;
                }
            }
            return nif;
        }
        #endregion FaceGen Eslify

        //Region for Eslifying Data files
        #region ESLify Data Files
        //Data Files Eslify Main Menu
        public static void ESLifyDataFilesMainMenu()
        {
            GetESLifyModConfigurationFiles();

            Console.WriteLine(GF.stringLoggingData.InputDataFileExecutionPromt);
            Console.WriteLine($"1. {GF.stringLoggingData.ESLEveryModConfig}");
            Console.WriteLine($"2. {GF.stringLoggingData.SelectESLModConfig}");
            GF.WriteLine(GF.stringLoggingData.ExitCodeInput, true, false);
            int selectedMenuItem;
            while (GF.WhileMenuSelect(2, out selectedMenuItem, 1) == false) ;
            switch (selectedMenuItem)
            {
                case -1:
                    GF.WriteLine(GF.stringLoggingData.ExitCodeInputOutput);
                    break;
                case 1:
                    ESLifyAllDataFiles();
                    break;
                case 2:
                    ESLifySelectedDataFilesMenu();
                    break;
                default:
                    break;
            }
        }

        //Runs all Mod Configurations
        public static void ESLifyAllDataFiles()
        {
            foreach (var modConfiguration in BasicSingleModConfigurations)
            {
                HandleConfigurationType(modConfiguration);
            }
            foreach (var modConfiguration in BasicDirectFolderModConfigurations)
            {
                HandleConfigurationType(modConfiguration);
            }
            foreach (var modConfiguration in BasicDataSubfolderModConfigurations)
            {
                HandleConfigurationType(modConfiguration);
            }
            foreach (var modConfiguration in ComplexTOMLModConfigurations)
            {
                HandleConfigurationType(modConfiguration);
            }
            foreach (var modConfiguration in DelimitedFormKeysModConfigurations)
            {
                HandleConfigurationType(modConfiguration);
            }
            InternallyCodedDataFileConfigurations();
        }

        //Gets the Mod Configuration files for ESLifying Data Files
        public static void GetESLifyModConfigurationFiles()
        {
            //                 BasicSingleFile
            IEnumerable<string> basicSingleFilesModConfigurations = Directory.EnumerateFiles(
                    ".\\Properties\\DataFileTypes",
                    "*_BasicSingleFile.json",
                    SearchOption.TopDirectoryOnly);
            foreach (string file in basicSingleFilesModConfigurations)
            {
                try
                {
                    HashSet<BasicSingleFile> basicSingleFile = JsonSerializer.Deserialize<HashSet<BasicSingleFile>>(File.ReadAllText(file))!;
                    if (basicSingleFile != null)
                    {
                        foreach (BasicSingleFile modConfiguration in basicSingleFile)
                        {
                            if (modConfiguration.Enabled)
                            {
                                BasicSingleModConfigurations.Add(modConfiguration);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    GF.WriteLine(GF.stringLoggingData.ConfiguartionFileFailed + Path.GetFileName(file));
                }
            }
            //                 BasicDirectFolder
            IEnumerable<string> basicDirectFolderModConfigurations = Directory.EnumerateFiles(
                    ".\\Properties\\DataFileTypes",
                    "*_BasicDirectFolder.json",
                    SearchOption.TopDirectoryOnly);

            foreach (var file in basicDirectFolderModConfigurations)
            {
                try
                {
                    HashSet<BasicDirectFolder> basicDirectFolderFile = JsonSerializer.Deserialize<HashSet<BasicDirectFolder>>(File.ReadAllText(file))!;
                    if (basicDirectFolderFile != null)
                    {
                        foreach (BasicDirectFolder modConfiguration in basicDirectFolderFile)
                        {
                            if (modConfiguration.Enabled)
                            {
                                BasicDirectFolderModConfigurations.Add(modConfiguration);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    GF.WriteLine(GF.stringLoggingData.ConfiguartionFileFailed + Path.GetFileName(file));
                }
            }
            //                 BasicDataSubfolder
            IEnumerable<string> basicDataSubfolderModConfigurations = Directory.EnumerateFiles(
                    ".\\Properties\\DataFileTypes",
                    "*_BasicDataSubfolder.json",
                    SearchOption.TopDirectoryOnly);

            foreach (var file in basicDataSubfolderModConfigurations)
            {
                try
                {
                    HashSet<BasicDataSubfolder> basicDirectFolderFile = JsonSerializer.Deserialize<HashSet<BasicDataSubfolder>>(File.ReadAllText(file))!;
                    if (basicDirectFolderFile != null)
                    {
                        foreach (BasicDataSubfolder modConfiguration in basicDirectFolderFile)
                        {
                            if (modConfiguration.Enabled)
                            {
                                BasicDataSubfolderModConfigurations.Add(modConfiguration);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    GF.WriteLine(GF.stringLoggingData.ConfiguartionFileFailed + Path.GetFileName(file));
                }
            }
            //                 ComplexTOML
            IEnumerable<string> complexTOMLModConfigurations = Directory.EnumerateFiles(
                    ".\\Properties\\DataFileTypes",
                    "*_ComplexTOML.json",
                    SearchOption.TopDirectoryOnly);
            foreach (var file in complexTOMLModConfigurations)
            {
                try
                {
                    HashSet<ComplexTOML> basicDirectFolderFile = JsonSerializer.Deserialize<HashSet<ComplexTOML>>(File.ReadAllText(file))!;
                    if (basicDirectFolderFile != null)
                    {
                        foreach (ComplexTOML modConfiguration in basicDirectFolderFile)
                        {
                            if (modConfiguration.Enabled)
                            {
                                ComplexTOMLModConfigurations.Add(modConfiguration);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    GF.WriteLine(GF.stringLoggingData.ConfiguartionFileFailed + Path.GetFileName(file));
                }
            }
            //                 delimitedFormKeys
            IEnumerable<string> delimitedFormKeysModConfigurations = Directory.EnumerateFiles(
                    ".\\Properties\\DataFileTypes",
                    "*_DelimitedFormKeys.json",
                    SearchOption.TopDirectoryOnly);
            foreach (var file in delimitedFormKeysModConfigurations)
            {
                try
                {
                    HashSet<DelimitedFormKeys> basicDirectFolderFile = JsonSerializer.Deserialize<HashSet<DelimitedFormKeys>>(File.ReadAllText(file))!;
                    if (basicDirectFolderFile != null)
                    {
                        foreach (DelimitedFormKeys modConfiguration in basicDirectFolderFile)
                        {
                            if (modConfiguration.Enabled)
                            {
                                DelimitedFormKeysModConfigurations.Add(modConfiguration);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    GF.WriteLine(GF.stringLoggingData.ConfiguartionFileFailed + Path.GetFileName(file));
                }
            }
        }

        //Handles Logging for BasicSingleFile
        public static void HandleConfigurationType(BasicSingleFile ModConfiguration)
        {
            Console.WriteLine("\n\n\n\n");
            GF.WriteLine(ModConfiguration.StartingLogLine);
            SingleBasicFile(ModConfiguration);
        }

        //Handles Logging for BasicDirectFolder
        public static void HandleConfigurationType(BasicDirectFolder ModConfiguration)
        {
            Console.WriteLine("\n\n\n\n");
            GF.WriteLine(ModConfiguration.StartingLogLine);
            EnumDirectFolder(ModConfiguration);
        }

        //Handles Logging for BasicDataSubfolder
        public static void HandleConfigurationType(BasicDataSubfolder ModConfiguration)
        {
            Console.WriteLine("\n\n\n\n");
            GF.WriteLine(ModConfiguration.StartingLogLine);
            Task t = EnumDataSubfolder(ModConfiguration);
            t.Wait();
            t.Dispose();
        }

        //Handles Logging for ComplexTOML
        public static void HandleConfigurationType(ComplexTOML ModConfiguration)
        {
            Console.WriteLine("\n\n\n\n");
            GF.WriteLine(ModConfiguration.StartingLogLine);
            Task t = EnumToml(ModConfiguration);
            t.Wait();
            t.Dispose();
        }

        //Handles Logging for DelimitedFormKeys
        public static void HandleConfigurationType(DelimitedFormKeys ModConfiguration)
        {
            Console.WriteLine("\n\n\n\n");
            GF.WriteLine(ModConfiguration.StartingLogLine);
            EnumDelimitedFormKeys(ModConfiguration);
        }

        //Menu for selecting what Mod Configuration to check to run on Data Files
        public static void ESLifySelectedDataFilesMenu()
        {
            bool endMenu = false;
            string[] modConMenuList = GetModConList();
            string[] GetModConList()
            {
                List<string> modConMenuList = new List<string>();
                foreach (var modConfiguration in BasicSingleModConfigurations)
                {
                    modConMenuList.Add(modConfiguration.Name);
                }
                foreach (var modConfiguration in BasicDirectFolderModConfigurations)
                {
                    modConMenuList.Add(modConfiguration.Name);
                }
                foreach (var modConfiguration in BasicDataSubfolderModConfigurations)
                {
                    modConMenuList.Add(modConfiguration.Name);
                }
                foreach (var modConfiguration in ComplexTOMLModConfigurations)
                {
                    modConMenuList.Add(modConfiguration.Name);
                }
                foreach (var modConfiguration in DelimitedFormKeysModConfigurations)
                {
                    modConMenuList.Add(modConfiguration.Name);
                }
                modConMenuList.AddRange(InternallyCodedDataFileConfigurationsList);
                return modConMenuList.ToArray();
            }

            if (modConMenuList.Length <= 0)
            {
                GF.WriteLine(GF.stringLoggingData.NoModConfigurationFilesFound);
                return;
            }

            do
            {
                Console.WriteLine("\n\n");
                Console.WriteLine(GF.stringLoggingData.ModConfigInputPrompt);
                Console.WriteLine($"0. {GF.stringLoggingData.SwitchToEverythingMenuItem}");
                for (int i = 0; i < modConMenuList.Length; i++)
                {
                    Console.WriteLine($"{i + 1}. {modConMenuList[i]}");
                }
                GF.WriteLine(GF.stringLoggingData.ExitCodeInput, true, false);
                if (GF.WhileMenuSelect(modConMenuList.Length + 1, out int selectedMenuItem, 0))
                {
                    if (selectedMenuItem == 0)
                    {
                        ESLifyAllDataFiles();
                        endMenu = true;
                    }
                    else if (selectedMenuItem == -1)
                    {
                        GF.WriteLine(GF.stringLoggingData.ExitCodeInputOutput);
                        endMenu = true;
                    }
                    else
                    {
                        RunSelectedModConfig(modConMenuList[selectedMenuItem - 1]);
                    }
                }
            } while (endMenu == false);
        }

        //Runs the selected Mod Configuration over Data Files
        public static void RunSelectedModConfig(string selectedMenuItem)
        {
            foreach (var modConfiguration in BasicSingleModConfigurations)
            {
                if (modConfiguration.Name.Equals(selectedMenuItem)) HandleConfigurationType(modConfiguration);
            }
            foreach (var modConfiguration in BasicDirectFolderModConfigurations)
            {
                if (modConfiguration.Name.Equals(selectedMenuItem)) HandleConfigurationType(modConfiguration);
            }
            foreach (var modConfiguration in BasicDataSubfolderModConfigurations)
            {
                if (modConfiguration.Name.Equals(selectedMenuItem)) HandleConfigurationType(modConfiguration);
            }
            foreach (var modConfiguration in ComplexTOMLModConfigurations)
            {
                if (modConfiguration.Name.Equals(selectedMenuItem)) HandleConfigurationType(modConfiguration);
            }
            foreach (var modConfiguration in DelimitedFormKeysModConfigurations)
            {
                if (modConfiguration.Name.Equals(selectedMenuItem)) HandleConfigurationType(modConfiguration);
            }
            RunSelectedInternallyCodedDataFileConfigurations(selectedMenuItem);
        }
        #endregion ESLify Data Files

        //Region for Internally Codeded Data File Configurations
        #region Internally Coded Data File Configurations
        //Readonly property for the RaceMenu menu item
        public const string RaceMenuMenuItem = "RaceMenu";
        //Readonly property for the Custom Skills menu item
        public const string CustomSkillsMenuItem = "Custom Skills";
        //Readonly property for the Open Animation Replacer menu item
        public const string OpenAnimationReplacerMenuItem = "Open Animation Replacer";
        //Readonly property for the OBodyNG menu item
        public const string OBodyNG = "OBody";

        //lamda get for the list of internally coded ESLifies
        public readonly static string[] InternallyCodedDataFileConfigurationsList = new string[]
        {
            RaceMenuMenuItem,
            CustomSkillsMenuItem,
            OpenAnimationReplacerMenuItem,
            OBodyNG
        };

        public static void RunSelectedInternallyCodedDataFileConfigurations(string selectedMenuItem)
        {
            switch (selectedMenuItem)
            {
                case RaceMenuMenuItem://
                    RaceMenuESLify();
                    break;
                case CustomSkillsMenuItem://
                    Task CustomSkills = CustomSkillsFramework();
                    CustomSkills.Wait();
                    CustomSkills.Dispose();
                    break;
                case OpenAnimationReplacerMenuItem://
                    OARESLify();
                    break;
                case OBodyNG://
                    OBodyNGESLify(Path.Combine(GF.Settings.DataFolderPath, "SKSE\\Plugins\\OBody_presetDistributionConfig.json"));
                    break;
                default:
                    GF.WriteLine("How did you get here: RunSelectedInternallyCodedDataFileConfigurations Switch");
                    throw new Exception("How did you get here: RunSelectedInternallyCodedDataFileConfigurations Switch");
            }
            //if (RaceMenuMenuItem.Equals(selectedMenuItem))
            //{
            //    Console.WriteLine("\n\n\n\n");
            //    GF.WriteLine(GF.stringLoggingData.StartingRaceMenuESLify);
            //    RaceMenuESLify();
            //}
            //if (CustomSkillsMenuItem.Equals(selectedMenuItem))
            //{
            //    Console.WriteLine("\n\n\n\n");
            //    GF.WriteLine(GF.stringLoggingData.StartingCustomSkillsESLify);
            //    Task CustomSkills = CustomSkillsFramework();
            //    CustomSkills.Wait();
            //    CustomSkills.Dispose();
            //}
            //if (OpenAnimationReplacerMenuItem.Equals(selectedMenuItem))
            //{
            //    Console.WriteLine("\n\n\n\n");
            //    GF.WriteLine(GF.stringLoggingData.StartingOARESLify);
            //    OARESLify();
            //}
            //if (OBodyNG.Equals(selectedMenuItem))
            //{
            //    Console.WriteLine("\n\n\n\n");
            //    GF.WriteLine(GF.stringLoggingData.StartingOBodyNGESLify);
            //    OARESLify();
            //}
        }

        //Runs the Internally coded Mod Configurations
        public static void InternallyCodedDataFileConfigurations()
        {
            foreach(string dfc in InternallyCodedDataFileConfigurationsList)
            {
                RunSelectedInternallyCodedDataFileConfigurations(dfc);
            }
        }

        //method for RaceMenu Eslify
        public static void RaceMenuESLify()
        {
            Console.WriteLine("\n\n\n\n");
            GF.WriteLine(GF.stringLoggingData.StartingRaceMenuESLify);
            if (Directory.Exists(Path.Combine(GF.Settings.DataFolderPath, "SKSE\\Plugins\\CharGen\\Presets")))
            {
                string FixDecimalValue(string line, string CompactedFormID)
                {
                    string[] arr = line.Split(':');
                    string decStr = arr[1].Replace(",", "").Trim();
                    string inGameFormID = string.Format("{0:x}", long.Parse(decStr)).Substring(0, 2) + CompactedFormID;
                    string compDec = Convert.ToInt64(inGameFormID, 16).ToString();
                    return line.Replace(decStr, compDec);
                }

                IEnumerable<string> jslotFiles = Directory.EnumerateFiles(
                    Path.Combine(GF.Settings.DataFolderPath, "SKSE\\plugins\\CharGen\\Presets"),
                    "*.jslot",
                    SearchOption.AllDirectories);
                foreach (string jslotFile in jslotFiles)
                {
                    bool changed = false;
                    GF.WriteLine(GF.stringLoggingData.RaceMenuFileAt + jslotFile, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                    string[] jslotFileLines = File.ReadAllLines(jslotFile);
                    for (int i = 0; i < jslotFileLines.Length; i++)
                    {
                        if (jslotFileLines[i].Contains("\"formIdentifier\"", StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (string modName in CompactedModDataD.Keys)
                            {
                                if (jslotFileLines[i].Contains(modName, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (CompactedModDataD.TryGetValue(modName, out CompactedModData? mod))
                                    {
                                        foreach (FormHandler form in mod.CompactedModFormList)
                                        {
                                            if (jslotFileLines[i].Contains(form.GetOriginalFormID(), StringComparison.OrdinalIgnoreCase))
                                            {
                                                GF.WriteLine(GF.stringLoggingData.OldLine + jslotFileLines[i], true, GF.Settings.VerboseFileLoging);
                                                jslotFileLines[i] = jslotFileLines[i].Replace(form.GetOriginalFormID(), form.GetCompactedFormID());
                                                jslotFileLines[i] = jslotFileLines[i].Replace(modName, form.ModName);
                                                GF.WriteLine(GF.stringLoggingData.NewLine + jslotFileLines[i], true, GF.Settings.VerboseFileLoging);

                                                GF.WriteLine(GF.stringLoggingData.OldLine + jslotFileLines[i - 1], true, GF.Settings.VerboseFileLoging);
                                                jslotFileLines[i - 1] = FixDecimalValue(jslotFileLines[i - 1], form.CompactedFormID);
                                                GF.WriteLine(GF.stringLoggingData.NewLine + jslotFileLines[i - 1], true, GF.Settings.VerboseFileLoging);
                                                changed = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    OuputDataFileToOutputFolder(changed, jslotFile, jslotFileLines, GF.stringLoggingData.RaceMenuFileUnchanged);

                }
            }
            else
            {
                GF.WriteLine(GF.stringLoggingData.FolderNotFoundError + Path.Combine(GF.Settings.DataFolderPath, "SKSE\\plugins\\CharGen\\Presets"));
            }
        }

        //method for Custom Skills Framework Eslify
        public static async Task<int> CustomSkillsFramework()
        {
            Console.WriteLine("\n\n\n\n");
            GF.WriteLine(GF.stringLoggingData.StartingCustomSkillsESLify);
            string startSearchPath = Path.Combine(GF.Settings.DataFolderPath, "NetScriptFramework\\Plugins");
            if (Directory.Exists(startSearchPath))
            {
                IEnumerable<string> customSkillConfigs = Directory.EnumerateFiles(
                    startSearchPath,
                    "CustomSkill*config.txt",
                    SearchOption.TopDirectoryOnly);
                foreach (string customSkillConfig in customSkillConfigs)
                {
                    GF.WriteLine(GF.stringLoggingData.CustomSkillsFileAt + customSkillConfig, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);

                    #region OldCustSkillsFrameWork_NotWorkingWell_integration
                    //bool changed = false;
                    //string[] customSkillConfigFile = File.ReadAllLines(customSkillConfig);
                    //string[] newCustomSkillConfigFile = new string[customSkillConfigFile.Length];
                    //string currentModName = "";
                    //int currentModNameLine = -1;
                    //CompactedModData currentMod = new CompactedModData();

                    //for (int i = 0; i < customSkillConfigFile.Length; i++)
                    //{
                    //    string line = customSkillConfigFile[i];
                    //    foreach (string modName in CompactedModDataD.Keys)
                    //    {
                    //        if (customSkillConfigFile[i].Contains(modName, StringComparison.OrdinalIgnoreCase))
                    //        {
                    //            currentModName = modName;
                    //            currentModNameLine = i;
                    //            CompactedModDataD.TryGetValue(modName, out currentMod!);
                    //            GF.WriteLine("", GF.Settings.VerboseConsoleLoging, false);
                    //            GF.WriteLine(GF.stringLoggingData.ModLine + line, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                    //        }
                    //    }
                    //    if (!currentModName.Equals(""))
                    //    {
                    //        foreach (FormHandler form in currentMod.CompactedModFormList)
                    //        {
                    //            if (line.Contains(form.GetOriginalFormID()))
                    //            {
                    //                GF.WriteLine(GF.stringLoggingData.OldLine + line, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                    //                line = line.Replace(form.GetOriginalFormID(), form.GetCompactedFormID());
                    //                customSkillConfigFile[currentModNameLine] = customSkillConfigFile[currentModNameLine].Replace(currentModName, form.ModName);
                    //                GF.WriteLine(GF.stringLoggingData.NewLine + line, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                    //                currentModName = "";
                    //                changed = true;
                    //            }
                    //        }
                    //    }
                    //    newCustomSkillConfigFile[i] = line;
                    //}
                    #endregion OldCustSkillsFrameWork_NotWorkingWell_integration
                    CustomSkillsFramework customSkillsFramework = new CustomSkillsFramework(File.ReadAllLines(customSkillConfig));

                    customSkillsFramework.UpdateFileLines();

                    OuputDataFileToOutputFolder(customSkillsFramework.ChangedFile, customSkillConfig, customSkillsFramework.FileLines, GF.stringLoggingData.CustomSkillsFileUnchanged);
                }

            }
            else
            {
                GF.WriteLine(GF.stringLoggingData.FolderNotFoundError + startSearchPath);
            }
            return await Task.FromResult(0);
        }

        public static void OARESLify()
        {
            Console.WriteLine("\n\n\n\n");
            GF.WriteLine(GF.stringLoggingData.StartingOARESLify);
            string meshesPath = Path.Combine(GF.Settings.DataFolderPath, "Meshes");
            if (!Directory.Exists(meshesPath))
            {
                GF.WriteLine("Could not find Meshes folder in Data Folder...", true, true);
                return;
            }
            IEnumerable<string> openAnimationReplacerFolders = Directory.GetDirectories(meshesPath, 
                "OpenAnimationReplacer", 
                SearchOption.AllDirectories);
            foreach (string openAnimationReplacerFolder in openAnimationReplacerFolders)
            {
                IEnumerable<string> configFiles = Directory.GetDirectories(openAnimationReplacerFolder,
                    "config.json",
                    SearchOption.AllDirectories);
                foreach(string configFile in configFiles)
                {
                    GF.WriteLine(GF.stringLoggingData.OARFileFound + configFile);
                    string[] fileLines = File.ReadAllLines(configFile);
                    if(fileLines.Length == 1)
                    {
                        GF.WriteLine(GF.stringLoggingData.OARFileOneLineSkip);
                        continue;
                    }
                    bool changed = false;
                    for (int i = 0; i < fileLines.Length; i++)
                    {
                        string line = fileLines[i];
                        foreach (var modData in CompactedModDataD)
                        {
                            if (!line.Contains(modData.Key, StringComparison.OrdinalIgnoreCase)) continue;

                            string prevline = string.Empty;
                            string nextLine = string.Empty;
                            if (i != 0) prevline = fileLines[i - 1];
                            if(fileLines.Length-1 != i) nextLine = fileLines[i + 1];
                            bool foundThisLine = false;
                            foreach (var formHandler in modData.Value.CompactedModFormList)
                            {
                                Regex regex = new Regex($"\"formID\"[ 0]*:[ 0]*\"[0]*{formHandler.GetOriginalFormID()}\"");
                                if(!prevline.Equals(string.Empty))
                                {
                                    Match match = regex.Match(prevline);
                                    if (match.Success)
                                    {
                                        GF.WriteLine(GF.stringLoggingData.OldLine + fileLines[i - 1].Trim());
                                        fileLines[i - 1] = fileLines[i - 1].Replace(formHandler.GetOriginalFormID(), formHandler.GetCompactedFormID(true));
                                        GF.WriteLine(GF.stringLoggingData.NewLine + fileLines[i - 1].Trim());
                                        changed = true;
                                        foundThisLine = true;
                                    }
                                }

                                if (!line.Contains("\"formID\"", StringComparison.OrdinalIgnoreCase))
                                {
                                    Match match = regex.Match(line);
                                    if (match.Success)
                                    {
                                        GF.WriteLine(GF.stringLoggingData.OldLine + fileLines[i].Trim());
                                        fileLines[i] = fileLines[i].Replace(formHandler.GetOriginalFormID(), formHandler.GetCompactedFormID(true));
                                        GF.WriteLine(GF.stringLoggingData.NewLine + fileLines[i].Trim());
                                        changed = true;
                                        foundThisLine = true;
                                    }
                                }

                                if (!nextLine.Equals(string.Empty))
                                {
                                    Match match = regex.Match(nextLine);
                                    if (match.Success)
                                    {
                                        GF.WriteLine(GF.stringLoggingData.OldLine + fileLines[i + 1].Trim());
                                        fileLines[i + 1] = fileLines[i + 1].Replace(formHandler.GetOriginalFormID(), formHandler.GetCompactedFormID(true));
                                        GF.WriteLine(GF.stringLoggingData.NewLine + fileLines[i + 1].Trim());
                                        changed = true;
                                        foundThisLine = true;
                                    }
                                }
                            }

                            if (!foundThisLine)
                            {
                                GF.WriteLine(GF.stringLoggingData.OARErrorLine1 + modData.Key);
                                GF.WriteLine(GF.stringLoggingData.OARErrorLine2 + i);
                                GF.WriteLine(GF.stringLoggingData.OARErrorLine3);
                                GF.WriteLine(GF.stringLoggingData.OARErrorLine4);
                            }
                        }
                    }
                    OuputDataFileToOutputFolder(changed, configFile, fileLines, GF.stringLoggingData.OARFileUnchanged);




                }
            }
        }

        public static OBodyJson? OBodyNGESLify(string potentialPath, bool test = false)
        {
            Console.WriteLine("\n\n\n\n");
            GF.WriteLine(GF.stringLoggingData.StartingOBodyNGESLify);
            if (!File.Exists(potentialPath))
            {
                GF.WriteLine(GF.stringLoggingData.OBodyNGDoesNotExist, GF.Settings.VerboseConsoleLoging);
                return null;
            }
            OBodyJson? oBodyJson = OBodyJson.LoadOBodyJson(potentialPath);
            if(oBodyJson == null)
            {
                GF.WriteLine(GF.stringLoggingData.OBodyNGCantbeLoaded, GF.Settings.VerboseConsoleLoging);
                return null;
            }

            foreach (var npcFormID in oBodyJson.npcFormID.ToArray())//base plugin Nested List in Map in Map
            {
                foreach(var compactedModDataKeyPair in CompactedModDataD)//ESLify Everything base
                {
                    if (!compactedModDataKeyPair.Key.Equals(npcFormID.Key, StringComparison.OrdinalIgnoreCase)) continue;
                    foreach(var npcFormIDValueKeyPair in npcFormID.Value.ToArray())//FormIDs inside Json
                    {
                        foreach (var form in compactedModDataKeyPair.Value.CompactedModFormList)//ESLify Everything CompactedModData
                        {
                            if (!form.CompareOrgFormID(npcFormIDValueKeyPair.Key)) continue;
                            GF.WriteLine(String.Format(GF.stringLoggingData.OBodyNGChanging, npcFormIDValueKeyPair.Key, form.CompactedFormID));

                            oBodyJson.npcFormID[npcFormID.Key].Remove(npcFormIDValueKeyPair.Key);//Regardless remove the original key

                            if (npcFormID.Key.Equals(form.ModName, StringComparison.OrdinalIgnoreCase))//If the file was not merged or the form was not split out
                            {
                                oBodyJson.npcFormID[npcFormID.Key].Add(form.CompactedFormID, npcFormIDValueKeyPair.Value);//Add the new FormID
                            }//End if the file was merged or the form was split out
                            else
                            {
                                if (!oBodyJson.npcFormID.ContainsKey(form.ModName))//If the new Plugin is not in the json
                                {
                                    oBodyJson.npcFormID.Add(form.ModName, new Dictionary<string, List<string>>());//add the new Plugin
                                }//End if the new Plugin is not in the json

                                GF.WriteLine(String.Format(GF.stringLoggingData.OBodyNGMoving, form.CompactedFormID, form.ModName));
                                oBodyJson.npcFormID[form.ModName].Add(form.CompactedFormID, npcFormIDValueKeyPair.Value);//Add the new FormID
                            }
                            break;
                        }//End ESLify Everything CompactedModData
                    }//End FormIDs inside Json
                }//End ESLify Everything base
            }//End base plugin Nested List in Map in Map

            foreach(var blacklistedNpcsFormID in oBodyJson.blacklistedNpcsFormID.ToArray())//base plugin Nested List in Map
            {
                foreach (var compactedModDataKeyPair in CompactedModDataD)//ESLify Everything base
                {
                    if (!compactedModDataKeyPair.Key.Equals(blacklistedNpcsFormID.Key, StringComparison.OrdinalIgnoreCase)) continue;
                    
                    List<string> removeList = new List<string>();

                    for (int i = 0; i < blacklistedNpcsFormID.Value.Count; i++)//FormIDs inside Json
                    {
                        foreach (var form in compactedModDataKeyPair.Value.CompactedModFormList)//ESLify Everything CompactedModData
                        {
                            if (!form.CompareOrgFormID(blacklistedNpcsFormID.Value[i])) continue;
                            GF.WriteLine(String.Format(GF.stringLoggingData.OBodyNGChanging, blacklistedNpcsFormID.Value[i], form.CompactedFormID));

                            if (blacklistedNpcsFormID.Key.Equals(form.ModName, StringComparison.OrdinalIgnoreCase))//If the file was not merged or the form was not split out
                            {
                                blacklistedNpcsFormID.Value[i] = form.CompactedFormID;
                            }//End if the file was not merged or the form was not split out
                            else
                            {
                                removeList.Add(blacklistedNpcsFormID.Value[i]);
                                GF.WriteLine(String.Format(GF.stringLoggingData.OBodyNGMoving, form.CompactedFormID, form.ModName));
                                if (!oBodyJson.blacklistedNpcsFormID.ContainsKey(form.ModName))//If the new Plugin is not in the json
                                {
                                    oBodyJson.blacklistedNpcsFormID.Add(form.ModName, new List<string>());
                                }//End if the new Plugin is not in the json
                                oBodyJson.blacklistedNpcsFormID[form.ModName].Add(form.CompactedFormID);


                            }
                            break;
                        }//End ESLify Everything CompactedModData
                    }//End FormIDs inside Json

                    foreach(string list in removeList)
                    {
                        oBodyJson.blacklistedNpcsFormID[blacklistedNpcsFormID.Key].Remove(list);
                    }
                }//End ESLify Everything base
            }//End base plugin Nested List in Map

            foreach (var blacklistedOutfitsFromORefitFormID in oBodyJson.blacklistedOutfitsFromORefitFormID.ToArray())//base plugin Nested List in Map
            {
                foreach (var compactedModDataKeyPair in CompactedModDataD)//ESLify Everything base
                {
                    if (!compactedModDataKeyPair.Key.Equals(blacklistedOutfitsFromORefitFormID.Key, StringComparison.OrdinalIgnoreCase)) continue;

                    List<string> removeList = new List<string>();

                    for (int i = 0; i < blacklistedOutfitsFromORefitFormID.Value.Count; i++)//FormIDs inside Json
                    {
                        foreach (var form in compactedModDataKeyPair.Value.CompactedModFormList)//ESLify Everything CompactedModData
                        {
                            if (!form.CompareOrgFormID(blacklistedOutfitsFromORefitFormID.Value[i])) continue;
                            GF.WriteLine(String.Format(GF.stringLoggingData.OBodyNGChanging, blacklistedOutfitsFromORefitFormID.Value[i], form.CompactedFormID));

                            if (blacklistedOutfitsFromORefitFormID.Key.Equals(form.ModName, StringComparison.OrdinalIgnoreCase))//If the file was not merged or the form was not split out
                            {
                                blacklistedOutfitsFromORefitFormID.Value[i] = form.CompactedFormID;
                            }//End if the file was not merged or the form was not split out
                            else
                            {
                                removeList.Add(blacklistedOutfitsFromORefitFormID.Value[i]);
                                GF.WriteLine(String.Format(GF.stringLoggingData.OBodyNGMoving, form.CompactedFormID, form.ModName));
                                if (!oBodyJson.blacklistedOutfitsFromORefitFormID.ContainsKey(form.ModName))//If the new Plugin is not in the json
                                {
                                    oBodyJson.blacklistedOutfitsFromORefitFormID.Add(form.ModName, new List<string>());
                                }//End if the new Plugin is not in the json
                                oBodyJson.blacklistedOutfitsFromORefitFormID[form.ModName].Add(form.CompactedFormID);


                            }
                            break;
                        }//End ESLify Everything CompactedModData
                    }//End FormIDs inside Json

                    foreach (string list in removeList)
                    {
                        oBodyJson.blacklistedOutfitsFromORefitFormID[blacklistedOutfitsFromORefitFormID.Key].Remove(list);
                    }
                }//End ESLify Everything base
            }//End base plugin Nested List in Map

            foreach (var outfitsForceRefitFormID in oBodyJson.outfitsForceRefitFormID.ToArray())//base plugin Nested List in Map
            {
                foreach (var compactedModDataKeyPair in CompactedModDataD)//ESLify Everything base
                {
                    if (!compactedModDataKeyPair.Key.Equals(outfitsForceRefitFormID.Key, StringComparison.OrdinalIgnoreCase)) continue;

                    List<string> removeList = new List<string>();

                    for (int i = 0; i < outfitsForceRefitFormID.Value.Count; i++)//FormIDs inside Json
                    {
                        foreach (var form in compactedModDataKeyPair.Value.CompactedModFormList)//ESLify Everything CompactedModData
                        {
                            if (!form.CompareOrgFormID(outfitsForceRefitFormID.Value[i])) continue;
                            GF.WriteLine(String.Format(GF.stringLoggingData.OBodyNGChanging, outfitsForceRefitFormID.Value[i], form.CompactedFormID));

                            if (outfitsForceRefitFormID.Key.Equals(form.ModName, StringComparison.OrdinalIgnoreCase))//If the file was not merged or the form was not split out
                            {
                                outfitsForceRefitFormID.Value[i] = form.CompactedFormID;
                            }//End if the file was not merged or the form was not split out
                            else
                            {
                                removeList.Add(outfitsForceRefitFormID.Value[i]);
                                GF.WriteLine(String.Format(GF.stringLoggingData.OBodyNGMoving, form.CompactedFormID, form.ModName));
                                if (!oBodyJson.outfitsForceRefitFormID.ContainsKey(form.ModName))//If the new Plugin is not in the json
                                {
                                    oBodyJson.outfitsForceRefitFormID.Add(form.ModName, new List<string>());
                                }//End if the new Plugin is not in the json
                                oBodyJson.outfitsForceRefitFormID[form.ModName].Add(form.CompactedFormID);


                            }
                            break;
                        }//End ESLify Everything CompactedModData
                    }//End FormIDs inside Json

                    foreach (string list in removeList)
                    {
                        oBodyJson.outfitsForceRefitFormID[outfitsForceRefitFormID.Key].Remove(list);
                    }
                }//End ESLify Everything base
            }//End base plugin Nested List in Map

            if (test) return oBodyJson;

            File.WriteAllText(Path.Combine(GF.Settings.OutputFolder, "SKSE\\Plugins\\OBody_presetDistributionConfig.json"), 
                JsonSerializer.Serialize(oBodyJson, GF.JsonSerializerOptions));

            return null;
        }

        #endregion Internally Coded Data File Configurations

        //Region for fixing records and references inside of plugins
        #region Plugins
        //Starts the chack for Master file and runs what was selected in SelectCompactedModsMenu()
        public static void ReadLoadOrder()
        {
            HashSet<string> checkPlugins = SelectCompactedModsMenu();

            HashSet<string> runPlugins = new HashSet<string>();
            for (int i = 1; i < ActiveLoadOrder.Length; i++)
            {
                if (File.Exists(Path.Combine(GF.Settings.DataFolderPath, ActiveLoadOrder[i])))
                {
                    if (!GF.IgnoredPlugins.Contains(ActiveLoadOrder[i], StringComparer.OrdinalIgnoreCase))
                    {
                        GF.WriteLine(String.Format(GF.stringLoggingData.PluginCheckMod, ActiveLoadOrder[i]), GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                        if (File.Exists(Path.Combine(GF.Settings.DataFolderPath, ActiveLoadOrder[i])))
                        {
                            MasterReferenceCollection? masterCollection = MasterReferenceCollection.FromPath(Path.Combine(GF.Settings.DataFolderPath, ActiveLoadOrder[i]), GameRelease.SkyrimSE);
                            foreach (var master in masterCollection.Masters.ToHashSet())
                            {
                                if (checkPlugins.Contains(master.Master.FileName))
                                {
                                    GF.WriteLine(String.Format(GF.stringLoggingData.PluginAttemptFix, ActiveLoadOrder[i]));
                                    runPlugins.Add(ActiveLoadOrder[i]);
                                    break;
                                }
                            }
                        }
                    }
                }
                GF.WriteLine(String.Format(GF.stringLoggingData.ProcessedPluginsLogCount, i, ActiveLoadOrder.Length, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging));
            }

            //Fix and output plugins that still use uncompacted data
            foreach (string pluginToRun in runPlugins)
            {
                Task<int>? handlePluginTask = null;
                try
                {
                    handlePluginTask = HandleMod.HandleSkyrimMod(pluginToRun);
                    handlePluginTask.Wait();
                    switch (handlePluginTask.Result)
                    {
                        case 0:
                            GF.WriteLine(pluginToRun + GF.stringLoggingData.PluginNotFound, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                            break;
                        case 1:
                            GF.WriteLine(String.Format(GF.stringLoggingData.PluginFixed, pluginToRun));
                            break;
                        case 2:
                            GF.WriteLine(pluginToRun + GF.stringLoggingData.PluginNotChanged, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                            break;
                        case 3:
                            GF.WriteLine(pluginToRun + GF.stringLoggingData.PluginMissingMasterFile);
                            break;
                        default:
                            GF.WriteLine(GF.stringLoggingData.PluginSwitchDefaultMessage);
                            break;
                    }
                    handlePluginTask.Dispose();
                }
                catch(Exception e)
                {
                    if(handlePluginTask != null) handlePluginTask.Dispose();
                    GF.WriteLine("Error reading " + pluginToRun);
                    GF.WriteLine(e.Message);
                    if(e.StackTrace != null) GF.WriteLine(e.StackTrace);

                }
                
            }

        }

        //Menu to select Compacted Mod Data to check over load order
        public static HashSet<string> SelectCompactedModsMenu()
        {
            HashSet<string> slectedCompactedMods = new HashSet<string>();
            List<string> menuList = new List<string>();
            menuList.AddRange(CompactedModDataD.Keys);

            foreach(CompactedModData compactedModData in CompactedModDataD.Values)
            {
                if (!compactedModData.PreviouslyESLified)
                {
                    menuList.Remove(compactedModData.ModName);
                    slectedCompactedMods.Add(compactedModData.ModName);
                }
            }

            bool exit = false;
            int menuModifier = 3;//1 is for offsetting the 0. in the menu add one for each extra menu item.
            do
            {
                Console.WriteLine("\n\n");
                GF.WriteLine(GF.stringLoggingData.SelectCompactedModsMenuHeader);
                Console.WriteLine(GF.stringLoggingData.ExitCodeInput);
                Console.WriteLine("1. " + GF.stringLoggingData.RunAllPluginChecks);//menuModifier = 2
                Console.WriteLine("2. " + "Check the selected plugins");//menuModifier = 3
                for (int i = 0; i < menuList.Count; i++)
                {
                    Console.WriteLine($"{i + menuModifier}. {menuList.ElementAt(i)}");
                }
                Console.WriteLine();
                Console.WriteLine("1. " + GF.stringLoggingData.RunAllPluginChecks);
                Console.WriteLine("2. " + "Check the selected plugins");
                if (GF.WhileMenuSelect(menuList.Count + menuModifier, out int selectedMenuItem, 1))
                {
                    if (selectedMenuItem == -1)
                    {
                        GF.WriteLine(GF.stringLoggingData.ExitCodeInputOutput);
                        exit = true;
                    }
                    else if (selectedMenuItem == 1)
                    {
                        GF.WriteLine(GF.stringLoggingData.RunAllPluginChecks + GF.stringLoggingData.SingleWordSelected);
                        return CompactedModDataD.Keys.ToHashSet();
                    }
                    else if (selectedMenuItem == 2)
                    {
                        exit = true;
                    }
                    else
                    {
                        GF.WriteLine(menuList.ElementAt(selectedMenuItem - menuModifier) + GF.stringLoggingData.SingleWordSelected);
                        slectedCompactedMods.Add(menuList.ElementAt(selectedMenuItem - menuModifier));
                        menuList.RemoveAt(selectedMenuItem - menuModifier);
                    }
                    Console.WriteLine();
                }
            } while (exit == false);

            return slectedCompactedMods;
        }
        #endregion Plugins

        public static void MergifyBashTagsMenu()
        {
            GF.WriteLine(GF.stringLoggingData.AskToStartMergifyBashTags);
            GF.WriteLine(GF.stringLoggingData.PressYToStartMergifyBashTags);
            string input = Console.ReadLine() ?? "";
            GF.WriteLine(input, consoleLog: false);
            if (input.Equals("Y", StringComparison.OrdinalIgnoreCase)) RunMergifyBashTags();
        }

        public static void RunMergifyBashTags()
        {
            GF.WriteLine(GF.stringLoggingData.StartingMBT);
            Process p = new Process();
            p.StartInfo.FileName = "MergifyBashTags.exe";
            p.StartInfo.Arguments = $"\"{GF.Settings.DataFolderPath}\" \"{GF.Settings.LootAppDataFolder}\" \"{GF.Settings.OutputFolder}\" -np";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            using (StreamWriter stream = File.AppendText(GF.logName))
            {
                while (!p.StandardOutput.EndOfStream)
                {
                    string line = p.StandardOutput.ReadLine()!;
                    Console.WriteLine(line);
                    if (!line.Equals(string.Empty))
                    {
                        stream.WriteLine(line);
                    }
                }

                while (!p.StandardError.EndOfStream)
                {
                    string line = p.StandardError.ReadLine()!;
                    Console.WriteLine(line);
                    if (!line.Equals(string.Empty))
                    {
                        stream.WriteLine(line);
                    }
                }
            }
            p.WaitForExit();
            p.Dispose();

        }

        public static void FinalizeData()
        {
            GF.WriteLine(GF.stringLoggingData.FinalizingDataHeader);
            HashSet<string> mergeDatasNames = new HashSet<string>();
            foreach (CompactedModData compactedModData in CompactedModDataD.Values)
            {
                if (compactedModData.FromMerge)
                {
                    mergeDatasNames.Add(compactedModData.MergeName);
                }
                else
                {
                    compactedModData.PreviouslyESLified = true;
                    compactedModData.OutputModData(false, false);
                }
            }

            foreach (string mergeName in mergeDatasNames)
            {
                string path = Path.Combine(GF.CompactedFormsFolder, mergeName + GF.MergeCacheExtension);
                if (File.Exists(path))
                {
                    CompactedMergeData mergeData = JsonSerializer.Deserialize<CompactedMergeData>(File.ReadAllText(path))!;
                    mergeData.PreviouslyESLified = true;
                    mergeData.OutputModData(false);
                }
                else
                {
                    GF.WriteLine(GF.stringLoggingData.WhyMustYouChangeMyStuff);
                    IEnumerable<string> compactedFormsModFiles = Directory.EnumerateFiles(
                        GF.CompactedFormsFolder,
                        "*" + GF.MergeCacheExtension,
                        SearchOption.AllDirectories);
                    foreach (string file in compactedFormsModFiles)
                    {
                        CompactedMergeData mergeData = JsonSerializer.Deserialize<CompactedMergeData>(File.ReadAllText(file))!;
                        if (mergeName.Equals(mergeData.MergeName))
                        {
                            File.Delete(file);
                            mergeData.PreviouslyESLified = true;
                            mergeData.OutputModData(false);
                            break;
                        }
                    }
                }
            }


        }

    }
}
