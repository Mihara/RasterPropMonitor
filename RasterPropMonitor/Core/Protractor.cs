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
    // This class encapsulates the functionality of the Protractor plugin.  It
    // is intended to be called in a just-in-time manner by calling the
    // Update method just before querying one of the public accessor variables.
    // Doing so allows us to avoid all of these computations every frame update
    // even when none of these variables are in use.
    static class Protractor
    {
        private static bool needsUpdate = true;
        private static double phaseAngle;
        public static double PhaseAngle
        {
            get
            {
                return phaseAngle;
            }
        }
        private static double timeToPhaseAngle;
        public static double TimeToPhaseAngle
        {
            get
            {
                return timeToPhaseAngle;
            }
        }
        private static double ejectionAngle;
        public static double EjectionAngle
        {
            get
            {
                return ejectionAngle;
            }
        }
        private static double timeToEjectionAngle;
        public static double TimeToEjectionAngle
        {
            get
            {
                return timeToEjectionAngle;
            }
        }
        private static double moonEjectionAngle;
        public static double MoonEjectionAngle
        {
            get
            {
                return moonEjectionAngle;
            }
        }
        private static double ejectionAltitude;
        public static double EjectionAltitude
        {
            get
            {
                return ejectionAltitude;
            }
        }
        private static double targetBodyDeltaV;
        public static double TargetBodyDeltaV
        {
            get
            {
                return targetBodyDeltaV;
            }
        }

        public static void OnFixedUpdate()
        {
            needsUpdate = true;
        }

        public static void Update(Vessel vessel, double altitudeASL, Orbit targetOrbit)
        {
            if (!needsUpdate)
            {
                return;
            }

            if (targetOrbit != null)
            {
                bool isSimpleTransfer;
                Orbit orbitOfOrigin;
                Orbit orbitOfDestination;
                int upshiftLevels;

                FindProtractorOrbitParameters(vessel.orbit, targetOrbit, out isSimpleTransfer, out orbitOfOrigin, out orbitOfDestination, out upshiftLevels);

                double delta_theta = 0.0;
                if (isSimpleTransfer)
                {
                    // Simple transfer: we orbit the same referenceBody as the target.
                    phaseAngle = UpdatePhaseAngleSimple(vessel, altitudeASL, orbitOfOrigin, orbitOfDestination);
                    delta_theta = (360.0 / orbitOfOrigin.period) - (360.0 / orbitOfDestination.period);

                    ejectionAngle = -1.0;

                    moonEjectionAngle = -1.0;
                    ejectionAltitude = -1.0;

                    targetBodyDeltaV = CalculateDeltaV(vessel, altitudeASL, orbitOfDestination);
                }
                else if (upshiftLevels == 1)
                {
                    // Our referenceBody orbits the same thing as our target.
                    phaseAngle = UpdatePhaseAngleAdjacent(vessel, orbitOfOrigin, orbitOfDestination);
                    delta_theta = (360.0 / orbitOfOrigin.period) - (360.0 / orbitOfDestination.period);

                    ejectionAngle = (CalculateDesiredEjectionAngle(vessel, altitudeASL, vessel.mainBody, orbitOfDestination) - CurrentEjectAngle(vessel) + 360.0) % 360.0;

                    moonEjectionAngle = -1.0;
                    ejectionAltitude = -1.0;

                    targetBodyDeltaV = CalculateDeltaV(vessel, altitudeASL, orbitOfDestination);
                }
                else if (upshiftLevels == 2)
                {
                    // Our referenceBody is a moon and we're doing an Oberth transfer.
                    phaseAngle = UpdatePhaseAngleOberth(vessel, orbitOfOrigin, orbitOfDestination);
                    delta_theta = (360.0 / orbitOfOrigin.period) - (360.0 / orbitOfDestination.period);

                    ejectionAngle = -1.0;

                    moonEjectionAngle = (MoonAngle(vessel, altitudeASL) - CurrentEjectAngle(vessel) + 360.0) % 360.0;
                    ejectionAltitude = 1.05 * vessel.mainBody.referenceBody.atmosphereDepth;
                    targetBodyDeltaV = CalculateDeltaV(vessel, altitudeASL, orbitOfDestination);
                }
                else
                {
                    // What case does this cover?  I *think* it can't happen.
                    phaseAngle = -1.0;
                    ejectionAngle = -1.0;
                    moonEjectionAngle = -1.0;
                    ejectionAltitude = -1.0;
                    targetBodyDeltaV = -1.0;
                }

                if (phaseAngle >= 0.0)
                {
                    if (delta_theta > 0.0)
                    {
                        timeToPhaseAngle = phaseAngle / delta_theta;
                    }
                    else
                    {
                        timeToPhaseAngle = Math.Abs((360.0 - phaseAngle) / delta_theta);
                    }
                }
                else
                {
                    timeToPhaseAngle = -1.0;
                }

                if (ejectionAngle >= 0.0)
                {
                    timeToEjectionAngle = ejectionAngle * vessel.orbit.period / 360.0;
                }
                else
                {
                    timeToEjectionAngle = -1.0;
                }
            }
            else
            {
                // We ain't targetin' nothin'...
                phaseAngle = -1.0;
                timeToPhaseAngle = -1.0;
                ejectionAngle = -1.0;
                timeToEjectionAngle = -1.0;
                moonEjectionAngle = -1.0;
                ejectionAltitude = -1.0;
                targetBodyDeltaV = -1.0;
            }
        }

        //--- Protractor utility methods
        /// <summary>
        /// FindProtractorOrbitParameters takes the current vessel orbit, and
        /// the orbit of the target vessel / body, and it determines the
        /// parameters needed for computing the phase angle, ejection angle,
        /// and moon ejection angle (where appropriate).
        /// </summary>
        /// <param name="vesselOrbit"></param>
        /// <param name="targetOrbit"></param>
        /// <param name="isSimpleTransfer"></param>
        /// <param name="newVesselOrbit"></param>
        /// <param name="newTargetOrbit"></param>
        /// <param name="upshiftLevels"></param>
        private static void FindProtractorOrbitParameters(Orbit vesselOrbit, Orbit targetOrbit,
                                                          out bool isSimpleTransfer, out Orbit newVesselOrbit, out Orbit newTargetOrbit,
                                                          out int upshiftLevels)
        {
            // Test for the early out case
            if (vesselOrbit.referenceBody == targetOrbit.referenceBody)
            {
                // Target orbits the same body we do.
                isSimpleTransfer = true;
                newVesselOrbit = vesselOrbit;
                newTargetOrbit = targetOrbit;
                upshiftLevels = 0;
            }
            else if (vesselOrbit.referenceBody == Planetarium.fetch.Sun)
            {
                // We orbit the sun.  We need the target's sun-orbiting
                // parameters.
                isSimpleTransfer = true;
                newVesselOrbit = vesselOrbit;
                newTargetOrbit = GetSunOrbit(targetOrbit);
                upshiftLevels = 0;
            }
            else
            {
                // Not a simple case.
                int vesselDistFromSun = GetDistanceFromSun(vesselOrbit);
                int targetDistFromSun = GetDistanceFromSun(targetOrbit);
                isSimpleTransfer = false;

                if (targetDistFromSun == 0)
                {
                    // Target orbits the sun.
                    newVesselOrbit = GetReferencePlanet(vesselOrbit).GetOrbit();
                    newTargetOrbit = targetOrbit;
                    upshiftLevels = vesselDistFromSun;
                }
                else if (GetReferencePlanet(vesselOrbit) != GetReferencePlanet(targetOrbit))
                {
                    // Interplanetary transfer
                    newVesselOrbit = GetReferencePlanet(vesselOrbit).GetOrbit();
                    newTargetOrbit = GetReferencePlanet(targetOrbit).GetOrbit();
                    upshiftLevels = vesselDistFromSun;
                }
                else
                {
                    // vessel and target are in the same planetary system.
                    --vesselDistFromSun;
                    --targetDistFromSun;
                    if (vesselDistFromSun == 0)
                    {
                        // Vessel orbits the planet; the target *must* orbit a
                        // moon, or we would have found it in a previous case.
                        if (targetDistFromSun != 1)
                        {
                            throw new ArithmeticException("Protractor::FindProtractorOrbitParameters(): vessel and target are in the same planetary system, but the target isn't orbiting a moon (but should be).");
                        }

                        newVesselOrbit = vesselOrbit;
                        newTargetOrbit = targetOrbit.referenceBody.GetOrbit();
                        isSimpleTransfer = true;
                    }
                    else
                    {
                        // Vessel is orbiting a moon; target is either a moon,
                        // or a vessel orbiting a moon.
                        newVesselOrbit = vesselOrbit.referenceBody.GetOrbit();
                        newTargetOrbit = (targetDistFromSun == 1) ? targetOrbit.referenceBody.GetOrbit() : targetOrbit;
                    }
                    upshiftLevels = vesselDistFromSun;
                }
            }
        }


        private static double CalculateDesiredEjectionAngle(Vessel vessel, double altitudeASL, CelestialBody orig, Orbit dest)
        {
            double o_alt = CalcMeanAlt(orig.orbit);
            double d_alt = CalcMeanAlt(dest);
            double o_soi = orig.sphereOfInfluence;
            double o_radius = orig.Radius;
            double o_mu = orig.gravParameter;
            double u = orig.referenceBody.gravParameter;
            double exitalt = o_alt + o_soi;
            double v2 = Math.Sqrt(u / exitalt) * (Math.Sqrt((2 * d_alt) / (exitalt + d_alt)) - 1);
            double r = o_radius + altitudeASL;
            double v = Math.Sqrt((r * (o_soi * v2 * v2 - 2 * o_mu) + 2 * o_soi * o_mu) / (r * o_soi));
            double eta = Math.Abs(v * v / 2 - o_mu / r);
            double h = r * v;
            double e = Math.Sqrt(1 + ((2 * eta * h * h) / (o_mu * o_mu)));
            double eject = (180 - (Math.Acos(1 / e) * (180 / Math.PI))) % 360;

            eject = o_alt > d_alt ? 180 - eject : 360 - eject;

            return vessel.orbit.inclination > 90 && !(vessel.Landed) ? 360 - eject : eject;
        }

        //calculates ejection v to reach destination
        private static double CalculateDeltaV(Vessel vessel, double altitudeASL, Orbit destOrbit)
        {
            if (vessel.mainBody == destOrbit.referenceBody)
            {
                double radius = destOrbit.referenceBody.Radius;
                double u = destOrbit.referenceBody.gravParameter;
                double d_alt = CalcMeanAlt(destOrbit);
                double alt = altitudeASL + radius;
                double v = Math.Sqrt(u / alt) * (Math.Sqrt((2 * d_alt) / (alt + d_alt)) - 1);
                return Math.Abs((Math.Sqrt(u / alt) + v) - vessel.orbit.GetVel().magnitude);
            }
            else
            {
                CelestialBody orig = vessel.mainBody;
                double d_alt = CalcMeanAlt(destOrbit);
                double o_radius = orig.Radius;
                double u = orig.referenceBody.gravParameter;
                double o_mu = orig.gravParameter;
                double o_soi = orig.sphereOfInfluence;
                double o_alt = CalcMeanAlt(orig.orbit);
                double exitalt = o_alt + o_soi;
                double v2 = Math.Sqrt(u / exitalt) * (Math.Sqrt((2 * d_alt) / (exitalt + d_alt)) - 1);
                double r = o_radius + altitudeASL;
                double v = Math.Sqrt((r * (o_soi * v2 * v2 - 2 * o_mu) + 2 * o_soi * o_mu) / (r * o_soi));
                return Math.Abs(v - vessel.orbit.GetVel().magnitude);
            }
        }

        // calculates angle between vessel's position and prograde of orbited body
        // MOARdV: The parameter 'check' is always NULL in protractor.  Factored it out
        private static double CurrentEjectAngle(Vessel vessel)
        {
            Vector3d vesselvec = vessel.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());

            // get planet's position relative to universe
            Vector3d bodyvec = vessel.mainBody.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());

            double eject = Angle2d(vesselvec, Quaternion.AngleAxis(90.0f, Vector3d.forward) * bodyvec);

            if (Angle2d(vesselvec, Quaternion.AngleAxis(180.0f, Vector3d.forward) * bodyvec) > Angle2d(vesselvec, bodyvec))
            {
                eject = 360.0 - eject;//use cross vector to determine up or down
            }

            return eject;
        }

        // Compute the current phase of the target.
        private static double CurrentPhase(Orbit originOrbit, Orbit destinationOrbit)
        {
            Vector3d vecthis = originOrbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());
            Vector3d vectarget = destinationOrbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());

            double phase = Angle2d(vecthis, vectarget);

            vecthis = Quaternion.AngleAxis(90.0f, Vector3d.forward) * vecthis;

            if (Angle2d(vecthis, vectarget) > 90.0)
                phase = 360.0 - phase;

            return (phase + 360.0) % 360.0;
        }

        // Calculates phase angle for rendezvous between two bodies orbiting same parent
        private static double DesiredPhase(double vesselAlt, double destAlt, double gravParameter)
        {
            double o_alt = vesselAlt;

            double d_alt = destAlt;
            double u = gravParameter;
            double th = Math.PI * Math.Sqrt(Math.Pow(o_alt + d_alt, 3.0) / (8.0 * u));
            double phase = (180.0 - Math.Sqrt(u / d_alt) * (th / d_alt) * (180.0 / Math.PI));

            while (phase < 0.0)
                phase += 360.0;

            return phase % 360.0;
        }

        // For going from a moon to another planet exploiting oberth effect
        private static double OberthDesiredPhase(Vessel vessel, Orbit destOrbit)
        {
            CelestialBody moon = vessel.mainBody;
            CelestialBody planet = vessel.mainBody.referenceBody;
            double planetalt = CalcMeanAlt(planet.orbit);
            double destalt = CalcMeanAlt(destOrbit);
            double moonalt = CalcMeanAlt(moon.orbit);
            double usun = Planetarium.fetch.Sun.gravParameter;
            double uplanet = planet.gravParameter;
            double oberthalt = (planet.Radius + planet.atmosphereDepth) * 1.05;

            double th1 = Math.PI * Math.Sqrt(Math.Pow(moonalt + oberthalt, 3.0) / (8.0 * uplanet));
            double th2 = Math.PI * Math.Sqrt(Math.Pow(planetalt + destalt, 3.0) / (8.0 * usun));

            double phase = (180.0 - Math.Sqrt(usun / destalt) * ((th1 + th2) / destalt) * (180.0 / Math.PI));

            while (phase < 0.0)
                phase += 360.0;

            return phase % 360.0;
        }

        private static double MoonAngle(Vessel vessel, double altitudeASL)  //calculates eject angle for moon -> planet in preparation for planet -> planet transfer
        {
            CelestialBody orig = vessel.mainBody;
            double o_alt = CalcMeanAlt(orig.orbit);
            double d_alt = (orig.orbit.referenceBody.Radius + orig.orbit.referenceBody.atmosphereDepth) * 1.05;
            double o_soi = orig.sphereOfInfluence;
            double o_radius = orig.Radius;
            double o_mu = orig.gravParameter;
            double u = orig.referenceBody.gravParameter;
            double exitalt = o_alt + o_soi;
            double v2 = Math.Sqrt(u / exitalt) * (Math.Sqrt((2.0 * d_alt) / (exitalt + d_alt)) - 1.0);
            double r = o_radius + altitudeASL;
            double v = Math.Sqrt((r * (o_soi * v2 * v2 - 2.0 * o_mu) + 2 * o_soi * o_mu) / (r * o_soi));
            double eta = Math.Abs(v * v / 2.0 - o_mu / r);
            double h = r * v;
            double e = Math.Sqrt(1.0 + ((2.0 * eta * h * h) / (o_mu * o_mu)));
            double eject = (180.0 - (Math.Acos(1.0 / e) * (180.0 / Math.PI))) % 360.0;

            eject = (o_alt > d_alt) ? (180.0 - eject) : (360.0 - eject);

            return (vessel.orbit.inclination > 90.0 && !(vessel.Landed)) ? (360.0 - eject) : eject;
        }

        // Simple phase angle: transfer from sun -> planet or planet -> moon
        private static double UpdatePhaseAngleSimple(Vessel vessel, double altitudeASL, Orbit srcOrbit, Orbit destOrbit)
        {
            if (destOrbit == null)
            {
                JUtil.LogMessage(null, "!!! UpdatePhaseAngleSimple got a NULL orbit !!!");
                return 0.0;
            }

            // MOARdV TODO: Can this be made more accurate using the orbit
            // altitude at the point of intercept?
            double destAlt = CalcMeanAlt(destOrbit);

            double phase = CurrentPhase(srcOrbit, destOrbit) - DesiredPhase(altitudeASL + vessel.mainBody.Radius, destAlt, vessel.mainBody.gravParameter);
            phase = (phase + 360.0) % 360.0;

            return phase;
        }

        // Adjacent phase angle: transfer planet -> planet or moon -> moon
        private static double UpdatePhaseAngleAdjacent(Vessel vessel, Orbit srcOrbit, Orbit destOrbit)
        {
            if (destOrbit == null)
            {
                JUtil.LogMessage(null, "!!! UpdatePhaseAngleAdjacent got a NULL orbit !!!");
                return 0.0;
            }

            double srcAlt = CalcMeanAlt(srcOrbit);
            double destAlt = CalcMeanAlt(destOrbit);

            double phase = CurrentPhase(srcOrbit, destOrbit) - DesiredPhase(srcAlt, destAlt, vessel.mainBody.gravParameter);
            phase = (phase + 360.0) % 360.0;

            return phase;
        }

        // Oberth phase angle: transfer moon -> another planet
        private static double UpdatePhaseAngleOberth(Vessel vessel, Orbit srcOrbit, Orbit destOrbit)
        {
            if (destOrbit == null)
            {
                JUtil.LogMessage(null, "!!! UpdatePhaseAngleOberth got a NULL orbit !!!");
                return 0.0;
            }

            //double srcAlt = CalcMeanAlt(srcOrbit);
            //double destAlt = CalcMeanAlt(destOrbit);

            double phase = CurrentPhase(srcOrbit, destOrbit) - OberthDesiredPhase(vessel, destOrbit);
            phase = (phase + 360.0) % 360.0;

            return phase;
        }

        // project two vectors to 2D plane and returns the angle between them
        private static double Angle2d(Vector3d vector1, Vector3d vector2)
        {
            Vector3d v1 = Vector3d.Project(new Vector3d(vector1.x, 0, vector1.z), vector1);
            Vector3d v2 = Vector3d.Project(new Vector3d(vector2.x, 0, vector2.z), vector2);
            return Vector3d.Angle(v1, v2);
        }

        private static double CalcMeanAlt(Orbit orbit)
        {
            return orbit.semiMajorAxis * (1.0 + orbit.eccentricity * orbit.eccentricity / 2.0);
        }


        /// <summary>
        /// Counts how many reference bodies there are between the supplied
        /// orbit and the sun.  0 indicates the orbit is around the sun, 1
        /// indicates the orbit is around a planet, and 2 indicates the orbit
        /// is around a moon.
        /// </summary>
        /// <param name="startingOrbit"></param>
        /// <returns></returns>
        static private int GetDistanceFromSun(Orbit startingOrbit)
        {
            int count = 0;
            while (startingOrbit.referenceBody != Planetarium.fetch.Sun)
            {
                ++count;
                startingOrbit = startingOrbit.referenceBody.GetOrbit();
            }
            return count;
        }

        /// <summary>
        /// GetSunOrbit walks up the given orbit's referenceBody chain to
        /// return the parent orbit that
        /// </summary>
        /// <param name="orbitOfOrigin"></param>
        /// <returns></returns>
        static private Orbit GetSunOrbit(Orbit orbitOfOrigin)
        {
            while (orbitOfOrigin.referenceBody != Planetarium.fetch.Sun)
            {
                orbitOfOrigin = orbitOfOrigin.referenceBody.GetOrbit();
                if (orbitOfOrigin == null)
                {
                    throw new ArithmeticException("RasterPropMonitorComputer::GetSunOrbit() could not find a solar orbit.");
                }
            }

            return orbitOfOrigin;
        }

        private static CelestialBody GetReferencePlanet(Orbit o)
        {
            if (o.referenceBody == Planetarium.fetch.Sun)
            {
                // I think this shouldn't happen...
                return o.referenceBody;
            }
            // Orbit is around a planet or a moon?
            return o.referenceBody.GetOrbit().referenceBody == Planetarium.fetch.Sun ? o.referenceBody : o.referenceBody.GetOrbit().referenceBody;
        }
    }
}
