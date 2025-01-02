using DAL;
using HealthMinistry.Controllers;
using HealthMinistry.NautObjects;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace HealthMinistry.NautObjects
{
    public partial class SdgNaut
    {
        public SdgNaut(int numSmp)
        {
            this.WorkflowName = ConfigurationManager.AppSettings["SDG_WF"];
            description = ConfigurationManager.AppSettings["SDG_DESC"];
            fcsMessageId = "TEMP";
            sampledBy = ConfigurationManager.AppSettings["SAMPLED_BY"];
            Samples = new List<SampleObj>();
            for (int i = 0; i < numSmp; i++)
            {
                Samples.Add(new SampleObj());
            }
            U_FOOD_TEMPERATURE = "קירור";


        }

        public string WorkflowName { get; }
        public string U_FOOD_TEMPERATURE { get; }
        public string description { get; }
        public string fcsMessageId { get; }
        public string sampledBy { get; }
        public string Barcode { get; internal set; }
        internal ClientObj Client { get; set; }

        public List<SampleObj> Samples { get; set; }
    }
}