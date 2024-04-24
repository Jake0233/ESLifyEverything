﻿using ESLifyEverything.FormData;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using ESLifyEverythingGlobalDataLibrary;
using Mutagen.Bethesda.Plugins.Binary.Parameters;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda;
using Noggog;

namespace ESLifyEverything.PluginHandles
{
    public static partial class HandleMod
    {
        //Lambda get for the Program.CompactedModDataD located in the Program data
        public static Dictionary<string, CompactedModData> CompactedModDataD => ESLify.CompactedModDataD;

        //Dictionary of Plugin Names and Output Locations
        //                        \/        \/
        public static Dictionary<string, string> CustomPluginOutputLocations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        //Uses the Plugin name to find and read the plugin
        //Changing FormKeys on Forms are handled by HandleSubFormHeaders() and HandleUniformFormHeaders()
        //FormLinks are handled using RemapLinks()
        public static async Task<int> HandleSkyrimMod(string pluginName, string? customInternalOutputLocation = null)
        {
            try
            {
                string path = Path.Combine(GF.Settings.DataFolderPath, pluginName);
                if (!File.Exists(path))
                {
                    return await Task.FromResult(0);
                }

                SkyrimMod? mod = null;
                try
                {
                    mod = SkyrimMod.CreateFromBinary(path, SkyrimRelease.SkyrimSE);
                }
                catch (Exception e)
                {
                    GF.WriteLine(pluginName + " was not output Errored.");
                    GF.WriteLine(e.Message);
                    GF.WriteLine(e.StackTrace!);
                    return await Task.FromResult(2);
                }
                GF.WriteLine(pluginName + " loaded.", GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);

                foreach (IMasterReferenceGetter masterReference in mod.ModHeader.MasterReferences.ToArray())
                {
                    if (!ESLify.ActiveLoadOrder.Contains(masterReference.Master.ToString(), StringComparer.OrdinalIgnoreCase))
                    {
                        if (CompactedModDataD.TryGetValue(masterReference.Master.ToString(), out CompactedModData? modData))
                        {
                            if (!modData.FromMerge)
                            {
                                GF.WriteLine(GF.stringLoggingData.MissingMaster + masterReference.Master.ToString());
                                return await Task.FromResult(3);
                            }
                        }
                    }
                }

                DevLog.Log("Handling " + mod.ModKey.ToString());

                bool ModEdited = false;

                mod = HandleUniformFormHeaders(mod, out bool ModEditedU);
                DevLog.Log("Finnished handling uniform keys in " + mod.ModKey.ToString());

                mod = HandleSubFormHeaders(mod, out bool ModEditedS);
                DevLog.Log("Finnished handling sub form keys in " + mod.ModKey.ToString());

                if (ModEditedU || ModEditedS)
                {
                    ModEdited = true;
                    DevLog.Log(mod.ModKey.ToString() + " was changed.");
                }

                //if(!ModEdited)
                //{
                HashSet<string> modNames = new HashSet<string>();

                foreach (IFormLinkGetter? link in mod.EnumerateFormLinks())
                {
                    FormKey formKey = link.FormKey;
                    if (CompactedModDataD.TryGetValue(formKey.ModKey.ToString(), out CompactedModData? modData))
                    {
                        if (mod.ModKey.ToString().Equals(formKey.ModKey.ToString(), StringComparison.OrdinalIgnoreCase) && modData.FromMerge)
                        {
                            continue;
                        }
                        else
                        {
                            foreach (FormHandler form in modData.CompactedModFormList)
                            {
                                if (formKey.IDString().Equals(form.OriginalFormID, StringComparison.OrdinalIgnoreCase))
                                {
                                    modNames.Add(formKey.ModKey.ToString());
                                }
                            }
                        }
                    }
                }

                foreach (MasterReference master in mod.ModHeader.MasterReferences)
                {
                    modNames.Add(master.ToString()!);
                }

                if (modNames.Any())
                {
                    foreach (string modName in modNames)
                    {
                        if(CompactedModDataD.TryGetValue(modName, out var value))
                        {
                            DevLog.Log(mod.ModKey.ToString() + " attempting remapping with CompactedModData from " + modName);
                            mod.RemapLinks(value.ToDictionary());
                            ModEdited = true;
                        }
                    }
                }
            
                //}
                //else
                //{
                //    foreach (IMasterReferenceGetter masterReference in mod.ModHeader.MasterReferences.ToArray())
                //    {
                //        if(CompactedModDataD.TryGetValue(masterReference.Master.ToString(), out CompactedModData? modData))
                //        {
                //            DevLog.Log(mod.ModKey.ToString() + " attempting remapping with CompactedModData from " + modData.ModName);
                //            mod.RemapLinks(modData.ToDictionary());
                //            ModEdited = true;
                //        }
                //    }
                //}

                //ModEdited = true;
                if (ModEdited)
                {
                    foreach (var rec in mod.EnumerateMajorRecords())
                    {
                        rec.IsCompressed = false;
                    }

                    string outputPath = customInternalOutputLocation ?? GetPluginModOutputPath(pluginName);

                    mod.WriteToBinary(Path.Combine(outputPath, pluginName),
                    new BinaryWriteParameters()
                    {
                        MastersListOrdering =
                        new MastersListOrderingByLoadOrder(LoadOrder.GetLoadOrderListings(GameRelease.SkyrimSE, new DirectoryPath(GF.Settings.DataFolderPath)).ToLoadOrder())
                    });

                    GF.WriteLine(String.Format(GF.stringLoggingData.PluginOutputTo, pluginName, outputPath));

                    return await Task.FromResult(1);
                }
            }
            catch (Exception e)
            {
                GF.WriteLine(e.Message);
                GF.WriteLine(e.StackTrace!);
            }

            GF.WriteLine(pluginName + " was not output.", GF.Settings.VerboseConsoleLoging, GF.Settings.VerboseFileLoging);

            return await Task.FromResult(2);
        }

        //Gets the output folder for where plugins need to be outputed to
        public static string GetPluginModOutputPath(string pluginName)
        {
            if (CustomPluginOutputLocations.TryGetValue(pluginName, out string? location))
            {
                if (location.Contains("@mods", StringComparison.OrdinalIgnoreCase))
                {
                    if (GF.Settings.MO2.MO2Support)
                    {
                        location = location.Replace("@mods", GF.Settings.MO2.MO2ModFolder, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        GF.WriteLine(GF.stringLoggingData.AtModsUnsupported);
                        location = GF.Settings.OutputFolder;
                    }
                }

                if (Directory.Exists(location))
                {
                    return location;
                }
            }

            if (GF.Settings.MO2.MO2Support && GF.Settings.MO2.OutputPluginsToSeperateFolders)
            {
                string masterExtentions = pluginName;
                GF.NewMO2FolderPaths = true;
                string OutputPath = Path.Combine(GF.Settings.MO2.MO2ModFolder, $"{masterExtentions}_ESlEverything");
                Directory.CreateDirectory(OutputPath);
                return OutputPath;
            }

            if (GF.Settings.ChangedPluginsOutputToDataFolder)
            {
                return GF.Settings.DataFolderPath;
            }
            return GF.Settings.OutputFolder;
        }

        //Gets the the Compacted FormKey that the Original was changed to
        public static FormKey HandleFormKeyFix(FormKey OrgFormKey, CompactedModData compactedModData, out bool changed)
		{
			changed = false;
			foreach (FormHandler formHandler in compactedModData.CompactedModFormList)
			{
				if (OrgFormKey.IDString().Equals(formHandler.OriginalFormID))
				{
					changed = true;
					return formHandler.CreateCompactedFormKey();
				}
			}
			return OrgFormKey;
		}

    }
}
