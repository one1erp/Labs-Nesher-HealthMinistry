using System.Collections.Generic;

namespace HealthMinistry.FCSObject.ToFCS
{


    public class LabNotes
    {
        public string Note { get; set; }
        public string Note_Type { get; set; }
    }
    public class FCS_Date
    {
        public int Day { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
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


    public class Test_Results
    {
        public List<Test_Result> Test_Result = new List<Test_Result>();

        public Test_Results(List<Test_Result> test_Result)
        {
            Test_Result = test_Result;
        }
    }

}
