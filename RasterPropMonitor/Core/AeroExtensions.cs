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
/*
Ferram Aerospace Research v0.15.3.1 "Garabedian"
=========================
Aerodynamics model for Kerbal Space Program

Copyright 2015, Michael Ferrara, aka Ferram4

   This file is part of Ferram Aerospace Research.

   Ferram Aerospace Research is free software: you can redistribute it and/or modify
   it under the terms of the GNU General Public License as published by
   the Free Software Foundation, either version 3 of the License, or
   (at your option) any later version.

   Ferram Aerospace Research is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
   GNU General Public License for more details.

   You should have received a copy of the GNU General Public License
   along with Ferram Aerospace Research.  If not, see <http://www.gnu.org/licenses/>.

   Serious thanks:		a.g., for tons of bugfixes and code-refactorings   
				stupid_chris, for the RealChuteLite implementation
            			Taverius, for correcting a ton of incorrect values  
				Tetryds, for finding lots of bugs and issues and not letting me get away with them, and work on example crafts
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager updates  
            			ialdabaoth (who is awesome), who originally created Module Manager  
                        	Regex, for adding RPM support  
				DaMichel, for some ferramGraph updates and some control surface-related features  
            			Duxwing, for copy editing the readme  
   
   CompatibilityChecker by Majiir, BSD 2-clause http://opensource.org/licenses/BSD-2-Clause

   Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/55219

   ModularFLightIntegrator by Sarbian, Starwaster and Ferram4, MIT: http://opensource.org/licenses/MIT
	http://forum.kerbalspaceprogram.com/threads/118088

   Toolbar integration powered by blizzy78's Toolbar plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/60863
 */

using System;

namespace JSI
{
    // Imported from FerramAerospaceResearch FARAeroUtil.cs
    public static class AeroExtensions
    {
        //Based on ratio of density of water to density of air at SL
        private const double UNDERWATER_DENSITY_FACTOR_MINUS_ONE = 814.51020408163265306122448979592;

        public static double GetCurrentDensity(CelestialBody body, double altitude, bool densitySmoothingAtOcean = true)
        {
            double pressure, temperature;
            pressure = FlightGlobals.getStaticPressure(altitude, body);
            temperature = FlightGlobals.getExternalTemperature(altitude, body);

            double density = FlightGlobals.getAtmDensity(pressure, temperature, body);

            if (altitude < 0 && densitySmoothingAtOcean)
            {
                double densityMultFromOcean = Math.Max(-altitude, 1);
                densityMultFromOcean *= UNDERWATER_DENSITY_FACTOR_MINUS_ONE;
                densityMultFromOcean++;
                density *= densityMultFromOcean;
            }

            return density;
        }

        public static double StagnationPressureCalc(CelestialBody body, double M)
        {
            double gamma = body.atmosphereAdiabaticIndex;

            double ratio;
            ratio = M * M;
            ratio *= (gamma - 1);
            ratio *= 0.5;
            ratio++;

            ratio = Math.Pow(ratio, gamma / (gamma - 1));
            return ratio;
        }
    }
}
