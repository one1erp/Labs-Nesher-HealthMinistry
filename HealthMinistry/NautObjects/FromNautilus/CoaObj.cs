using System;

namespace HealthMinistry.NautObjects.FromNautilus
{
    public class CoaObj
    {
        public string NAME { get; internal set; }
        public string Status { get; internal set; }
        public string U_PARTIAL { get; internal set; }
        public string PdfPath { get; internal set; }
        public string U_Sdg { get; internal set; }

        public string FCSguide { get; set; }
        public DateTime U_Created_On { get; internal set; }
        public string U_Coa_Report_id { get; internal set; }
    }
}