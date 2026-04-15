using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace DataExtractor.Domain
{
    public class OarToPtvDistances
    {
        // possibilidade de passar como argumento "Points Inside Oar In" ou "Points Inside Oar Out"
        public static List<double> GetOarToPtvDistances(Structure ptv, List<VVector> pointsInsideOar)
        {                      
            List<VVector> pointsOnPTVSurface = ptv.MeshGeometry.Positions.Select(p => new VVector(p.X, p.Y, p.Z)).ToList();
            
            List<double> distancesInMM = new List<double>();

            for (var i = 0; i < pointsInsideOar.Count; i++)
            {
                int currentDistance = (int)Math.Round(Math.Pow(Math.Pow(pointsInsideOar[i].x - pointsOnPTVSurface[0].x, 2) +
                                                    Math.Pow(pointsInsideOar[i].y - pointsOnPTVSurface[0].y, 2) +
                                                    Math.Pow(pointsInsideOar[i].z - pointsOnPTVSurface[0].z, 2), 0.5), 0);

                int shortestDistance = currentDistance;

                for (var j = 1; j < pointsOnPTVSurface.Count; j++)
                {
                    currentDistance = (int)Math.Round(Math.Pow(Math.Pow(pointsInsideOar[i].x - pointsOnPTVSurface[j].x, 2) +
                                                       Math.Pow(pointsInsideOar[i].y - pointsOnPTVSurface[j].y, 2) +
                                                       Math.Pow(pointsInsideOar[i].z - pointsOnPTVSurface[j].z, 2), 0.5), 0);

                    if (currentDistance < shortestDistance)
                        shortestDistance = currentDistance;
                }

                if (ptv.IsPointInsideSegment(pointsInsideOar[i]))
                    shortestDistance *= -1;
                distancesInMM.Add(shortestDistance);
            }
            distancesInMM.Sort();
            return distancesInMM;
        }

        public static int GetOarToPtvMinLongDistance(Structure ptv, List<VVector> pointsInsideOar)
        {
            List<VVector> pointsOnPTVSurface = ptv.MeshGeometry.Positions.Select(p => new VVector(p.X, p.Y, p.Z)).ToList();

            int currentLongDistance, minLongDistance = Math.Abs((int)Math.Round(pointsInsideOar[0].z - pointsOnPTVSurface[0].z, 0));            

            for (var i = 1; i < pointsInsideOar.Count; i++)
            {                           

                for (var j = 1; j < pointsOnPTVSurface.Count; j++)
                {
                    currentLongDistance = Math.Abs((int)Math.Round(pointsInsideOar[i].z - pointsOnPTVSurface[j].z, 0)); 

                    if (currentLongDistance < minLongDistance)
                        minLongDistance = currentLongDistance;
                }                
            }
            
            return minLongDistance;
        }
    }
}
