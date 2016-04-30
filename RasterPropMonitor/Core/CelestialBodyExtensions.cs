using System;
using UnityEngine;

namespace JSI
{
    // This class was imported wholesale from MechJeb.
    public static class CelestialBodyExtensions
    {
        public static double TerrainAltitude(this CelestialBody body, Vector3d worldPosition)
        {
            return body.TerrainAltitude(body.GetLatitude(worldPosition), body.GetLongitude(worldPosition));
        }


        //CelestialBody.maxAtmosphereAltitude doesn't actually give the upper edge of 
        //the atmosphere. Use this function instead. 
        public static double RealMaxAtmosphereAltitude(this CelestialBody body)
        {
            //#warning check if atmosphereDepth = 0 when !body.atmosphere and remove the whole ext
            if (!body.atmosphere)
                return 0;
            return body.atmosphereDepth;
        }
    }
}
