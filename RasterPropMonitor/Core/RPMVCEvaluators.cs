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
using UnityEngine;

namespace JSI
{
    public partial class RPMVesselComputer : VesselModule
    {
        // Plugin-modifiable Evaluators
        //private Func<double> evaluateAngleOfAttack;
        //private Func<double> evaluateSideSlip;
        //private Func<double> evaluateTerminalVelocity;
        //private Func<double> evaluateTimeToImpact;

        ////--- Plugin-enabled evaluators
        //#region PluginEvaluators
        //private float EvaluateAngleOfAttack()
        //{
        //    if (evaluateAngleOfAttack == null)
        //    {
        //        Func<double> accessor = null;

        //        accessor = (Func<double>)GetInternalMethod("JSIFAR:GetAngleOfAttack", typeof(Func<double>));
        //        if (accessor != null)
        //        {
        //            double value = accessor();
        //            if (double.IsNaN(value))
        //            {
        //                accessor = null;
        //            }
        //        }

        //        if (accessor == null)
        //        {
        //            evaluateAngleOfAttack = FallbackEvaluateAngleOfAttack;
        //        }
        //        else
        //        {
        //            evaluateAngleOfAttack = accessor;
        //        }
        //    }

        //    return (float)evaluateAngleOfAttack();
        //}

        //private VariableEvaluator AngleOfAttack()
        //{
        //    Func<double> accessor = null;

        //    accessor = (Func<double>)GetInternalMethod("JSIFAR:GetAngleOfAttack", typeof(Func<double>));
        //    if (accessor != null)
        //    {
        //        double value = accessor();
        //        if (double.IsNaN(value))
        //        {
        //            accessor = null;
        //        }
        //    }

        //    if (accessor == null)
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return FallbackEvaluateAngleOfAttack(); };
        //    }
        //    else
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor(); };
        //    }
        //}

        //private VariableEvaluator DeltaV()
        //{
        //    Func<double> accessor = null;

        //    accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetDeltaV", typeof(Func<double>));
        //    if (accessor != null)
        //    {
        //        double value = accessor();
        //        if (double.IsNaN(value))
        //        {
        //            accessor = null;
        //        }
        //    }

        //    if (accessor == null)
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return (actualAverageIsp * gee) * Math.Log(totalShipWetMass / (totalShipWetMass - resources.PropellantMass(false))); };
        //    }
        //    else
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor(); };
        //    }
        //}

        //private VariableEvaluator DeltaVStage()
        //{
        //    Func<double> accessor = null;

        //    accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetStageDeltaV", typeof(Func<double>));
        //    if (accessor != null)
        //    {
        //        double value = accessor();
        //        if (double.IsNaN(value))
        //        {
        //            accessor = null;
        //        }
        //    }

        //    if (accessor == null)
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return (actualAverageIsp * gee) * Math.Log(totalShipWetMass / (totalShipWetMass - resources.PropellantMass(true))); };
        //    }
        //    else
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor(); };
        //    }
        //}

        //private VariableEvaluator DragAccel()
        //{
        //    Func<double> accessor = null;

        //    accessor = (Func<double>)GetInternalMethod("JSIFAR:GetDragForce", typeof(Func<double>));
        //    if (accessor != null)
        //    {
        //        double value = accessor();
        //        if (double.IsNaN(value))
        //        {
        //            accessor = null;
        //        }
        //    }

        //    if (accessor == null)
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return FallbackEvaluateDragForce() / totalShipWetMass; };
        //    }
        //    else
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor() / totalShipWetMass; };
        //    }
        //}

        //private VariableEvaluator DragForce()
        //{
        //    Func<double> accessor = null;

        //    accessor = (Func<double>)GetInternalMethod("JSIFAR:GetDragForce", typeof(Func<double>));
        //    if (accessor != null)
        //    {
        //        double value = accessor();
        //        if (double.IsNaN(value))
        //        {
        //            accessor = null;
        //        }
        //    }

        //    if (accessor == null)
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return FallbackEvaluateDragForce(); };
        //    }
        //    else
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor(); };
        //    }
        //}

        //private VariableEvaluator DynamicPressure()
        //{
        //    Func<double> accessor = null;

        //    accessor = (Func<double>)GetInternalMethod("JSIFAR:GetDynamicPressure", typeof(Func<double>));
        //    if (accessor != null)
        //    {
        //        double value = accessor();
        //        if (double.IsNaN(value))
        //        {
        //            accessor = null;
        //        }
        //    }

        //    if (accessor == null)
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return vessel.dynamicPressurekPa; };
        //    }
        //    else
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor(); };
        //    }
        //}

        //private VariableEvaluator LandingError()
        //{
        //    Func<double> accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingError", typeof(Func<double>));

        //    return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor(); };
        //}

        //private VariableEvaluator LandingAltitude()
        //{
        //    Func<double> accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingAltitude", typeof(Func<double>));

        //    return (string variable, RasterPropMonitorComputer rpmComp) => 
        //    {
        //        double est = accessor();
        //        return (est == 0.0) ? estLandingAltitude : est;
        //    };
        //}

        //private VariableEvaluator LandingLatitude()
        //{
        //    Func<double> accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingLatitude", typeof(Func<double>));

        //    return (string variable, RasterPropMonitorComputer rpmComp) =>
        //    {
        //        double est = accessor();
        //        return (est == 0.0) ? estLandingLatitude : est;
        //    };
        //}

        //private VariableEvaluator LandingLongitude()
        //{
        //    Func<double> accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingLongitude", typeof(Func<double>));

        //    return (string variable, RasterPropMonitorComputer rpmComp) =>
        //    {
        //        double est = accessor();
        //        return (est == 0.0) ? estLandingLongitude : est;
        //    };
        //}

        //private VariableEvaluator LiftAccel()
        //{
        //    Func<double> accessor = null;

        //    accessor = (Func<double>)GetInternalMethod("JSIFAR:GetLiftForce", typeof(Func<double>));
        //    if (accessor != null)
        //    {
        //        double value = accessor();
        //        if (double.IsNaN(value))
        //        {
        //            accessor = null;
        //        }
        //    }

        //    if (accessor == null)
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return FallbackEvaluateLiftForce() / totalShipWetMass; };
        //    }
        //    else
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor() / totalShipWetMass; };
        //    }
        //}

        //private VariableEvaluator LiftForce()
        //{
        //    Func<double> accessor = null;

        //    accessor = (Func<double>)GetInternalMethod("JSIFAR:GetLiftForce", typeof(Func<double>));
        //    if (accessor != null)
        //    {
        //        double value = accessor();
        //        if (double.IsNaN(value))
        //        {
        //            accessor = null;
        //        }
        //    }

        //    if (accessor == null)
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return FallbackEvaluateLiftForce(); };
        //    }
        //    else
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor(); };
        //    }
        //}

        //private VariableEvaluator MechJebAvailable()
        //{
        //    Func<bool> accessor = null;

        //    accessor = (Func<bool>)GetInternalMethod("JSIMechJeb:GetMechJebAvailable", typeof(Func<bool>));
        //    if (accessor == null)
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return false; };
        //    }
        //    else
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor().GetHashCode(); };
        //    }
        //}

        //private float EvaluateSideSlip()
        //{
        //    if (evaluateSideSlip == null)
        //    {
        //        Func<double> accessor = null;

        //        accessor = (Func<double>)GetInternalMethod("JSIFAR:GetSideSlip", typeof(Func<double>));
        //        if (accessor != null)
        //        {
        //            double value = accessor();
        //            if (double.IsNaN(value))
        //            {
        //                accessor = null;
        //            }
        //        }

        //        if (accessor == null)
        //        {
        //            evaluateSideSlip = FallbackEvaluateSideSlip;
        //        }
        //        else
        //        {
        //            evaluateSideSlip = accessor;
        //        }
        //    }

        //    return (float)evaluateSideSlip();
        //}

        //private VariableEvaluator SideSlip()
        //{
        //    Func<double> accessor = null;

        //    accessor = (Func<double>)GetInternalMethod("JSIFAR:GetSideSlip", typeof(Func<double>));
        //    if (accessor != null)
        //    {
        //        double value = accessor();
        //        if (double.IsNaN(value))
        //        {
        //            accessor = null;
        //        }
        //    }

        //    if (accessor == null)
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return FallbackEvaluateSideSlip(); };
        //    }
        //    else
        //    {
        //        return (string variable, RasterPropMonitorComputer rpmComp) => { return accessor(); };
        //    }
        //}

        //private double TerminalVelocity()
        //{
        //    if (evaluateTerminalVelocity == null)
        //    {
        //        Func<double> accessor = null;

        //        accessor = (Func<double>)GetInternalMethod("JSIFAR:GetTerminalVelocity", typeof(Func<double>));
        //        if (accessor != null)
        //        {
        //            double value = accessor();
        //            if (value < 0.0)
        //            {
        //                accessor = null;
        //            }
        //        }

        //        if (accessor == null)
        //        {
        //            accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetTerminalVelocity", typeof(Func<double>));
        //            double value = accessor();
        //            if (double.IsNaN(value))
        //            {
        //                accessor = null;
        //            }
        //        }

        //        if (accessor == null)
        //        {
        //            accessor = FallbackEvaluateTerminalVelocity;
        //        }

        //        evaluateTerminalVelocity = accessor;
        //    }

        //    return evaluateTerminalVelocity();
        //}

        //private double TimeToImpact()
        //{
        //    if (evaluateTimeToImpact == null)
        //    {
        //        Func<double> accessor = null;

        //        if (accessor == null)
        //        {
        //            accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingTime", typeof(Func<double>));
        //            double value = accessor();
        //            if (double.IsNaN(value))
        //            {
        //                accessor = null;
        //            }
        //        }

        //        if (accessor == null)
        //        {
        //            accessor = FallbackEvaluateTimeToImpact;
        //        }

        //        evaluateTimeToImpact = accessor;
        //    }

        //    double timeToImpact = evaluateTimeToImpact();
        //    if (double.IsNaN(timeToImpact) || timeToImpact > 365.0 * 24.0 * 60.0 * 60.0 || timeToImpact < 0.0)
        //    {
        //        timeToImpact = -1.0;
        //    }
        //    else if(timeToImpact == 0.0)
        //    {
        //        return estLandingUT - Planetarium.GetUniversalTime();
        //    }
        //    return timeToImpact;
        //}
        //#endregion

        //--- Fallback evaluators
        #region FallbackEvaluators
        internal double FallbackEvaluateAngleOfAttack()
        {
            // Code courtesy FAR.
            Transform refTransform = vessel.GetTransform();
            Vector3 velVectorNorm = vessel.srf_velocity.normalized;

            Vector3 tmpVec = refTransform.up * Vector3.Dot(refTransform.up, velVectorNorm) + refTransform.forward * Vector3.Dot(refTransform.forward, velVectorNorm);   //velocity vector projected onto a plane that divides the airplane into left and right halves
            float AoA = Vector3.Dot(tmpVec.normalized, refTransform.forward);
            AoA = Mathf.Rad2Deg * Mathf.Asin(AoA);
            if (float.IsNaN(AoA))
            {
                AoA = 0.0f;
            }

            return (double)AoA;
        }

        internal double FallbackEvaluateDragForce()
        {
            // Equations based on https://github.com/NathanKell/AeroGUI/blob/master/AeroGUI/AeroGUI.cs and MechJeb.
            double dragForce = 0.0;

            if (altitudeASL < vessel.mainBody.RealMaxAtmosphereAltitude())
            {
                Vector3 pureDragV = Vector3.zero, pureLiftV = Vector3.zero;

                for (int i = 0; i < vessel.parts.Count; i++)
                {
                    Part p = vessel.parts[i];

                    pureDragV += -p.dragVectorDir * p.dragScalar;

                    if (!p.hasLiftModule)
                    {
                        Vector3 bodyLift = p.transform.rotation * (p.bodyLiftScalar * p.DragCubes.LiftForce);
                        bodyLift = Vector3.ProjectOnPlane(bodyLift, -p.dragVectorDir);
                        pureLiftV += bodyLift;

                        for (int m = 0; m < p.Modules.Count; m++)
                        {
                            PartModule pm = p.Modules[m];
                            if (pm.isEnabled && pm is ModuleLiftingSurface)
                            {
                                ModuleLiftingSurface liftingSurface = pm as ModuleLiftingSurface;
                                if (!p.ShieldedFromAirstream)
                                {
                                    pureLiftV += liftingSurface.liftForce;
                                    pureDragV += liftingSurface.dragForce;
                                }
                            }
                        }
                    }
                }

                // Per NathanKell here http://forum.kerbalspaceprogram.com/threads/125746-Drag-Api?p=2029514&viewfull=1#post2029514
                // drag is in kN.  Divide by wet mass to get m/s^2 acceleration
                Vector3 force = pureDragV + pureLiftV;
                dragForce = Vector3.Dot(force, -vessel.srf_velocity.normalized);
            }

            return dragForce;
        }

        internal double FallbackEvaluateSideSlip()
        {
            // Code courtesy FAR.
            Transform refTransform = vessel.GetTransform();
            Vector3 velVectorNorm = vessel.srf_velocity.normalized;

            Vector3 tmpVec = refTransform.up * Vector3.Dot(refTransform.up, velVectorNorm) + refTransform.right * Vector3.Dot(refTransform.right, velVectorNorm);     //velocity vector projected onto the vehicle-horizontal plane
            float sideslipAngle = Vector3.Dot(tmpVec.normalized, refTransform.right);
            sideslipAngle = Mathf.Rad2Deg * Mathf.Asin(sideslipAngle);
            if (float.IsNaN(sideslipAngle))
            {
                sideslipAngle = 0.0f;
            }

            return (double)sideslipAngle;
        }

        internal double FallbackEvaluateLiftForce()
        {
            double liftForce = 0.0;

            if (altitudeASL < vessel.mainBody.RealMaxAtmosphereAltitude())
            {
                Vector3 pureDragV = Vector3.zero, pureLiftV = Vector3.zero;

                for (int i = 0; i < vessel.parts.Count; i++)
                {
                    Part p = vessel.parts[i];

                    pureDragV += -p.dragVectorDir * p.dragScalar;

                    if (!p.hasLiftModule)
                    {
                        Vector3 bodyLift = p.transform.rotation * (p.bodyLiftScalar * p.DragCubes.LiftForce);
                        bodyLift = Vector3.ProjectOnPlane(bodyLift, -p.dragVectorDir);
                        pureLiftV += bodyLift;

                        for (int m = 0; m < p.Modules.Count; m++)
                        {
                            PartModule pm = p.Modules[m];
                            if (pm.isEnabled && pm is ModuleLiftingSurface)
                            {
                                ModuleLiftingSurface liftingSurface = pm as ModuleLiftingSurface;
                                if (!p.ShieldedFromAirstream)
                                {
                                    pureLiftV += liftingSurface.liftForce;
                                    pureDragV += liftingSurface.dragForce;
                                }
                            }
                        }
                    }
                }

                Vector3 force = pureDragV + pureLiftV;
                Vector3 liftDir = -Vector3.Cross(vessel.transform.right, vessel.srf_velocity.normalized);
                liftForce = Vector3.Dot(force, liftDir);
            }

            return liftForce;
        }

        internal double FallbackEvaluateTerminalVelocity()
        {
            // Terminal velocity computation based on MechJeb 2.5.1 or one of the later snapshots
            if (altitudeASL > vessel.mainBody.RealMaxAtmosphereAltitude())
            {
                return float.PositiveInfinity;
            }

            Vector3 pureDragV = Vector3.zero, pureLiftV = Vector3.zero;

            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part p = vessel.parts[i];

                pureDragV += -p.dragVectorDir * p.dragScalar;

                if (!p.hasLiftModule)
                {
                    Vector3 bodyLift = p.transform.rotation * (p.bodyLiftScalar * p.DragCubes.LiftForce);
                    bodyLift = Vector3.ProjectOnPlane(bodyLift, -p.dragVectorDir);
                    pureLiftV += bodyLift;

                    for (int m = 0; m < p.Modules.Count; m++)
                    {
                        PartModule pm = p.Modules[m];
                        if (!pm.isEnabled)
                        {
                            continue;
                        }

                        if (pm is ModuleLiftingSurface)
                        {
                            ModuleLiftingSurface liftingSurface = (ModuleLiftingSurface)pm;
                            if (p.ShieldedFromAirstream)
                                continue;
                            pureLiftV += liftingSurface.liftForce;
                            pureDragV += liftingSurface.dragForce;
                        }
                    }
                }
            }

            // Why?
            pureDragV = pureDragV / totalShipWetMass;
            pureLiftV = pureLiftV / totalShipWetMass;

            Vector3 force = pureDragV + pureLiftV;
            double drag = Vector3.Dot(force, -vessel.srf_velocity.normalized);

            return Math.Sqrt(localGeeDirect / drag) * vessel.srfSpeed;
        }

        /// <summary>
        /// Estimates the number of seconds before impact.  It's not precise,
        /// since precise is also computationally expensive.
        /// </summary>
        /// <returns></returns>
        internal double FallbackEvaluateTimeToImpact()
        {
            double secondsToImpact;
            if (vessel.situation == Vessel.Situations.SUB_ORBITAL || vessel.situation == Vessel.Situations.FLYING)
            {
                // Mental note: the local g taken from vessel.mainBody.GeeASL will suffice.
                //  t = (v+sqrt(v²+2gd))/g or something.

                // What is the vertical component of current acceleration?
                double accelUp = Vector3d.Dot(vessel.acceleration, up);

                double altitude = altitudeTrue;
                if (vessel.mainBody.ocean && altitudeASL > 0.0)
                {
                    // AltitudeTrue shows distance above the floor of the ocean,
                    // so use ASL if it's closer in this case, and we're not
                    // already below SL.
                    altitude = Math.Min(altitudeASL, altitudeTrue);
                }

                if (accelUp < 0.0 || speedVertical >= 0.0 || Planetarium.TimeScale > 1.0)
                {
                    // If accelUp is negative, we can't use it in the general
                    // equation for finding time to impact, since it could
                    // make the term inside the sqrt go negative.
                    // If we're going up, we can use this as well, since
                    // the precision is not critical.
                    // If we are warping, accelUp is always zero, so if we
                    // do not use this case, we would fall to the simple
                    // formula, which is wrong.
                    secondsToImpact = (speedVertical + Math.Sqrt(speedVertical * speedVertical + 2 * localGeeASL * altitude)) / localGeeASL;
                }
                else if (accelUp > 0.005)
                {
                    // This general case takes into account vessel acceleration,
                    // so estimates on craft that include parachutes or do
                    // powered descents are more accurate.
                    secondsToImpact = (speedVertical + Math.Sqrt(speedVertical * speedVertical + 2 * accelUp * altitude)) / accelUp;
                }
                else
                {
                    // If accelUp is small, we get floating point precision
                    // errors that tend to make secondsToImpact get really big.
                    secondsToImpact = altitude / -speedVertical;
                }
            }
            else
            {
                secondsToImpact = Double.NaN;
            }

            return secondsToImpact;
        }
        #endregion
    }
}
