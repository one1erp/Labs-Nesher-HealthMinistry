namespace HealthMinistry.FCSObject.ToFCS
{
    class Request
    {


        public string Arrival_Temp { get; set; }

        public Attached_Document Attached_Document { get; set; }

        public long Barcode { get; set; }

        public bool Is_Complete { get; set; }

        public string Lab_Code { get; set; }

        public string Lab_Report_ID { get; set; }

        public int Lab_Report_Ver { get; set; }

        //    public bool? Matching { get; set; }

        public string Organization { get; set; }

        //   public string Package_Condition { get; set; }

        public Report_Date Report_Date { get; set; }

        public Report_Notes Report_Notes { get; set; }

        public int Sample_Form_Num { get; set; }

        public Test_Results Test_Results { get; set; }

        public string Tester_Name { get; set; }

    }

}
