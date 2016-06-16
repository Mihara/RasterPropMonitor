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
using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Text;

namespace JSI
{
    public partial class RPMVesselComputer : VesselModule
    {
        /// This partial module is used to track the many per-module fields in
        /// a vessel.  The original implementation looped every FixedUpdate
        /// over every single part, and every single module in the part, to
        /// track certain values.  By registering for the OnVesselChanged,
        /// OnVesselDestroy, and OnVesselModified events, I can reduce the
        /// need to iterate over _everything_ per FixedUpdate.
        ///
        /// A secondary benefit is that much of the overhead in some of the
        /// JSIInternalButtons methods is outright eliminated.

        private bool listsInvalid = true;

        //--- Docking Nodes
        internal ModuleDockingNode mainDockingNode;
        /// <summary>
        /// Contains the state of the mainDockingNode in a queriable numeric
        /// instead of the string in ModuleDockingNode.  If mainDockingNode is
        /// null, this state is UNKNOWN.
        /// </summary>
        internal DockingNodeState mainDockingNodeState;
        internal enum DockingNodeState
        {
            UNKNOWN,
            DOCKED,
            PREATTACHED,
            READY
        };

        //--- Engines
        internal List<ModuleEngines> availableEngines = new List<ModuleEngines>();
        internal List<MultiModeEngine> availableMultiModeEngines = new List<MultiModeEngine>();
        internal float totalCurrentThrust;
        internal float totalLimitedMaximumThrust;
        internal float totalRawMaximumThrust;
        internal float maxEngineFuelFlow;
        internal float currentEngineFuelFlow;
        internal int currentEngineCount;
        internal int activeEngineCount;
        internal bool anyEnginesFlameout;
        internal bool anyEnginesOverheating;
        internal bool anyEnginesEnabled;
        internal bool anyMmePrimary;

        //--- Gimbals
        internal List<ModuleGimbal> availableGimbals = new List<ModuleGimbal>();
        internal bool gimbalsLocked;

        //--- Heat shields
        internal List<ModuleAblator> availableAblators = new List<ModuleAblator>();
        internal float heatShieldTemperature;
        internal float heatShieldFlux;

        //--- Intake air
        internal List<ModuleResourceIntake> availableAirIntakes = new List<ModuleResourceIntake>();
        internal float currentAirFlow;
        private static float IntakeAir_U_to_grams;

        //--- Parachutes
        internal List<ModuleParachute> availableParachutes = new List<ModuleParachute>();
        internal List<PartModule> availableRealChutes = new List<PartModule>();
        internal bool anyParachutesDeployed;
        internal bool allParachutesSafe;

        //--- Power production
        internal List<ModuleAlternator> availableAlternators = new List<ModuleAlternator>();
        internal List<float> availableAlternatorOutput = new List<float>();
        internal List<ModuleResourceConverter> availableFuelCells = new List<ModuleResourceConverter>();
        internal List<float> availableFuelCellOutput = new List<float>();
        internal List<ModuleGenerator> availableGenerators = new List<ModuleGenerator>();
        internal List<float> availableGeneratorOutput = new List<float>();
        internal List<ModuleDeployableSolarPanel> availableSolarPanels = new List<ModuleDeployableSolarPanel>();
        internal bool generatorsActive; // Returns true if at least one generator or fuel cell is active that can be otherwise switched off
        internal bool solarPanelsDeployable;
        internal bool solarPanelsRetractable;
        internal bool solarPanelsState; // Returns false if the solar panels are extendable or are retracting
        internal int solarPanelMovement;
        internal float alternatorOutput;
        internal float fuelcellOutput;
        internal float generatorOutput;
        internal float solarOutput;

        //--- Radar
        internal List<JSIRadar> availableRadars = new List<JSIRadar>();
        internal bool radarActive;

        //--- Wheels
        internal List<ModuleWheels.ModuleWheelDeployment> availableDeployableWheels = new List<ModuleWheels.ModuleWheelDeployment>();
        internal List<ModuleWheels.ModuleWheelBrakes> availableWheelBrakes = new List<ModuleWheels.ModuleWheelBrakes>();
        internal List<ModuleWheels.ModuleWheelDamage> availableWheelDamage = new List<ModuleWheels.ModuleWheelDamage>();
        internal bool wheelsDamaged;
        internal bool wheelsRepairable;
        internal float wheelBrakeSetting;
        internal float wheelStress;
        internal int gearState;
        internal float gearPosition;

        #region List Management
        /// <summary>
        /// Flag the lists as invalid due to craft changes / destruction.
        /// </summary>
        internal void InvalidateModuleLists()
        {
            listsInvalid = true;

            availableAblators.Clear();
            availableAirIntakes.Clear();
            availableAlternators.Clear();
            availableAlternatorOutput.Clear();
            availableDeployableWheels.Clear();
            availableEngines.Clear();
            availableFuelCells.Clear();
            availableFuelCellOutput.Clear();
            availableGenerators.Clear();
            availableGeneratorOutput.Clear();
            availableGimbals.Clear();
            availableMultiModeEngines.Clear();
            availableParachutes.Clear();
            availableRadars.Clear();
            availableRealChutes.Clear();
            availableSolarPanels.Clear();
            availableWheelBrakes.Clear();
            availableWheelDamage.Clear();

            mainDockingNode = null;
        }

        /// <summary>
        /// Iterate over all of the modules in all of the parts and filter then
        /// into the myriad lists we keep, so when we do the FixedUpdate refresh
        /// of values, we only iterate over the modules we know we care about,
        /// instead of every module on every part.
        /// </summary>
        internal void UpdateModuleLists()
        {
            if (listsInvalid && vessel != null)
            {
                var partsList = vessel.parts;
                for (int partsIdx = 0; partsIdx < partsList.Count; ++partsIdx)
                {
                    foreach (PartModule module in partsList[partsIdx].Modules)
                    {
                        if (module.isEnabled)
                        {
                            if (module is ModuleEngines)
                            {
                                availableEngines.Add(module as ModuleEngines);
                            }
                            else if (module is MultiModeEngine)
                            {
                                availableMultiModeEngines.Add(module as MultiModeEngine);
                            }
                            else if (module is ModuleAblator)
                            {
                                availableAblators.Add(module as ModuleAblator);
                            }
                            else if (module is ModuleResourceIntake)
                            {
                                if ((module as ModuleResourceIntake).resourceName == "IntakeAir")
                                {
                                    availableAirIntakes.Add(module as ModuleResourceIntake);
                                }
                                else
                                {
                                    JUtil.LogMessage(this, "intake resource is {0}?", (module as ModuleResourceIntake).resourceName);
                                }
                            }
                            else if (module is ModuleAlternator)
                            {
                                ModuleAlternator alt = module as ModuleAlternator;
                                for (int i = 0; i < alt.outputResources.Count; ++i)
                                {
                                    if (alt.outputResources[i].name == "ElectricCharge")
                                    {
                                        availableAlternators.Add(alt);
                                        availableAlternatorOutput.Add((float)alt.outputResources[i].rate);
                                        break;
                                    }
                                }
                            }
                            else if (module is ModuleGenerator)
                            {
                                ModuleGenerator gen = module as ModuleGenerator;
                                for (int i = 0; i < gen.outputList.Count; ++i)
                                {
                                    if (gen.outputList[i].name == "ElectricCharge")
                                    {
                                        availableGenerators.Add(gen);
                                        availableGeneratorOutput.Add((float)gen.outputList[i].rate);
                                        break;
                                    }
                                }
                            }
                            else if (module is ModuleResourceConverter)
                            {
                                ModuleResourceConverter gen = module as ModuleResourceConverter;
                                ConversionRecipe recipe = gen.Recipe;
                                for (int i = 0; i < recipe.Outputs.Count; ++i)
                                {
                                    if (recipe.Outputs[i].ResourceName == "ElectricCharge")
                                    {
                                        availableFuelCells.Add(gen);
                                        availableFuelCellOutput.Add((float)recipe.Outputs[i].Ratio);
                                        break;
                                    }
                                }
                            }
                            else if (module is ModuleDeployableSolarPanel)
                            {
                                ModuleDeployableSolarPanel sp = module as ModuleDeployableSolarPanel;

                                if (sp.resourceName == "ElectricCharge")
                                {
                                    availableSolarPanels.Add(sp);
                                }
                            }
                            else if (module is ModuleGimbal)
                            {
                                availableGimbals.Add(module as ModuleGimbal);
                            }
                            else if (module is JSIRadar)
                            {
                                availableRadars.Add(module as JSIRadar);
                            }
                            else if (module is ModuleParachute)
                            {
                                availableParachutes.Add(module as ModuleParachute);
                            }
                            else if (module is ModuleWheels.ModuleWheelDeployment)
                            {
                                availableDeployableWheels.Add(module as ModuleWheels.ModuleWheelDeployment);
                            }
                            else if (module is ModuleWheels.ModuleWheelDamage)
                            {
                                availableWheelDamage.Add(module as ModuleWheels.ModuleWheelDamage);
                            }
                            else if (module is ModuleWheels.ModuleWheelBrakes)
                            {
                                availableWheelBrakes.Add(module as ModuleWheels.ModuleWheelBrakes);
                            }
                            else if (JSIParachute.rcFound && module.GetType() == JSIParachute.rcModuleRealChute)
                            {
                                availableRealChutes.Add(module);
                            }
                        }
                    }
                }

                listsInvalid = false;
            }
        }

        /// <summary>
        /// Refresh ablator-specific fields (hottest ablator and flux).
        /// </summary>
        private void FetchAblatorData()
        {
            heatShieldTemperature = heatShieldFlux = 0.0f;
            float hottestShield = float.MinValue;

            for (int i = 0; i < availableAblators.Count; ++i)
            {
                Part thatPart = availableAblators[i].part;
                // Even though the interior contains a lot of heat, I think ablation is based on skin temp.
                // Although it seems odd that the skin temp quickly cools off after re-entry, while the
                // interior temp doesn't move cool much (for instance, I saw a peak ablator skin temp
                // of 950K, while the interior eventually reached 345K after the ablator had cooled below
                // 390K.  By the time the capsule landed, skin temp matched exterior temp (304K) but the
                // interior still held 323K.
                if (thatPart.skinTemperature - availableAblators[i].ablationTempThresh > hottestShield)
                {
                    hottestShield = (float)(thatPart.skinTemperature - availableAblators[i].ablationTempThresh);
                    heatShieldTemperature = (float)(thatPart.skinTemperature);
                    heatShieldFlux = (float)(thatPart.thermalConvectionFlux + thatPart.thermalRadiationFlux);
                }

            }
        }

        /// <summary>
        /// Refresh airflow rate (g/s).
        /// </summary>
        private void FetchAirIntakeData()
        {
            currentAirFlow = 0.0f;

            for (int i = 0; i < availableAirIntakes.Count; ++i)
            {
                if (availableAirIntakes[i].enabled)
                {
                    currentAirFlow += availableAirIntakes[i].airFlow;
                }
            }

            // Convert airflow from U to g/s, same as fuel flow.
            currentAirFlow *= IntakeAir_U_to_grams;
        }

        /// <summary>
        /// Refresh electric data - generators active, solar panels deployable
        /// and retractable, output of each category of power production
        /// (generator, fuel cell/resource converter, alternator, and solar).
        /// </summary>
        private void FetchElectricData()
        {
            if (availableAlternators.Count > 0)
            {
                alternatorOutput = 0.0f;
            }
            else
            {
                alternatorOutput = -1.0f;
            }
            if (availableFuelCells.Count > 0)
            {
                fuelcellOutput = 0.0f;
            }
            else
            {
                fuelcellOutput = -1.0f;
            }
            if (availableGenerators.Count > 0)
            {
                generatorOutput = 0.0f;
            }
            else
            {
                generatorOutput = -1.0f;
            }
            if (availableSolarPanels.Count > 0)
            {
                solarOutput = 0.0f;
            }
            else
            {
                solarOutput = -1.0f;
            }

            generatorsActive = false;
            solarPanelsDeployable = solarPanelsRetractable = solarPanelsState = false;
            solarPanelMovement = -1;

            for (int i = 0; i < availableGenerators.Count; ++i)
            {
                generatorsActive |= (availableGenerators[i].generatorIsActive && !availableGenerators[i].isAlwaysActive);

                if (availableGenerators[i].generatorIsActive)
                {
                    float output = availableGenerators[i].efficiency * availableGeneratorOutput[i];
                    if (availableGenerators[i].isThrottleControlled)
                    {
                        output *= availableGenerators[i].throttle;
                    }
                    generatorOutput += output;
                }
            }

            for (int i = 0; i < availableFuelCells.Count; ++i)
            {
                generatorsActive |= (availableFuelCells[i].IsActivated && !availableFuelCells[i].AlwaysActive);

                if (availableFuelCells[i].IsActivated)
                {
                    fuelcellOutput += (float)availableFuelCells[i].lastTimeFactor * availableFuelCellOutput[i];
                }
            }

            for (int i = 0; i < availableAlternators.Count; ++i)
            {
                // I assume there's only one ElectricCharge output in a given ModuleAlternator
                alternatorOutput += availableAlternatorOutput[i] * availableAlternators[i].outputRate;
            }

            for (int i = 0; i < availableSolarPanels.Count; ++i)
            {
                solarOutput += availableSolarPanels[i].flowRate;
                solarPanelsRetractable |= (availableSolarPanels[i].useAnimation && availableSolarPanels[i].retractable && availableSolarPanels[i].panelState == ModuleDeployableSolarPanel.panelStates.EXTENDED);
                solarPanelsDeployable |= (availableSolarPanels[i].useAnimation && availableSolarPanels[i].panelState == ModuleDeployableSolarPanel.panelStates.RETRACTED);
                solarPanelsState |= (availableSolarPanels[i].useAnimation && (availableSolarPanels[i].panelState == ModuleDeployableSolarPanel.panelStates.EXTENDED || availableSolarPanels[i].panelState == ModuleDeployableSolarPanel.panelStates.EXTENDING));

                if ((solarPanelMovement == -1 || solarPanelMovement == (int)ModuleDeployableSolarPanel.panelStates.BROKEN) && availableSolarPanels[i].useAnimation)
                {
                    solarPanelMovement = (int)availableSolarPanels[i].panelState;
                }
            }
        }

        /// <summary>
        /// Convert the textual docking node state into an enum, so we don't
        /// need to do string compares.
        /// </summary>
        /// <param name="whichNode"></param>
        /// <returns></returns>
        internal static DockingNodeState GetNodeState(ModuleDockingNode whichNode)
        {
            if (whichNode == null)
            {
                return DockingNodeState.UNKNOWN;
            }

            switch (whichNode.state)
            {
                case "PreAttached":
                    return DockingNodeState.PREATTACHED;
                case "Docked (docker)":
                    return DockingNodeState.DOCKED;
                case "Docked (dockee)":
                    return DockingNodeState.DOCKED;
                case "Ready":
                    return DockingNodeState.READY;
                default:
                    return DockingNodeState.UNKNOWN;
            }
        }

        /// <summary>
        /// Refresh docking node data, including selecting the "reference"
        /// docking node (for docking node control).
        /// </summary>
        private void FetchDockingNodeData()
        {
            mainDockingNode = null;
            mainDockingNodeState = DockingNodeState.UNKNOWN;

            Part referencePart = vessel.GetReferenceTransformPart();
            if (referencePart != null)
            {
                ModuleDockingNode node = referencePart.FindModuleImplementing<ModuleDockingNode>();
                if (node != null)
                {
                    // The current reference part is a docking node, so we
                    // choose it.
                    mainDockingNode = node;
                }
            }

            if (mainDockingNode == null)
            {
                uint launchId;
                Part currentPart = JUtil.DeduceCurrentPart(vessel);
                if (currentPart == null)
                {
                    launchId = 0u;
                }
                else
                {
                    launchId = currentPart.launchID;
                }

                for (int i = 0; i < vessel.parts.Count; ++i)
                {
                    if (vessel.parts[i].launchID == launchId)
                    {
                        ModuleDockingNode node = vessel.parts[i].FindModuleImplementing<ModuleDockingNode>();
                        if (node != null)
                        {
                            // We found a docking node that has the same launch
                            // ID as the current IVA part, so we consider it our
                            // main docking node.
                            mainDockingNode = node;
                            break;
                        }
                    }
                }
            }

            mainDockingNodeState = GetNodeState(mainDockingNode);
        }

        /// <summary>
        /// Refresh engine data: current thrust, max thrust, max raw
        /// thrust (without throttle limits), current and max fuel flow,
        /// hottest engine temperature and limit, current and max ISP,
        /// boolean flags of engine states.
        /// </summary>
        /// <returns>true if something changed that warrants a reset
        /// of tracked modules.</returns>
        private bool FetchEngineData()
        {
            if (availableMultiModeEngines.Count == 0)
            {
                anyMmePrimary = true;
            }
            else
            {
                anyMmePrimary = false;
                for (int i = 0; i < availableMultiModeEngines.Count; ++i)
                {
                    if (availableMultiModeEngines[i].runningPrimary)
                    {
                        anyMmePrimary = true;
                    }
                }
            }

            // Per-engine values
            currentEngineCount = 0;
            totalCurrentThrust = totalLimitedMaximumThrust = totalRawMaximumThrust = 0.0f;
            maxEngineFuelFlow = currentEngineFuelFlow = 0.0f;
            float hottestEngine = float.MaxValue;
            hottestEngineTemperature = hottestEngineMaxTemperature = 0.0f;
            anyEnginesOverheating = anyEnginesFlameout = anyEnginesEnabled = false;

            float averageIspContribution = 0.0f;
            float maxIspContribution = 0.0f;
            List<Part> visitedParts = new List<Part>();

            bool requestReset = false;
            for (int i = 0; i < availableEngines.Count; ++i)
            {
                requestReset |= (!availableEngines[i].isEnabled);

                Part thatPart = availableEngines[i].part;
                if (thatPart.inverseStage == StageManager.CurrentStage)
                {
                    if (!visitedParts.Contains(thatPart))
                    {
                        currentEngineCount++;
                        if (availableEngines[i].getIgnitionState)
                        {
                            activeEngineCount++;
                        }
                        visitedParts.Add(thatPart);
                    }
                }

                anyEnginesOverheating |= (thatPart.skinTemperature / thatPart.skinMaxTemp > 0.9) || (thatPart.temperature / thatPart.maxTemp > 0.9);
                anyEnginesEnabled |= availableEngines[i].allowShutdown && availableEngines[i].getIgnitionState;
                anyEnginesFlameout |= (availableEngines[i].isActiveAndEnabled && availableEngines[i].flameout);

                float currentThrust = GetCurrentThrust(availableEngines[i]);
                totalCurrentThrust += currentThrust;
                float rawMaxThrust = GetMaximumThrust(availableEngines[i]);
                totalRawMaximumThrust += rawMaxThrust;
                float maxThrust = rawMaxThrust * availableEngines[i].thrustPercentage * 0.01f;
                totalLimitedMaximumThrust += maxThrust;
                float realIsp = GetRealIsp(availableEngines[i]);
                if (realIsp > 0.0f)
                {
                    averageIspContribution += maxThrust / realIsp;

                    // Compute specific fuel consumption and
                    // multiply by thrust to get grams/sec fuel flow
                    float specificFuelConsumption = 101972f / realIsp;
                    maxEngineFuelFlow += specificFuelConsumption * rawMaxThrust;
                    currentEngineFuelFlow += specificFuelConsumption * currentThrust;
                }

                foreach (Propellant thatResource in availableEngines[i].propellants)
                {
                    resources.MarkPropellant(thatResource);
                }

                float minIsp, maxIsp;
                availableEngines[i].atmosphereCurve.FindMinMaxValue(out minIsp, out maxIsp);
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

            return requestReset;
        }

        /// <summary>
        /// Refresh gimbal data: any gimbals locked.
        /// </summary>
        private void FetchGimbalData()
        {
            gimbalsLocked = false;

            for (int i = 0; i < availableGimbals.Count; ++i)
            {
                gimbalsLocked |= availableGimbals[i].gimbalLock;
            }
        }

        /// <summary>
        /// Refresh parachute data
        /// </summary>
        private void FetchParachuteData()
        {
            anyParachutesDeployed = false;
            allParachutesSafe = true;

            for (int i = 0; i < availableParachutes.Count; ++i)
            {
                if (availableParachutes[i].deploymentState == ModuleParachute.deploymentStates.SEMIDEPLOYED || availableParachutes[i].deploymentState == ModuleParachute.deploymentStates.DEPLOYED)
                {
                    anyParachutesDeployed = true;
                }

                if (availableParachutes[i].deploySafe != "Safe")
                {
                    allParachutesSafe = false;
                }
            }
        }

        /// <summary>
        /// Refresh radar data: any radar active.
        /// </summary>
        private void FetchRadarData()
        {
            radarActive = false;

            for (int i = 0; i < availableRadars.Count; ++i)
            {
                radarActive |= availableRadars[i].radarEnabled;
            }
        }

        /// <summary>
        /// Refresh wheel data: current landing gear deployment state, wheel
        /// damage state, brake settings.
        /// </summary>
        private void FetchWheelData()
        {
            gearState = -1;
            gearPosition = 0.0f;

            for (int i = 0; i < availableDeployableWheels.Count; ++i)
            {
                if (gearState == -1 || gearState == 4)
                {
                    try
                    {
                        gearPosition = UnityEngine.Mathf.Lerp(availableDeployableWheels[i].retractedPosition, availableDeployableWheels[i].deployedPosition, availableDeployableWheels[i].position);
                        var state = availableDeployableWheels[i].fsm.CurrentState;
                        if (state == availableDeployableWheels[i].st_deployed)
                        {
                            gearState = 1;
                        }
                        else if (state == availableDeployableWheels[i].st_retracted)
                        {
                            gearState = 0;
                        }
                        else if (state == availableDeployableWheels[i].st_deploying)
                        {
                            gearState = 2;
                        }
                        else if (state == availableDeployableWheels[i].st_retracting)
                        {
                            gearState = 3;
                        }
                        else if (state == availableDeployableWheels[i].st_inoperable)
                        {
                            gearState = 4;
                        }
                    }
                    catch { }
                }
            }

            wheelsDamaged = wheelsRepairable = false;
            wheelStress = 0.0f;

            for (int i = 0; i < availableWheelDamage.Count; ++i)
            {
                wheelsDamaged |= availableWheelDamage[i].isDamaged;
                wheelsRepairable |= availableWheelDamage[i].isRepairable;
                wheelStress = Math.Max(wheelStress, availableWheelDamage[i].stressPercent);
            }

            wheelBrakeSetting = 0.0f;
            if (availableWheelBrakes.Count > 0)
            {
                for (int i = 0; i < availableWheelBrakes.Count; ++i)
                {
                    wheelBrakeSetting += availableWheelBrakes[i].brakeTweakable;
                }
                wheelBrakeSetting /= (float)availableWheelBrakes.Count;
            }
        }

        /// <summary>
        /// Master update method.
        /// </summary>
        internal void FetchPerModuleData()
        {
            if (vessel == null)
            {
                return;
            }

            UpdateModuleLists();

            bool requestReset = false;
            FetchAblatorData();
            FetchAirIntakeData();
            FetchDockingNodeData();
            FetchElectricData();
            requestReset |= FetchEngineData();
            FetchGimbalData();
            FetchParachuteData();
            FetchRadarData();
            FetchWheelData();

            if (requestReset)
            {
                InvalidateModuleLists();
            }
        }
        #endregion

        #region Interface

        /// <summary>
        /// Toggle the state of any engines that we can control (currently-staged
        /// engines, or engines that are on if we are turning them off).
        /// </summary>
        /// <param name="state"></param>
        internal void SetEnableEngines(bool state)
        {
            for (int i = 0; i < availableEngines.Count; ++i)
            {
                Part thatPart = availableEngines[i].part;

                if (thatPart.inverseStage == StageManager.CurrentStage || !state)
                {
                    if (availableEngines[i].EngineIgnited != state)
                    {
                        if (state && availableEngines[i].allowRestart)
                        {
                            availableEngines[i].Activate();
                        }
                        else if (availableEngines[i].allowShutdown)
                        {
                            availableEngines[i].Shutdown();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Toggle the state of any generators or resource converters that can
        /// be toggled.
        /// </summary>
        /// <param name="state"></param>
        internal void SetEnableGenerators(bool state)
        {
            for (int i = 0; i < availableGenerators.Count; ++i)
            {
                if (!availableGenerators[i].isAlwaysActive)
                {
                    if (state)
                    {
                        availableGenerators[i].Activate();
                    }
                    else
                    {
                        availableGenerators[i].Shutdown();
                    }
                }
            }

            for (int i = 0; i < availableFuelCells.Count; ++i)
            {
                if (!availableFuelCells[i].AlwaysActive)
                {
                    if (state)
                    {
                        availableFuelCells[i].StartResourceConverter();
                    }
                    else
                    {
                        availableFuelCells[i].StopResourceConverter();
                    }
                }
            }
        }

        /// <summary>
        /// Deploy and retract (where applicable) deployable solar panels.
        /// </summary>
        /// <param name="state"></param>
        internal void SetDeploySolarPanels(bool state)
        {
            if (state)
            {
                for (int i = 0; i < availableSolarPanels.Count; ++i)
                {
                    if (availableSolarPanels[i].useAnimation && availableSolarPanels[i].panelState == ModuleDeployableSolarPanel.panelStates.RETRACTED)
                    {
                        availableSolarPanels[i].Extend();
                    }
                }
            }
            else
            {
                for (int i = 0; i < availableSolarPanels.Count; ++i)
                {
                    if (availableSolarPanels[i].useAnimation && availableSolarPanels[i].retractable && availableSolarPanels[i].panelState == ModuleDeployableSolarPanel.panelStates.EXTENDED)
                    {
                        availableSolarPanels[i].Retract();
                    }
                }
            }
        }

        #endregion
    }
}
