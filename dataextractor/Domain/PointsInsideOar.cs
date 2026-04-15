using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace DataExtractor.Domain
{
    class PointsInsideOar
    {
        public static Tuple<List<VVector>, List<VVector>> GetPointsInsideOarInAndOutWithMargin(Structure oar, Structure ptv, Image image, double axialResolution = 2, int slicesMargin = 2)
        {
            List<VVector> pointsInsideOarIn = new List<VVector>();
            List<VVector> pointsInsideOarOut = new List<VVector>();

            var oarBounds = oar.MeshGeometry.Bounds;

            for (var i = 0; i < image.ZSize; i++)
            {
                if (oar.GetContoursOnImagePlane(i).Count() > 0)
                {
                    for (var x = oarBounds.X; x <= oarBounds.X + oarBounds.SizeX; x += axialResolution)
                        for (var y = oarBounds.Y; y <= oarBounds.Y + oarBounds.SizeY; y += axialResolution)
                        {
                            var point = new VVector(x, y, image.Origin.z + i * image.ZRes);
                            bool insideOar = oar.IsPointInsideSegment(point);

                            // Verifica se o PTV está presente dentro da margem de n cortes
                            bool withinPtvAxialPlanesWithMargin = false;
                            for (int j = -slicesMargin; j <= slicesMargin; j++)
                            {
                                int sliceIndex = i + j;
                                if (sliceIndex >= 0 && sliceIndex < image.ZSize && ptv.GetContoursOnImagePlane(sliceIndex).Count() > 0)
                                {
                                    withinPtvAxialPlanesWithMargin = true;
                                    break;
                                }
                            }

                            if (insideOar && withinPtvAxialPlanesWithMargin)
                                pointsInsideOarIn.Add(point);
                            else if (insideOar)
                                pointsInsideOarOut.Add(point);
                        }
                }
            }

            return Tuple.Create(pointsInsideOarIn, pointsInsideOarOut);
        }
    }
}
