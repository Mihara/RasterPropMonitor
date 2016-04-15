using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace JSIAdvTransparentPods
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class LoadGlobals : MonoBehaviour
    {
        public static LoadGlobals Instance;
        internal static Settings settings;
        private string globalConfigFilename;
        private ConfigNode globalNode = new ConfigNode();

        //Awake Event - when the DLL is loaded
        public void Awake()
        {
            if (Instance != null)
                return;
            Instance = this;
            DontDestroyOnLoad(this);
            settings = new Settings();
            globalConfigFilename = Path.Combine(_AssemblyFolder, "Config.cfg").Replace("\\", "/");
            JSIAdvTPodsUtil.Log("globalConfigFilename = " + globalConfigFilename);
            if (!File.Exists(globalConfigFilename))
            {
                settings.Save(globalNode);
                globalNode.Save(globalConfigFilename);
            }
            globalNode = ConfigNode.Load(globalConfigFilename);
            settings.Load(globalNode);
            JSIAdvTPodsUtil.debugLoggingEnabled = settings.DebugLogging;
            JSIAdvTPodsUtil.Log("JSIAdvTransparentPods LoadGlobals Awake Complete");
        }

        public void Start()
        {
            //GameEvents.onGameSceneSwitchRequested.Add(onGameSceneSwitchRequested);
        }

        public void OnDestroy()
        {
            //GameEvents.onGameSceneSwitchRequested.Remove(onGameSceneSwitchRequested);
        }

        #region Assembly/Class Information

        /// <summary>
        /// Name of the Assembly that is running this MonoBehaviour
        /// </summary>
        internal static String _AssemblyName
        { get { return Assembly.GetExecutingAssembly().GetName().Name; } }

        /// <summary>
        /// Full Path of the executing Assembly
        /// </summary>
        internal static String _AssemblyLocation
        { get { return Assembly.GetExecutingAssembly().Location; } }

        /// <summary>
        /// Folder containing the executing Assembly
        /// </summary>
        internal static String _AssemblyFolder
        { get { return Path.GetDirectoryName(_AssemblyLocation); } }

        #endregion Assembly/Class Information
    }

    internal class Settings
    {
        // this class stores the DeepFreeze Settings from the config file.
        private const string configNodeName = "JSIAdvTransparentPodsSettings";

        internal bool DebugLogging { get; set; }
        internal bool LoadedInactive { get; set; }



        internal Settings()
        {
            DebugLogging = false;
        }

        //Settings Functions Follow

        internal void Load(ConfigNode node)
        {
            if (node.HasNode(configNodeName))
            {
                ConfigNode settingsNode = node.GetNode(configNodeName);
                DebugLogging = GetNodeValue(settingsNode, "DebugLogging", DebugLogging);
                LoadedInactive = GetNodeValue(settingsNode, "LoadedInactive", LoadedInactive);

            }
        }

        internal void Save(ConfigNode node)
        {
            ConfigNode settingsNode;
            if (node.HasNode(configNodeName))
            {
                settingsNode = node.GetNode(configNodeName);
                settingsNode.ClearData();
            }
            else
            {
                settingsNode = node.AddNode(configNodeName);
            }
            
            settingsNode.AddValue("DebugLogging", DebugLogging);
            settingsNode.AddValue("LoadedInactive", LoadedInactive);
        }

        internal bool GetNodeValue(ConfigNode confignode, string fieldname, bool defaultValue)
        {
            bool newValue;
            if (confignode.HasValue(fieldname) && Boolean.TryParse(confignode.GetValue(fieldname), out newValue))
            {
                return newValue;
            }
            return defaultValue;
        }
    }
}
