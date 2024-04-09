using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HealthMinistry.Controllers
{

    class Request
    {


        public string Arrival_Temp { get; set; }

        public Attached_Document Attached_Document { get; set; }

        public long? Barcode { get; set; }

        public bool Is_Complete { get; set; }

        public int Lab_Code { get; set; }

        public string Lab_Report_ID { get; set; }

        public int Lab_Report_Ver { get; set; }

        public bool Matching { get; set; }

        public string Organization { get; set; }

        public string Package_Condition { get; set; }

        public Report_Date Report_Date { get; set; }

        public Report_Notes Report_Notes { get; set; }

        public int Sample_Form_Num { get; set; }

        public Test_Results Test_Results { get; set; }

        public string Tester_Name { get; set; }

        public Request(string sdgUFoodTemperature, Attached_Document attachedDocument, long? sdgUFcsMsgId, int labCode, string labReportId, int labReportVer, bool matching, string packageCondition, Report_Date reportDate, Report_Notes reportNotes, Test_Results sampleFormNum)
        {

            Arrival_Temp = sdgUFoodTemperature;
            Attached_Document = attachedDocument;
            Barcode = sdgUFcsMsgId;
            Lab_Code = labCode;
            Lab_Report_ID = labReportId;
            Lab_Report_Ver = labReportVer;
            Matching = matching;
            Package_Condition = packageCondition;
            Report_Date = reportDate;
            Report_Notes = reportNotes;
            Test_Results = sampleFormNum;
        }
    }
    public class ATTACHED_DOCUMENT
    {
        public string Document_Name { get; set; }

        public int Document_Type_Code { get; set; }

        public string FileExt { get; set; }

        public string FileName { get; set; }

        public string GUID { get; set; }

        public string Link_To_Document { get; set; }

        public ATTACHED_DOCUMENT(string document_Name, int document_Type_Code, string file_Ext, string file_Name, string guid, string link_To_Document)
        {
            Document_Name = document_Name;
            Document_Type_Code = document_Type_Code;
            FileExt = file_Ext;
            FileName = file_Name;
            GUID = guid;
            Link_To_Document = link_To_Document;
        }
    }

    public class Attached_Document
    {
        public List<ATTACHED_DOCUMENT> ATTACHED_DOCUMENT = new List<ATTACHED_DOCUMENT>();

        public Attached_Document(List<ATTACHED_DOCUMENT> aTTACHED_DOCUMENT)
        {
            ATTACHED_DOCUMENT = aTTACHED_DOCUMENT;
        }
    }

    public class Report_Date
    {

        public string Day { get; set; }

        public string Month { get; set; }

        public string Year { get; set; }


        public Report_Date(string _date)
        {
            var dt = _date.Split('/');
            Day = dt[0];
            Month = dt[1];
            Year = dt[2];
        }


    }

    public class _LabNotes
    {
        public string Note { get; set; }

        public string Note_Type { get; set; }

        public _LabNotes(string note, string note_Type)
        {
            Note = note;
            Note_Type = note_Type;
        }
    }

    public class Report_Notes
    {
        public List<_LabNotes> LabNotes = new List<_LabNotes>();

        public Report_Notes(List<_LabNotes> labNotes)
        {
            LabNotes = labNotes;
        }
    }

    public class Notes
    {
        public List<_LabNotes> Lab_Notes = new List<_LabNotes>();

        public Notes(List<_LabNotes> labNotes)
        {
            Lab_Notes = labNotes;
        }
    }


    public class Test_Result
    {
        public string Analyte_Name { get; set; }

        public string LIMS_Samp_ID { get; set; }

        public string LOD { get; set; }

        public string LOQ { get; set; }

        public int Lims_Test_Code { get; set; }

        public string ManufResult { get; set; }

        public string Marking { get; set; }

        public string Measurement_Unit { get; set; }

        public int Measurement_Unit_Code { get; set; }

        public string Method { get; set; }

        public Notes Notes { get; set; }

        public string Result { get; set; }

        public double Result_Num { get; set; }

        public string Sample_Description { get; set; }

        public string Temprature { get; set; }

        public string Test_Name { get; set; }

        public int Test_Sub_Code { get; set; }

        public string uncertainty { get; set; }

        public Test_Result(string analyte_Name, string lIMS_Samp_ID, string lOD, string lOQ, int lims_Test_Code, string manuf_Result, string marking, string measurement_Unit, int measurement_Unit_Code, string method, Notes notes, string result, double result_Num, string sample_Description, string temprature, string test_Name, int test_Sub_Code, string Uncertainty)
        {
            Analyte_Name = analyte_Name;
            LIMS_Samp_ID = lIMS_Samp_ID;
            LOD = lOD;
            LOQ = lOQ;
            Lims_Test_Code = lims_Test_Code;
            ManufResult = manuf_Result;
            Marking = marking;
            Measurement_Unit = measurement_Unit;
            Measurement_Unit_Code = measurement_Unit_Code;
            Method = method;
            Notes = notes;
            Result = result;
            Result_Num = result_Num;
            Sample_Description = sample_Description;
            Temprature = temprature;
            Test_Name = test_Name;
            Test_Sub_Code = test_Sub_Code;
            uncertainty = Uncertainty;
        }
    }


    public class Test_Results
    {
        public List<Test_Result> Test_Result = new List<Test_Result>();

        public Test_Results(List<Test_Result> test_Result)
        {
            Test_Result = test_Result;
        }
    }

}
