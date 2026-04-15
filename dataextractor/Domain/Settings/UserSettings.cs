using System.Runtime.Serialization;

namespace DataExtractor.Domain.Settings
{
    /// <summary>
    /// Valores padrão que controlam a discretização dos DTHs
    /// e a resolução axial da amostragem de pontos no OAR.
    /// </summary>
    [DataContract]
    public sealed class ExtractionDefaults
    {
        [DataMember(Order = 1)]
        public double[] DthInBins { get; set; }

        [DataMember(Order = 2)]
        public double[] DthOutBins { get; set; }

        [DataMember(Order = 3)]
        public double PointsInsideOarAxialResMm { get; set; }

        // DVH (sempre cumulativo)
        [DataMember(Order = 4)]
        public double DvhResolution { get; set; }

        [DataMember(Order = 5)]
        public double DvhMaxDose { get; set; }
    }

    /// <summary>
    /// Configurações efetivas do usuário para a extração.
    /// Parte vem do JSON (defaults) e parte é perguntada no console (parâmetros opcionais).
    /// </summary>
    public sealed class UserSettings
    {
        public double[] DthInBins;

        public double[] DthOutBins;

        public double PointsInsideOarAxialResMm;

        public bool DthInAndOut;
        public bool CumulativeDth;
        public int DthInSlicesMargin;

        public double DvhResolution;
        public double DvhMaxDose;

        public UserSettings(ExtractionDefaults defaults)
        {
            DthInBins = defaults.DthInBins;

            DthOutBins = defaults.DthOutBins;

            PointsInsideOarAxialResMm = defaults.PointsInsideOarAxialResMm;

            DvhResolution = defaults.DvhResolution;
            DvhMaxDose = defaults.DvhMaxDose;
        }
    }
}
