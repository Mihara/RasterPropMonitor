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

        //--- Engines
        internal List<ModuleEngines> availableEngines = new List<ModuleEngines>();
        internal float totalCurrentThrust;
        internal float totalLimitedMaximumThrust;
        internal float totalRawMaximumThrust;
        internal float maxEngineFuelFlow;
        internal float currentEngineFuelFlow;
        internal bool anyEnginesFlameout;
        internal bool anyEnginesOverheating;
        internal bool anyEnginesEnabled;

        //--- Heat shields
        internal List<ModuleAblator> availableAblators = new List<ModuleAblator>();
        internal float heatShieldTemperature;
        internal float heatShieldFlux;

        //--- Intake air
        internal List<ModuleResourceIntake> availableAirIntakes = new List<ModuleResourceIntake>();
        internal float currentAirFlow;
        private static float IntakeAir_U_to_grams;

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
        internal float alternatorOutput;
        internal float fuelcellOutput;
        internal float generatorOutput;
        internal float solarOutput;

        #region List Management
        internal void InvalidateModuleLists()
        {
            listsInvalid = true;

            availableAblators.Clear();
            availableAirIntakes.Clear();
            availableAlternators.Clear();
            availableAlternatorOutput.Clear();
            availableEngines.Clear();
            availableFuelCells.Clear();
            availableFuelCellOutput.Clear();
            availableGenerators.Clear();
            availableSolarPanels.Clear();
        }

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
                        }
                    }
                }

                listsInvalid = false;
            }
        }

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

        private void FetchElectricData()
        {
            solarOutput = fuelcellOutput = generatorOutput = alternatorOutput = 0.0f;
            generatorsActive = false;
            solarPanelsDeployable = solarPanelsRetractable = false;

            for (int i = 0; i < availableGenerators.Count; ++i)
            {
                generatorsActive |= (availableGenerators[i].generatorIsActive && !availableGenerators[i].isAlwaysActive);

                float output = availableGenerators[i].efficiency * availableGeneratorOutput[i];
                if (availableGenerators[i].isThrottleControlled)
                {
                    output *= availableGenerators[i].throttle;
                }
                generatorOutput += output;
            }

            for (int i = 0; i < availableFuelCells.Count; ++i)
            {
                generatorsActive |= (availableFuelCells[i].IsActivated && !availableFuelCells[i].AlwaysActive);
                // TODO: Figure out how much energy is being generated (instantaneous rate)
                //var recipe = availableFuelCells[i].Recipe;
                //JUtil.LogMessage(this, "fuelcell efficiency {0}, out {1}", availableFuelCells[i].Efficiency, availableFuelCellOutput[i]);
                //JUtil.LogMessage(this, "fuelcell recipe: TakeAmount {0}, FillAmount {1}", recipe.TakeAmount, recipe.FillAmount);
                //for (int j = 0; j < recipe.Outputs.Count; ++j)
                //{
                //if (recipe.Outputs[j].ResourceName == "ElectricCharge")
                //{
                //    //JUtil.LogMessage(this, "fuelcell recipe: Ratio {0}", recipe.Outputs[j].Ratio);
                //}
                //}
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
            }
        }

        private void FetchEngineData()
        {
            // Per-engine values
            totalCurrentThrust = totalLimitedMaximumThrust = totalRawMaximumThrust = 0.0f;
            maxEngineFuelFlow = currentEngineFuelFlow = 0.0f;
            float hottestEngine = float.MaxValue;
            hottestEngineTemperature = hottestEngineMaxTemperature = 0.0f;
            anyEnginesOverheating = anyEnginesFlameout = anyEnginesEnabled = false;

            float averageIspContribution = 0.0f;
            float maxIspContribution = 0.0f;

            for (int i = 0; i < availableEngines.Count; ++i)
            {
                Part thatPart = availableEngines[i].part;
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
        }

        internal void FetchPerModuleData()
        {
            if (vessel == null)
            {
                return;
            }

            UpdateModuleLists();

            FetchAblatorData();
            FetchAirIntakeData();
            FetchElectricData();
            FetchEngineData();
        }
        #endregion

        #region Interface

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
