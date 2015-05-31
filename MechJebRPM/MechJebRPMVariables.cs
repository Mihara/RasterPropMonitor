using MuMech;
using System.Linq;

namespace MechJebRPM
{
    public class MechJebRPMVariables : PartModule
    {
        public object ProcessVariable(string variable)
        {
            MechJebCore activeJeb = vessel.GetMasterMechJeb();
            switch (variable)
            {
                case "MECHJEBAVAILABLE":
                    if (activeJeb != null)
                    {
                        return 1;
                    }
                    return -1;
                case "DELTAV":
                    if (activeJeb != null)
                    {
                        MechJebModuleStageStats stats = activeJeb.GetComputerModule<MechJebModuleStageStats>();
                        stats.RequestUpdate(this);
                        if (stats.vacStats.Length > 0 && stats.atmoStats.Length > 0)
                        {
                            double dVvac = stats.vacStats.Sum(s => s.deltaV);
                            double dVatm = stats.atmoStats.Sum(s => s.deltaV);
                            return UtilMath.LerpUnclamped(dVvac, dVatm, vessel.atmDensity);
                        }
                    }
                    return null;
                case "DELTAVSTAGE":
                    if (activeJeb != null)
                    {
                        MechJebModuleStageStats stats = activeJeb.GetComputerModule<MechJebModuleStageStats>();
                        stats.RequestUpdate(this);
                        if (stats.vacLastStage != null && stats.atmLastStage != null)
                        {
                            return UtilMath.LerpUnclamped(stats.vacLastStage.deltaV, stats.atmLastStage.deltaV, vessel.atmDensity);
                        }
                    }
                    return null;
                case "PREDICTEDLANDINGERROR":
                    // If there's a failure at any step, exit with a -1.
                    // The landing prediction system can be costly, and it
                    // expects someone to be registered with it for it to run.
                    // So, we'll have a MechJebRPM button to enable landing
                    // predictions.  And maybe add it in to the MJ menu.
                    if (activeJeb != null && activeJeb.target.PositionTargetExists == true)
                    {
                        var predictor = activeJeb.GetComputerModule<MechJebModuleLandingPredictions>();
                        if (predictor != null && predictor.enabled)
                        {
                            ReentrySimulation.Result result = predictor.GetResult();
                            if (result != null && result.outcome == ReentrySimulation.Outcome.LANDED)
                            {
                                // We're going to hit something!
                                double error = Vector3d.Distance(vessel.mainBody.GetRelSurfacePosition(result.endPosition.latitude, result.endPosition.longitude, 0),
                                                   vessel.mainBody.GetRelSurfacePosition(activeJeb.target.targetLatitude, activeJeb.target.targetLongitude, 0));
                                return error;
                            }
                        }
                    }
                    return -1;
            }
            return null;
        }
    }
}
