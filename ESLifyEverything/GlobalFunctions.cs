﻿using ESLifyEverything.FormData;
using ESLifyEverything.Properties;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Text.Json;

namespace ESLifyEverything
{
    public static partial class GF
    {
        public static readonly string SettingsVersion = "3.0.0";
        public static readonly string ChangedScriptsPath = ".\\ChangedScripts";
        public static readonly string CompactedFormsFolder = ".\\CompactedForms";
        public static readonly string ExtractedBSAModDataPath = ".\\ExtractedBSAModData";
        public static readonly string SourceSubPath = "Source\\Scripts";

        public static AppSettings Settings = new AppSettings();
        public static StringResources stringsResources = new StringResources();
        public static StringLoggingData stringLoggingData = new StringLoggingData();
        public static JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions() { WriteIndented = true };
        public static string[] DefaultScriptBSAs = new string[0];
        public static HashSet<string> IgnoredPlugins = new HashSet<string>();

        public static string logName = "log.txt";
        public static string FaceGenFileFixPath = "";

        public static List<string> BSALoadOrder = new List<string>();

        public static bool Startup(out int startupError, string ProgramLogName)
        {
            logName = ProgramLogName;
            File.Create(logName).Close();
            startupError = 0;

            if (!File.Exists("AppSettings.json"))
            {
                startupError = 1;
                IConfiguration stringResorsConfig = new ConfigurationBuilder().AddJsonFile(".\\Properties\\StringResources.json").AddEnvironmentVariables().Build();
                stringLoggingData = stringResorsConfig.GetRequiredSection("StringLoggingData").Get<StringLoggingData>();
                return false;
            }
            
            bool startUp = true;
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("AppSettings.json")
                .AddJsonFile(".\\Properties\\StringResources.json")
                .AddJsonFile(".\\Properties\\DefaultBSAs.json")
                .AddJsonFile(".\\Properties\\IgnoredPugins.json")
                .AddEnvironmentVariables().Build();
            stringLoggingData = config.GetRequiredSection("StringLoggingData").Get<StringLoggingData>();

            try
            {
                string version = config.GetRequiredSection("SettingsVersion").Get<string>();
                if (!version.Equals(GF.SettingsVersion))
                {
                    startupError = 3;
                    return false;
                }
            }
            catch (Exception)
            {
                startupError = 1;
                return false;
            }
            
            Settings = config.GetRequiredSection("Settings").Get<AppSettings>();
            stringsResources = config.GetRequiredSection("StringResources").Get<StringResources>();
            DefaultScriptBSAs = config.GetRequiredSection("DefaultScriptBSAs").Get<string[]>();
            IgnoredPlugins = config.GetRequiredSection("IgnoredPugins").Get<HashSet<string>>();

            if (GF.Settings.AutoReadAllxEditSeesion == false)
            {
                GF.Settings.DeletexEditLogAfterRun_Requires_AutoReadAllxEditSeesion = false;
            }

            if (!Directory.Exists(GF.Settings.DataFolderPath))
            {
                GF.WriteLine(GF.stringLoggingData.DataFolderNotFound);
                startUp = false;
            }
            
            
            if (!Directory.Exists(GF.Settings.XEditFolderPath))
            {
                GF.WriteLine(GF.stringLoggingData.XEditLogNotFoundStartup);
                startUp = false;
                if (File.Exists(GF.Settings.XEditFolderPath))
                {
                    GF.WriteLine(GF.stringLoggingData.XEditFolderSetToFile);
                }
            }
            else if (!File.Exists(Path.Combine(GF.Settings.XEditFolderPath, "SSEEdit.exe")))
            {
                GF.WriteLine(GF.stringLoggingData.IntendedForSSE);
            }

            if (Directory.Exists(GF.Settings.XEditFolderPath))
            {
                FaceGenFileFixPath = Path.Combine(GF.Settings.XEditFolderPath, "FaceGenEslIfyFix.txt");
                File.Create(FaceGenFileFixPath).Close();
            }

            if (!File.Exists(Path.Combine(GF.Settings.XEditFolderPath, GF.Settings.XEditLogFileName)))
            {
                GF.WriteLine(GF.stringLoggingData.XEditLogNotFound);
                startupError = 2;
            }

            if (!File.Exists(".\\Champollion\\Champollion.exe"))
            {
                startUp = false;
                GF.WriteLine(GF.stringLoggingData.ChampollionMissing);
            }

            if (!File.Exists(Path.Combine(GF.GetSkyrimRootFolder(), "Papyrus Compiler\\PapyrusCompiler.exe")))
            {
                startUp = false;
                GF.WriteLine(GF.stringLoggingData.PapyrusCompilerMissing);
                GF.WriteLine(GF.stringLoggingData.PapyrusCompilerMissing2);
                GF.WriteLine(GF.stringLoggingData.PapyrusCompilerMissing3);
                GF.WriteLine(GF.stringLoggingData.PapyrusCompilerMissing4);
            }
            else
            {
                if(File.Exists(Path.Combine(GF.Settings.DataFolderPath, $"{GF.SourceSubPath}\\{GF.Settings.PapyrusFlag}")))
                {
                    Directory.CreateDirectory(Path.Combine(GF.ExtractedBSAModDataPath, $"{GF.SourceSubPath}"));
                    File.Copy(Path.Combine(GF.Settings.DataFolderPath, $"{GF.SourceSubPath}\\{GF.Settings.PapyrusFlag}"), 
                        Path.Combine(GF.ExtractedBSAModDataPath, $"{GF.SourceSubPath}\\{GF.Settings.PapyrusFlag}"), true);
                }
                else
                {
                    GF.WriteLine(GF.stringLoggingData.PapyrusFlagFileMissing);
                    GF.WriteLine(String.Format(GF.stringLoggingData.PapyrusFlagFileMissing2, GF.Settings.PapyrusFlag));
                    GF.WriteLine(GF.stringLoggingData.PapyrusCompilerMissing4);
                }
            }

            if (!Directory.Exists(GF.Settings.OutputFolder))
            {
                startUp = false;
                GF.WriteLine(GF.stringLoggingData.OutputFolderNotFound);
                GF.WriteLine(String.Format(GF.stringLoggingData.OutputFolderIsRequired, GF.stringLoggingData.PotectOrigonalScripts));
            }
            else
            {
                GF.WriteLine(GF.stringLoggingData.OutputFolderWarning, true, false);
            }

            if (Directory.Exists(Path.Combine(GF.Settings.OutputFolder, "scripts")))
            {
                IEnumerable<string> scripts = Directory.EnumerateFiles(
                    Path.Combine(GF.Settings.OutputFolder, "scripts"),
                    "*.pex",
                    SearchOption.TopDirectoryOnly);
                if (scripts.Any())
                {
                    startUp = false;
                    GF.WriteLine(String.Format(GF.stringLoggingData.ClearYourOutputFolderScripts, GF.stringLoggingData.PotectOrigonalScripts));
                }
            }

            if (!startUp)
            {
                return startUp;
            }

            if (GF.Settings.RunSubPluginCompaction)
            {
                if (!GF.Settings.PapyrusFlag.Equals("TESV_Papyrus_Flags.flg"))
                {
                    if(!File.Exists(Path.Combine(GF.Settings.DataFolderPath, "Skyrim.esm")))
                    {
                        GF.WriteLine(GF.stringLoggingData.ESLifyEverythingIsNotSetUpForSkyrim);
                        GF.Settings.RunSubPluginCompaction = false;
                    }
                }
            }

            if (GF.Settings.MO2Support)
            {
                if (!Directory.Exists(GF.Settings.MO2ModFolder))
                {
                    GF.WriteLine(GF.stringLoggingData.MO2ModsFolderDoesNotExist);
                    GF.Settings.MO2Support = false;
                }
            }

            Directory.CreateDirectory(GF.ExtractedBSAModDataPath);
            Directory.CreateDirectory(GF.ChangedScriptsPath);
            GF.ClearChangedScripts();

            Directory.CreateDirectory(CompactedFormsFolder);

            if (Directory.Exists(Path.Combine(GF.ExtractedBSAModDataPath, GF.SourceSubPath)))
            {
                IEnumerable<string> scripts = Directory.EnumerateFiles(
                    Path.Combine(GF.ExtractedBSAModDataPath, GF.SourceSubPath),
                    "*.psc",
                    SearchOption.TopDirectoryOnly);
                if (!scripts.Any())
                {
                    GF.Settings.AutoRunScriptDecompile = true;
                }
            }

            BSAData.GetBSAData();


            return startUp;
        }

        private static void ClearChangedScripts()
        {
            IEnumerable<string> changedSouce = Directory.EnumerateFiles(
                    GF.ChangedScriptsPath,
                    "*.psc",
                    SearchOption.TopDirectoryOnly);
            if (changedSouce.Any())
            {
                foreach(string script in changedSouce)
                {
                    File.Delete(script);
                }
            }
        }

        public static void WriteLine(string logLine, bool consoleLog = true, bool fileLogging = true)
        {
            if (consoleLog)
            {
                Console.WriteLine(logLine);
            }
            if (fileLogging)
            {
                using (StreamWriter stream = File.AppendText(logName))
                {
                    stream.WriteLine(logLine);
                }
            }
        }

        public static void WriteLine(List<FormHandler> logData, bool consoleLog = true, bool fileLogging = true)
        {
            if (consoleLog)
            {
                foreach (FormHandler item in logData)
                {
                    Console.WriteLine(item!.ToString());
                }
            }
            if (fileLogging)
            {
                using (StreamWriter stream = File.AppendText(logName))
                {
                    foreach (FormHandler item in logData)
                    {
                        stream.WriteLine(item!.ToString());
                    }
                }
            }
        }

        public static void WriteLine(HashSet<FormHandler> logData, bool consoleLog = true, bool fileLogging = true)
        {
            if (consoleLog)
            {
                foreach (FormHandler item in logData)
                {
                    Console.WriteLine(item!.ToString());
                }
            }
            if (fileLogging)
            {
                using (StreamWriter stream = File.AppendText("ESLifyEverything_Log.txt"))
                {
                    foreach (FormHandler item in logData)
                    {
                        stream.WriteLine(item!.ToString());
                    }
                }
            }
        }

        //returns true when a valid input number is input
        //-1 = return exit code in selectedMenuItem
        public static bool WhileMenuSelect(int menuMaxNum, out int selectedMenuItem, int MenuMinNum = 0)
        {
            string input = Console.ReadLine() ?? "";
            if (input.Equals("XXX", StringComparison.OrdinalIgnoreCase))
            {
                selectedMenuItem = -1;
                return true;
            }

            if (Int32.TryParse(input, out selectedMenuItem))
            {
                if (selectedMenuItem >= MenuMinNum && selectedMenuItem <= menuMaxNum)
                {
                    return true;
                }
            }

            return false;
        }

        public static void GenerateSettingsFileError()
        {
            GF.WriteLine(GF.stringLoggingData.SettingsFileNotFound);
            GF.WriteLine(GF.stringLoggingData.GenSettingsFile);
            GF.WriteLine(GF.stringLoggingData.EditYourSettings);
            new AppSettings().Build();
        }

        public static void UpdateSettingsFile()
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("AppSettings.json")
                .AddEnvironmentVariables().Build();
            
            switch (config.GetRequiredSection("SettingsVersion").Get<string>())
            {
                case "1.9":
                    GF.WriteLine(GF.stringLoggingData.GenSettingsFile);
                    GF.WriteLine(GF.stringLoggingData.EditYourSettings);
                    UAppSettings.AppSettings(config.GetRequiredSection("Settings").Get<AppSettings19>()).Build();
                    break;
                default:
                    GenerateSettingsFileError();
                    break;
            }
        }

        //origonalPath = Origonal path with replaced origonal FormID if it contains it
        public static string FixOuputPath(string origonalPath)
        {
            string newPath = origonalPath.Replace(GF.Settings.DataFolderPath, "");
            if (newPath[0] == '\\')
            {
                newPath = newPath.Substring(1);
            }
            return Path.Combine(GF.Settings.OutputFolder, newPath);
        }

        public static string FixOuputPath(string origonalPath, string origonalDataStartPath, string newStartPath)
        {
            string newPath = origonalPath.Replace(origonalDataStartPath, "");
            if (newPath[0] == '\\')
            {
                newPath = newPath.Substring(1);
            }
            return Path.Combine(newStartPath, newPath);
        }

        public static string GetSkyrimRootFolder()
        {
            return Path.GetFullPath(GF.Settings.DataFolderPath).Replace("\\Data", "");
        }

        public static void RunFaceGenFix()
        {
            string loadorder = Path.GetFullPath(".\\Properties\\JustSkyrimLO.txt");
            string gameType = "-SSE";
            if (File.Exists(Path.Combine(GF.Settings.XEditFolderPath, "Edit Scripts\\_ESLifyEverythingFaceGenFix.pas")))
            {
                bool run = true;
                Process RunXEditFaceGenFix = new Process();
                if (File.Exists(Path.Combine(GF.Settings.XEditFolderPath, "SSEEdit64.exe")))
                {
                    RunXEditFaceGenFix.StartInfo.FileName = Path.Combine(GF.Settings.XEditFolderPath, "SSEEdit64.exe");
                }
                else if (File.Exists(Path.Combine(GF.Settings.XEditFolderPath, "SSEEdit.exe")))
                {
                    RunXEditFaceGenFix.StartInfo.FileName = Path.Combine(GF.Settings.XEditFolderPath, "SSEEdit.exe");
                }
                else if (File.Exists(Path.Combine(GF.Settings.XEditFolderPath, "FO4Edit64.exe")))
                {
                    loadorder = Path.GetFullPath(".\\Properties\\JustFalloutLO.txt");
                    gameType = "-fo4";
                    RunXEditFaceGenFix.StartInfo.FileName = Path.Combine(GF.Settings.XEditFolderPath, "FO4Edit64.exe");
                }
                else if (File.Exists(Path.Combine(GF.Settings.XEditFolderPath, "FO4Edit.exe")))
                {
                    loadorder = Path.GetFullPath(".\\Properties\\JustFalloutLO.txt");
                    gameType = "-fo4";
                    RunXEditFaceGenFix.StartInfo.FileName = Path.Combine(GF.Settings.XEditFolderPath, "FO4Edit.exe");
                }
                else
                {
                    GF.WriteLine(GF.stringLoggingData.NoxEditEXE);
                    run = false;
                }

                if (run)
                {
                    if (File.Exists(Path.Combine(Environment.GetEnvironmentVariable("LocalAppData")!, "Skyrim Special Edition", "Skyrim.ini")))
                    {
                        RunXEditFaceGenFix.StartInfo.Arguments = $"{gameType} " +
                        $"-D:\"{GF.Settings.DataFolderPath}\" " +
                        $"-I:\"{Path.Combine(Environment.GetEnvironmentVariable("LocalAppData")!, "Skyrim Special Edition", "Skyrim.ini")}\" " +
                        $" {loadorder}" +
                        "-script:\"_ESLifyEverythingFaceGenFix.pas\" -autoload";
                        GF.WriteLine(GF.stringLoggingData.RunningxEditEXE);
                        RunXEditFaceGenFix.Start();
                        RunXEditFaceGenFix.WaitForExit();
                    }
                    else 
                    {
                        RunXEditFaceGenFix.StartInfo.Arguments = "-TES5 -script:\"_ESLifyEverythingFaceGenFix.pas\" -autoload";
                        GF.WriteLine(GF.stringLoggingData.RunningxEditEXE);
                        RunXEditFaceGenFix.Start();
                        RunXEditFaceGenFix.WaitForExit();
                    }
                }
                RunXEditFaceGenFix.Dispose();

            }
            else
            {
                GF.WriteLine(GF.stringLoggingData.FixFaceGenScriptNotFound);
            }

        }
        
        public static void MoveCompactedModDataJsons()
        {
            string oldCompactedFormsFolder = Path.Combine(GF.Settings.OutputFolder, "CompactedForms");
            if (Directory.Exists(oldCompactedFormsFolder))
            {
                IEnumerable<string> compactedFormsModFiles = Directory.EnumerateFiles(oldCompactedFormsFolder, "*_ESlEverything.json", SearchOption.TopDirectoryOnly);

                foreach(string files in compactedFormsModFiles)
                {
                    File.Move(files, Path.Combine(CompactedFormsFolder, Path.GetFileName(files)), true);
                }

            }

        }
    }
}
