using System;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Linq;
using System.Threading;
using System.Security.Permissions;
using Newtonsoft.Json;
using osu_patch.Explorers;
using osu_patch.Naming;
using osu_patch.Lib.StringFixer;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace osu_rewrite
{
    public class Base
    {
        private static INameProvider _nameStorage;
        public static string OsuPath;
        public static Assembly OsuAssembly;
        private static ModuleDefMD _obfOsuModule;
        private static ModuleDefMD _cleanOsuModule;
        private static RConfig _config;
        private static string _osuHash;
        public static ModuleExplorer exp;
        private readonly static string _cachePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "//cache";
        private readonly static string _osuPath = Path.Combine(_cachePath, $"osupath.ch");
        private const MetadataFlags DEFAULT_METADATA_FLAGS = MetadataFlags.PreserveRids |
                                                     MetadataFlags.PreserveUSOffsets |
                                                     MetadataFlags.PreserveBlobOffsets |
                                                     MetadataFlags.PreserveExtraSignatureData |
                                                     MetadataFlags.KeepOldMaxStack;
        [STAThread]
        static void Main(string[] args)
        {
            if (!Directory.Exists(_cachePath))
            {
                Directory.CreateDirectory(_cachePath);
            }

            if (args.Length < 1 && !File.Exists(_osuPath))
            {
                Console.WriteLine("[RW]: Please pass osu! file or run with osu! path (e.g osu!rewrite.exe C:/osu!/osu!.exe)");
                Console.ReadKey();
                return;
            }
            else if (args.Length == 1 && !File.Exists(_osuPath))
            {
                File.WriteAllText(_osuPath, args[0]);
                Initialize();
            }
            else
            {
                Initialize();
            }
        }
        private static void Initialize()
        {
            var lines = File.ReadAllLines(_osuPath);
            OsuPath = lines[0];
            _osuHash = MD5Helper.ComputeFile(OsuPath);
            OsuAssembly = Assembly.LoadFrom(OsuPath);
            _obfOsuModule = ModuleDefMD.Load(OsuPath);
            _cleanOsuModule = ModuleDefMD.Load(Resource.clean);

            StringFixer.Fix(_obfOsuModule, OsuAssembly); // Fix troubles when NameMapper can't find name due to unsimilar opcodes.

            _nameStorage = InitializeNameMapper();

            _config = new RConfig();

            exp = new ModuleExplorer(_obfOsuModule, _nameStorage);

            if (_config.Profiles == string.Empty)
            {
                // hacky thing because cant read json array when we have only one object so we add two objects, maybe there's a way to do it in other way?
                var profiles = new List<Profile>();

                profiles.Add(new Profile()
                {
                    Name = "Default",
                    UniqueId = GetRandomUniqueString(),
                    UninstallID = Guid.NewGuid().ToString(),
                    Adapters = GetRandomMacAddress()
                });
                profiles.Add(new Profile()
                {
                    Name = "Default2",
                    UniqueId = GetRandomUniqueString(),
                    UninstallID = Guid.NewGuid().ToString(),
                    Adapters = GetRandomMacAddress()
                });

                _config.Profiles = JsonConvert.SerializeObject(profiles);
                _config.SelectedProfile = "Default";
            }
            Console.WriteLine("       --- Select Profile or use current one ---       \n");
            Console.WriteLine("--- 1) Select");
            Console.WriteLine("--- 2) Use current one");
            var key = Console.ReadKey(false).Key;
            Console.Clear();
            switch (key)
            {
                case ConsoleKey.D1:
                    var profiles = JsonConvert.DeserializeObject<List<Profile>>(_config.Profiles);
                    for (var i = 0; i < profiles.Count; i++)
                    {
                        Console.WriteLine($"{i + 1}) {profiles[i].Name}");
                    }
                    Console.WriteLine("Select one of them or press SPACE to generate new one.");
                    var _key = Console.ReadKey(false).Key;

                    // TODO: Add support for selecting profiles count more than 9
                    if (!(_key == ConsoleKey.Spacebar) && _key >= ConsoleKey.D1 && _key <= ConsoleKey.D9)
                    {
                        var number = Convert.ToInt32(_key.ToString().Replace("D", ""));
                        Console.Clear();
                        _config.SelectedProfile = profiles[number - 1].Name; 
                        Console.WriteLine("Selected Profile: " + _config.SelectedProfile);
                        Thread.Sleep(2000);
                        Console.Clear();
                    }
                    else if (_key == ConsoleKey.Spacebar)
                    {
                        Console.Clear();
                        Console.WriteLine("       --- Generation Process ---       ");
                        Console.Write("Please type name of profile: ");
                        var profileName = Console.ReadLine();
                        Console.WriteLine("Started generating profile.");
                        profiles.Add(new Profile()
                        {
                            Name = profileName,
                            UniqueId = GetRandomUniqueString(),
                            UninstallID = Guid.NewGuid().ToString(),
                            Adapters = GetRandomMacAddress()
                        });
                        _config.Profiles = JsonConvert.SerializeObject(profiles);
                        _config.SelectedProfile = profileName;
                        Console.Clear();
                        Console.WriteLine("Done!\n");
                        Console.WriteLine($"UniqueId: {_config.CurrentProfile.UniqueId}\nUninstallID: {_config.CurrentProfile.UninstallID}\nMAC Address: {_config.CurrentProfile.Adapters}\n");
                        Thread.Sleep(2000);
                        Console.Clear();
                    }
                    break;
                case ConsoleKey.D2:
                    break;
            }
            Patch();
        }
        private static void Patch()
        {
            var OsuMain = OsuAssembly.GetType(exp["osu.OsuMain"].Type.Name);
            var pWebRequest = OsuAssembly.GetType(exp["osu_common.Helpers.pWebRequest"].Type.Name);
            var GameBase = OsuAssembly.GetType(exp["osu.GameBase"].Type.Name);
            var BanchoClient = OsuAssembly.GetType(exp["osu.Online.BanchoClient"].Type.Name);
            var ClientHash = GameBase.GetField(exp["osu.GameBase"].FindField("ClientHash").Name, BindingFlags.NonPublic | BindingFlags.Static);
            var OsuMain_FullPath = OsuMain.GetMethod(exp["osu.OsuMain"]["get_FullPath"].Method.Name, BindingFlags.Static | BindingFlags.NonPublic);
            var OsuMain_FullPath_patched = typeof(Patches).GetMethod("FullPath");

            var OsuMain_Filename = OsuMain.GetMethod(exp["osu.OsuMain"]["get_Filename"].Method.Name, BindingFlags.Static | BindingFlags.NonPublic);
            var OsuMain_Filename_patched = typeof(Patches).GetMethod("Filename");

            var pWebRequest_checkCertificate = pWebRequest.GetMethod(exp["osu_common.Helpers.pWebRequest"]["checkCertificate"].Method.Name, BindingFlags.Instance | BindingFlags.NonPublic);
            var pWebRequest_checkCertificate_patched = typeof(Patches).GetMethod("checkCertificate");

            var BanchoClient_InitializePrivate = BanchoClient.GetMethod(exp["osu.Online.BanchoClient"]["initializePrivate"].Method.Name, BindingFlags.Static | BindingFlags.NonPublic);
            var BanchoClient_InitializePrivate_patched = typeof(Patches).GetMethod("initializePrivate");

            // copied from osu.Launcher, because lazy to write it myself
            unsafe
            {
                int* p_OsuMain_FullPath = (int*)OsuMain_FullPath.MethodHandle.Value.ToPointer() + 2;
                int* p_OsuMain_FullPath_patched = (int*)OsuMain_FullPath_patched.MethodHandle.Value.ToPointer() + 2;

                int* p_OsuMain_Filename = (int*)OsuMain_Filename.MethodHandle.Value.ToPointer() + 2;
                int* p_OsuMain_Filename_patched = (int*)OsuMain_Filename_patched.MethodHandle.Value.ToPointer() + 2;

                *p_OsuMain_FullPath = *p_OsuMain_FullPath_patched;
                *p_OsuMain_Filename = *p_OsuMain_Filename_patched;

                int* p_pWebRequest_checkCertificate = (int*)pWebRequest_checkCertificate.MethodHandle.Value.ToPointer() + 2;
                int* p_pWebRequest_checkCertificate_patched = (int*)pWebRequest_checkCertificate_patched.MethodHandle.Value.ToPointer() + 2;

                *p_pWebRequest_checkCertificate = *p_pWebRequest_checkCertificate_patched;

                int* p_BanchoClient_InitializePrivate = (int*)BanchoClient_InitializePrivate.MethodHandle.Value.ToPointer() + 2;
                int* p_BanchoClient_InitializePrivate_patched = (int*)BanchoClient_InitializePrivate_patched.MethodHandle.Value.ToPointer() + 2;

                *p_BanchoClient_InitializePrivate = *p_BanchoClient_InitializePrivate_patched;
            }
            ClientHash.SetValue(null, MD5Helper.ComputeFile(OsuPath) + @":" + _config.CurrentProfile.Adapters + @":" + MD5Helper.ComputeString(_config.CurrentProfile.Adapters) + ":" + MD5Helper.ComputeString(_config.CurrentProfile.UniqueId) + @":" + MD5Helper.ComputeString(_config.CurrentProfile.UninstallID) + @":");

            new ReflectionPermission(ReflectionPermissionFlag.RestrictedMemberAccess).Assert();
            OsuAssembly.EntryPoint.Invoke(null, null);    
        }

        // https://stackoverflow.com/a/19696105
        public static string GetRandomMacAddress()
        {
            string result = string.Empty;
            var _random = new Random();
            using (var rng = new RNGCryptoServiceProvider())
            {
                for (int i = 0; i < _random.Next(3, 4); i++)
                {
                    var random = new Random();
                    var buffer = new byte[6];
                    rng.GetBytes(buffer);
                    result += string.Concat(buffer.Select(x => string.Format("{0}", x.ToString("X2"))).ToArray()) + ".";
                }
            }
            return result;
        }

        // somewhere on stackoverflow too
        public static string GetRandomUniqueString()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                var byte_count = (36 + 7) / 8; // rounded up
                var bytes = new byte[byte_count];
                rng.GetBytes(bytes);
                var result = string.Concat(bytes.Select(x => string.Format("{0}", x.ToString("X2"))).ToArray());
                return result;
            }
        }

        private static INameProvider InitializeNameMapper()
        {
            var dictFile = Path.Combine(_cachePath, $"{_osuHash}.dic");

            if (File.Exists(dictFile))
            {
                return SimpleNameProvider.Initialize(dictFile);
            }

            MapperNameProvider.Initialize(_cleanOsuModule, _obfOsuModule);

            File.WriteAllBytes(dictFile, MapperNameProvider.Instance.Pack());

            return MapperNameProvider.Instance;
        }
    }
}
