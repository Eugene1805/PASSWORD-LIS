using System.Runtime.Serialization;

namespace Services.Contracts.DTOs
{
    [DataContract]
    public class ReportDTO
    {
        [DataMember]
        public int Id { get; set; }
        [DataMember]
        public int ReporterPlayerId { get; set; }
        [DataMember]
        public int ReportedPlayerId { get; set; }
        [DataMember]
        public string Reason { get; set; }
    }
}
