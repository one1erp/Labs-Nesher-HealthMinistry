using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HealthMinistry.FCSObjects.FromFCS
{

    public class Sample_Form_Response
    {
        internal List<RequestedTest> requestedTestsList;

        public string Barcode { get; set; }
        public string ContainerNum { get; set; }
        public string CountryName { get; set; }
        public string DelFileNum { get; set; }
        public string DeliveryToLab { get; set; }
        public AmilDetails AmilDetails { get; set; }
        public string AttachedDocument { get; set; }
        public string BatchNum { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string FormType { get; set; }
        public ImporterDetails ImporterDetails { get; set   ; }
        public string ImporterStore { get; set; }
        public string InspectorTitle { get; set; }
        public string IsOpenPackaging { get; set; }
        public string IsOriginalPacking { get; set; }
        public string IsVet { get; set; }
        public DateTime? ManufactureDate { get; set; }

        //Old Version
      //  public string NumOfSamples { get; set; }
    //    public string NumOfSamplesVet { get; set; }
        public string Organization { get; set; }
        public string PackingType { get; set; }
        public string PayerID { get; set; }
        public string ProducerCountry { get; set; }
        public string ProducerName { get; set; }
        public string ProductBrandName { get; set; }
        public string ProductGroupCode { get; set; }
        public string ProductGroupDescription { get; set; }
        public string ProductOwner { get; set; }
        public string ProductNameEng { get; set; }
        public string ProductNameHeb { get; set; }
        public string PropertyPlus { get; set; }
        public string Remark { get; set; }
        public string RequestedTests { get; set; }
        public string ReturnCode { get; set; }
        public string ReturnCodeDesc { get; set; }
        public string SampleFormNum { get; set; }
        public DateTime? SamplingDate { get; set; }
        public string SamplingInspector { get; set; }
        public string SamplingPlace { get; set; }
        public string SamplingReason { get; set; }
        public string SamplingTemp { get; set; }
        public string SamplingTime { get; set; }
        public string TestDescription { get; set; }
        public string TestSubCode { get; set; }
        public string EmailTo { get; set; }

  
    }


}

