/*****************************************************************************
 * RasterPropMonitor
 * =================
 * Plugin for Kerbal Space Program
 *
 *  by Mihara (Eugene Medvedev), MOARdV, and other contributors
 * 
 * RasterPropMonitor is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, revision
 * date 29 June 2007, or (at your option) any later version.
 * 
 * RasterPropMonitor is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License
 * for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with RasterPropMonitor.  If not, see <http://www.gnu.org/licenses/>.
 ****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace JSI
{
    public class MonitorPage
    {
        // We still need a numeric ID cause it makes persistence easier.
        public readonly int pageNumber;
        public readonly string name = string.Empty;
        public readonly bool unlocker;
        private readonly string text;
        private StringProcessorFormatter[] spf;
        private string processedText = string.Empty;

        public string Text
        {
            get
            {
                return processedText;
            }
        }

        private bool isActive;
        public bool IsActive
        {
            get
            {
                return isActive;
            }
        }

        public readonly string textOverlay = string.Empty;
        public bool isDefault;
        // A page is immutable if and only if it has only unchanging text and unchanging background and no handlers.
        public bool isMutable;

        private enum BackgroundType
        {
            None,
            Texture,
            Handler,
        };

        public readonly int screenXMin, screenYMin;
        public readonly int pageFont = 0;
        private readonly Texture2D overlayTexture, interlayTexture;
        public readonly Color defaultColor;
        private readonly BackgroundType background = BackgroundType.None;
        private readonly Texture2D backgroundTexture;
        private readonly Func<int, int, string> pageHandlerMethod;
        private readonly Func<RenderTexture, float, bool> backgroundHandlerMethod;
        private readonly HandlerSupportMethods pageHandlerS, backgroundHandlerS;
        private readonly RasterPropMonitor ourMonitor;
        private readonly int screenWidth, screenHeight;
        private readonly float cameraAspect;
        private readonly bool showNoSignal;
        private readonly bool simpleLockingPage;
        private readonly List<string> disableSwitchingTo = new List<string>();
        private readonly DefaultableDictionary<string, string> redirectPages = new DefaultableDictionary<string, string>(string.Empty);
        private readonly DefaultableDictionary<int, int?> redirectGlobals = new DefaultableDictionary<int, int?>(null);
        private readonly MonoBehaviour backgroundHandlerModule, pageHandlerModule;
        private readonly List<string> techsRequired = new List<string>();
        private readonly string fallbackPageName = string.Empty;


        private struct HandlerSupportMethods
        {
            public Action<bool, int> activate;
            public Action<int> buttonClick;
            public Action<int> buttonRelease;
            public Action<MonoBehaviour, MonoBehaviour> getHandlerReferences;
        }

        public void UpdateText(RasterPropMonitorComputer rpmComp)
        {
            // If there's a handler references method, it gets called before each text call.
            if (pageHandlerS.getHandlerReferences != null)
            {
                pageHandlerS.getHandlerReferences(pageHandlerModule, backgroundHandlerModule);
            }

            if (pageHandlerMethod != null)
            {
                processedText = pageHandlerMethod(screenWidth, screenHeight);

                if (processedText.IndexOf("$&$", StringComparison.Ordinal) != -1)
                {
                    // There are processed variables in here?
                    StringBuilder bf = new StringBuilder();
                    string[] linesArray = processedText.Split(JUtil.LineSeparator, StringSplitOptions.None);
                    for (int i = 0; i < linesArray.Length; i++)
                    {
                        bf.AppendLine(StringProcessor.ProcessString(linesArray[i], rpmComp));
                    }
                    processedText = bf.ToString();
                }
            }
            else
            {
                if (isMutable)
                {
                    if (spf == null)
                    {
                        string[] linesArray = text.Split(JUtil.LineSeparator, StringSplitOptions.None);
                        spf = new StringProcessorFormatter[linesArray.Length];
                        for (int i = 0; i < linesArray.Length; ++i)
                        {
                            spf[i] = new StringProcessorFormatter(linesArray[i], rpmComp);
                        }
                    }

                    StringBuilder bf = new StringBuilder();
                    for (int i = 0; i < spf.Length; i++)
                    {
                        bf.AppendLine(StringProcessor.ProcessString(spf[i], rpmComp));
                    }

                    processedText = bf.ToString();
                }
            }

        }

        public bool SwitchingPermitted(string destination)
        {
            if (string.IsNullOrEmpty(destination))
            {
                return false;
            }
            return !simpleLockingPage && !disableSwitchingTo.Contains(destination);
        }

        public string ContextRedirect(string destination)
        {
            foreach (string techName in techsRequired)
            {
                if (ResearchAndDevelopment.GetTechnologyState(techName) == RDTech.State.Unavailable)
                {
                    return fallbackPageName;
                }
            }

            return redirectPages[destination];
        }

        private static bool IsValidPageName(string thatName)
        {
            char[] illegalChars = { ' ', ',', '#', '=' };
            foreach (char thatChar in illegalChars)
            {
                if (thatName.IndexOf(thatChar) != -1)
                {
                    return false;
                }
            }
            return true;
        }

        public MonitorPage(int idNum, ConfigNode node, RasterPropMonitor thatMonitor)
        {
            ourMonitor = thatMonitor;
            screenWidth = ourMonitor.screenWidth;
            screenHeight = ourMonitor.screenHeight;
            cameraAspect = ourMonitor.cameraAspect;
            defaultColor = ourMonitor.defaultFontTintValue;
            screenXMin = 0;
            screenYMin = 0;

            pageNumber = idNum;
            isMutable = false;
            if (!node.HasData)
            {
                throw new ArgumentException("Empty page?");
            }

            if (node.HasValue("name"))
            {
                string value = node.GetValue("name").Trim();
                if (!IsValidPageName(value))
                {
                    JUtil.LogMessage(ourMonitor, "Warning, name given for page #{0} is invalid, ignoring.", pageNumber);
                }
                else
                {
                    name = value;
                }
            }
            else
            {
                JUtil.LogMessage(ourMonitor, "Warning, page #{0} has no name. It's much better if it does.", pageNumber);
            }

            isDefault |= node.HasValue("default");

            if (node.HasValue("button"))
            {
                SmarterButton.CreateButton(thatMonitor.internalProp, node.GetValue("button"), this, thatMonitor.PageButtonClick);
            }

            // Page locking system -- simple locking:
            simpleLockingPage |= node.HasValue("lockingPage");
            // and name-based locking.
            if (node.HasValue("disableSwitchingTo"))
            {
                string[] tokens = node.GetValue("disableSwitchingTo").Split(',');
                foreach (string token in tokens)
                {
                    disableSwitchingTo.Add(token.Trim());
                }
            }

            unlocker |= node.HasValue("unlockerPage");

            if (node.HasValue("localMargins"))
            {
                Vector4 margindata = ConfigNode.ParseVector4(node.GetValue("localMargins"));
                screenXMin = (int)margindata.x;
                screenYMin = (int)margindata.y;
                screenWidth = screenWidth - (int)margindata.z - screenXMin;
                screenHeight = screenHeight - (int)margindata.w - screenYMin;
            }

            pageFont = node.GetInt("defaultFontNumber") ?? 0;

            if (node.HasValue("defaultFontTint"))
            {
                defaultColor = ConfigNode.ParseColor32(node.GetValue("defaultFontTint"));
            }

            if (node.HasValue("techsRequired"))
            {
                techsRequired = node.GetValue("techsRequired").Split(new[] { ' ', ',', ';', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }

            if (node.HasValue("fallbackOnNoTech"))
            {
                fallbackPageName = node.GetValue("fallbackOnNoTech").Trim();
            }

            if (node.HasNode("CONTEXTREDIRECT"))
            {
                ConfigNode[] redirectnodes = node.GetNodes("CONTEXTREDIRECT");
                for (int i = 0; i < redirectnodes.Length; ++i)
                {
                    string[] redirects = redirectnodes[i].GetValues("redirect");
                    for (int j = 0; j < redirects.Length; ++j)
                    {
                        string[] tokens = redirects[j].Split(',');
                        if (tokens.Length > 2 || !IsValidPageName(tokens[0].Trim()) || !IsValidPageName(tokens[1].Trim()))
                        {
                            JUtil.LogMessage(ourMonitor, "Warning, invalid page redirect statement on page #{0}.", pageNumber);
                            continue;
                        }
                        redirectPages[tokens[0].Trim()] = tokens[1].Trim();
                    }

                    string[] renumbers = redirectnodes[i].GetValues("renumber");
                    for (int j = 0; j < renumbers.Length; ++j)
                    {
                        string[] tokens = renumbers[j].Split(',');
                        if (tokens.Length > 2)
                        {
                            JUtil.LogMessage(ourMonitor, "Warning, invalid global button redirect statement on page #{0}: requires two arguments.", pageNumber);
                            continue;
                        }
                        int from, to;
                        if (!int.TryParse(tokens[0], out from) || !int.TryParse(tokens[1], out to))
                        {
                            JUtil.LogMessage(ourMonitor, "Warning, invalid global button redirect statement on page #{0}: something isn't a number", pageNumber);
                            continue;
                        }
                        redirectGlobals[from] = to;
                    }
                }

                JUtil.LogMessage(this, "Page '{2}' (#{0}) registers {1} page redirects and {3} global button redirects.", idNum, redirectPages.Count, name, redirectGlobals.Count);
            }

            foreach (ConfigNode handlerNode in node.GetNodes("PAGEHANDLER"))
            {
                MonoBehaviour handlerModule;
                HandlerSupportMethods supportMethods;
                MethodInfo handlerMethod = InstantiateHandler(handlerNode, ourMonitor, out handlerModule, out supportMethods);
                if (handlerMethod != null && handlerModule != null)
                {
                    try
                    {
                        pageHandlerMethod = (Func<int, int, string>)Delegate.CreateDelegate(typeof(Func<int, int, string>), handlerModule, handlerMethod);
                    }
                    catch
                    {
                        JUtil.LogErrorMessage(ourMonitor, "Incorrect signature for the page handler method {0}", handlerModule.name);
                        break;
                    }
                    pageHandlerS = supportMethods;
                    isMutable = true;
                    pageHandlerModule = handlerModule;
                    break;
                }
            }

            if (pageHandlerMethod == null)
            {
                if (node.HasValue("text"))
                {
                    text = JUtil.LoadPageDefinition(node.GetValue("text"));
                    isMutable |= text.IndexOf("$&$", StringComparison.Ordinal) != -1;
                    if (!isMutable)
                    {
                        processedText = text;
                    }
                }
                else
                {
                    text = string.Empty;
                }
            }

            if (node.HasValue("textOverlay"))
            {
                textOverlay = JUtil.LoadPageDefinition(node.GetValue("textOverlay"));
            }

            foreach (ConfigNode handlerNode in node.GetNodes("BACKGROUNDHANDLER"))
            {
                MonoBehaviour handlerModule;
                HandlerSupportMethods supportMethods;
                MethodInfo handlerMethod = InstantiateHandler(handlerNode, ourMonitor, out handlerModule, out supportMethods);
                if (handlerMethod != null && handlerModule != null)
                {
                    try
                    {
                        backgroundHandlerMethod = (Func<RenderTexture, float, bool>)Delegate.CreateDelegate(typeof(Func<RenderTexture, float, bool>), handlerModule, handlerMethod);
                    }
                    catch
                    {
                        JUtil.LogErrorMessage(ourMonitor, "Incorrect signature for the background handler method {0}", handlerModule.name);
                        break;
                    }
                    backgroundHandlerS = supportMethods;
                    isMutable = true;
                    showNoSignal = node.HasValue("showNoSignal");
                    background = BackgroundType.Handler;
                    backgroundHandlerModule = handlerModule;
                    break;
                }
            }

            if (background == BackgroundType.None)
            {
                if (node.HasValue("textureURL"))
                {
                    string textureURL = node.GetValue("textureURL").EnforceSlashes();
                    if (GameDatabase.Instance.ExistsTexture(textureURL))
                    {
                        backgroundTexture = GameDatabase.Instance.GetTexture(textureURL, false);
                        background = BackgroundType.Texture;
                    }
                }
            }
            if (node.HasValue("textureInterlayURL"))
            {
                string textureURL = node.GetValue("textureInterlayURL").EnforceSlashes();
                if (GameDatabase.Instance.ExistsTexture(textureURL))
                {
                    interlayTexture = GameDatabase.Instance.GetTexture(textureURL, false);
                }
                else
                {
                    JUtil.LogErrorMessage(ourMonitor, "Interlay texture {0} could not be loaded.", textureURL);
                }
            }
            if (node.HasValue("textureOverlayURL"))
            {
                string textureURL = node.GetValue("textureOverlayURL").EnforceSlashes();
                if (GameDatabase.Instance.ExistsTexture(textureURL))
                {
                    overlayTexture = GameDatabase.Instance.GetTexture(textureURL, false);
                }
                else
                {
                    JUtil.LogErrorMessage(ourMonitor, "Overlay texture {0} could not be loaded.", textureURL);
                }
            }

        }

        private static MethodInfo InstantiateHandler(ConfigNode node, RasterPropMonitor ourMonitor, out MonoBehaviour moduleInstance, out HandlerSupportMethods support)
        {
            moduleInstance = null;
            support.activate = null;
            support.buttonClick = null;
            support.buttonRelease = null;
            support.getHandlerReferences = null;
            if (node.HasValue("name") && node.HasValue("method"))
            {
                string moduleName = node.GetValue("name");
                string methodName = node.GetValue("method");

                var handlerConfiguration = new ConfigNode("MODULE");
                node.CopyTo(handlerConfiguration);

                MonoBehaviour thatModule = null;
                // Part modules are different in that they remain instantiated when you switch vessels, while the IVA doesn't.
                // Because of this RPM can't instantiate partmodule-based handlers itself -- there's no way to tell if this was done already or not.
                // Which means there can only be one instance of such a handler per pod, and it can't receive configuration values from RPM.
                if (node.HasValue("isPartModule"))
                {
                    foreach (PartModule potentialModule in ourMonitor.part.Modules)
                    {
                        if (potentialModule.ClassName == moduleName)
                        {
                            thatModule = potentialModule;
                            break;
                        }
                    }
                }
                else if (node.HasValue("multiHandler"))
                {

                    foreach (InternalModule potentialModule in ourMonitor.internalProp.internalModules)
                    {
                        if (potentialModule.ClassName == moduleName)
                        {
                            thatModule = potentialModule;
                            break;
                        }
                    }

                }

                if (thatModule == null && !node.HasValue("isPartModule"))
                {
                    try
                    {
                        thatModule = ourMonitor.internalProp.AddModule(handlerConfiguration);
                    }
                    catch
                    {
                        JUtil.LogErrorMessage(ourMonitor, "Caught exception when trying to instantiate module '{0}'. Something's fishy here", moduleName);
                    }
                    if (thatModule != null)
                    {
                        try
                        {
                            MethodInfo configureMethod = thatModule.GetType().GetMethod("Configure", BindingFlags.Instance | BindingFlags.Public);
                            ParameterInfo[] parms = configureMethod.GetParameters();

                            if (parms.Length == 1 && parms[0].ParameterType == typeof(ConfigNode))
                            {
                                configureMethod.Invoke(thatModule, new object[] { handlerConfiguration });
                            }
                        }
                        catch//(Exception e)
                        {
                            //JUtil.LogMessage(ourMonitor, "Exception {0}", e);
                            //JUtil.LogMessage(ourMonitor, "Module didn't have a Configure method.  This could be normal.");
                        }
                    }
                }

                if (thatModule == null)
                {
                    JUtil.LogMessage(ourMonitor, "Warning, handler module \"{0}\" could not be loaded. This could be perfectly normal.", moduleName);
                    return null;
                }

                const string sigError = "Incorrect signature of the {0} method in {1}, ignoring option. If it doesn't work later, that's why.";

                if (node.HasValue("pageActiveMethod"))
                {
                    foreach (MethodInfo m in thatModule.GetType().GetMethods())
                    {
                        if (m.Name == node.GetValue("pageActiveMethod"))
                        {
                            try
                            {
                                support.activate = (Action<bool, int>)Delegate.CreateDelegate(typeof(Action<bool, int>), thatModule, m);
                            }
                            catch
                            {
                                JUtil.LogErrorMessage(ourMonitor, sigError, "page activation", moduleName);
                            }
                            break;
                        }
                    }
                }

                if (node.HasValue("buttonClickMethod"))
                {
                    foreach (MethodInfo m in thatModule.GetType().GetMethods())
                    {
                        if (m.Name == node.GetValue("buttonClickMethod"))
                        {
                            try
                            {
                                support.buttonClick = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), thatModule, m);
                            }
                            catch
                            {
                                JUtil.LogErrorMessage(ourMonitor, sigError, "button click", moduleName);
                            }
                            break;
                        }
                    }
                }

                if (node.HasValue("buttonReleaseMethod"))
                {
                    foreach (MethodInfo m in thatModule.GetType().GetMethods())
                    {
                        if (m.Name == node.GetValue("buttonReleaseMethod"))
                        {
                            try
                            {
                                support.buttonRelease = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), thatModule, m);
                            }
                            catch
                            {
                                JUtil.LogErrorMessage(ourMonitor, sigError, "button release", moduleName);
                            }
                            break;
                        }
                    }
                }

                if (node.HasValue("getHandlerReferencesMethod"))
                {
                    foreach (MethodInfo m in thatModule.GetType().GetMethods())
                    {
                        if (m.Name == node.GetValue("getHandlerReferencesMethod"))
                        {
                            try
                            {
                                support.getHandlerReferences = (Action<MonoBehaviour, MonoBehaviour>)Delegate.CreateDelegate(typeof(Action<MonoBehaviour, MonoBehaviour>), thatModule, m);
                            }
                            catch
                            {
                                JUtil.LogErrorMessage(ourMonitor, sigError, "handler references", moduleName);
                            }
                            break;
                        }
                    }
                }

                moduleInstance = thatModule;
                foreach (MethodInfo m in thatModule.GetType().GetMethods())
                {
                    if (m.Name == methodName)
                    {
                        return m;
                    }
                }

            }
            return null;
        }

        public void Active(bool state)
        {
            isActive = state;
            if (pageHandlerS.activate != null)
            {
                pageHandlerS.activate(state, pageNumber);
            }
            if (backgroundHandlerS.activate != null && backgroundHandlerS.activate != pageHandlerS.activate)
            {
                backgroundHandlerS.activate(state, pageNumber);
            }
        }

        public bool GlobalButtonClick(int buttonID)
        {
            buttonID = redirectGlobals[buttonID] ?? buttonID;
            if (buttonID == -1)
            {
                return false;
            }
            bool actionTaken = false;
            if (pageHandlerS.buttonClick != null)
            {
                pageHandlerS.buttonClick(buttonID);
                actionTaken = true;
            }
            if (backgroundHandlerS.buttonClick != null && pageHandlerS.buttonClick != backgroundHandlerS.buttonClick)
            {
                backgroundHandlerS.buttonClick(buttonID);
                actionTaken = true;
            }
            return actionTaken;
        }

        public bool GlobalButtonRelease(int buttonID)
        {
            buttonID = redirectGlobals[buttonID] ?? buttonID;
            if (buttonID == -1)
            {
                return false;
            }

            bool actionTaken = false;
            if (pageHandlerS.buttonRelease != null)
            {
                pageHandlerS.buttonRelease(buttonID);
                actionTaken = true;
            }
            if (backgroundHandlerS.buttonRelease != null && backgroundHandlerS.buttonRelease != pageHandlerS.buttonRelease)
            {
                actionTaken = true;
                backgroundHandlerS.buttonRelease(buttonID);
            }
            return actionTaken;
        }

        public void RenderBackground(RenderTexture screen)
        {
            switch (background)
            {
                case BackgroundType.None:
                    GL.Clear(true, true, ourMonitor.emptyColorValue);
                    break;
                case BackgroundType.Texture:
                    //call clear before redraw of textures
                    GL.Clear(true, true, ourMonitor.emptyColorValue);
                    Graphics.DrawTexture(new Rect(0, 0, screen.width, screen.height), backgroundTexture);
                    break;
                case BackgroundType.Handler:
                    //No clear here as it would interfere with the handlers(Causing effects such as VesselViewer to blink)
                    // If there's a handler references method, it gets called before each render.
                    if (backgroundHandlerS.getHandlerReferences != null)
                    {
                        backgroundHandlerS.getHandlerReferences(pageHandlerModule, backgroundHandlerModule);
                    }

                    if (!backgroundHandlerMethod(screen, cameraAspect))
                    {
                        if (ourMonitor.noSignalTexture != null && showNoSignal)
                        {
                            Graphics.DrawTexture(new Rect(0, 0, screen.width, screen.height), ourMonitor.noSignalTexture);
                        }
                        else
                        {
                            GL.Clear(true, true, ourMonitor.emptyColorValue);
                        }
                    }
                    break;
            }
            // If the handlers aren't missing their popmatrix, it should be alright.
            if (interlayTexture != null)
            {
                Graphics.DrawTexture(new Rect(0, 0, screen.width, screen.height), interlayTexture);
            }
        }

        public void RenderOverlay(RenderTexture screen)
        {
            if (overlayTexture != null)
            {
                Graphics.DrawTexture(new Rect(0, 0, screen.width, screen.height), overlayTexture);
            }
        }
    }
}
