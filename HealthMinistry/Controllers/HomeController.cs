//using Common;


using HealthMinistry.NautObjects.FromNautilus;
using HealthMinistry.Common;
using HealthMinistry.FCSObjects;
using HealthMinistry.FCSObjects.FromFCS;
using HealthMinistry.Logic;
using HealthMinistry.NautObjects;
using HealthMinistry.NautObjects.ToNautilus;
using MSXML;
using Oracle.DataAccess.Client;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using System.Web.UI.WebControls;
using System.Xml;

using System.Xml.Linq;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Net.Http;

namespace HealthMinistry.Controllers
{
    public class HomeController : Controller
    {

        public ActionResult Index()
        {

            #region Result Form Testing
            // InitParam();

            // string pdfpath = string.Empty;        
            // NautilusToFcs toFcs = new NautilusToFcs();
            // string pdfFCSresult = "Some Guide return from FCS";
            // CoaObj coa = toFcs.GetCoaReportById("603");
            //coa.FCSguide=pdfFCSresult;
            // var NautDataXml = toFcs.BuildXmlToFcs(coa);


            #endregion
      
            #region Sample Form Testing

            //string f = "";
            //if (1 == 1)
            //{ f = "Sample Response 637815.xml"; }
            //else { f = "Sample with sampling val.xml"; }

            //InitParam();

            //var d = XDocument.Load(@"C:/TEMP/" + f);


            //Sample_Form_Response labSample = ExtractLabSampleForm(d);
            //d.Save(Path.Combine(LOG, "Sample Response " + labSample.Barcode + "YYY.xml"));
            //var x = labSample.TestDescription;
            //var y = labSample.requestedTestsList.Count;

            //FcsToNautilus fcsToNautilus = new FcsToNautilus(labSample);

            //string succsses = fcsToNautilus.Convert();
            ////validiating labSampleForm object

            //if (string.IsNullOrEmpty(succsses))
            //{
            //    CreateNautilusXML(fcsToNautilus.sdgNaut);
            //}
            #endregion




            return View();

        }

        private void InitParam()
        {

            _urlApiService = ConfigurationManager.AppSettings["API_HM_INTERFACE_ADDRESS"];
            _soapActionSample = ConfigurationManager.AppSettings["SOAP_ACTION_SAMPLE"];
            _soapActionResult = ConfigurationManager.AppSettings["SOAP_ACTION_RESULT"];
            LOG = ConfigurationManager.AppSettings["LOG"];
            _pfxPath = ConfigurationManager.AppSettings["PFX_DIRECTION_PATH"];
            _hmApiFiles = ConfigurationManager.AppSettings["API_HM_INTERFACE_ADDRESS_FILES"];
            _xmlTemplateDirection = ConfigurationManager.AppSettings["xmlTemplateDirection"];
            _cs = ConfigurationManager.ConnectionStrings["connectionStringEF"].ConnectionString;
        }


        #region variables
 
        private string _soapActionSample, _cs, _urlApiService, _soapActionResult, LOG, _pfxPath, _hmApiFiles, _xmlTemplateDirection;

        #region Constants


        #endregion

        #endregion


        #region Api functions


        [HttpPost]
        public string FcsSampleRequest(string barcode)
        {
            try
            {

                InitParam();

                Logger.Write("Start FcsSampleRequest");



                //building xml with barcode for 1st request
                string xmlStringBarcode = BuildSampleRquest(barcode);

                //adding fcs_msg - removed, stayed in git's 1st version
                //-----------------------------------------------------

                //sending 1st request to HM
                var response = SendRequest2FCS(xmlStringBarcode, _urlApiService, _soapActionSample, _pfxPath);

                //In case of error while sending - return.
                if (response.Equals(ConstantsMsg.msgList[5])) return ConstantsMsg.msgList[3];

                //parsing the response to xml
                XDocument xmlDocResponse = XDocument.Parse(response);
                xmlDocResponse.Save(Path.Combine(LOG, "Sample Response " + barcode + ".xml"));

                //parsing the xml to labSampleForm - a HM object
                Sample_Form_Response labSampleForm = ExtractLabSampleForm(xmlDocResponse);

                if (labSampleForm == null)
                {
                    Logger.Write($"From FcsSampleRequest: failed");
                    return ConstantsMsg.msgList[3];
                }

                //labSampleForm contains information about itself
                if (labSampleForm.ReturnCode != "0" && labSampleForm.ReturnCode != "1")
                {
                    Logger.Write($"The process was completed unsuccessfully, {labSampleForm.ReturnCodeDesc} )");
                    return labSampleForm.ReturnCodeDesc;
                }

                Logger.Write($"Converting Fcs To Nautilus.");

                FcsToNautilus fcsToNautilus = new FcsToNautilus(labSampleForm);
                string succsses = fcsToNautilus.Convert();
                //validiating labSampleForm object

                Logger.Write($"Converting " + succsses);


                if (!string.IsNullOrEmpty(succsses))
                    return succsses;

                Logger.Write($"Build Xml For Nautilus.");

                //if labSampleForm is valid, parsing it to xml
                DOMDocument nautilusXml = CreateNautilusXML(fcsToNautilus.sdgNaut);

                //The calling method from orderv2 proj gets the xml and proccesses it to a sdg
                if (nautilusXml != null)
                {
                    Logger.Write($"The process was completed successfully, xml string for creating sdg was sent to order_v2");
                    return nautilusXml.xml;
                }

                //debug mode - for postman
                //return labSampleForm.ToString();

                Logger.Write($"labSampleForm created, but docXml is null");
                return null;

            }
            catch (Exception ex)
            {
                Logger.Write($"From FcsSampleRequest: {ex.Message}");
                return ConstantsMsg.msgList[3];
            }

        }


        [HttpPost]
        public string FcsResultRequest(string coaId)
        {

            try
            {
                // bool dbg = true;// (Environment.MachineName == "ONE1PC2643");//true                    
                InitParam();
                Logger.Write("Start FcsResultRequest");
                string pdfpath = string.Empty;

                NautilusToFcs toFcs = new NautilusToFcs();
                CoaObj coaReport = toFcs.GetCoaReportById(coaId);



                if (coaReport == null) return ConstantsMsg.msgList[0];

                if (coaReport.Status != "A") { Logger.Write(ConstantsMsg.msgList[1]); return ConstantsMsg.msgList[1]; }
                if (coaReport.PdfPath == null) { Logger.Write(ConstantsMsg.msgList[2]); return ConstantsMsg.msgList[2]; }





                //     if (dbg)
                //        pdfpath = "C:\\TEMP\\TEST PDF.pdf";

                //sending 2nd request (result) proccessing response and sending back to HM

                string pdfFCSresult = SendRequestWithPDF(coaReport.PdfPath, _pfxPath);

                //sending request failed
                if (pdfFCSresult.Equals("failed")) return ConstantsMsg.msgList[3];



                //3rd request to HM - proccessed response from 2nd request:

                //parsing 2nd response to unique xml format


                coaReport.FCSguide = pdfFCSresult;

                string xmlString;


                    var NautDataXml = toFcs.BuildXmlToFcs(coaReport);
                    xmlString = NautDataXml.OuterXml;
                
          


                Logger.Write(xmlString);

                //sending 3rd request to HM, with proccessed string
                string responseFrom2ndResReq = SendRequest2FCS(xmlString, _urlApiService, _soapActionResult, _pfxPath);

                //loading 3rd response to xml (for easy serching information)
                string dt = DateTime.Now.ToString("dd-MM-yyyy-HH-mm-ss");
                XmlDocument xmlDoc = new XmlDocument();

                if (!IsXml(responseFrom2ndResReq))
                {
                    System.IO.File.WriteAllText(Path.Combine(LOG, "Result from Fcs COA -" + dt + ".log"), responseFrom2ndResReq);

                    return responseFrom2ndResReq;
                }
                else
                {



                    xmlDoc.LoadXml(responseFrom2ndResReq);


                    xmlDoc.Save(Path.Combine(LOG, "Result Response COA -" + dt + coaId + ".xml"));
                }
                XmlNamespaceManager nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
                nsManager.AddNamespace("a", "http://schemas.datacontract.org/2004/07/FCS_LabsLib");

                // Select the specified node
                XmlNode returnCodeNode = xmlDoc.SelectSingleNode("//a:Return_Code", nsManager);

                if (returnCodeNode != null)
                {
                    //Return_Code - tracking number to monitor the result
                    string returnCodeValue = returnCodeNode.InnerText;

                    //Return_Code is 0/1 - success
                    if (returnCodeValue == "0" || returnCodeValue == "1")
                    {
                        Logger.Write($"The process was completed successfully, return code: {returnCodeValue}");
                        return ConstantsMsg.msgList[4];
                    }
                    //something was wrong
                    else
                    {
                        //find Return_Code_Description and return it
                        XmlNode returnCodeDescNode = xmlDoc.SelectSingleNode("//a:Return_Code_Desc", nsManager);
                        if (returnCodeDescNode != null)
                        {
                            Logger.Write($"The process was completed unsuccessfully, return code: {returnCodeValue}");
                            return returnCodeDescNode.InnerText;
                        }
                    }

                }

                Logger.Write($"returnCodeDescNode is null");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Write($"From FcsResultRequest: {ex.Message}");
                return ex.Message;
            }

        }


        #endregion


        #region FCS requests

        /// <summary>
        /// Function For sending To health Ministry
        /// </summary>
        /// <param name="xmlString">Body</param>
        /// <param name="url"></param>
        /// <param name="soapAction"></param>
        /// <param name="pfxPath"></param>
        /// <returns></returns>
        string SendRequest2FCS(string xmlString, string url, string soapAction, string pfxPath)
        {


            bool dbg = false;

            try
            {

                bool isTestEnvironment = url.ToUpper().Contains("TEST");

                Logger.Write($"[DEBUG] Loading certificates: {DateTime.Now}");
                X509Certificate2Collection certificates = LoadCertificates(isTestEnvironment, pfxPath);
                Logger.Write($"[DEBUG] Certificates loaded: {DateTime.Now}");


                ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;


                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "text/xml;charset=utf-8";
                request.Timeout = 600000;
                request.Headers.Add("SOAPAction", soapAction);
                request.ClientCertificates = new X509CertificateCollection();
                request.ClientCertificates.AddRange(certificates);



                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                //      string b = "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"\r\n                  xmlns:fcs=\"http://www.moh.gov.co.il/FoodApp/FCS_Labs\"\r\n                  xmlns:fcs1=\"http://schemas.datacontract.org/2004/07/FCS_LabsLib\">\r\n   <soapenv:Header/>\r\n   <soapenv:Body>\r\n      <fcs:Lab_Results_Form>\r\n         <fcs:Request>\r\n            <fcs1:Arrival_Temp>קירור</fcs1:Arrival_Temp>\r\n            <fcs1:Attached_Document>\r\n               <fcs1:ATTACHED_DOCUMENT>\r\n                  <fcs1:Document_Name>TEST PDF.pdf</fcs1:Document_Name>\r\n                  <fcs1:Document_Type_Code>1</fcs1:Document_Type_Code>\r\n                  <fcs1:FileExt>.pdf</fcs1:FileExt>\r\n                  <fcs1:FileName>Test_File.pdf</fcs1:FileName>\r\n                  <fcs1:GUID>35fb2b9d-7c1f-41a9-8935-b66e0d39774c.pdf</fcs1:GUID>\r\n                  <fcs1:Link_To_Document>TEST PDF.pdf</fcs1:Link_To_Document>\r\n               </fcs1:ATTACHED_DOCUMENT>\r\n            </fcs1:Attached_Document>\r\n            <fcs1:Barcode>637815</fcs1:Barcode>\r\n            <fcs1:Is_Complete>true</fcs1:Is_Complete>\r\n            <fcs1:Lab_Code>13</fcs1:Lab_Code>\r\n            <fcs1:Lab_Report_ID>25-000009 (3)F</fcs1:Lab_Report_ID>\r\n            <fcs1:Lab_Report_Ver>1</fcs1:Lab_Report_Ver>\r\n            <fcs1:Report_Date>\r\n               <fcs1:Day>13</fcs1:Day>\r\n               <fcs1:Month>01</fcs1:Month>\r\n               <fcs1:Year>2025</fcs1:Year>\r\n            </fcs1:Report_Date>\r\n            <fcs1:Sample_Form_Num>1</fcs1:Sample_Form_Num>\r\n            <fcs1:Test_Results>\r\n               <fcs1:Test_Result>\r\n                  <fcs1:LIMS_Samp_ID>25-000009/001</fcs1:LIMS_Samp_ID>\r\n                  <fcs1:Measurement_Unit>SI 885/3</fcs1:Measurement_Unit>\r\n                  <fcs1:Result>15</fcs1:Result>\r\n                  <fcs1:Sample_Description>ירקות קפואים.</fcs1:Sample_Description>\r\n                  <fcs1:Test_Name>Total Count</fcs1:Test_Name>\r\n                  <fcs1:Test_Sub_Code>1</fcs1:Test_Sub_Code>\r\n               </fcs1:Test_Result>\r\n            </fcs1:Test_Results>\r\n            <fcs1:Tester_Name>המכון למיקרוביולוגיה</fcs1:Tester_Name>\r\n         </fcs:Request>\r\n      </fcs:Lab_Results_Form>\r\n   </soapenv:Body>\r\n</soapenv:Envelope>\r\n";
                //      string lbr_examp = "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:fcs=\"http://www.moh.gov.co.il/FoodApp/FCS_Labs\" xmlns:fcs1=\"http://schemas.datacontract.org/2004/07/FCS_LabsLib\">\r\n<soapenv:Header/>\r\n<soapenv:Body>\r\n<fcs:Lab_Results_Form>\r\n<!-- Optional: -->\r\n<fcs:Request>\r\n<!-- Optional: -->\r\n<fcs1:Arrival_Temp/>\r\n<!-- Optional: -->\r\n<fcs1:Attached_Document>\r\n<!-- 0 to 100 repetitions: -->\r\n<fcs1:ATTACHED_DOCUMENT>\r\n<!-- Optional: -->\r\n<fcs1:Document_Name>צרופה לתעודה</fcs1:Document_Name>\r\n<!-- Optional: -->\r\n<fcs1:Document_Type_Code>2</fcs1:Document_Type_Code>\r\n<!-- Optional: -->\r\n<fcs1:FileExt>jpg</fcs1:FileExt>\r\n<!-- Optional: -->\r\n<fcs1:FileName/>\r\n<!-- Optional: -->\r\n<fcs1:GUID/>\r\n<!-- Optional: -->\r\n<fcs1:Link_To_Document>?</fcs1:Link_To_Document>\r\n</fcs1:ATTACHED_DOCUMENT>\r\n</fcs1:Attached_Document>\r\n<!-- Optional: -->\r\n<fcs1:Barcode>387086</fcs1:Barcode>\r\n<!-- Optional: -->\r\n<fcs1:Is_Complete>true</fcs1:Is_Complete>\r\n<!-- Optional: -->\r\n<fcs1:Lab_Code>3</fcs1:Lab_Code>\r\n<!-- Optional: -->\r\n<fcs1:Lab_Report_ID>M-2020/764</fcs1:Lab_Report_ID>\r\n<!-- Optional: -->\r\n<fcs1:Lab_Report_Ver>1</fcs1:Lab_Report_Ver>\r\n<!-- Optional: -->\r\n<fcs1:Matching>true</fcs1:Matching>\r\n<!-- Optional: -->\r\n<fcs1:Organization/>\r\n<!-- Optional: -->\r\n<fcs1:Package_Condition/>\r\n<!-- Optional: -->\r\n<fcs1:Report_Date>\r\n<!-- Optional: -->\r\n<fcs1:Day>22</fcs1:Day>\r\n<!-- Optional: -->\r\n<fcs1:Month>11</fcs1:Month>\r\n<!-- Optional: -->\r\n<fcs1:Year>20</fcs1:Year>\r\n</fcs1:Report_Date>\r\n<!-- Optional: -->\r\n<fcs1:Report_Notes>\r\n<!-- 0 to 100 repetitions: -->\r\n<fcs1:LabNotes>\r\n<!-- Optional: -->\r\n<fcs1:Note/>\r\n<!-- Optional: -->\r\n<fcs1:Note_Type/>\r\n</fcs1:LabNotes>\r\n</fcs1:Report_Notes>\r\n<!-- Optional: -->\r\n<fcs1:Sample_Form_Num>1</fcs1:Sample_Form_Num>\r\n<!-- Optional: -->\r\n<fcs1:Test_Results>\r\n<!-- 0 to 100 repetitions: -->\r\n<fcs1:Test_Result>\r\n<!-- Optional: -->\r\n<fcs1:Analyte_Name>שינוי במרקם/ ריח/ צבע</fcs1:Analyte_Name>\r\n<!-- Optional: -->\r\n<fcs1:LIMS_Samp_ID>B-M-202003680</fcs1:LIMS_Samp_ID>\r\n<!-- Optional: -->\r\n<fcs1:LOD>0.00</fcs1:LOD>\r\n<!-- Optional: -->\r\n<fcs1:LOQ>0.00</fcs1:LOQ>\r\n<!-- Optional: -->\r\n<fcs1:Lims_Test_Code>119</fcs1:Lims_Test_Code>\r\n<!-- Optional: -->\r\n<fcs1:ManufResult/>\r\n<!-- Optional: -->\r\n<fcs1:Marking/>\r\n<!-- Optional: -->\r\n<fcs1:Measurement_Unit>גרם</fcs1:Measurement_Unit>\r\n<!-- Optional: -->\r\n<fcs1:Measurement_Unit_Code>1</fcs1:Measurement_Unit_Code>\r\n<!-- Optional: -->\r\n<fcs1:Method>?</fcs1:Method>\r\n<!-- Optional: -->\r\n<fcs1:Notes>\r\n<!-- 0 to 100 repetitions: -->\r\n<fcs1:LabNotes>\r\n<!-- Optional: -->\r\n<fcs1:Note/>\r\n<!-- Optional: -->\r\n<fcs1:Note_Type/>\r\n</fcs1:LabNotes>\r\n</fcs1:Notes>\r\n<!-- Optional: -->\r\n<fcs1:Result>5.6</fcs1:Result>\r\n<!-- Optional: -->\r\n<fcs1:Result_Num>1</fcs1:Result_Num>\r\n<!-- Optional: -->\r\n<fcs1:Sample_Description>?</fcs1:Sample_Description>\r\n<!-- Optional: -->\r\n<fcs1:Temprature>55</fcs1:Temprature>\r\n<!-- Optional: -->\r\n<fcs1:Test_Name>כשר השתמרות</fcs1:Test_Name>\r\n<!-- Optional: -->\r\n<fcs1:Test_Sub_Code>13</fcs1:Test_Sub_Code>\r\n<!-- Optional: -->\r\n<fcs1:uncertainty/>\r\n</fcs1:Test_Result>\r\n</fcs1:Test_Results>\r\n<!-- Optional: -->\r\n<fcs1:Tester_Name>ph</fcs1:Tester_Name>\r\n</fcs:Request>\r\n</fcs:Lab_Results_Form>\r\n</soapenv:Body>\r\n</soapenv:Envelope>";

                byte[] xmlBytes = Encoding.UTF8.GetBytes(xmlString);



                using (Stream requestStream = request.GetRequestStream())
                {

                    requestStream.Write(xmlBytes, 0, xmlBytes.Length);
                }

                Logger.Write("[DEBUG] Sending request to server..." + url);
                using (WebResponse response = request.GetResponse())
                {
                    Logger.Write("[DEBUG] Successfully received response.");

                    using (Stream responseStream = response.GetResponseStream())
                    {
                        if (dbg) Logger.Write("BEFORE reader");

                        using (StreamReader reader = new StreamReader(responseStream))
                        {
                            if (dbg) Logger.Write("BEFORE ReadToEnd");

                            string responseXml = reader.ReadToEnd();
                            Logger.Write(responseXml);
                            return responseXml;
                        }
                    }
                }

            }
            catch (WebException ex)
            {
                Logger.Write($"[ERROR] WebException occurred: {ex.Message}");

                // Log detailed error information
                if (ex.Response != null)
                {
                    using (Stream errorStream = ex.Response.GetResponseStream())
                    {
                        if (errorStream != null)
                        {
                            using (StreamReader reader = new StreamReader(errorStream))
                            {
                                string errorResponse = reader.ReadToEnd();
                                Logger.Write($"[ERROR] Response from server: {errorResponse}");
                                System.IO.File.WriteAllText(Path.Combine(LOG, "Error on SendRequest2FCS-" + DateTime.Now.ToString("dd MM yyyyy HH mm ss") + ".xml"), errorResponse);

                            }
                        }
                    }
                }

                // Log stack trace and inner exception details
                if (ex.InnerException != null)
                {
                    Logger.Write($"[ERROR] Inner Exception: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}");
                }

                return ConstantsMsg.msgList[5];
            }
        }

        private X509Certificate2Collection LoadCertificates(bool isTestEnvironment, string pfxPath)
        {
            try
            {
                if (isTestEnvironment)
                {
                    Logger.Write("Loading certificates for Test environment.");
                    var collection = new X509Certificate2Collection();
                    collection.Import(pfxPath, "12345678", X509KeyStorageFlags.PersistKeySet);
                    return collection;
                }
                else
                {
                    Logger.Write("Loading certificates for Production environment.");
                    string subjectName = "Institute For Food Microbiology";
                    using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
                    {
                        store.Open(OpenFlags.ReadOnly);
                        var certificates = store.Certificates.Find(X509FindType.FindBySubjectName, subjectName, false);
                        if (certificates.Count == 0)
                        {
                            throw new Exception($"No certificates found for subject name: {subjectName}");
                        }
                        return certificates;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Error loading certificates: {ex.Message}");
                throw;
            }
        }


        //sending api request (1st result)
        string SendRequestWithPDF(string coaReportPdfPath, string pfxPath)
        {

            try
            {
                var uploadUrl = _hmApiFiles;
                Logger.Write($"Start Upload PDF. \n COA Path is {coaReportPdfPath} \n Url is {uploadUrl}");



                bool isTestEnvironment = uploadUrl.ToUpper().Contains("TEST");


                Logger.Write($"[DEBUG] Loading certificates: {DateTime.Now}");
                X509Certificate2Collection certificates = LoadCertificates(isTestEnvironment, pfxPath);
                Logger.Write($"[DEBUG] Certificates loaded: {DateTime.Now}");

                ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;



                var client = new RestClient(uploadUrl);
                client.ClientCertificates = new X509CertificateCollection();
                client.ClientCertificates.AddRange(certificates);

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;


                var request = new RestRequest(Method.POST);
                request.AlwaysMultipartFormData = true;
                request.AddHeader("Content-Type", "multipart/form-data");
                request.AddFile("", coaReportPdfPath);

                var response = client.Execute(request);


                string dt = DateTime.Now.ToString("dd-MM-yyyy HH-mm-ss");
                Logger.LogRequestParameters(request, Path.Combine(LOG, "Upload Pdf Request" + dt + ".log"));
                Logger.LogResponse(response, Path.Combine(LOG, "Upload Pdf Response" + dt + ".log"));

                return response.Content;
            }
            catch (HttpRequestException hex)
            {
                // Log the specific HTTP error
                Logger.Write($"HTTP Request failed: {hex.Message}");
                System.IO.File.WriteAllText(Path.Combine(LOG, "Error on SendRequest2FCS-" + DateTime.Now.ToString("dd MM yyyyy HH mm ss") + ".xml"), hex.Message);

                throw;
            }
            catch (Exception ex)
            {
                Logger.Write($"From SendRequestWithPDF: {ex.Message}");

                return ConstantsMsg.msgList[5];
            }

        }
        #endregion

        #region other functions

        #region sample request

        //building initial xml(for sending to HM interface)
        private string BuildSampleRquest(string barcode_number)
        {
            try
            {
                string lab_code = ConfigurationManager.AppSettings["LAB_CODE"];
                string barcode = barcode_number;

                var xml = string.Format(
                    @"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:fcs=""http://www.moh.gov.co.il/FoodApp/FCS_Labs"" xmlns:fcs1=""http://schemas.datacontract.org/2004/07/FCS_LabsLib"">
             <soapenv:Header/>
             <soapenv:Body>
                <fcs:Lab_Sample_Form>
                   <fcs:Request>
                      <fcs1:Barcode>0</fcs1:Barcode>
                      <fcs1:Lab_Code>{1}</fcs1:Lab_Code>
                      <fcs1:Main_Barcode>{0}</fcs1:Main_Barcode>
                   </fcs:Request>
                </fcs:Lab_Sample_Form>
             </soapenv:Body>
          </soapenv:Envelope>", barcode, lab_code);

                Logger.Write(xml);
                return xml;
            }
            catch (Exception e)
            {
                Logger.Write($"From BuildInitialXML: {e.Message}");
                return ConstantsMsg.msgList[6];
            }
        }


        public Sample_Form_Response ExtractLabSampleForm(XDocument xmlDoc)
        {
            try
            {
                XNamespace ns = "http://schemas.datacontract.org/2004/07/FCS_LabsLib";
                XNamespace dataContractNs = "http://schemas.datacontract.org/2004/07/FCS_LabsLib";
                XNamespace xsiNs = "http://www.w3.org/2001/XMLSchema-instance";
                xsiNs = "i:";

                var result = new Sample_Form_Response();// LabSampleFormResult();

                var barcodeElement = xmlDoc.Descendants(ns + "Barcode").FirstOrDefault();
                if (barcodeElement != null)
                    result.Barcode = barcodeElement.Value;

                var containerNumElement = xmlDoc.Descendants(ns + "Container_Num").FirstOrDefault();
                if (containerNumElement != null)
                    result.ContainerNum = containerNumElement.Value;

                var countryNameElement = xmlDoc.Descendants(ns + "Country_Name").FirstOrDefault();
                if (countryNameElement != null)
                    result.CountryName = countryNameElement.Value;

                var delFileNumElement = xmlDoc.Descendants(dataContractNs + "Del_File_Num").FirstOrDefault();
                if (delFileNumElement != null)
                    result.DelFileNum = delFileNumElement.Value;

                var deliveryToLabElement = xmlDoc.Descendants(dataContractNs + "Delivery_To_Lab").FirstOrDefault();
                if (deliveryToLabElement != null)
                    result.DeliveryToLab = deliveryToLabElement.Value;

                var amilDetailsElement = xmlDoc.Descendants(dataContractNs + "Amil_Details").FirstOrDefault();
                if (amilDetailsElement != null && amilDetailsElement.HasElements)
                {
                    result.AmilDetails = new AmilDetails
                    {
                        Address = amilDetailsElement.Elements(dataContractNs + "Address").FirstOrDefault().Value,
                        City = amilDetailsElement.Elements(dataContractNs + "City").FirstOrDefault().Value,
                        CompanyID = amilDetailsElement.Elements(dataContractNs + "Company_ID").FirstOrDefault().Value,
                        CompanyName = amilDetailsElement.Elements(dataContractNs + "Company_Name").FirstOrDefault().Value,
                        Email = amilDetailsElement.Elements(dataContractNs + "Email").FirstOrDefault().Value,
                        Fax = amilDetailsElement.Elements(dataContractNs + "Fax").FirstOrDefault().Value,
                        Phone1 = amilDetailsElement.Elements(dataContractNs + "Phone1").FirstOrDefault().Value,
                        Phone2 = amilDetailsElement.Elements(dataContractNs + "Phone2").FirstOrDefault().Value,
                        ZIPCode = amilDetailsElement.Elements(dataContractNs + "ZIP_Code").FirstOrDefault().Value,
                    };
                }

                var attachedDocumentElement = xmlDoc.Descendants(dataContractNs + "Attached_Document").FirstOrDefault();
                if (attachedDocumentElement != null)
                    result.AttachedDocument = attachedDocumentElement.Value;

                var batchNumElement = xmlDoc.Descendants(dataContractNs + "Batch_Num").FirstOrDefault();
                if (batchNumElement != null)
                    result.BatchNum = batchNumElement.Value;

                var samplingDateElement = xmlDoc.Descendants(dataContractNs + "Sampling_Date").FirstOrDefault();
                if (samplingDateElement != null)
                    result.SamplingDate = GetDate(samplingDateElement, ns);

                var expiryDateElement = xmlDoc.Descendants(dataContractNs + "Expiry_Date").FirstOrDefault();
                if (expiryDateElement != null)
                    result.ExpiryDate = GetDate(expiryDateElement, ns);

                var manufactureDateElement = xmlDoc.Descendants(dataContractNs + "Manufacture_Date").FirstOrDefault();
                if (manufactureDateElement != null)
                    result.ManufactureDate = GetDate(manufactureDateElement, ns);

                var formTypeElement = xmlDoc.Descendants(dataContractNs + "Form_Type").FirstOrDefault();
                if (formTypeElement != null)
                    result.FormType = formTypeElement.Value;

                var importerDetailsElement = xmlDoc.Descendants(dataContractNs + "Importer_Details").FirstOrDefault();
                if (importerDetailsElement != null && importerDetailsElement.HasElements)
                {
                    result.ImporterDetails = new ImporterDetails
                    {
                        Address = importerDetailsElement.Elements(dataContractNs + "Address").FirstOrDefault().Value,
                        City = importerDetailsElement.Elements(dataContractNs + "City").FirstOrDefault().Value,
                        CompanyId = importerDetailsElement.Elements(dataContractNs + "Company_ID").FirstOrDefault().Value,
                        CompanyName = importerDetailsElement.Elements(dataContractNs + "Company_Name").FirstOrDefault().Value,
                        Email = importerDetailsElement.Elements(dataContractNs + "Email").FirstOrDefault().Value,
                        Fax = importerDetailsElement.Elements(dataContractNs + "Fax").FirstOrDefault().Value,
                        Phone1 = importerDetailsElement.Elements(dataContractNs + "Phone1").FirstOrDefault().Value,
                        Phone2 = importerDetailsElement.Elements(dataContractNs + "Phone2").FirstOrDefault().Value,
                        ZIPCode = importerDetailsElement.Elements(dataContractNs + "ZIP_Code").FirstOrDefault().Value,
                    };
                }

                var importerStoreElement = xmlDoc.Descendants(dataContractNs + "Importer_Store").FirstOrDefault();
                if (importerStoreElement != null)
                    result.ImporterStore = importerStoreElement.Value;

                var inspectorTitleElement = xmlDoc.Descendants(dataContractNs + "Inspector_Title").FirstOrDefault();
                if (inspectorTitleElement != null)
                    result.InspectorTitle = inspectorTitleElement.Value;


                //
                var isOpenPackagingElement = xmlDoc.Descendants(dataContractNs + "Is_Open_Packaging").FirstOrDefault();
                if (isOpenPackagingElement.Attribute(xsiNs + "nil") != null)
                    result.IsOpenPackaging = isOpenPackagingElement.Attribute(xsiNs + "nil").Value;

                //
                var isOriginalPackingElement = xmlDoc.Descendants(dataContractNs + "Is_Original_Packing").FirstOrDefault();
                if (isOriginalPackingElement.Attribute(xsiNs + "nil") != null)
                    result.IsOriginalPacking = isOriginalPackingElement.Attribute(xsiNs + "nil").Value;

                //
                var isVetElement = xmlDoc.Descendants(dataContractNs + "Is_Vet").FirstOrDefault();
                if (isVetElement.Attribute(xsiNs + "nil") != null)
                    result.IsVet = isVetElement.Attribute(xsiNs + "nil").Value;

                //Old Version
                //var numOfSamplesElement = xmlDoc.Descendants(dataContractNs + "Num_Of_Samples").FirstOrDefault();
                //if (numOfSamplesElement != null)
                //    result.NumOfSamples = numOfSamplesElement.Value;

                //var numOfSamplesVetElement = xmlDoc.Descendants(dataContractNs + "Num_Of_Samples_Vet").FirstOrDefault();
                //if (numOfSamplesVetElement != null)
                //    result.NumOfSamplesVet = numOfSamplesVetElement.Value;

                var organizationElement = xmlDoc.Descendants(dataContractNs + "Organization").FirstOrDefault();
                if (organizationElement != null)
                    result.Organization = organizationElement.Value;

                var packingTypeElement = xmlDoc.Descendants(dataContractNs + "Packing_Type").FirstOrDefault();
                if (packingTypeElement != null)
                    result.PackingType = packingTypeElement.Value;

                var payerIDElement = xmlDoc.Descendants(dataContractNs + "Payer_ID").FirstOrDefault();
                if (payerIDElement != null)
                    result.PayerID = payerIDElement.Value;

                var producerCountryElement = xmlDoc.Descendants(dataContractNs + "Producer_Country").FirstOrDefault();
                if (producerCountryElement != null)
                    result.ProducerCountry = producerCountryElement.Value;

                var producerNameElement = xmlDoc.Descendants(dataContractNs + "Producer_Name").FirstOrDefault();
                if (producerNameElement != null)
                    result.ProducerName = producerNameElement.Value;

                var productBrandNameElement = xmlDoc.Descendants(dataContractNs + "Product_Brand_Name").FirstOrDefault();
                if (productBrandNameElement != null)
                    result.ProductBrandName = productBrandNameElement.Value;

                var productGroupCodeElement = xmlDoc.Descendants(dataContractNs + "Product_Group_Code").FirstOrDefault();
                if (productGroupCodeElement != null)
                    result.ProductGroupCode = productGroupCodeElement.Value;

                var productGroupDescriptionElement = xmlDoc.Descendants(dataContractNs + "Product_Group_Description").FirstOrDefault();
                if (productGroupDescriptionElement != null)
                    result.ProductGroupDescription = productGroupDescriptionElement.Value;

                //
                var productOwnerElement = xmlDoc.Descendants(dataContractNs + "Product_Owner").FirstOrDefault();
                if (productOwnerElement.Attribute(xsiNs + "nil") != null)
                    result.ProductOwner = productOwnerElement.Attribute(xsiNs + "nil").Value;

                var productNameEngElement = xmlDoc.Descendants(dataContractNs + "Product_name_eng").FirstOrDefault();
                if (productNameEngElement != null)
                    result.ProductNameEng = productNameEngElement.Value;

                var productNameHebElement = xmlDoc.Descendants(dataContractNs + "Product_name_heb").FirstOrDefault();
                if (productNameHebElement != null)
                    result.ProductNameHeb = productNameHebElement.Value;

                var propertyPlusElement = xmlDoc.Descendants(dataContractNs + "Property_Plus").FirstOrDefault();
                if (propertyPlusElement != null)
                    result.PropertyPlus = propertyPlusElement.Value;

                //
                var remarkElement = xmlDoc.Descendants(dataContractNs + "Remark").FirstOrDefault();
                if (remarkElement.Attribute(xsiNs + "nil") != null)
                    result.Remark = remarkElement.Attribute(xsiNs + "nil").Value;

                //It's for old version
                //var requestedTestsElement = xmlDoc.Descendants(dataContractNs + "Requested_Tests").FirstOrDefault();
                //if (requestedTestsElement != null)
                //{

                //    result.RequestedTests = requestedTestsElement.Value;
                //}

                List<RequestedTest> requestedTestsList = xmlDoc.Descendants(ns + "REQUESTED_TESTS")
                     .Select(x => new RequestedTest
                     {
                         Barcode = (string)x.Element(dataContractNs + "Barcode"),
                         BatchNum = (string)x.Element(dataContractNs + "Batch_Num"),
                         NumOfSamples = (int?)x.Element(dataContractNs + "Num_Of_Samples") ?? 0,
                         NumOfSamplesVet = (int?)x.Element(dataContractNs + "Num_Of_Samples_Vet") ?? 0,
                         TestDescription = (string)x.Element(dataContractNs + "Test_Description"),
                         TestSubCode = (int?)x.Element(dataContractNs + "Test_Sub_Code") ?? 0,
                         TestTypeCode = (int?)x.Element(dataContractNs + "Test_Type_Code") ?? 0
                     })
       .ToList();
                result.requestedTestsList = requestedTestsList;



                var returnCodeElement = xmlDoc.Descendants(dataContractNs + "Return_Code").FirstOrDefault();
                if (returnCodeElement != null)
                    result.ReturnCode = returnCodeElement.Value;

                var returnCodeDescElement = xmlDoc.Descendants(dataContractNs + "Return_Code_Desc").FirstOrDefault();
                if (returnCodeDescElement != null)
                {
                    result.ReturnCodeDesc = returnCodeDescElement.Value;
                }

                var sampleFormNumElement = xmlDoc.Descendants(dataContractNs + "Sample_Form_Num").FirstOrDefault();
                if (sampleFormNumElement != null)
                    result.SampleFormNum = sampleFormNumElement.Value;

                var samplingInspectorElement = xmlDoc.Descendants(dataContractNs + "Sampling_Inspector").FirstOrDefault();
                if (samplingInspectorElement != null)
                    result.SamplingInspector = samplingInspectorElement.Value;

                var samplingPlaceElement = xmlDoc.Descendants(dataContractNs + "Sampling_Place").FirstOrDefault();
                if (samplingPlaceElement != null)
                    result.SamplingPlace = samplingPlaceElement.Value;

                var samplingReasonElement = xmlDoc.Descendants(dataContractNs + "Sampling_Reason").FirstOrDefault();
                if (samplingReasonElement != null)
                    result.SamplingReason = samplingReasonElement.Value;

                var samplingTempElement = xmlDoc.Descendants(dataContractNs + "Sampling_Temp").FirstOrDefault();
                if (samplingTempElement != null)
                    result.SamplingTemp = samplingTempElement.Value;

                var samplingTimeElement = xmlDoc.Descendants(dataContractNs + "Sampling_Time").FirstOrDefault();
                if (samplingTimeElement != null)
                    result.SamplingTime = samplingTimeElement.Value;

                var testDescriptionElement = xmlDoc.Descendants(dataContractNs + "Test_Description").FirstOrDefault();
                if (testDescriptionElement != null)
                    result.TestDescription = testDescriptionElement.Value;

                var testSubCodeElement = xmlDoc.Descendants(dataContractNs + "Test_Sub_Code").FirstOrDefault();
                if (testSubCodeElement != null)
                    result.TestSubCode = testSubCodeElement.Value;

                var emailToElement = xmlDoc.Descendants(dataContractNs + "eMailTo").FirstOrDefault();
                if (emailToElement != null)
                    result.EmailTo = emailToElement.Value;

                return result;
            }
            catch (Exception e)
            {
                Logger.Write($"From ExtractLabSampleForm: {e.Message}");
                return null;
            }


        }

        private DOMDocument CreateNautilusXML(SdgNaut sdg)//, string barcode)
        {
            try
            {


                DOMDocument objDom = new DOMDocument();

                //Creates lims request element
                var objLimsElem = objDom.createElement("lims-request");
                objDom.appendChild(objLimsElem);

                // Creates login request element
                var objLoginElem = objDom.createElement("login-request");
                objLimsElem.appendChild(objLoginElem);

                // Creates Entity element
                var objSdg = objDom.createElement("SDG");
                objLoginElem.appendChild(objSdg);

                // Creates   create-by-workflow element
                var objCreateByWorkflowElem = objDom.createElement("create-by-workflow");
                objSdg.appendChild(objCreateByWorkflowElem);

                var objWorkflowName = objDom.createElement("workflow-name");
                objCreateByWorkflowElem.appendChild(objWorkflowName);
                objWorkflowName.text = ConfigurationManager.AppSettings["SDG_WF"];

                objSdg.appendChild(objDom.createElement("U_SAMPLED_BY")).text = ConfigurationManager.AppSettings["SAMPLED_BY"];
                objSdg.appendChild(objDom.createElement("DESCRIPTION")).text = ConfigurationManager.AppSettings["SDG_DESC"];
                objSdg.appendChild(objDom.createElement("U_FCS_MSG_ID")).text = "TEMP";
                objSdg.appendChild(objDom.createElement("EXTERNAL_REFERENCE")).text = sdg.Barcode;
                objSdg.appendChild(objDom.createElement("U_MINISTRY_OF_HEALTH")).text = sdg.U_MINISTRY_OF_HEALTH;
                objSdg.appendChild(objDom.createElement("U_SDG_CLIENT")).text = sdg.Client.U_SDG_CLIENT;
                objSdg.appendChild(objDom.createElement("U_PHONE")).text = sdg.Client.U_PHONE;
                objSdg.appendChild(objDom.createElement("U_ADDRESS")).text = sdg.Client.U_ADDRESS;
                objSdg.appendChild(objDom.createElement("U_EMAIL")).text = sdg.Client.U_EMAIL;
                objSdg.appendChild(objDom.createElement("U_CONTECT_NAME")).text = sdg.Client.U_CONTECT_NAME;
                objSdg.appendChild(objDom.createElement("U_CONTACT_PHONE")).text = sdg.Client.U_CONTACT_PHONE;
                objSdg.appendChild(objDom.createElement("U_FOOD_TEMPERATURE")).text = sdg.U_FOOD_TEMPERATURE;


                foreach (var smpl in sdg.Samples)
                {


                    // Create samples
                    var objSample = objDom.createElement("SAMPLE");
                    objSdg.appendChild(objSample);

                    objCreateByWorkflowElem = objDom.createElement("create-by-workflow");
                    objSample.appendChild(objCreateByWorkflowElem);

                    objWorkflowName = objDom.createElement("workflow-name");
                    objCreateByWorkflowElem.appendChild(objWorkflowName);
                    objWorkflowName.text = ConfigurationManager.AppSettings["SAMPLE_WF"];



                    objSample.appendChild(objDom.createElement("DESCRIPTION")).text = smpl.description;
                    objSample.appendChild(objDom.createElement("EXTERNAL_REFERENCE")).text = smpl.Barcode;
                    objSample.appendChild(objDom.createElement("U_DEL_FILE_NUM")).text = smpl.DelFileNum;
                    objSample.appendChild(objDom.createElement("U_CONTAINER_NUMBER")).text = smpl.ContainerNum;
                    objSample.appendChild(objDom.createElement("PRODUCT_ID")).text = smpl.product;
                    objSample.appendChild(objDom.createElement("U_DATE_PRODUCTION")).text = smpl.dateProduction;
                    objSample.appendChild(objDom.createElement("U_BATCH")).text = smpl.batchNum;
                    objSample.appendChild(objDom.createElement("U_TXT_SAMPLING_TIME")).text = smpl.SamplingDate;
                    objSample.appendChild(objDom.createElement("U_TXT_SAMPLING_TIME2")).text = smpl.SamplingTime;
                    objSample.appendChild(objDom.createElement("U_FIELD_TEXT_1")).text = smpl.Producer_Name;
                    objSample.appendChild(objDom.createElement("U_FIELD_TEXT_2")).text = sdg.Barcode;


                    foreach (AliquotObj aliq in smpl.aliquots)
                    {
                        var objAliquot = objDom.createElement("ALIQUOT");
                        objSample.appendChild(objAliquot);

                        objCreateByWorkflowElem = objDom.createElement("create-by-workflow");
                        objAliquot.appendChild(objCreateByWorkflowElem);

                        objWorkflowName = objDom.createElement("workflow-name");
                        objCreateByWorkflowElem.appendChild(objWorkflowName);
                        objWorkflowName.text = aliq.Workflow_name;

                        //var aliquotWorkf = objDom.createElement("aliquotWorkf");
                        //objAliquot.appendChild(aliquotWorkf);
                        //aliquotWorkf.text = aliq.Workflow_name;

                        objAliquot.appendChild(objDom.createElement("DESCRIPTION")).text = aliq.DESCRIPTION;
                        objAliquot.appendChild(objDom.createElement("U_TEST_TEMPLATE_EXTENDED")).text = aliq.U_TEST_TEMPLATE_EXTENDED;
                        objAliquot.appendChild(objDom.createElement("EXTERNAL_REFERENCE")).text = aliq.External_ref;
                        objAliquot.appendChild(objDom.createElement("U_RETEST")).text = "F";


                    }
                }
                //debug mode
                objDom.save(Path.Combine(LOG, "Login " + sdg.Barcode + ".xml"));
                Logger.Write($"From Create_XML: xml obj created successfully");
                return objDom;
            }
            catch (Exception ex)
            {
                Logger.Write($"From Create_XML: {ex.Message}");
                return null;
            }
        }

        #endregion


        #region generic functions



        private DateTime? GetDate(XElement dateElements, XNamespace ns)
        {
            try
            {
                int year, month, day;

                XElement yearElement = dateElements.Elements(ns + "Year").FirstOrDefault();
                XElement monthElement = dateElements.Elements(ns + "Month").FirstOrDefault();
                XElement dayElement = dateElements.Elements(ns + "Day").FirstOrDefault();

                string yearValue = (yearElement != null) ? yearElement.Value : null;
                string monthValue = (monthElement != null) ? monthElement.Value : null;
                string dayValue = (dayElement != null) ? dayElement.Value : null;

                if (!string.IsNullOrWhiteSpace(yearValue) && int.TryParse(yearValue, out year) &&
                    !string.IsNullOrWhiteSpace(monthValue) && int.TryParse(monthValue, out month) &&
                    !string.IsNullOrWhiteSpace(dayValue) && int.TryParse(dayValue, out day))
                {
                    if (year == 0 || month == 0 || day == 0)
                    {
                        return null;
                    }
                    return new DateTime(year, month, day);
                }

            }
            catch (Exception ex)
            {

                Logger.Write($"From GetDate: {ex.Message}");
                return null;
            }

            return null;

        }


        #endregion

        #endregion

        public static bool IsXml(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;

            // Simple regex to check for basic XML structure
            return Regex.IsMatch(input.Trim(), @"^<[^?].*>$", RegexOptions.Singleline);
        }

    }
}