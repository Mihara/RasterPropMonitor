//#define SHOW_FIXEDUPDATE_TIMING
//#define SHOW_DOCKING_EVENTS
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
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using KSP.UI.Screens.Flight;

// MOARdV TODO:
// Add callbacks for docking, undocking, staging, vessel switching
// + GameEvents.onUndock
// ? GameEvents.onSameVesselDock
// ? GameEvents.onSameVesselUndock
// ? GameEvents.onStageSeparation
//
// ? GameEvents.onCrewOnEva
// ? GameEvents.onCrewTransferred
// ? GameEvents.onKerbalAdded
// ? GameEvents.onKerbalRemoved
namespace JSI
{
    public partial class RPMVesselComputer : VesselModule
    {
        #region Static Variables
        /*
         * This region contains static variables - variables that only need to
         * exist in a single instance.  They are instantiated by the first
         * vessel to enter flight, and released by the last vessel before a
         * scene change.
         */
        private static Dictionary<Guid, RPMVesselComputer> instances;

        internal static readonly int gearGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.Gear);
        internal static readonly int brakeGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.Brakes);
        internal static readonly int sasGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.SAS);
        internal static readonly int lightGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.Light);
        internal static readonly int rcsGroupNumber = BaseAction.GetGroupIndex(KSPActionGroup.RCS);
        internal static readonly int[] actionGroupID = {
            BaseAction.GetGroupIndex(KSPActionGroup.Custom10),
            BaseAction.GetGroupIndex(KSPActionGroup.Custom01),
            BaseAction.GetGroupIndex(KSPActionGroup.Custom02),
            BaseAction.GetGroupIndex(KSPActionGroup.Custom03),
            BaseAction.GetGroupIndex(KSPActionGroup.Custom04),
            BaseAction.GetGroupIndex(KSPActionGroup.Custom05),
            BaseAction.GetGroupIndex(KSPActionGroup.Custom06),
            BaseAction.GetGroupIndex(KSPActionGroup.Custom07),
            BaseAction.GetGroupIndex(KSPActionGroup.Custom08),
            BaseAction.GetGroupIndex(KSPActionGroup.Custom09)
        };
        internal static readonly string[] actionGroupMemo = {
            "AG0",
            "AG1",
            "AG2",
            "AG3",
            "AG4",
            "AG5",
            "AG6",
            "AG7",
            "AG8",
            "AG9"
        };
        private const float gee = 9.81f;
        private readonly double upperAtmosphereLimit = Math.Log(100000.0);
        #endregion

        #region Instance Variables
        /*
         * This region contains variables that apply per-instance (per vessel).
         */
        private Vessel vessel;
        private Guid vid;
        internal Vessel getVessel() { return vessel; }
        internal Guid id
        {
            get
            {
                return (vessel == null) ? Guid.Empty : vessel.id;
            }
        }
        private NavBall navBall;
        internal LinearAtmosphereGauge linearAtmosGauge;
        private Part part;
        internal Part CurrentIVAPart
        {
            // Return the part that RPMVesselComputer considers the reference
            // part (the part we're "in" during IVA).
            get
            {
                return part;
            }
        }

        // Data refresh
        private int dataUpdateCountdown;
        private int refreshDataRate = 60;
        private bool timeToUpdate = false;

        // Craft-relative basis vectors
        internal Vector3 forward;
        public Vector3 Forward
        {
            get
            {
                return forward;
            }
        }
        internal Vector3 right;
        //public Vector3 Right
        //{
        //    get
        //    {
        //        return right;
        //    }
        //}
        internal Vector3 top;
        //public Vector3 Top
        //{
        //    get
        //    {
        //        return top;
        //    }
        //}

        // Orbit-relative vectors
        internal Vector3 prograde;
        public Vector3 Prograde
        {
            get
            {
                return prograde;
            }
        }
        internal Vector3 radialOut;
        public Vector3 RadialOut
        {
            get
            {
                return radialOut;
            }
        }
        internal Vector3 normalPlus;
        public Vector3 NormalPlus
        {
            get
            {
                return normalPlus;
            }
        }

        // Surface-relative vectors
        internal Vector3 up;
        public Vector3 Up
        {
            get
            {
                return up;
            }
        }
        // surfaceRight is the projection of the right vector onto the surface.
        // If up x right is a degenerate vector (rolled on the side), we use
        // the forward vector to compose a new basis
        private Vector3 surfaceRight;
        public Vector3 SurfaceRight
        {
            get
            {
                return surfaceRight;
            }
        }
        // surfaceForward is the cross of the up vector and right vector, so
        // that surface velocity can be decomposed to surface-relative components.
        private Vector3 surfaceForward;
        public Vector3 SurfaceForward
        {
            get
            {
                return surfaceForward;
            }
        }

        internal Quaternion rotationVesselSurface;
        public Quaternion RotationVesselSurface
        {
            get
            {
                return rotationVesselSurface;
            }
        }

        // Tracked vessel variables
        internal float actualAverageIsp;
        internal float actualMaxIsp;
        internal double altitudeASL;
        internal double altitudeBottom;
        internal double altitudeTrue;
        internal bool anyEnginesFlameout;
        internal bool anyEnginesOverheating;
        internal Vector3d CoM;
        internal float heatShieldTemperature;
        internal float heatShieldFlux;
        internal float hottestPartTemperature;
        internal float hottestPartMaxTemperature;
        internal string hottestPartName;
        internal float hottestEngineTemperature;
        internal float hottestEngineMaxTemperature;
        internal float localGeeASL;
        internal float localGeeDirect;
        private bool orbitSensibility;
        internal ResourceDataStorage resources = new ResourceDataStorage();
        internal float slopeAngle;
        internal double speedHorizontal;
        internal double speedVertical;
        internal double speedVerticalRounded;
        internal float totalCurrentThrust;
        internal float totalDataAmount;
        internal float totalExperimentCount;
        internal float totalLimitedMaximumThrust;
        internal float totalRawMaximumThrust;
        internal float totalShipDryMass;
        internal float totalShipWetMass;
        internal float maxEngineFuelFlow;
        internal float currentEngineFuelFlow;

        internal List<ProtoCrewMember> vesselCrew = new List<ProtoCrewMember>();
        internal List<kerbalExpressionSystem> vesselCrewMedical = new List<kerbalExpressionSystem>();

        private double lastAltitudeBottomSampleTime;
        private double lastAltitudeBottom;
        internal double terrainDelta;
        // radarAltitudeRate as computed using a simple exponential smoothing.
        internal float radarAltitudeRate = 0.0f;
        private double lastRadarAltitudeTime;

        // Target values
        internal ITargetable target;
        internal CelestialBody targetBody;
        internal ModuleDockingNode targetDockingNode;
        internal Vessel targetVessel;
        internal Orbit targetOrbit;
        internal bool targetOrbitSensibility;
        internal double targetDistance;
        internal Vector3d targetSeparation;
        public Vector3d TargetSeparation
        {
            get
            {
                return targetSeparation;
            }
        }
        internal Vector3d velocityRelativeTarget;
        internal float approachSpeed;
        private Quaternion targetOrientation;

        // Diagnostics
        private int debug_fixedUpdates = 0;
        private DefaultableDictionary<string, int> debug_callCount = new DefaultableDictionary<string, int>(0);
#if SHOW_FIXEDUPDATE_TIMING
        private Stopwatch stopwatch = new Stopwatch();
#endif
        #endregion

        /// <summary>
        /// Attempt to get a vessel computer from the instances dictionary.
        /// For this case, do not fail if it is not found.
        /// </summary>
        /// <param name="v">Vessel for which we want an instance</param>
        /// <param name="comp">[out] The RPMVesselComputer, untouched if this method returns false.</param>
        /// <returns>true if the vessel has a computer, false otherwise</returns>
        public static bool TryGetInstance(Vessel v, ref RPMVesselComputer comp)
        {
            if (instances != null && v != null)
            {
                if (instances.ContainsKey(v.id))
                {
                    comp = instances[v.id];
                    return (comp != null);
                }
            }

            return false;
        }

        /// <summary>
        /// Attempt to get a vessel computer based on the vessel's Guid.
        /// </summary>
        /// <param name="vid">The Guid of the vessel we want</param>
        /// <param name="comp">[out] The RPMVesselComputer, untouched if this method returns false.</param>
        /// <returns>true if the vessel has a computer, false otherwise</returns>
        public static bool TryGetInstance(Guid vid, ref RPMVesselComputer comp)
        {
            if (instances != null && vid != Guid.Empty)
            {
                if (instances.ContainsKey(vid))
                {
                    comp = instances[vid];
                    return (comp != null);
                }
            }

            return false;
        }

        /// <summary>
        /// Fetch the RPMVesselComputer corresponding to the vessel.  Assumes
        /// everything has been initialized.
        /// </summary>
        /// <param name="vid">The Guid of the Vessel we want</param>
        /// <returns>The vessel, or null if it's not in hte dictionary.</returns>
        public static RPMVesselComputer Instance(Guid vid)
        {
            if (instances != null && vid != Guid.Empty)
            {
                if (instances.ContainsKey(vid))
                {
                    return instances[vid];
                }
            }

            return null;
        }

        /// <summary>
        /// Fetch the RPMVesselComputer corresponding to the vessel.  Throws an
        /// exception if the instances dictionary is null or if the vessel
        /// does not have an RPMVesselComputer.
        /// </summary>
        /// <param name="v">The Vessel we want</param>
        /// <returns></returns>
        public static RPMVesselComputer Instance(Vessel v)
        {
            if (instances == null)
            {
                JUtil.LogErrorMessage(null, "RPMVesselComputer.Instance called with uninitialized instances.");
                throw new Exception("RPMVesselComputer.Instance called with uninitialized instances.");
            }

            if (!instances.ContainsKey(v.id))
            {
                JUtil.LogMessage(null, "RPMVesselComputer.Instance called with unrecognized vessel {0} ({1}).", v.vesselName, v.id);
                RPMVesselComputer comp = v.GetComponent<RPMVesselComputer>();
                if (comp == null)
                {
                    foreach (var val in instances.Keys)
                    {
                        JUtil.LogMessage(null, "Known Vessel {0}", val);
                    }

                    throw new Exception("RPMVesselComputer.Instance called with an unrecognized vessel, and I can't find one on the vessel.");
                }

                instances.Add(v.id, comp);
            }

            return instances[v.id];
        }

        private Kerbal lastActiveKerbal = null;
        /// <summary>
        /// Used to control what portion of a Kerbal is visible while "looking
        /// through its eyes".  This capability is managed in RPMVesselComputer
        /// because the JSISetInternalCameraFOV is disabled when leaving the
        /// part (such as returning from IVA to external camera), so the
        /// portrait view shows a partially-missing Kerbal.
        /// </summary>
        /// <param name="activeKerbal">Which Kerbal we're changing.  Can be null.</param>
        /// <param name="hideKerbal">What portion of the Kerbal to hide.</param>
        internal void SetKerbalVisible(Kerbal activeKerbal, JSISetInternalCameraFOV.HideKerbal hideKerbal)
        {
            if (lastActiveKerbal != activeKerbal)
            {
                //JUtil.LogMessage(this, "SetKerbalVisible({0}, {1})", (activeKerbal != null) ? activeKerbal.crewMemberName : "(null)", hideKerbal.ToString());
                if (lastActiveKerbal != null)
                {
                    lastActiveKerbal.headTransform.parent.gameObject.SetActive(true);
                    lastActiveKerbal.headTransform.gameObject.SetActive(true);
                }

                if (hideKerbal == JSISetInternalCameraFOV.HideKerbal.none)
                {
                    // If we aren't going to hide it, don't track it.
                    activeKerbal = null;
                }

                if (activeKerbal != null)
                {
                    if (hideKerbal == JSISetInternalCameraFOV.HideKerbal.all)
                    {
                        activeKerbal.headTransform.parent.gameObject.SetActive(false);
                    }
                    else
                    {
                        activeKerbal.headTransform.gameObject.SetActive(false);
                    }
                }

                lastActiveKerbal = activeKerbal;
            }
        }

        #region VesselModule Overrides
        /// <summary>
        /// Load and parse persistent variables
        /// </summary>
        /// <param name="node"></param>
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            // null vessels are possible - if I detect the craft is
            // uncontrollable at Awake, I don't bother storing vessel, so we
            // can see null here.  It is not an error.
            if (vessel != null)
            {
                JUtil.LogMessage(this, "OnLoad for vessel {0}", vessel.id);
                List<RasterPropMonitorComputer> knownRpmc = new List<RasterPropMonitorComputer>();
                for (int partIdx = 0; partIdx < vessel.parts.Count; ++partIdx)
                {
                    RasterPropMonitorComputer rpmc = RasterPropMonitorComputer.Instantiate(vessel.parts[partIdx], false);
                    if (rpmc != null)
                    {
                        knownRpmc.Add(rpmc);
                    }
                }

                ConfigNode[] pers = node.GetNodes("RPM_PERSISTENT_VARS");
                for (int nodeIdx = 0; nodeIdx < pers.Length; ++nodeIdx)
                {
                    string nodeName = string.Empty;
                    if (pers[nodeIdx].TryGetValue("name", ref nodeName))
                    {
                        Dictionary<string, object> myPersistentVars = new Dictionary<string, object>();

                        for (int i = 0; i < pers[nodeIdx].CountValues; ++i)
                        {
                            ConfigNode.Value val = pers[nodeIdx].values[i];

                            string[] value = val.value.Split(',');
                            if (value.Length > 2) // urk.... commas in the stored string
                            {
                                string s = value[1].Trim();
                                for (int j = 2; j < value.Length; ++j)
                                {
                                    s = s + ',' + value[i].Trim();
                                }
                                value[1] = s;
                            }

                            if (value[0] != nodeName)
                            {
                                switch (value[0].Trim())
                                {
                                    case "System.Boolean":
                                        bool vb = false;
                                        if (Boolean.TryParse(value[1].Trim(), out vb))
                                        {
                                            myPersistentVars[val.name.Trim()] = vb;
                                        }
                                        else
                                        {
                                            JUtil.LogErrorMessage(this, "Failed to parse {0} as a boolean", val.name);
                                        }
                                        break;
                                    case "System.Int32":
                                        int vi = 0;
                                        if (Int32.TryParse(value[1].Trim(), out vi))
                                        {
                                            myPersistentVars[val.name.Trim()] = vi;
                                        }
                                        else
                                        {
                                            JUtil.LogErrorMessage(this, "Failed to parse {0} as an int", val.name);
                                        }
                                        break;
                                    case "System.Single":
                                        float vf = 0.0f;
                                        if (Single.TryParse(value[1].Trim(), out vf))
                                        {
                                            myPersistentVars[val.name.Trim()] = vf;
                                        }
                                        else
                                        {
                                            JUtil.LogErrorMessage(this, "Failed to parse {0} as a float", val.name);
                                        }
                                        break;
                                    default:
                                        JUtil.LogErrorMessage(this, "Found unknown persistent type {0}", value[0]);
                                        break;
                                }
                            }
                        }

                        for (int rpmIdx = 0; rpmIdx < knownRpmc.Count; ++rpmIdx)
                        {
                            if (knownRpmc[rpmIdx].RPMCid == nodeName)
                            {
                                JUtil.LogMessage(this, "Loading RPMC {0} persistents ({1} values)", nodeName, myPersistentVars.Count);
                                knownRpmc[rpmIdx].persistentVars = myPersistentVars;
                                break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Save our persistent variables
        /// </summary>
        /// <param name="node"></param>
        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            // null vessels are possible - if I detect the craft is
            // uncontrollable at Awake, I don't bother storing vessel, so we
            // can see null here.  It is not an error.
            if (vessel != null)
            {
                JUtil.LogMessage(this, "OnSave for vessel {0}", vessel.id);

                for (int partIdx = 0; partIdx < vessel.parts.Count; ++partIdx)
                {
                    RasterPropMonitorComputer rpmc = RasterPropMonitorComputer.Instantiate(vessel.parts[partIdx], false);
                    if (rpmc != null && rpmc.persistentVars.Count > 0)
                    {
                        JUtil.LogMessage(this, "Storing RPMC {0} persistents", rpmc.RPMCid);
                        ConfigNode rpmcPers = new ConfigNode("RPM_PERSISTENT_VARS");
                        rpmcPers.AddValue("name", rpmc.RPMCid);
                        foreach (var val in rpmc.persistentVars)
                        {
                            string value = string.Format("{0},{1}", val.Value.GetType().ToString(), val.Value.ToString());
                            rpmcPers.AddValue(val.Key, value);
                        }
                        node.AddNode(rpmcPers);
                    }
                }

            }
            else
            {
                JUtil.LogMessage(this, "OnSave vessel is null? expected for {0}", vid);
            }
        }

        public override void OnAwake()
        {
            base.OnAwake();

            if (!InstallationPathWarning.Warn())
            {
                return;
            }

            vessel = GetComponent<Vessel>();
            if (vessel == null || vessel.isEVA || !vessel.isCommandable)
            {
                vessel = null;
                //Destroy(this);
                return;
            }
            if (!GameDatabase.Instance.IsReady())
            {
                throw new Exception("GameDatabase is not ready?");
            }

            if (instances == null)
            {
                JUtil.LogInfo(this, "Initializing RPM version {0}", FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);
                instances = new Dictionary<Guid, RPMVesselComputer>();
            }

            if (instances.ContainsKey(vessel.id))
            {
                JUtil.LogErrorMessage(this, "Awake for vessel {0} ({1}), but it's already in the dictionary.", (string.IsNullOrEmpty(vessel.vesselName)) ? "(no name)" : vessel.vesselName, vessel.id);
            }
            else
            {
                instances.Add(vessel.id, this);
                JUtil.LogMessage(this, "Awake for vessel {0} ({1}).", (string.IsNullOrEmpty(vessel.vesselName)) ? "(no name)" : vessel.vesselName, vessel.id);
            }
            vid = vessel.id;

            //GameEvents.onGameSceneLoadRequested.Add(onGameSceneLoadRequested);
            GameEvents.onVesselChange.Add(onVesselChange);
            GameEvents.onVesselWasModified.Add(onVesselWasModified);
#if SHOW_DOCKING_EVENTS
            GameEvents.onPartCouple.Add(onPartCouple);
            GameEvents.onPartUndock.Add(onPartUndock);
#endif
            GameEvents.onVesselDestroy.Add(onVesselDestroy);
            GameEvents.onVesselCreate.Add(onVesselCreate);
        }

        public void Start()
        {
            if (vessel == null)
            {
                return;
            }

            //JUtil.LogMessage(this, "Start for vessel {0} ({1})", (string.IsNullOrEmpty(vessel.vesselName)) ? "(no name)" : vessel.vesselName, vessel.id);
            try
            {
                navBall = UnityEngine.Object.FindObjectOfType<KSP.UI.Screens.Flight.NavBall>();
            }
            catch (Exception e)
            {
                JUtil.LogErrorMessage(this, "Failed to fetch the NavBall: {0}", e);
                navBall = new NavBall();
            }

            try
            {
                linearAtmosGauge = UnityEngine.Object.FindObjectOfType<KSP.UI.Screens.Flight.LinearAtmosphereGauge>();
            }
            catch (Exception e)
            {
                JUtil.LogErrorMessage(this, "Failed to fetch the LinearAtmosphereGauge: {0}", e);
                linearAtmosGauge = new LinearAtmosphereGauge();
            }

            if (JUtil.IsActiveVessel(vessel))
            {
                FetchPerPartData();
                FetchAltitudes();
                FetchVesselData();
                FetchTargetData();
            }
        }

        public void OnDestroy()
        {
            if (vessel == null)
            {
                Vessel avessel = GetComponent<Vessel>();
                if (avessel == null)
                {
                    JUtil.LogMessage(this, "OnDestroy with GetComponent<Vessel> null, expected vid {0}", vid);
                }
                else
                {
                    JUtil.LogMessage(this, "OnDestroy with GetComponent<Vessel> {0}, expected vid {1}", avessel.id, vid);
                }
                return;
            }

            if (RPMGlobals.debugShowVariableCallCount)
            {
                List<KeyValuePair<string, int>> l = new List<KeyValuePair<string, int>>();
                l.AddRange(debug_callCount);
                l.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
                    {
                        return a.Value - b.Value;
                    });
                for (int i = 0; i < l.Count; ++i)
                {
                    JUtil.LogMessage(this, "{0} queried {1} times {2:0.0} calls/FixedUpdate", l[i].Key, l[i].Value, (float)(l[i].Value) / (float)(debug_fixedUpdates));
                }
            }

            //JUtil.LogMessage(this, "OnDestroy for vessel {0} ({1})", (string.IsNullOrEmpty(vessel.vesselName)) ? "(no name)" : vessel.vesselName, vessel.id);
            //GameEvents.onGameSceneLoadRequested.Remove(onGameSceneLoadRequested);
            GameEvents.onVesselChange.Remove(onVesselChange);
            GameEvents.onVesselWasModified.Remove(onVesselWasModified);
#if SHOW_DOCKING_EVENTS
            GameEvents.onPartCouple.Remove(onPartCouple);
            GameEvents.onPartUndock.Remove(onPartUndock);
#endif
            GameEvents.onVesselDestroy.Remove(onVesselDestroy);
            GameEvents.onVesselCreate.Remove(onVesselCreate);

            if (instances.ContainsKey(vessel.id))
            {
                instances.Remove(vessel.id);
                JUtil.LogMessage(this, "OnDestroy for vessel {0}", vessel.id);
            }

            vessel = null;
            navBall = null;
            part = null;

            target = null;
            targetDockingNode = null;
            targetVessel = null;
            targetOrbit = null;
            targetBody = null;

            resources = null;

            vesselCrew.Clear();
            vesselCrewMedical.Clear();
        }

        public void Update()
        {
            if (vessel == null)
            {
                return;
            }

            if (JUtil.IsActiveVessel(vessel) && UpdateCheck())
            {
                timeToUpdate = true;
            }

            if (!JUtil.IsInIVA() && lastActiveKerbal != null)
            {
                // If JSISetInternalCameraFOV asked us to hide a kerbal's head
                // (or body), we need to undo that change here, since we're no
                // longer in IVA.
                lastActiveKerbal.headTransform.parent.gameObject.SetActive(true);
                lastActiveKerbal.headTransform.gameObject.SetActive(true);
                lastActiveKerbal = null;
            }
        }

        public void FixedUpdate()
        {
            if (vessel == null)
            {
                return;
            }

            if (JUtil.RasterPropMonitorShouldUpdate(vessel))
            {
                UpdateVariables();
            }
        }

        public void UpdateVariables()
        {
            // Update values related to the vessel (position, CoM, etc)
            if (timeToUpdate)
            {
#if SHOW_FIXEDUPDATE_TIMING
                stopwatch.Reset();
                stopwatch.Start();
#endif
                Protractor.OnFixedUpdate();

#if SHOW_FIXEDUPDATE_TIMING
                long newPart = stopwatch.ElapsedMilliseconds;
#endif
                timeToUpdate = false;

#if SHOW_FIXEDUPDATE_TIMING
                long invalidate = stopwatch.ElapsedMilliseconds;
#endif

                //DebugFunction();

                FetchPerPartData();
#if SHOW_FIXEDUPDATE_TIMING
                long perpart = stopwatch.ElapsedMilliseconds;
#endif
                FetchAltitudes();
#if SHOW_FIXEDUPDATE_TIMING
                long altitudes = stopwatch.ElapsedMilliseconds;
#endif
                FetchVesselData();
#if SHOW_FIXEDUPDATE_TIMING
                long vesseldata = stopwatch.ElapsedMilliseconds;
#endif
                FetchTargetData();

#if SHOW_FIXEDUPDATE_TIMING
                long targetdata = stopwatch.ElapsedMilliseconds;
                stopwatch.Stop();

                JUtil.LogMessage(this, "FixedUpdate net ms: deduceNewPart = {0}, invalidate = {1}, FetchPerPart = {2}, FetchAlt = {3}, FetchVessel = {4}, FetchTarget = {5}",
                    newPart, invalidate, perpart, altitudes, vesseldata, targetdata);
#endif
            }
        }

        //private void DebugFunction()
        //{
        //    JUtil.LogMessage(this, "TimeWarp.CurrentRate = {0}, TimeWarp.WarpMode = {1}, TimeWarp.deltaTime = {2:0.000}",
        //        TimeWarp.CurrentRate, TimeWarp.WarpMode, TimeWarp.deltaTime);
        //}
        #endregion

        #region Interface Methods
        /// <summary>
        /// Initialize vessel description-based values.
        /// </summary>
        /// <param name="vesselDescription"></param>
        internal void SetVesselDescription(string vesselDescription)
        {
            string[] descriptionStrings = vesselDescription.UnMangleConfigText().Split(JUtil.LineSeparator, StringSplitOptions.None);
            for (int i = 0; i < descriptionStrings.Length; i++)
            {
                if (descriptionStrings[i].StartsWith("AG", StringComparison.Ordinal) && descriptionStrings[i][3] == '=')
                {
                    uint groupID;
                    if (uint.TryParse(descriptionStrings[i][2].ToString(), out groupID))
                    {
                        actionGroupMemo[groupID] = descriptionStrings[i].Substring(4).Trim();
                        descriptionStrings[i] = string.Empty;
                    }
                }
            }
        }

        /// <summary>
        /// Set the refresh rate (number of Update() calls per triggered update).
        /// The lower of the current data rate and the new data rate is used.
        /// </summary>
        /// <param name="newDataRate">New data rate</param>
        internal void UpdateDataRefreshRate(int newDataRate)
        {
            refreshDataRate = Math.Max(1, Math.Min(newDataRate, refreshDataRate));
        }
        #endregion

        #region Internal Methods
        /// <summary>
        /// Try to figure out which part on the craft is the current part.
        /// </summary>
        /// <returns></returns>
        private Part DeduceCurrentPart()
        {
            Part currentPart = null;

            if (JUtil.VesselIsInIVA(vessel))
            {
                Kerbal thatKerbal = CameraManager.Instance.IVACameraActiveKerbal;
                if (thatKerbal != null)
                {
                    // This should be a drastically faster way to determine
                    // where we are.  I hope.
                    currentPart = thatKerbal.InPart;
                }

                if (currentPart == null)
                {
                    Transform internalCameraTransform = InternalCamera.Instance.transform;
                    foreach (Part thisPart in InternalModelParts(vessel))
                    {
                        for (int seatIdx = 0; seatIdx < thisPart.internalModel.seats.Count; ++seatIdx)
                        {
                            if (thisPart.internalModel.seats[seatIdx].kerbalRef != null)
                            {
                                if (thisPart.internalModel.seats[seatIdx].kerbalRef.eyeTransform == internalCameraTransform.parent)
                                {
                                    currentPart = thisPart;
                                    break;
                                }
                            }
                        }

                        if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal)
                        {
                            Transform[] modelTransforms = thisPart.internalModel.GetComponentsInChildren<Transform>();
                            for (int xformIdx = 0; xformIdx < modelTransforms.Length; ++xformIdx)
                            {
                                if (modelTransforms[xformIdx] == InternalCamera.Instance.transform.parent)
                                {
                                    currentPart = thisPart;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return currentPart;
        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="vessel"></param>
        /// <returns></returns>
        private static IEnumerable<Part> InternalModelParts(Vessel vessel)
        {
            foreach (Part thatPart in vessel.parts)
            {
                if (thatPart.internalModel != null)
                {
                    yield return thatPart;
                }
            }
        }

        /// <summary>
        /// Fetch altitude-related values
        /// </summary>
        private void FetchAltitudes()
        {
            CoM = vessel.CoM;
            altitudeASL = vessel.mainBody.GetAltitude(CoM);
            altitudeTrue = altitudeASL - vessel.terrainAltitude;
            // MOARdV notes - on a test ship (Mk1-2 pod on a FASA Gemini-based launch stack):
            // vessel.heightFromSurface appears to be -1 at all times.
            // vessel.heightFromTerrain, sometime around 12.5km ASL, goes to -1; otherwise, it's about 8m higher than altitudeTrue reports.
            //  which means ASL isn't computed from CoM in vessel?
            // vessel.altitude reports ~10.7m higher than altitudeASL (CoM) - so it may be that vessel altitude is based on the root part.
            // sfc.distance in the raycast below is likewise 10.7m below vessel.heightFromTerrain, although heightFromTerrain goes
            //  to -1 before the raycast starts failing.
            // vessel.pqsAltitude reports distance to the surface (effectively, altitudeTrue).
            RaycastHit sfc;
            if (Physics.Raycast(CoM, -up, out sfc, (float)altitudeASL + 10000.0F, 1 << 15))
            {
                slopeAngle = Vector3.Angle(up, sfc.normal);
                //JUtil.LogMessage(this, "sfc.distance = {0}, vessel.heightFromTerrain = {1}", sfc.distance, vessel.heightFromTerrain);
            }
            else
            {
                slopeAngle = -1.0f;
            }
            //JUtil.LogMessage(this, "vessel.altitude = {0}, vessel.pqsAltitude = {2}, altitudeASL = {1}", vessel.altitude, altitudeASL, vessel.pqsAltitude);

            float priorAltitudeBottom = (float)altitudeBottom;
            altitudeBottom = (vessel.mainBody.ocean) ? Math.Min(altitudeASL, altitudeTrue) : altitudeTrue;
            if (altitudeBottom < 500d)
            {
                double lowestPoint = altitudeASL;
                foreach (Part p in vessel.parts)
                {
                    if (p.collider != null)
                    {
                        Vector3d bottomPoint = p.collider.ClosestPointOnBounds(vessel.mainBody.position);
                        double partBottomAlt = vessel.mainBody.GetAltitude(bottomPoint);
                        lowestPoint = Math.Min(lowestPoint, partBottomAlt);
                    }
                }
                lowestPoint -= altitudeASL;
                altitudeBottom += lowestPoint;
            }
            altitudeBottom = Math.Max(0.0, altitudeBottom);

            float d1 = (float)altitudeBottom - priorAltitudeBottom;
            float t1 = (float)(Planetarium.GetUniversalTime() - lastRadarAltitudeTime);
            // simple exponential smoothing - radar altitude gets very noisy when terrain is hilly.
            const float alpha = 0.0625f;
            radarAltitudeRate = radarAltitudeRate * (1.0f - alpha) + (d1 / t1) * alpha;
            lastRadarAltitudeTime = Planetarium.GetUniversalTime();

            if (Planetarium.GetUniversalTime() >= lastAltitudeBottomSampleTime + 1.0)
            {
                terrainDelta = altitudeBottom - lastAltitudeBottom;
                lastAltitudeBottom = altitudeBottom;
                lastAltitudeBottomSampleTime = Planetarium.GetUniversalTime();
            }
        }

        /// <summary>
        /// Update all of the data that is part dependent (and thus requires iterating over the vessel)
        /// </summary>
        private void FetchPerPartData()
        {
            totalCurrentThrust = totalLimitedMaximumThrust = totalRawMaximumThrust = 0.0f;
            maxEngineFuelFlow = currentEngineFuelFlow = 0.0f;
            totalDataAmount = totalExperimentCount = 0.0f;
            heatShieldTemperature = heatShieldFlux = 0.0f;
            hottestPartTemperature = hottestEngineTemperature = 0.0f;
            hottestPartMaxTemperature = hottestEngineMaxTemperature = 0.0f;
            hottestPartName = string.Empty;
            float hottestPart = float.MaxValue;
            float hottestEngine = float.MaxValue;
            float hottestShield = float.MinValue;
            float totalResourceMass = 0.0f;

            float averageIspContribution = 0.0f;
            float maxIspContribution = 0.0f;

            anyEnginesOverheating = anyEnginesFlameout = false;

            resources.StartLoop();

            foreach (Part thatPart in vessel.parts)
            {
                foreach (PartResource resource in thatPart.Resources)
                {
                    resources.Add(resource);
                }

                if (thatPart.skinMaxTemp - thatPart.skinTemperature < hottestPart)
                {
                    hottestPartTemperature = (float)thatPart.skinTemperature;
                    hottestPartMaxTemperature = (float)thatPart.skinMaxTemp;
                    hottestPartName = thatPart.partInfo.title;
                    hottestPart = hottestPartMaxTemperature - hottestPartTemperature;
                }
                if (thatPart.maxTemp - thatPart.temperature < hottestPart)
                {
                    hottestPartTemperature = (float)thatPart.temperature;
                    hottestPartMaxTemperature = (float)thatPart.maxTemp;
                    hottestPartName = thatPart.partInfo.title;
                    hottestPart = hottestPartMaxTemperature - hottestPartTemperature;
                }
                totalResourceMass += thatPart.GetResourceMass();

                for (int moduleIdx = 0; moduleIdx < thatPart.Modules.Count; ++moduleIdx)
                {
                    if (thatPart.Modules[moduleIdx].isEnabled)
                    {
                        if (thatPart.Modules[moduleIdx] is ModuleEngines || thatPart.Modules[moduleIdx] is ModuleEnginesFX)
                        {
                            var thatEngineModule = thatPart.Modules[moduleIdx] as ModuleEngines;
                            anyEnginesOverheating |= (thatPart.skinTemperature / thatPart.skinMaxTemp > 0.9) || (thatPart.temperature / thatPart.maxTemp > 0.9);
                            anyEnginesFlameout |= (thatEngineModule.isActiveAndEnabled && thatEngineModule.flameout);

                            float currentThrust = GetCurrentThrust(thatEngineModule);
                            totalCurrentThrust += currentThrust;
                            float rawMaxThrust = GetMaximumThrust(thatEngineModule);
                            totalRawMaximumThrust += rawMaxThrust;
                            float maxThrust = rawMaxThrust * thatEngineModule.thrustPercentage * 0.01f;
                            totalLimitedMaximumThrust += maxThrust;
                            float realIsp = GetRealIsp(thatEngineModule);
                            if (realIsp > 0.0f)
                            {
                                averageIspContribution += maxThrust / realIsp;

                                // Compute specific fuel consumption and
                                // multiply by thrust to get grams/sec fuel flow
                                float specificFuelConsumption = 101972f / realIsp;
                                maxEngineFuelFlow += specificFuelConsumption * rawMaxThrust;
                                currentEngineFuelFlow += specificFuelConsumption * currentThrust;
                            }

                            foreach (Propellant thatResource in thatEngineModule.propellants)
                            {
                                resources.MarkPropellant(thatResource);
                            }

                            float minIsp, maxIsp;
                            thatEngineModule.atmosphereCurve.FindMinMaxValue(out minIsp, out maxIsp);
                            if (maxIsp > 0.0f)
                            {
                                maxIspContribution += maxThrust / maxIsp;
                            }

                            if (thatPart.skinMaxTemp - thatPart.skinTemperature < hottestEngine)
                            {
                                hottestEngineTemperature = (float)thatPart.skinTemperature;
                                hottestEngineMaxTemperature = (float)thatPart.skinMaxTemp;
                                hottestEngine = hottestEngineMaxTemperature - hottestEngineTemperature;
                            }
                            if (thatPart.maxTemp - thatPart.temperature < hottestEngine)
                            {
                                hottestEngineTemperature = (float)thatPart.temperature;
                                hottestEngineMaxTemperature = (float)thatPart.maxTemp;
                                hottestEngine = hottestEngineMaxTemperature - hottestEngineTemperature;
                            }
                        }
                        else if (thatPart.Modules[moduleIdx] is ModuleAblator)
                        {
                            var thatAblator = thatPart.Modules[moduleIdx] as ModuleAblator;

                            // Even though the interior contains a lot of heat, I think ablation is based on skin temp.
                            // Although it seems odd that the skin temp quickly cools off after re-entry, while the
                            // interior temp doesn't move cool much (for instance, I saw a peak ablator skin temp
                            // of 950K, while the interior eventually reached 345K after the ablator had cooled below
                            // 390K.  By the time the capsule landed, skin temp matched exterior temp (304K) but the
                            // interior still held 323K.
                            if (thatPart.skinTemperature - thatAblator.ablationTempThresh > hottestShield)
                            {
                                hottestShield = (float)(thatPart.skinTemperature - thatAblator.ablationTempThresh);
                                heatShieldTemperature = (float)(thatPart.skinTemperature);
                                heatShieldFlux = (float)(thatPart.thermalConvectionFlux + thatPart.thermalRadiationFlux);
                            }
                        }
                        //else if (pm is ModuleScienceExperiment)
                        //{
                        //    var thatExperiment = pm as ModuleScienceExperiment;
                        //    JUtil.LogMessage(this, "Experiment: {0} in {1} (action name {2}):", thatExperiment.experiment.experimentTitle, thatPart.partInfo.name, thatExperiment.experimentActionName);
                        //    JUtil.LogMessage(this, " - collection action {0}, collect warning {1}, is collectable {2}", thatExperiment.collectActionName, thatExperiment.collectWarningText, thatExperiment.dataIsCollectable);
                        //    JUtil.LogMessage(this, " - Inoperable {0}, resetActionName {1}, resettable {2}, reset on EVA {3}, review {4}", thatExperiment.Inoperable, thatExperiment.resetActionName, thatExperiment.resettable, thatExperiment.resettableOnEVA, thatExperiment.reviewActionName);
                        //}
                        //else if (pm is ModuleScienceContainer)
                        //{
                        //    var thatContainer = pm as ModuleScienceContainer;
                        //    JUtil.LogMessage(this, "Container: in {0}: allow repeats {1}, isCollectable {2}, isRecoverable {3}, isStorable {4}, evaOnlyStorage {5}", thatPart.partInfo.name,
                        //        thatContainer.allowRepeatedSubjects, thatContainer.dataIsCollectable, thatContainer.dataIsRecoverable, thatContainer.dataIsStorable, thatContainer.evaOnlyStorage);
                        //}
                    }
                }

                foreach (IScienceDataContainer container in thatPart.FindModulesImplementing<IScienceDataContainer>())
                {
                    foreach (ScienceData datapoint in container.GetData())
                    {
                        if (datapoint != null)
                        {
                            totalDataAmount += datapoint.dataAmount;
                            totalExperimentCount += 1.0f;
                        }
                    }
                }
            }

            totalShipWetMass = vessel.GetTotalMass();
            totalShipDryMass = totalShipWetMass - totalResourceMass;

            if (averageIspContribution > 0.0f)
            {
                actualAverageIsp = totalLimitedMaximumThrust / averageIspContribution;
            }
            else
            {
                actualAverageIsp = 0.0f;
            }

            if (maxIspContribution > 0.0f)
            {
                actualMaxIsp = totalLimitedMaximumThrust / maxIspContribution;
            }
            else
            {
                actualMaxIsp = 0.0f;
            }

            // We can use the stock routines to get at the per-stage resources.
            // Except KSP 1.1.1 broke GetActiveResources() and GetActiveResource(resource).
            // Like exception-throwing broke.  It was fixed in 1.1.2, but I
            // already put together a work-around.
            try
            {
                var activeResources = vessel.GetActiveResources();
                for (int i = 0; i < activeResources.Count; ++i)
                {
                    resources.SetActive(activeResources[i]);
                }
            }
            catch { }

            resources.EndLoop(Planetarium.GetUniversalTime());

            // MOARdV TODO: Migrate this to a callback system:
            // I seriously hope you don't have crew jumping in and out more than once per second.
            vesselCrew = vessel.GetVesselCrew();
            // The sneaky bit: This way we can get at their panic and whee values!
            if (vesselCrewMedical.Count != vesselCrew.Count)
            {
                vesselCrewMedical.Clear();
                for (int i = 0; i < vesselCrew.Count; i++)
                {
                    vesselCrewMedical.Add((vesselCrew[i].KerbalRef != null) ? vesselCrew[i].KerbalRef.GetComponent<kerbalExpressionSystem>() : null);
                }
            }
            else
            {
                for (int i = 0; i < vesselCrew.Count; i++)
                {
                    vesselCrewMedical[i] = (vesselCrew[i].KerbalRef != null) ? vesselCrew[i].KerbalRef.GetComponent<kerbalExpressionSystem>() : null;
                }
            }
        }

        /// <summary>
        /// Fetch data on any targets being tracked.
        /// </summary>
        private void FetchTargetData()
        {
            target = FlightGlobals.fetch.VesselTarget;

            if (target != null)
            {
                targetSeparation = vessel.GetTransform().position - target.GetTransform().position;
                targetOrientation = target.GetTransform().rotation;

                targetVessel = target as Vessel;
                targetBody = target as CelestialBody;
                targetDockingNode = target as ModuleDockingNode;

                targetDistance = Vector3.Distance(target.GetTransform().position, vessel.GetTransform().position);

                if (targetVessel != null || targetDockingNode != null)
                {
                    targetOrbitSensibility = JUtil.OrbitMakesSense(target.GetVessel());
                }
                else
                {
                    // All celestial bodies except the sun have orbits that make sense.
                    targetOrbitSensibility = targetBody != null && targetBody != Planetarium.fetch.Sun;
                }

                targetOrbit = targetOrbitSensibility ? target.GetOrbit() : null;

                // TODO: Actually, there's a lot of nonsensical cases here that need more reasonable handling.
                // Like what if we're targeting a vessel landed on a moon of another planet?...
                if (targetOrbit != null)
                {
                    velocityRelativeTarget = vessel.orbit.GetVel() - target.GetOrbit().GetVel();
                }
                else
                {
                    velocityRelativeTarget = vessel.orbit.GetVel();
                }

                // If our target is somehow our own celestial body, approach speed is equal to vertical speed.
                if (targetBody == vessel.mainBody)
                {
                    approachSpeed = (float)speedVertical;
                }
                else
                {
                    // In all other cases, that should work. I think.
                    approachSpeed = Vector3.Dot(velocityRelativeTarget, (target.GetTransform().position - vessel.GetTransform().position).normalized);
                }
            }
            else
            {
                velocityRelativeTarget = targetSeparation = Vector3d.zero;
                targetOrbit = null;
                targetDistance = 0.0;
                approachSpeed = 0.0f;
                targetBody = null;
                targetVessel = null;
                targetDockingNode = null;
                targetOrientation = vessel.GetTransform().rotation;
                targetOrbitSensibility = false;
            }
        }

        /// <summary>
        /// Update ship-wide data.
        /// </summary>
        private void FetchVesselData()
        {
            orbitSensibility = JUtil.OrbitMakesSense(vessel);

            localGeeASL = (float)(vessel.orbit.referenceBody.GeeASL * gee);
            localGeeDirect = (float)FlightGlobals.getGeeForceAtPosition(CoM).magnitude;

            speedVertical = vessel.verticalSpeed;
            speedVerticalRounded = Math.Ceiling(speedVertical * 20.0) / 20.0;
            if (Math.Abs(speedVertical) < Math.Abs(vessel.srfSpeed))
            {
                speedHorizontal = Math.Sqrt(vessel.srfSpeed * vessel.srfSpeed - speedVertical * speedVertical);
            }
            else
            {
                speedHorizontal = 0.0;
            }

            // Record the vessel-relative basis
            // north isn't actually used anywhere...
            right = vessel.GetTransform().right;
            forward = vessel.GetTransform().up;
            top = vessel.GetTransform().forward;

            //north = Vector3.ProjectOnPlane((vessel.mainBody.position + (Vector3d)vessel.mainBody.transform.up * vessel.mainBody.Radius) - CoM, up).normalized;
            // Generate the surface-relative basis (up, surfaceRight, surfaceForward)
            up = FlightGlobals.upAxis;
            surfaceForward = Vector3.Cross(up, right);
            // If the craft is rolled sharply to the side, we have to re-do our basis.
            if (surfaceForward.sqrMagnitude < 0.5f)
            {
                surfaceRight = Vector3.Cross(forward, up);
                surfaceForward = Vector3.Cross(up, surfaceRight);
            }
            else
            {
                surfaceRight = Vector3.Cross(surfaceForward, up);
            }

            // This happens if we update right away, before navBall has been fetched.
            // Like, at load time.
            if (navBall != null)
            {
                rotationVesselSurface = Quaternion.Inverse(navBall.relativeGymbal);
            }
            else
            {
                rotationVesselSurface = Quaternion.identity;
            }

            prograde = vessel.orbit.GetVel().normalized;
            radialOut = Vector3.ProjectOnPlane(up, prograde).normalized;
            normalPlus = -Vector3.Cross(radialOut, prograde).normalized;

            UpdateLandingPredictions();
        }

        private bool runningPredicition = false;
        private double lastRadius;
        internal double estLandingUT;
        internal double estLandingLatitude;
        internal double estLandingLongitude;
        internal double estLandingAltitude;
        private void UpdateLandingPredictions()
        {
            if (orbitSensibility && vessel.orbit.PeA < 0.0)
            {
                try
                {
                    if (runningPredicition == false)
                    {
                        lastRadius = vessel.orbit.PeR;

                        // First estimate
                        double nextUt = vessel.orbit.NextTimeOfRadius(Planetarium.GetUniversalTime(), lastRadius);
                        Vector3d pos = vessel.orbit.getPositionAtUT(nextUt);
                        estLandingLatitude = vessel.mainBody.GetLatitude(pos);
                        estLandingLongitude = vessel.mainBody.GetLongitude(pos);
                        estLandingAltitude = vessel.mainBody.TerrainAltitude(estLandingLatitude, estLandingLongitude);
                        if (vessel.mainBody.ocean)
                        {
                            estLandingAltitude = Math.Max(estLandingAltitude, 0.0);
                        }
                        if (estLandingAltitude >= vessel.orbit.PeA)
                        {
                            //lastPoint = pos;
                            estLandingUT = nextUt;
                            lastRadius = estLandingAltitude + vessel.mainBody.Radius;
                            runningPredicition = true;
                            //JUtil.LogMessage(this, "Initial point-of-impact: {0:##0.00} x {1:###0.00} @ {2:0}m in {3:0}s",
                            //    estLandingLatitude, estLandingLongitude, estLandingAltitude, estLandingUT - Planetarium.GetUniversalTime());
                        }
                        else
                        {
                            // Have not hit the planet.  Try again next round
                            //JUtil.LogMessage(this, "Seeking point of impact");
                            runningPredicition = false;
                            estLandingLatitude = estLandingLongitude = estLandingAltitude = estLandingUT = 0.0;
                        }
                    }
                    else
                    {
                        double nextRadius = Math.Max(vessel.orbit.PeR, lastRadius);
                        double nextUt = vessel.orbit.NextTimeOfRadius(Planetarium.GetUniversalTime(), nextRadius);
                        Vector3d pos = vessel.orbit.getPositionAtUT(nextUt);
                        estLandingLatitude = vessel.mainBody.GetLatitude(pos);
                        estLandingLongitude = vessel.mainBody.GetLongitude(pos);
                        estLandingAltitude = vessel.mainBody.TerrainAltitude(estLandingLatitude, estLandingLongitude);
                        if (vessel.mainBody.ocean)
                        {
                            estLandingAltitude = Math.Max(estLandingAltitude, 0.0);
                        }
                        //lastPoint = pos;
                        estLandingUT = nextUt;
                        lastRadius = estLandingAltitude + vessel.mainBody.Radius;
                        runningPredicition = true;
                        //JUtil.LogMessage(this, "Revised point-of-impact: {0:##0.00} x {1:###0.00} @ {2:0}m in {3:0}s",
                        //    estLandingLatitude, estLandingLongitude, estLandingAltitude, estLandingUT - Planetarium.GetUniversalTime());
                    }
                }
                catch
                {
                    // Any exceptions probably came from the bugs in KSP 1.1.2 Orbit code, so we reset everything
                    runningPredicition = false;
                    estLandingLatitude = estLandingLongitude = estLandingAltitude = estLandingUT = 0.0;
                }
            }
            else
            {
                runningPredicition = false;
                estLandingLatitude = estLandingLongitude = estLandingAltitude = estLandingUT = 0.0;
            }
        }

        /// <summary>
        /// Get the current thrust of the engine
        /// </summary>
        /// <param name="engine"></param>
        /// <returns></returns>
        private static float GetCurrentThrust(ModuleEngines engine)
        {
            if (engine != null)
            {
                if ((!engine.EngineIgnited) || (!engine.isEnabled) || (!engine.isOperational))
                {
                    return 0.0f;
                }
                else
                {
                    return engine.finalThrust;
                }
            }
            else
            {
                return 0.0f;
            }
        }

        /// <summary>
        /// Get the maximum thrust of the engine.
        /// </summary>
        /// <param name="engine"></param>
        /// <returns></returns>
        private static float GetMaximumThrust(ModuleEngines engine)
        {
            if (engine != null)
            {
                if ((!engine.EngineIgnited) || (!engine.isEnabled) || (!engine.isOperational))
                {
                    return 0.0f;
                }

                float vacISP = engine.atmosphereCurve.Evaluate(0.0f);
                float maxThrustAtAltitude = engine.maxThrust * engine.realIsp / vacISP;

                return maxThrustAtAltitude;
            }
            else
            {
                return 0.0f;
            }
        }

        /// <summary>
        /// Get the instantaneous ISP of the engine.
        /// </summary>
        /// <param name="engine"></param>
        /// <returns></returns>
        private static float GetRealIsp(ModuleEngines engine)
        {
            if (engine != null)
            {
                if ((!engine.EngineIgnited) || (!engine.isEnabled) || (!engine.isOperational))
                {
                    return 0.0f;
                }
                else
                {
                    return engine.realIsp;
                }
            }
            else
            {
                return 0.0f;
            }
        }

        /// <summary>
        /// Determines the pitch angle between the vector supplied and the front of the craft.
        /// Original code from FAR.
        /// </summary>
        /// <param name="normalizedVectorOfInterest">The normalized vector we want to measure</param>
        /// <returns>Pitch in degrees</returns>
        internal double GetRelativePitch(Vector3 normalizedVectorOfInterest)
        {
            // vector projected onto a plane that divides the airplane into left and right halves
            Vector3 tmpVec = Vector3.ProjectOnPlane(normalizedVectorOfInterest, right);
            float dotpitch = Vector3.Dot(tmpVec.normalized, top);
            float pitch = Mathf.Rad2Deg * Mathf.Asin(dotpitch);
            if (float.IsNaN(pitch))
            {
                pitch = (dotpitch > 0.0f) ? 90.0f : -90.0f;
            }

            return pitch;
        }

        /// <summary>
        /// Determines the yaw angle between the vector supplied and the front of the craft.
        /// Original code from FAR, changed to Unity Vector3.Angle to provide the range 0-180.
        /// </summary>
        /// <param name="normalizedVectorOfInterest">The normalized vector we want to measure</param>
        /// <returns>Yaw in degrees</returns>
        internal double GetRelativeYaw(Vector3 normalizedVectorOfInterest)
        {
            //velocity vector projected onto the vehicle-horizontal plane
            Vector3 tmpVec = Vector3.ProjectOnPlane(normalizedVectorOfInterest, top).normalized;
            float dotyaw = Vector3.Dot(tmpVec, right);
            float angle = Vector3.Angle(tmpVec, forward);

            if (dotyaw < 0.0f)
            {
                angle = -angle;
            }
            return angle;
        }

        /// <summary>
        /// Returns a number identifying the next apsis type
        /// </summary>
        /// <returns></returns>
        private double NextApsisType()
        {
            if (orbitSensibility)
            {
                if (vessel.orbit.eccentricity < 1.0)
                {
                    // Which one will we reach first?
                    return (vessel.orbit.timeToPe < vessel.orbit.timeToAp) ? -1.0 : 1.0;
                } 	// Ship is hyperbolic.  There is no Ap.  Have we already
                // passed Pe?
                return (-vessel.orbit.meanAnomaly / (2 * Math.PI / vessel.orbit.period) > 0.0) ? -1.0 : 0.0;
            }

            return 0.0;
        }

        /// <summary>
        /// According to C# specification, switch-case is compiled to a constant hash table.
        /// So this is actually more efficient than a dictionary, who'd have thought.
        /// </summary>
        /// <param name="situation"></param>
        /// <returns></returns>
        private static string SituationString(Vessel.Situations situation)
        {
            switch (situation)
            {
                case Vessel.Situations.FLYING:
                    return "Flying";
                case Vessel.Situations.SUB_ORBITAL:
                    return "Sub-orbital";
                case Vessel.Situations.ESCAPING:
                    return "Escaping";
                case Vessel.Situations.LANDED:
                    return "Landed";
                case Vessel.Situations.DOCKED:
                    return "Docked"; // When does this ever happen exactly, I wonder?
                case Vessel.Situations.PRELAUNCH:
                    return "Ready to launch";
                case Vessel.Situations.ORBITING:
                    return "Orbiting";
                case Vessel.Situations.SPLASHED:
                    return "Splashed down";
            }
            return "??!";
        }

        /// <summary>
        /// Computes the estimated speed at impact based on the parameters supplied.
        /// </summary>
        /// <param name="thrust"></param>
        /// <returns></returns>
        internal double SpeedAtImpact(float thrust)
        {
            float acceleration = localGeeASL - (thrust / totalShipWetMass);
            double timeToImpact = (speedVertical + Math.Sqrt(speedVertical * speedVertical + 2.0f * acceleration * altitudeTrue)) / acceleration;
            double speedAtImpact = speedVertical - acceleration * timeToImpact;
            if (double.IsNaN(speedAtImpact))
            {
                speedAtImpact = 0.0;
            }
            return speedAtImpact;
        }

        /// <summary>
        /// Estimates how long before a suicide burn needs to start in order to
        /// avoid crashing.
        /// </summary>
        /// <returns></returns>
        internal double SuicideBurnCountdown()
        {
            Orbit orbit = vessel.orbit;
            if (orbit.PeA > 0.0) throw new ArgumentException("SuicideBurnCountdown: periapsis is above the ground");

            double angleFromHorizontal = 90 - Vector3d.Angle(-vessel.srf_velocity, up);
            angleFromHorizontal = JUtil.Clamp(angleFromHorizontal, 0.0, 90.0);
            double sine = Math.Sin(angleFromHorizontal * Math.PI / 180.0);
            double g = localGeeDirect;
            double T = totalLimitedMaximumThrust / totalShipWetMass;
            double decelTerm = (2.0 * g * sine) * (2.0 * g * sine) + 4.0 * (T * T - g * g);
            if (decelTerm < 0.0)
            {
                return double.NaN;
            }

            double effectiveDecel = 0.5 * (-2.0 * g * sine + Math.Sqrt(decelTerm));
            double decelTime = speedHorizontal / effectiveDecel;

            Vector3d estimatedLandingSite = CoM + 0.5 * decelTime * vessel.srf_velocity;
            double terrainRadius = vessel.mainBody.Radius + vessel.mainBody.TerrainAltitude(estimatedLandingSite);
            double impactTime = 0;
            try
            {
                impactTime = orbit.NextTimeOfRadius(Planetarium.GetUniversalTime(), terrainRadius);
            }
            catch (ArgumentException)
            {
                return double.NaN;
            }
            return impactTime - decelTime / 2.0 - Planetarium.GetUniversalTime();
        }

        /// <summary>
        /// Determines if enough screen updates have passed to trigger another data update.
        /// </summary>
        /// <returns>true if it's time to update things</returns>
        private bool UpdateCheck()
        {
            Part newpart = DeduceCurrentPart();
            if (part != newpart)
            {
                dataUpdateCountdown = refreshDataRate;
                part = newpart;

                return true;
            }

            if (--dataUpdateCountdown < 0)
            {
                dataUpdateCountdown = refreshDataRate;
                return true;
            }
            else
            {
                return false;
            }
        }
        #endregion

        //--- Callbacks for registered GameEvent
        #region GameEvent Callbacks
        //private void onGameSceneLoadRequested(GameScenes data)
        //{
        //    //JUtil.LogMessage(this, "onGameSceneLoadRequested({0}), active vessel is {1}", data, vessel.vesselName);

        //    if (data != GameScenes.FLIGHT)
        //    {
        //        // Are we leaving Flight?  If so, let's get rid of all of the tables we've created.
        //        VariableOrNumber.Clear();
        //    }
        //}

#if SHOW_DOCKING_EVENTS
        /// <summary>
        /// Callback to let us know we're docking.
        /// </summary>
        /// <param name="action"></param>
        private void onPartCouple(GameEvents.FromToAction<Part, Part> action)
        {
            if (action.from.vessel.id == vessel.id)
            {
                JUtil.LogMessage(this, "onPartCouple(): I am 'from' from:{0} to:{1}", action.from.vessel.id, action.to.vessel.id);
                timeToUpdate = true;
            }
            else if (action.to.vessel.id == vessel.id)
            {
                JUtil.LogMessage(this, "onPartCouple(): I am 'to' from:{0} to:{1}", action.from.vessel.id, action.to.vessel.id);
                timeToUpdate = true;
            }
        }

        /// <summary>
        /// Callback to let us know when we're undocking.
        /// </summary>
        /// <param name="p"></param>
        private void onPartUndock(Part p)
        {
            if (p.vessel.id == vessel.id)
            {
                JUtil.LogMessage(this, "onPartUndock(): I {0} expect to undock", vessel.id);
            }
        }
#endif

        /// <summary>
        /// Callback to let us know when we become active so we can refresh
        /// variables right away.
        /// </summary>
        /// <param name="who"></param>
        private void onVesselChange(Vessel who)
        {
            if (who.id == vessel.id)
            {
                JUtil.LogMessage(this, "onVesselChange(): for me {0}", who.id);

                // This looks messy because onVesselChange may be called before
                // the navBall variable has been initialized.  We set timeToUpdate
                // true so UpdateVariables executes now, and then we set it true
                // afterwards because UpdateVariables sets it to false.  That
                // way, the next FixedUpdate will trigger another update after
                // navBall is ready.
                timeToUpdate = true;
                UpdateVariables();
                // Re-trigger the update for the next FixedUpdate.
                timeToUpdate = true;
            }
        }

        /// <summary>
        /// Callback to let us know when our vessel was modified so we can
        /// refresh our variables.
        /// </summary>
        /// <param name="who"></param>
        private void onVesselWasModified(Vessel who)
        {
            if (who.id == vessel.id)
            {
                JUtil.LogMessage(this, "onVesselWasModified(): for me {0}", who.id);
                if (JUtil.IsActiveVessel(vessel))
                {
                    timeToUpdate = true;
                }
            }
        }

        /// <summary>
        /// Callback to catch when a vessel is being destroyed so we can remove
        /// the vessel from the instances dictionary.
        /// </summary>
        /// <param name="who"></param>
        private void onVesselDestroy(Vessel who)
        {
            if (vessel != null)
            {
                if (who.id == vessel.id)
                {
                    JUtil.LogMessage(this, "onVesselDestroy(): for me {0} - unregistering", who.id);
                    instances.Remove(who.id);
                }
            }
        }

        /// <summary>
        /// Callback to catch situations where a RPMVesselComputer was left a
        /// zombie because its craft was destroyed but it wasn't (as happens
        /// with docking)
        /// </summary>
        /// <param name="who"></param>
        private void onVesselCreate(Vessel who)
        {
            if (vessel == null)
            {
                Vessel avessel = GetComponent<Vessel>();
                if (avessel != null && avessel.id == who.id)
                {
                    JUtil.LogMessage(this, "onVesselCreate(): I am was zombie VesselModule now part of {0}", who.id);
                    instances.Add(who.id, this);
                    vid = who.id;
                }
            }
        }
        #endregion

        private class ResourceNameLengthComparer : IComparer<String>
        {
            public int Compare(string x, string y)
            {
                // Note that we need longer strings first so we invert numbers.
                int lengthComparison = -x.Length.CompareTo(y.Length);
                return lengthComparison == 0 ? -string.Compare(x, y, StringComparison.Ordinal) : lengthComparison;
            }
        }
    }
}
