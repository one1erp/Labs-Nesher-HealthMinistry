//using FoodApp.FCS_LabsNewObj;
using HealthMinistry.Common;

using HealthMinistry.FCSObject.ToFCS;
using HealthMinistry.FCSObjects;
using HealthMinistry.NautObjects;
using HealthMinistry.NautObjects.FromNautilus;
using Oracle.DataAccess.Client;
using RestSharp;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Common.CommandTrees;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using static System.Net.Mime.MediaTypeNames;
using Request = HealthMinistry.FCSObject.ToFCS.Request;

namespace HealthMinistry.Logic
{
    public class NautilusToFcs
    {

        private OracleCommand _cmd;
        private OracleConnection _oraCon;


    
        private string LOG;
        private string _xmlTemplateDirection;

        public NautilusToFcs()
        {
            _oraCon = new OracleConnection();
            OpenConnection();
            LOG = ConfigurationManager.AppSettings["LOG"];

            _xmlTemplateDirection = ConfigurationManager.AppSettings["xmlTemplateDirection"];

        }
        private void OpenConnection()
        {
            if (_oraCon.State != System.Data.ConnectionState.Open)
            {
                _oraCon = new OracleConnection(ConfigurationManager.ConnectionStrings["connectionString"].ConnectionString);

                _oraCon.Open();
            }
        }
      
        private string[] classes = new string[]
  { "Attached_Document", "ATTACHED_DOCUMENT", "Report_Notes", "Test_Results", "Test_Result", "Report_Notes", "Lab_Notes", "Report_Date" };

        public CoaObj GetCoaReportById(string coaId)
        {

            OpenConnection();

            string sql = $@"Select cru.U_Coa_Report_id,  NAME,U_STATUS,U_PDF_PATH,U_PARTIAL,Cru.U_Sdg,Cru.U_Created_On From U_Coa_Report cr
inner join  U_Coa_Report_User cru on Cru.U_Coa_Report_Id =Cr.U_Coa_Report_Id
and Cr.U_Coa_Report_Id={coaId}";




            CoaObj coaObj = new CoaObj();

            _cmd = new OracleCommand(sql, _oraCon);
            OracleDataReader reader3 = _cmd.ExecuteReader();
            if (reader3.HasRows)
            {


                coaObj = new CoaObj()
                {
                    NAME = reader3["NAME"].ToString(),

                    Status = reader3["U_STATUS"].ToString(),

                    PdfPath = reader3["U_PDF_PATH"].ToString(),

                    U_PARTIAL = reader3["U_PARTIAL"].ToString(),

                    U_Sdg = reader3["U_Sdg"].ToString(),

                    U_Created_On = (DateTime)reader3["U_Created_On"],

                    U_Coa_Report_id = reader3["U_Coa_Report_id"].ToString()



                };


            }

            return coaObj;

        }

        public SdgNautRes GetSDg(string sdgid)
        {

            OpenConnection();


            string sql = $@"select d.sdg_id, U_Comments,   U_COA_REMARKS,   U_FOOD_TEMPERATURE,
                            U_MINISTRY_OF_HEALTH,   D.External_Reference ,S.External_Reference SMPeXT
                            from sdg d,sdg_user du,SAMPLE S
                            where d.sdg_id =du.sdg_id and S.Sdg_Id=D.Sdg_Id AND d.sdg_id={sdgid}";




            SdgNautRes sdgNutREs = new SdgNautRes();

            _cmd = new OracleCommand(sql, _oraCon);
            OracleDataReader reader3 = _cmd.ExecuteReader();
            if (reader3.HasRows)
            {


                sdgNutREs = new SdgNautRes()
                {
                    U_Comments = reader3["U_Comments"].ToString(),
                    U_COA_REMARKS = reader3["U_COA_REMARKS"].ToString(),
                    sdg_id = reader3["sdg_id"].ToString(),
                    U_FOOD_TEMPERATURE = reader3["U_FOOD_TEMPERATURE"].ToString(),
                    U_MINISTRY_OF_HEALTH = reader3["U_MINISTRY_OF_HEALTH"].ToString(),
                    SMPeXT = Helpers.GetNumberOrDefault(reader3["SMPeXT"].ToString()),
                    ExternalReference = Helpers.GetNumberOrDefault(reader3["External_Reference"].ToString())
                };

            }


            return sdgNutREs;

        }


        private void GetSampleTestREsults(CoaObj coaReport, Request req)
        {
            string ttexid = ConfigurationManager.AppSettings["ExtraTest"];

            string sql = $@"SELECT 
    d.Name as LIMS_Samp_ID,
    s.Description as Sample_Description,
    a.Name as AliquotName,
    a.EXTERNAL_REFERENCE as TestSubCode,
    ttxu.u_Test_Code as LimsTestCode,
    ttx.Name as TestName,ttxu.U_Test_Template_Ex_Id as  Lims_Test_Code,
    r.formatted_Result as fResult,
    u.name  AS Measurement_Unit,
    ttxu.u_LOQ as LOQ,
    ttxu.u_Standard as Method
FROM u_coa_report_user cru
JOIN  u_coa_report cr ON cru.u_coa_report_Id = cr.u_coa_report_id
JOIN  sdg d ON d.sdg_id = cru.u_sdg
JOIN Sample s ON s.sdg_id = d.sdg_id
JOIN Aliquot a ON a.Sample_Id = s.Sample_Id
JOIN Aliquot_user au ON a.Aliquot_id = Au.Aliquot_id
JOIN U_Test_Template_Ex ttx ON ttx.U_Test_Template_Ex_id = Au.U_Test_Template_Extended
JOIN U_Test_Template_Ex_User ttxu ON ttx.U_Test_Template_Ex_id = ttxu.U_Test_Template_Ex_Id
JOIN Test t ON t.Aliquot_id = a.Aliquot_id
JOIN Result r ON r.Test_Id = t.Test_Id
left join unit u on U.Unit_Id=Ttxu.U_Default_Unit
WHERE s.Status != 'X' 
    AND a.Status != 'X'
    AND t.STATUS != 'X'
  AND d.STATUS = 'A'
    AND cru.u_Status = 'A'
    AND r.formatted_Result IS NOT NULL 
    AND  r.status in ('C','A')
    AND r.REPORTED = 'T'
    and  ttx.U_Test_Template_Ex_id<>{ttexid}
 and cr.u_coa_report_id={coaReport.U_Coa_Report_id}
ORDER BY s.Name, a.Name
";
            List<Test_Result> results = new List<Test_Result>();

            Notes notes = new Notes(new List<_LabNotes>() { });
            notes.Lab_Notes.Add(new _LabNotes("", ""));

            _cmd = new OracleCommand(sql, _oraCon);

            OracleDataReader reader3 = _cmd.ExecuteReader();
            while (reader3.Read())
            {
                Test_Result test_Result = new Test_Result()
                {
                    LIMS_Samp_ID = reader3["LIMS_Samp_ID"].ToString(),//עד 12 תווים
                    Sample_Description = reader3["Sample_Description"].ToString(),
                    //    Marking = "",
                    Test_Sub_Code = Helpers.GetNumberOrDefault(reader3["TestSubCode"].ToString()),
                    //    LimsTestCode = Helpers.GetNumberOrDefault(reader3["LimsTestCode"].ToString()),
                    Test_Name = reader3["TestName"].ToString(),
                    //    Result = "todo",//"\"" +  reader3["fResult"].ToString()+ "\"",
                    Result = Helpers.ReplaceHtmlEntities(reader3["fResult"].ToString()),

                    Lims_Test_Code = Helpers.GetNumberOrDefault(reader3["Lims_Test_Code"].ToString()),
                    Analyte_Name = "המכון למיקרוביולוגיה",//עד 50 תווים
                    Measurement_Unit = reader3["Measurement_Unit"].ToString(),
                    Method = reader3["Method"].ToString(),

                    //    Measurement_Unit = reader3["Method"].ToString(),
                    //   LOD = "",
                    //      LOQ = "",
                    //    uncertainty = "",
                    //   Notes = notes
                };


                req.Test_Results.Test_Result.Add(test_Result);


            }

        }
        
        public XmlDocument BuildXmlToFcs(CoaObj coaReport)
        {
            var sdg = GetSDg(coaReport.U_Sdg);

            string fileName = Path.GetFileName(coaReport.PdfPath);

            List<ATTACHED_DOCUMENT> AD = new List<ATTACHED_DOCUMENT>() {
                new ATTACHED_DOCUMENT("תעודה", 1, ".pdf",fileName, coaReport.FCSguide.Replace("\"",""), "") };

            Attached_Document at = new Attached_Document(AD);
            List<_LabNotes> LN = new List<_LabNotes>()
                {
                    new _LabNotes("",""),// sdg.U_Comments, "C"),
            new _LabNotes("","")// sdg.U_COA_REMARKS, "A")
                };
            Report_Notes Report_Notes = new Report_Notes(LN);

            List<LabNotes> Report_Notes2 = new List<LabNotes>();

            List<Test_Result> TR = new List<Test_Result>()
            {

            };

            Test_Results tr = new Test_Results(TR);


            string lab_code = ConfigurationManager.AppSettings["LAB_CODE"];

            Report_Date reportDate = new Report_Date(coaReport.U_Created_On.ToShortDateString());
            FCS_Date fcsreportDate = new FCS_Date()
            {
                Day = coaReport.U_Created_On.Day,
                Month = coaReport.U_Created_On.Month,
                Year = coaReport.U_Created_On.Year,
            };
            Request req = new Request()
            {
                Arrival_Temp = sdg.U_FOOD_TEMPERATURE,
                Attached_Document = at,
                Barcode = sdg.SMPeXT,// int.Parse(sdg.ExternalReference),
                Is_Complete = coaReport.U_PARTIAL == "F",
                Lab_Code = lab_code,// int.Parse(lab_code),
                Lab_Report_Ver = 1 /* Take from מהסוגריים*/,
                Lab_Report_ID = coaReport.NAME,
                //    Matching = null,
                Organization = sdg.U_MINISTRY_OF_HEALTH,
                //    Package_Condition = "",

                Sample_Form_Num = 1,//todo int.Parse(sdg.Samples.First().DelFileNum),
                Report_Date = reportDate,
                //Report_Notes = Report_Notes, 
                Tester_Name = "Institute for Food Microbiology and Consumer Goods Ltd",
                Test_Results = tr
            };
            GetSampleTestREsults(coaReport, req);
            XmlDocument xdoc = new XmlDocument();
            xdoc.Load(_xmlTemplateDirection);


            var nsmgr1 = new XmlNamespaceManager(xdoc.NameTable);
            nsmgr1.AddNamespace("fcs1", "http://schemas.datacontract.org/2004/07/FCS_LabsLib");
            XmlNode requestNode = xdoc.GetElementsByTagName("fcs:Request")[0];

            foreach (PropertyInfo prop in req.GetType().GetProperties())
            {
                var x = prop.Name;

                if (classes.Contains(prop.Name))
                {
                    XmlElement element = xdoc.CreateElement("fcs1", prop.Name,
                        "http://schemas.datacontract.org/2004/07/FCS_LabsLib");

                    switch (prop.Name)
                    {
                        case "Attached_Document":
                            element = GetAllChilds(req.Attached_Document.ATTACHED_DOCUMENT, "ATTACHED_DOCUMENT",
                                element, xdoc);
                            break;
                        //          case "Report_Notes":
                        //             element = GetAllChilds(req.Report_Notes.LabNotes, "LabNotes", element, xdoc);
                        //   break;
                        case "Test_Results":
                            element = GetAllChilds(req.Test_Results.Test_Result, "Test_Result", element, xdoc);
                            break;
                        case "Report_Date":
                            element = GetAllChildsReport_Date(req.Report_Date, "Report_Date", element, xdoc);
                            break;
                    }

                    requestNode.AppendChild(element);
                }
                else
                {
                    XmlElement element = AddXmlNode2(req, prop, xdoc);
                    requestNode.AppendChild(element);
                }
            }

            string dt = DateTime.Now.ToString("ddMMyyHHmmss");

            string XmlFileName = Path.Combine(LOG, "From Nautilus To FCS " + dt + "_" + coaReport.U_Coa_Report_id + ".xml");
            xdoc.Save(XmlFileName);

            Logger.Write($"From ParseToXML_res_1: xdoc created successfully");
            return xdoc;
        }


        //   creating a xml document for sending to fcs
        #region creating xml for result request
        protected XmlElement GetAllChildsReport_Date(Report_Date PropParent, string typeNode, XmlElement elementParent, XmlDocument xdoc)
        {
            try
            {
                XmlElement dayElement = xdoc.CreateElement("fcs1", "Day", "http://schemas.datacontract.org/2004/07/FCS_LabsLib");
                dayElement.InnerText = PropParent.Day;
                elementParent.AppendChild(dayElement);

                XmlElement monthElement = xdoc.CreateElement("fcs1", "Month", "http://schemas.datacontract.org/2004/07/FCS_LabsLib");
                monthElement.InnerText = PropParent.Month;
                elementParent.AppendChild(monthElement);

                XmlElement yearElement = xdoc.CreateElement("fcs1", "Year", "http://schemas.datacontract.org/2004/07/FCS_LabsLib");
                yearElement.InnerText = PropParent.Year;
                elementParent.AppendChild(yearElement);

                return elementParent;
            }
            catch (Exception e)
            {
                Logger.Write($"From GetAllChildsReport_Date: {e.Message}");
                return null;
            }
        }

        protected XmlElement GetAllChilds<T>(List<T> PropParent, string typeNode, XmlElement elementParent, XmlDocument xdoc) where T : class
        {
            try
            {
                foreach (var PropChilds in PropParent)
                {
                    XmlElement elementObj = xdoc.CreateElement("fcs1", typeNode, "http://schemas.datacontract.org/2004/07/FCS_LabsLib");

                    foreach (PropertyInfo prop in PropChilds.GetType().GetProperties())
                    {
                        if (prop.Name == "Notes")
                        {

                            XmlElement elementChild = xdoc.CreateElement("fcs1", prop.Name, "http://schemas.datacontract.org/2004/07/FCS_LabsLib");

                            elementChild = GetAllChildsLabNotes(PropChilds as Test_Result, "LabNotes", elementChild, xdoc);
                            elementObj.AppendChild(elementChild);
                        }
                        else
                        {
                            XmlElement elementChild = AddXmlNode2(PropChilds, prop, xdoc);
                            elementObj.AppendChild(elementChild);
                        }
                    }

                    elementParent.AppendChild(elementObj);
                }
                return elementParent;
            }
            catch (Exception e)
            {
                Logger.Write($"From GetAllChilds: {e.Message}");
                return null;
            }
        }

        protected XmlElement AddXmlNode2<T>(T objParent, PropertyInfo prop, XmlDocument xdoc) where T : class
        {
            try
            {
                XmlElement element = xdoc.CreateElement("fcs1", prop.Name, "http://schemas.datacontract.org/2004/07/FCS_LabsLib");
                string val = "";

                // Handle DateTime values
                if (prop.PropertyType == typeof(DateTime))
                {
                    DateTime dateValue = (DateTime)prop.GetValue(objParent, null);

                    // Create elements for day, month, and year
                    XmlElement dayElement = xdoc.CreateElement("fcs1", "Day", "http://schemas.datacontract.org/2004/07/FCS_LabsLib");
                    XmlElement monthElement = xdoc.CreateElement("fcs1", "Month", "http://schemas.datacontract.org/2004/07/FCS_LabsLib");
                    XmlElement yearElement = xdoc.CreateElement("fcs1", "Year", "http://schemas.datacontract.org/2004/07/FCS_LabsLib");

                    // Set values for day, month, and year
                    dayElement.InnerText = dateValue.Day.ToString();
                    monthElement.InnerText = dateValue.Month.ToString();
                    yearElement.InnerText = dateValue.Year.ToString();

                    // Append day, month, and year elements to the main element
                    element.AppendChild(dayElement);
                    element.AppendChild(monthElement);
                    element.AppendChild(yearElement);
                }
                else
                {
                    val = prop.GetValue(objParent, null) != null ? prop.GetValue(objParent, null).ToString() : "";

                    // Handle boolean values
                    if (prop.PropertyType == typeof(bool))
                    {
                        val = val.ToLower();
                    }

                    // Set the value for non-DateTime properties
                    element.InnerText = val;
                }

                return element;
            }
            catch (Exception e)
            {
                Logger.Write($"From AddXmlNode2: {e.Message}");
                return null;
            }
        }

        protected XmlElement GetAllChildsLabNotes(Test_Result PropParent, string typeNode, XmlElement elementParent, XmlDocument xdoc)
        {
            return null;
            //try
            //{
            //    foreach (var PropChilds in PropParent.Notes.Lab_Notes)
            //    {
            //        XmlElement elementObj = xdoc.CreateElement("fcs1", typeNode, "http://schemas.datacontract.org/2004/07/FCS_LabsLib");

            //        foreach (PropertyInfo prop in PropChilds.GetType().GetProperties())
            //        {

            //            XmlElement element = AddXmlNode2(PropChilds, prop, xdoc);
            //            elementObj.AppendChild(element);

            //        }
            //        elementParent.AppendChild(elementObj);
            //    }
            //    return elementParent;
            //}
            //catch (Exception e)
            //{
            //    Logger.Write($"From GetAllChildsLabNotes: {e.Message}");
            //    return null;
            //}
        }

        #endregion


    }



    public class AttachedDocument
    {
        public string DocumentName { get; set; }
        public string DocumentTypeCode { get; set; }
        public string FileExt { get; set; }
        public string FileName { get; set; }
        public string GUID { get; set; }
        public string LinkToDocument { get; set; }
    }
}