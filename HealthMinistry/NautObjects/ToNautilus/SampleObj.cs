using System.Collections.Generic;
using System.Configuration;

namespace HealthMinistry.NautObjects.ToNautilus
{
    public partial class SdgNaut
    {
        public class SampleObj
        {

            public SampleObj()
            {
                product = ConfigurationManager.AppSettings["PROD_ID"];
                aliquots = new List<AliquotObj>();

            }
            public List<AliquotObj> aliquots { get; private set; }

            public string ContainerNum { get; internal set; }
            public string DelFileNum { get; internal set; }
            public string description { get; internal set; }
            public string product { get; private set; }
            public string batchNum { get; internal set; }
            public string dateProduction { get; internal set; }

            public string SamplingTime { get; internal set; }
            public string Barcode { get; internal set; }
            public string SamplingDate { get; internal set; }
            public string Producer_Name { get; internal set; }

            

            public void AddAliquot(AliquotObj aliquot)
            {
                aliquots.Add(aliquot);
            }

        }
    }
}