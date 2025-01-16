using RestSharp;

namespace HealthMinistry.FCSObject.ToFCS
{
    public class Test_Result
    {
            public string Analyte_Name { get; set; }

        public string LIMS_Samp_ID { get; set; }

        //     public string LOD { get; set; }

        //      public string LOQ { get; set; }

            public int Lims_Test_Code { get; set; }

        //    public string ManufResult { get; set; }

        //     public string Marking { get; set; }

        public string Measurement_Unit { get; set; }

            public int Measurement_Unit_Code { get; set; }

           public string Method { get; set; }

        //     public Notes Notes { get; set; }

        public string Result { get; set; }

        //     public double Result_Num { get; set; }

        public string Sample_Description { get; set; }

        //    public string Temprature { get; set; }

           public string Test_Name { get; set; }

        public int Test_Sub_Code { get; set; }


        public Test_Result()
        {
        }
    }

}
