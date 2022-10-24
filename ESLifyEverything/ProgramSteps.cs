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

namespace ESLifyEverything
{
    public static partial class Program
    {
        //Extra Startup stuff that ESLify Everything needs
        private static bool StartUp(out StartupError startupError, string ProgramLogName)
        {
            bool startup = GF.Startup(out startupError, ProgramLogName);
            if (startup)
            {
                BSAData.GetBSAData();
            }
            return startup;
        }

        //Region for reading the xEdit log
        #region xEdit Log
        //Parses the xEdit log and readys it for output
        private static void XEditSession()
        {
            XEditLogReader.ReadLog(Path.Combine(GF.Settings.XEditFolderPath, GF.Settings.XEditLogFileName));
            int xEditSessionsCount = XEditLogReader.xEditLog.xEditSessions?.Length ?? 0;
            if (xEditSessionsCount <= 0)
            {
                GF.WriteLine(GF.stringLoggingData.NoxEditSessions);
            }
            else if (GF.Settings.AutoReadAllxEditSeesion)
            {
                string fileName = Path.Combine(GF.Settings.XEditFolderPath, GF.Settings.XEditLogFileName);
                FileInfo fi = new FileInfo(fileName);
                if (fi.Length > 10000000)//Path.Combine(GF.Settings.XEditFilePath, GF.Settings.XEditLogFileName)
                {
                    GF.WriteLine(GF.stringLoggingData.XEditLogFileSizeWarning);
                }
                XEditSessionAutoAll();
                if (GF.Settings.DeletexEditLogAfterRun_Requires_AutoReadAllxEditSeesion)
                {
                    File.Delete(Path.Combine(GF.Settings.XEditFolderPath, GF.Settings.XEditLogFileName));
                }
            }
            else if (GF.Settings.AutoReadNewestxEditSeesion)
            {
                XEditLogReader.xEditLog.xEditSessions![xEditSessionsCount - 1].GenerateCompactedModDatas();
            }
            else
            {
                XEditSessionMenu();
            }
        }

        //Menu to pick which sessions to output
        private static void XEditSessionMenu()
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
        private static void XEditSessionAutoAll()
        {
            foreach (XEditSession session in XEditLogReader.xEditLog.xEditSessions!)
            {
                session.GenerateCompactedModDatas();
            }
        }
        #endregion xEdit Log

        //Trys to locate and then build a zMerge cache
        private static void BuildMergedData()
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
                                    GF.WriteLine(GF.stringLoggingData.GetCompDataLog + potentialCompactedModDataPath, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
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
                                    //File.Move(potentialCompactedModDataPath, Path.Combine(GF.CompactedFormsFolder, mergeData.MergeName + GF.CompactedFormIgnoreExtension));
                                }

                                

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
        private static void ImportModData(string compactedFormsLocation)
        {
            if (!Directory.Exists(compactedFormsLocation))
            {
                GF.WriteLine(String.Format(GF.stringLoggingData.NoCMDinDataFolder, compactedFormsLocation));
                return;
            }

            ImportMergeData();

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
                GF.WriteLine(GF.stringLoggingData.GetCompDataLog + compactedFormsModFile);
                CompactedModData modData = JsonSerializer.Deserialize<CompactedModData>(File.ReadAllText(compactedFormsModFile))!;
                modData.Write();
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

                if (modData.Enabled == true)
                {
                    if (modData.PluginLastModifiedValidation is not null)
                    {
                        CompactedModDataD.TryAdd(modData.ModName, modData);
                    }
                }
                else
                {
                    GF.WriteLine(string.Format(GF.stringLoggingData.SkippingImport, modData.ModName + GF.CompactedFormExtension));
                }
                
            }
        }

        private static void ImportMergeData()
        {
            IEnumerable<string> compactedFormsModFiles = Directory.EnumerateFiles(
                GF.CompactedFormsFolder,
                "*" + GF.MergeCacheExtension,
                SearchOption.AllDirectories);
            foreach(string file in compactedFormsModFiles)
            {
                CompactedMergeData mergeData = JsonSerializer.Deserialize<CompactedMergeData>(File.ReadAllText(file))!;
                string pluginPath = Path.Combine(GF.Settings.DataFolderPath, mergeData.MergeName);
                if (File.Exists(pluginPath))
                {
                    if (mergeData.AlreadyCached())
                    {
                        GF.WriteLine(GF.stringLoggingData.ImportingMergeCache + file);
                        foreach(CompactedModData compactedModData in mergeData.CompactedModDatas)
                        {
                            GF.WriteLine(GF.stringLoggingData.ImportingMergeCompactedModData + compactedModData.ModName, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                            CompactedModDataD.TryAdd(compactedModData.ModName, compactedModData);
                        }
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
        private static CompactedModData ValidateCompactedModDataJson(CompactedModData modData)
        {
            if (modData.IsCompacted(false))
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

                GF.WriteLine(GF.stringLoggingData.EnterToContinue);
                Console.ReadLine();
                GF.WriteLine("", false, true);
                GF.WriteLine("", false, true);
                GF.WriteLine("");
            }
            modData.OutputModData(false, false);
            return modData;
        }

        //Recompacts the known Forms that relate to the CompactedModData
        //This does not change Forms that are not known inside the CompactedModData
        private static void RunRecompact(string pluginName)
        {
            Task<int> handlePluginTask = HandleMod.HandleSkyrimMod(pluginName);
            handlePluginTask.Wait();
            switch (handlePluginTask.Result)
            {
                case 0:
                    Console.WriteLine(pluginName + GF.stringLoggingData.PluginNotFound);
                    break;
                case 1:
                    break;
                case 2:
                    Console.WriteLine(pluginName + GF.stringLoggingData.PluginNotChanged);
                    break;
                default:
                    Console.WriteLine(GF.stringLoggingData.PluginSwitchDefaultMessage);
                    break;
            }
            handlePluginTask.Dispose();
        }
        #endregion Import Mod Data

        //Parses the BSA's in the Data folder
        private static async Task<int> LoadOrderBSAData()
        {
            string loadorderFilePath = Path.Combine(Environment.GetEnvironmentVariable("LocalAppData")!, "Skyrim Special Edition", "loadorder.txt");
            if (!File.Exists(loadorderFilePath))
            {
                loadorderFilePath = "loadorder.txt";
            }

            if (File.Exists(loadorderFilePath))
            {
                LoadOrder = File.ReadAllLines(loadorderFilePath);

                foreach (string plugin in LoadOrder)
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
                BSAExtracted = true;
            }
            return await Task.FromResult(0);
        }

        //Region for Voice Eslify
        #region Voice Eslify
        //Voice Eslify Main Menu
        private static void VoiceESlIfyMenu()
        {
            GF.WriteLine(GF.stringLoggingData.VoiceESLMenuHeader);
            GF.WriteLine($"1. {GF.stringLoggingData.ESLEveryMod}{GF.stringLoggingData.WithCompactedForms}", true, false);//with a _ESlEverything.json attached to it inside of the CompactedForms folders.
            GF.WriteLine($"2. {GF.stringLoggingData.SingleInputMod}{GF.stringLoggingData.WithCompactedForms}", true, false);
            GF.WriteLine(GF.stringLoggingData.CanLoop, true, false);
            GF.WriteLine(GF.stringLoggingData.ExitCodeInput, true, false);
            bool whileContinue = true;
            do
            {
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
        private static bool VoiceESLifyEverything()
        {
            foreach (CompactedModData modData in CompactedModDataD.Values)
            {
                VoiceESLifyMod(modData);
            }
            return false;
        }

        //Voice Eslify menu to select which Compacted Mod Data to check
        private static void VoiceESLifySingleMod()
        {
            bool whileContinue = true;
            string input;
            CompactedModData? modData;
            do
            {
                GF.WriteLine($"{GF.stringLoggingData.SingleModInputHeader}{GF.stringLoggingData.ExamplePlugin}");
                GF.WriteLine(GF.stringLoggingData.ExitCodeInput, true, false);
                input = Console.ReadLine() ?? "";
                if (CompactedModDataD.TryGetValue(input, out modData))
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
        private static void VoiceESLifyMod(CompactedModData modData)
        {
            Task v = ExtractBSAVoiceData(modData.ModName);
            v.Wait();
            v.Dispose();
            VoiceESLifyModData(modData, GF.ExtractedBSAModDataPath);
            VoiceESLifyModData(modData, GF.Settings.DataFolderPath);
        }

        //Extracts Voice Lines from BSA with Voice lines connected to the plugin
        private static async Task<int> ExtractBSAVoiceData(string pluginName)
        {
            foreach (string plugin in LoadOrderNoExtensions)
            {
                BSA? bsa;
                if (BSAData.BSAs.TryGetValue(plugin, out bsa))
                {
                    foreach (string connectedVoice in bsa.VoiceModConnections)
                    {
                        if (connectedVoice.Equals(pluginName, StringComparison.OrdinalIgnoreCase))
                        {
                            GF.WriteLine(String.Format(GF.stringLoggingData.BSAContainsData, bsa.BSAName_NoExtention, pluginName));
                            Process m = new Process();
                            m.StartInfo.FileName = ".\\BSABrowser\\bsab.exe";
                            m.StartInfo.Arguments = $"\"{Path.GetFullPath(Path.Combine(GF.Settings.DataFolderPath, bsa.BSAName_NoExtention + ".bsa"))}\" -f \"{pluginName}\"  -e -o \"{Path.GetFullPath(GF.ExtractedBSAModDataPath)}\"";
                            m.Start();
                            m.WaitForExit();
                            m.Dispose();
                        }
                    }
                }
            }
            return await Task.FromResult(0);
        }

        //Checks the given Compacted Mod Data for Voice lines and fixes them from targeted locations
        private static void VoiceESLifyModData(CompactedModData modData, string dataStartPath)
        {
            if (Directory.Exists(Path.Combine(dataStartPath, "sound\\voice", modData.ModName)))
            {
                foreach (FormHandler form in modData.CompactedModFormList)
                {
                    IEnumerable<string> voiceFilePaths = Directory.EnumerateFiles(
                        Path.Combine(dataStartPath, "sound\\voice", modData.ModName),
                        "*" + form.GetOriginalFormID() + "*",
                        SearchOption.AllDirectories);
                    foreach (string voiceFilePath in voiceFilePaths)
                    {
                        GF.WriteLine(GF.stringLoggingData.OriganalPath + voiceFilePath, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                        string[] pathArr = voiceFilePath.Split('\\');

                        string newStartPath = Path.Combine(GF.Settings.OutputFolder, $"sound\\voice\\{form.ModName}\\{pathArr[pathArr.Length - 2]}");
                        Directory.CreateDirectory(newStartPath);
                        string newPath = Path.Combine(newStartPath, pathArr[pathArr.Length - 1].Replace(form.GetOriginalFormID(), form.GetCompactedFormID(), StringComparison.OrdinalIgnoreCase));

                        File.Copy(voiceFilePath, newPath, true);
                        GF.WriteLine(GF.stringLoggingData.NewPath + newPath);
                    }
                }
            }
        }
        #endregion Voice Eslify

        //Region for FaceGen Eslify
        #region FaceGen Eslify
        //FaceGen Eslify Main Menu
        private static void FaceGenESlIfyMenu()
        {
            GF.WriteLine(GF.stringLoggingData.FaceGenESLMenuHeader);
            GF.WriteLine($"1. {GF.stringLoggingData.ESLEveryMod}{GF.stringLoggingData.WithCompactedForms}",true, false);//with a _ESlEverything.json attached to it inside of the CompactedForms folders.
            GF.WriteLine($"2. {GF.stringLoggingData.SingleInputMod}{GF.stringLoggingData.WithCompactedForms}", true, false);
            GF.WriteLine(GF.stringLoggingData.CanLoop, true, false);
            GF.WriteLine(GF.stringLoggingData.ExitCodeInput, true, false);
            bool whileContinue = true;
            do
            {
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
        private static bool FaceGenESLifyEverything()
        {
            foreach (CompactedModData modData in CompactedModDataD.Values)
            {
                FaceGenESLifyMod(modData);
            }
            return false;
        }

        //FaceGen Eslify menu to select which Compacted Mod Data to check
        private static void FaceGenESLifySingleMod()
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
        private static void FaceGenESLifyMod(CompactedModData modData)
        {
            Task f = ExtractBSAFaceGenData(modData.ModName);
            f.Wait();
            f.Dispose();
            FaceGenEslifyModData(modData, GF.ExtractedBSAModDataPath);
            FaceGenEslifyModData(modData, GF.Settings.DataFolderPath);
            
        }

        //Extracts Voice Lines from BSA with FaceGen connected to the plugin
        private static async Task<int> ExtractBSAFaceGenData(string pluginName)
        {
            foreach (string plugin in LoadOrderNoExtensions)
            {
                BSA? bsa;
                if (BSAData.BSAs.TryGetValue(plugin, out bsa))
                {
                    foreach (string connectedFaceGen in bsa.FaceGenModConnections)
                    {

                        if (connectedFaceGen.Equals(pluginName, StringComparison.OrdinalIgnoreCase))
                        {
                            GF.WriteLine(String.Format(GF.stringLoggingData.BSAContainsData, bsa.BSAName_NoExtention, pluginName));
                            Process m = new Process();
                            m.StartInfo.FileName = ".\\BSABrowser\\bsab.exe";
                            m.StartInfo.Arguments = $"\"{Path.GetFullPath(Path.Combine(GF.Settings.DataFolderPath, bsa.BSAName_NoExtention + ".bsa"))}\" -f \"{pluginName}\"  -e -o \"{Path.GetFullPath(GF.ExtractedBSAModDataPath)}\"";
                            m.Start();
                            m.WaitForExit();
                            m.Dispose();
                            if (bsa.HasTextureBSA)
                            {
                                Process t = new Process();
                                t.StartInfo.FileName = ".\\BSABrowser\\bsab.exe";
                                t.StartInfo.Arguments = $"\"{Path.GetFullPath(Path.Combine(GF.Settings.DataFolderPath, bsa.BSAName_NoExtention + " - Textures.bsa"))}\" -f \"{pluginName}\"  -e -o \"{Path.GetFullPath(GF.ExtractedBSAModDataPath)}\"";
                                t.Start();
                                t.WaitForExit();
                                t.Dispose();
                            }
                        }
                    }
                }
            }
            return await Task.FromResult(0);
        }

        //Checks the given Compacted Mod Data for FaceGen and fixes them from targeted locations
        private static void FaceGenEslifyModData(CompactedModData modData, string dataStartPath)
        {
            if (Directory.Exists(Path.Combine(dataStartPath, "Meshes\\Actors\\Character\\FaceGenData\\FaceGeom\\", modData.ModName)))
            {
                foreach (FormHandler form in modData.CompactedModFormList)
                {
                    IEnumerable<string> FaceGenTexFilePaths = Directory.EnumerateFiles(
                        Path.Combine(dataStartPath, "Textures\\Actors\\Character\\FaceGenData\\FaceTint\\", modData.ModName),
                        "*" + form.OriginalFormID + "*.dds",
                        SearchOption.AllDirectories);
                    foreach (string FaceGenFilePath in FaceGenTexFilePaths)
                    {
                        GF.WriteLine(GF.stringLoggingData.OriganalPath + FaceGenFilePath, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);

                        string newStartPath = Path.Combine(GF.Settings.OutputFolder, "Textures\\Actors\\Character\\FaceGenData\\FaceTint\\" + form.ModName);
                        Directory.CreateDirectory(newStartPath);
                        string newPath = Path.Combine(newStartPath, "00" + form.CompactedFormID + ".dds");

                        File.Copy(FaceGenFilePath, newPath, true);
                        GF.WriteLine(GF.stringLoggingData.NewPath + newPath);
                    }

                    IEnumerable<string> FaceGenFilePaths = Directory.EnumerateFiles(
                        Path.Combine(dataStartPath, "Meshes\\Actors\\Character\\FaceGenData\\FaceGeom\\", modData.ModName),
                        "*" + form.OriginalFormID + "*.nif",
                        SearchOption.AllDirectories);

                    foreach (string FaceGenFilePath in FaceGenFilePaths)
                    {
                        GF.WriteLine(GF.stringLoggingData.OriganalPath + FaceGenFilePath, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);

                        string newStartPath = Path.Combine(GF.Settings.OutputFolder, "Meshes\\Actors\\Character\\FaceGenData\\FaceGeom\\" + form.ModName);
                        Directory.CreateDirectory(newStartPath);
                        string newPath = Path.Combine(newStartPath, "00" + form.CompactedFormID + ".nif");

                        File.Copy(FaceGenFilePath, newPath, true);
                        GF.WriteLine(GF.stringLoggingData.NewPath + newPath);
                        EditedFaceGen = true;
                        using (StreamWriter stream = File.AppendText(GF.FaceGenFileFixPath))
                        {
                            stream.WriteLine(Path.GetFullPath(newPath) + ";" + form.OriginalFormID + ";" + form.CompactedFormID);
                        }
                    }

                }
            }
        }
        #endregion FaceGen Eslify

        //Region for Eslifying Data files
        #region ESLify Data Files
        //Data Files Eslify Main Menu
        private static void ESLifyDataFilesMainMenu()
        {
            GetESLifyModConfigurationFiles();

            Console.WriteLine(GF.stringLoggingData.InputDataFileExecutionPromt);
            Console.WriteLine($"1. {GF.stringLoggingData.ESLEveryModConfig}", true, false);
            Console.WriteLine($"2. {GF.stringLoggingData.SelectESLModConfig}", true, false);
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
            InternallyCodedDataFileConfigurations();
        }

        //Runs all Mod Configurations
        private static void ESLifyAllDataFiles()
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
        }

        //Runs the Internally coded Mod Configurations
        private static void InternallyCodedDataFileConfigurations()
        {
            Console.WriteLine("\n\n\n\n");
            GF.WriteLine(GF.stringLoggingData.StartingRaceMenuESLify);
            RaceMenuESLify();

            DevLog.Pause("After RaceMenu ESLify Pause");

            Console.WriteLine("\n\n\n\n");
            GF.WriteLine(GF.stringLoggingData.StartingCustomSkillsESLify);
            Task CustomSkills = CustomSkillsFramework();
            CustomSkills.Wait();
            CustomSkills.Dispose();

            DevLog.Pause("After Custom Skills Framework ESLify Pause");
        }

        //Gets the Mod Configuration files for ESLifying Data Files
        private static void GetESLifyModConfigurationFiles()
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
        private static void HandleConfigurationType(BasicSingleFile ModConfiguration)
        {
            Console.WriteLine("\n\n\n\n");
            GF.WriteLine(ModConfiguration.StartingLogLine);
            SingleBasicFile(ModConfiguration);
        }

        //Handles Logging for BasicDirectFolder
        private static void HandleConfigurationType(BasicDirectFolder ModConfiguration)
        {
            Console.WriteLine("\n\n\n\n");
            GF.WriteLine(ModConfiguration.StartingLogLine);
            EnumDirectFolder(ModConfiguration);
        }

        //Handles Logging for BasicDataSubfolder
        private static void HandleConfigurationType(BasicDataSubfolder ModConfiguration)
        {
            Console.WriteLine("\n\n\n\n");
            GF.WriteLine(ModConfiguration.StartingLogLine);
            Task t = EnumDataSubfolder(ModConfiguration);
            t.Wait();
            t.Dispose();
        }

        //Handles Logging for ComplexTOML
        private static void HandleConfigurationType(ComplexTOML ModConfiguration)
        {
            Console.WriteLine("\n\n\n\n");
            GF.WriteLine(ModConfiguration.StartingLogLine);
            Task t = EnumToml(ModConfiguration);
            t.Wait();
            t.Dispose();
        }

        //Handles Logging for DelimitedFormKeys
        private static void HandleConfigurationType(DelimitedFormKeys ModConfiguration)
        {
            Console.WriteLine("\n\n\n\n");
            GF.WriteLine(ModConfiguration.StartingLogLine);
            EnumDelimitedFormKeys(ModConfiguration);
        }

        //Menu for selecting what Mod Configuration to check to run on Data Files
        private static void ESLifySelectedDataFilesMenu()
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
                        RunSelectedModConfig(modConMenuList, selectedMenuItem -1);
                    }
                }


            } while (endMenu == false);
        }

        //Runs the selected Mod Configuration over Data Files
        private static void RunSelectedModConfig(string[] modConMenuList, int selectedMenuItem)
        {
            foreach (var modConfiguration in BasicSingleModConfigurations)
            {
                if (modConfiguration.Name.Equals(modConMenuList[selectedMenuItem]))
                {
                    HandleConfigurationType(modConfiguration);
                }
            }
            foreach (var modConfiguration in BasicDirectFolderModConfigurations)
            {
                if (modConfiguration.Name.Equals(modConMenuList[selectedMenuItem]))
                {
                    HandleConfigurationType(modConfiguration);
                }
            }
            foreach (var modConfiguration in BasicDataSubfolderModConfigurations)
            {
                if (modConfiguration.Name.Equals(modConMenuList[selectedMenuItem]))
                {
                    HandleConfigurationType(modConfiguration);
                }
            }
            foreach (var modConfiguration in ComplexTOMLModConfigurations)
            {
                if (modConfiguration.Name.Equals(modConMenuList[selectedMenuItem]))
                {
                    HandleConfigurationType(modConfiguration);
                }
            }
            foreach (var modConfiguration in DelimitedFormKeysModConfigurations)
            {
                if (modConfiguration.Name.Equals(modConMenuList[selectedMenuItem]))
                {
                    HandleConfigurationType(modConfiguration);
                }
            }

        }
        #endregion ESLify Data Files

        //Region for RaceMenu Eslify
        private static void RaceMenuESLify()
        {
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
                                    CompactedModData? mod;
                                    if (CompactedModDataD.TryGetValue(modName, out mod))
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

        //Region for Custom Skills Framework Eslify
        private static async Task<int> CustomSkillsFramework()
        {
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
                    string[] customSkillConfigFile = File.ReadAllLines(customSkillConfig);
                    string[] newCustomSkillConfigFile = new string[customSkillConfigFile.Length];
                    string currentModName = "";
                    int currentModNameLine = -1;
                    CompactedModData currentMod = new CompactedModData();
                    bool changed = false;
                    for (int i = 0; i < customSkillConfigFile.Length; i++)
                    {
                        string line = customSkillConfigFile[i];
                        foreach (string modName in CompactedModDataD.Keys)
                        {
                            if (customSkillConfigFile[i].Contains(modName, StringComparison.OrdinalIgnoreCase))
                            {
                                currentModName = modName;
                                currentModNameLine = i;
                                CompactedModDataD.TryGetValue(modName, out currentMod!);
                                GF.WriteLine("", GF.Settings.VerboseConsoleLoging, false);
                                GF.WriteLine(GF.stringLoggingData.ModLine + line, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                            }
                        }
                        if (!currentModName.Equals(""))
                        {
                            foreach (FormHandler form in currentMod.CompactedModFormList)
                            {
                                if (line.Contains(form.GetOriginalFormID()))
                                {
                                    GF.WriteLine(GF.stringLoggingData.OldLine + line, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                                    line = line.Replace(form.GetOriginalFormID(), form.GetCompactedFormID());
                                    customSkillConfigFile[currentModNameLine] = customSkillConfigFile[currentModNameLine].Replace(currentModName, form.ModName);
                                    GF.WriteLine(GF.stringLoggingData.NewLine + line, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                                    currentModName = "";
                                    changed = true;
                                }
                            }
                        }
                        newCustomSkillConfigFile[i] = line;
                    }
                    OuputDataFileToOutputFolder(changed, customSkillConfig, newCustomSkillConfigFile, GF.stringLoggingData.CustomSkillsFileUnchanged);
                }

            }
            else
            {
                GF.WriteLine(GF.stringLoggingData.FolderNotFoundError + startSearchPath);
            }
            return await Task.FromResult(0);
        }

        //Region for fixing records and references inside of plugins
        #region Plugins
        //Starts the chack for Master file and runs what was selected in SelectCompactedModsMenu()
        private static void ReadLoadOrder()
        {
            HashSet<string> checkPlugins = SelectCompactedModsMenu();

            HashSet<string> runPlugins = new HashSet<string>();
            for (int i = 1; i < LoadOrder.Length; i++)
            {
                if (File.Exists(Path.Combine(GF.Settings.DataFolderPath, LoadOrder[i])))
                {
                    if (!GF.IgnoredPlugins.Contains(LoadOrder[i]))
                    {
                        GF.WriteLine(String.Format(GF.stringLoggingData.PluginCheckMod, LoadOrder[i]), GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);
                        if (File.Exists(Path.Combine(GF.Settings.DataFolderPath, LoadOrder[i])))
                        {
                            MasterReferenceCollection? masterCollection = MasterReferenceCollection.FromPath(Path.Combine(GF.Settings.DataFolderPath, LoadOrder[i]), GameRelease.SkyrimSE);
                            foreach (var master in masterCollection.Masters.ToHashSet())
                            {
                                if (checkPlugins.Contains(master.Master.FileName))
                                {
                                    GF.WriteLine(String.Format(GF.stringLoggingData.PluginAttemptFix, LoadOrder[i]));
                                    runPlugins.Add(LoadOrder[i]);
                                    break;
                                }
                            }
                        }
                    }
                }
                GF.WriteLine(String.Format(GF.stringLoggingData.ProcessedPluginsLogCount, i, LoadOrder.Length, GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging));
            }

            //Fix and output plugins that still use uncompacted data
            foreach (string pluginToRun in runPlugins)
            {
                Task<int> handlePluginTask = HandleMod.HandleSkyrimMod(pluginToRun);
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
                    default:
                        GF.WriteLine(GF.stringLoggingData.PluginSwitchDefaultMessage);
                        break;
                }
                handlePluginTask.Dispose();
            }

        }

        //Menu to select Compacted Mod Data to check over load order
        private static HashSet<string> SelectCompactedModsMenu()
        {
            HashSet<string> slectedCompactedMods = new HashSet<string>();
            List<string> menuList = new List<string>();
            menuList.AddRange(CompactedModDataD.Keys);
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

    }
}
