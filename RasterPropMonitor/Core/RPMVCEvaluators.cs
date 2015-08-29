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
        private Func<bool> evaluateMechJebAvailable;
        private Func<double> evaluateAngleOfAttack;
        private Func<double> evaluateDeltaV;
        private Func<double> evaluateDeltaVStage;
        private Func<double> evaluateDynamicPressure;
        private Func<double> evaluateLandingError;
        private Func<double> evaluateLandingAltitude;
        private Func<double> evaluateLandingLatitude;
        private Func<double> evaluateLandingLongitude;
        private Func<double> evaluateSideSlip;
        private Func<double> evaluateTerminalVelocity;

        //--- Plugin-enabled evaluators
        #region PluginEvaluators
        private double AngleOfAttack()
        {
            if (evaluateAngleOfAttack == null)
            {
                Func<double> accessor = null;

                accessor = (Func<double>)GetInternalMethod("JSIFAR:GetAngleOfAttack", typeof(Func<double>));
                if (accessor != null)
                {
                    double value = accessor();
                    if (double.IsNaN(value))
                    {
                        accessor = null;
                    }
                }

                if (accessor == null)
                {
                    accessor = FallbackEvaluateAngleOfAttack;
                }

                evaluateAngleOfAttack = accessor;
            }

            return evaluateAngleOfAttack();
        }

        private double DeltaV()
        {
            if (evaluateDeltaV == null)
            {
                Func<double> accessor = null;

                accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetDeltaV", typeof(Func<double>));
                if (accessor != null)
                {
                    double value = accessor();
                    if (double.IsNaN(value))
                    {
                        accessor = null;
                    }
                }

                if (accessor == null)
                {
                    accessor = FallbackEvaluateDeltaV;
                }

                evaluateDeltaV = accessor;
            }

            return evaluateDeltaV();
        }

        private double DeltaVStage()
        {
            if (evaluateDeltaVStage == null)
            {
                Func<double> accessor = null;

                accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetStageDeltaV", typeof(Func<double>));
                if (accessor != null)
                {
                    double value = accessor();
                    if (double.IsNaN(value))
                    {
                        accessor = null;
                    }
                }

                if (accessor == null)
                {
                    accessor = FallbackEvaluateDeltaVStage;
                }

                evaluateDeltaVStage = accessor;
            }

            return evaluateDeltaVStage();
        }

        private double DynamicPressure()
        {
            if (evaluateDynamicPressure == null)
            {
                Func<double> accessor = null;

                accessor = (Func<double>)GetInternalMethod("JSIFAR:GetDynamicPressure", typeof(Func<double>));
                if (accessor != null)
                {
                    double value = accessor();
                    if (double.IsNaN(value))
                    {
                        accessor = null;
                    }
                }

                if (accessor == null)
                {
                    accessor = FallbackEvaluateDynamicPressure;
                }

                evaluateDynamicPressure = accessor;
            }

            return evaluateDynamicPressure();
        }

        private double LandingError()
        {
            if (evaluateLandingError == null)
            {
                evaluateLandingError = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingError", typeof(Func<double>));
            }

            return evaluateLandingError();
        }

        private double LandingAltitude()
        {
            if (evaluateLandingAltitude == null)
            {
                evaluateLandingAltitude = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingAltitude", typeof(Func<double>));
            }

            return evaluateLandingAltitude();
        }

        private double LandingLatitude()
        {
            if (evaluateLandingLatitude == null)
            {
                evaluateLandingLatitude = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingLatitude", typeof(Func<double>));
            }

            return evaluateLandingLatitude();
        }

        private double LandingLongitude()
        {
            if (evaluateLandingLongitude == null)
            {
                evaluateLandingLongitude = (Func<double>)GetInternalMethod("JSIMechJeb:GetLandingLongitude", typeof(Func<double>));
            }

            return evaluateLandingLongitude();
        }

        private bool MechJebAvailable()
        {
            if (evaluateMechJebAvailable == null)
            {
                Func<bool> accessor = null;

                accessor = (Func<bool>)GetInternalMethod("JSIMechJeb:GetMechJebAvailable", typeof(Func<bool>));
                if (accessor == null)
                {
                    accessor = JUtil.ReturnFalse;
                }

                evaluateMechJebAvailable = accessor;
            }

            return evaluateMechJebAvailable();
        }

        private double SideSlip()
        {
            if (evaluateSideSlip == null)
            {
                Func<double> accessor = null;

                accessor = (Func<double>)GetInternalMethod("JSIFAR:GetSideSlip", typeof(Func<double>));
                if (accessor != null)
                {
                    double value = accessor();
                    if (double.IsNaN(value))
                    {
                        accessor = null;
                    }
                }

                if (accessor == null)
                {
                    accessor = FallbackEvaluateSideSlip;
                }

                evaluateSideSlip = accessor;
            }

            return evaluateSideSlip();
        }

        private double TerminalVelocity()
        {
            if (evaluateTerminalVelocity == null)
            {
                Func<double> accessor = null;

                accessor = (Func<double>)GetInternalMethod("JSIFAR:GetTerminalVelocity", typeof(Func<double>));
                if (accessor != null)
                {
                    double value = accessor();
                    if (value < 0.0)
                    {
                        accessor = null;
                    }
                }

                if (accessor == null)
                {
                    accessor = (Func<double>)GetInternalMethod("JSIMechJeb:GetTerminalVelocity", typeof(Func<double>));
                    double value = accessor();
                    if (double.IsNaN(value))
                    {
                        accessor = null;
                    }
                }

                if (accessor == null)
                {
                    accessor = FallbackEvaluateTerminalVelocity;
                }

                evaluateTerminalVelocity = accessor;
            }

            return evaluateTerminalVelocity();
        }
        #endregion

        //--- Fallback evaluators
        #region FallbackEvaluators
        private double FallbackEvaluateAngleOfAttack()
        {
            // Code courtesy FAR.
            Transform refTransform = vessel.GetTransform();
            Vector3 velVectorNorm = vessel.srf_velocity.normalized;

            Vector3 tmpVec = refTransform.up * Vector3.Dot(refTransform.up, velVectorNorm) + refTransform.forward * Vector3.Dot(refTransform.forward, velVectorNorm);   //velocity vector projected onto a plane that divides the airplane into left and right halves
            double AoA = Vector3.Dot(tmpVec.normalized, refTransform.forward);
            AoA = Mathf.Rad2Deg * Math.Asin(AoA);
            if (double.IsNaN(AoA))
            {
                AoA = 0.0;
            }

            return AoA;
        }

        private double FallbackEvaluateDeltaV()
        {
            return (actualAverageIsp * gee) * Math.Log(totalShipWetMass / (totalShipWetMass - resources.PropellantMass(false)));
        }

        private double FallbackEvaluateDeltaVStage()
        {
            return (actualAverageIsp * gee) * Math.Log(totalShipWetMass / (totalShipWetMass - resources.PropellantMass(true)));
        }

        private double FallbackEvaluateDynamicPressure()
        {
            return vessel.dynamicPressurekPa;
        }

        private double FallbackEvaluateSideSlip()
        {
            // Code courtesy FAR.
            Transform refTransform = vessel.GetTransform();
            Vector3 velVectorNorm = vessel.srf_velocity.normalized;

            Vector3 tmpVec = refTransform.up * Vector3.Dot(refTransform.up, velVectorNorm) + refTransform.right * Vector3.Dot(refTransform.right, velVectorNorm);     //velocity vector projected onto the vehicle-horizontal plane
            double sideslipAngle = Vector3.Dot(tmpVec.normalized, refTransform.right);
            sideslipAngle = Mathf.Rad2Deg * Math.Asin(sideslipAngle);
            if (double.IsNaN(sideslipAngle))
            {
                sideslipAngle = 0.0;
            }

            return sideslipAngle;
        }

        private double FallbackEvaluateTerminalVelocity()
        {
            // Terminal velocity computation based on MechJeb 2.5.1 or one of the later snapshots
            if (altitudeASL > vessel.mainBody.RealMaxAtmosphereAltitude())
            {
                return float.PositiveInfinity;
            }

            Vector3d pureDragV = Vector3d.zero, pureLiftV = Vector3d.zero;

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

                        if (pm is ModuleControlSurface)
                        {
                            ModuleControlSurface cs = (pm as ModuleControlSurface);

                            if (p.ShieldedFromAirstream || cs.deploy)
                                continue;

                            pureLiftV += cs.liftForce;
                            pureDragV += cs.dragForce;
                        }
                        else if (pm is ModuleLiftingSurface)
                        {
                            ModuleLiftingSurface liftingSurface = (ModuleLiftingSurface)pm;
                            pureLiftV += liftingSurface.liftForce;
                            pureDragV += liftingSurface.dragForce;
                        }
                    }
                }
            }

            pureDragV = pureDragV / totalShipWetMass;
            pureLiftV = pureLiftV / totalShipWetMass;

            Vector3d force = pureDragV + pureLiftV;
            double drag = Vector3d.Dot(force, -vessel.srf_velocity.normalized);

            return Math.Sqrt(localGeeDirect / drag) * vessel.srfSpeed;
        }
        #endregion
    }
}
