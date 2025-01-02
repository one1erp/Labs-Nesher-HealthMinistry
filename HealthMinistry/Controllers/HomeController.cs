//using Common;
using DAL;
using HealthMinistry.FCSObjects;
using HealthMinistry.Logic;
using HealthMinistry.NautObjects;
using Microsoft.SqlServer.Server;
using MSXML;
using Oracle.DataAccess.Client;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Mvc;
using System.Web.UI.WebControls;
using System.Xml;

using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace HealthMinistry.Controllers
{
    public class HomeController : Controller
    {

        public ActionResult Index()
        {


            #region Testing

            string f = "";
            if (1 == 2)
            { f = "Sample Response 2024201756651.xml"; }
            else { f = "Sample with sampling val.xml"; }

            InitParam();

            var d = XDocument.Load(@"C:/TEMP/" + f);

            LabSampleFormResult labSample = ExtractLabSampleForm(d);
            d.Save(Path.Combine(LOG, "Sample Response " + labSample.Barcode + "YYY.xml"));
            var x = labSample.TestDescription;
            var y = labSample.requestedTestsList.Count;

            FcsToNautilus fcsToNautilus = new FcsToNautilus(labSample);

            string succsses = fcsToNautilus.Convert();
            //validiating labSampleForm object

            if (string.IsNullOrEmpty(succsses))
            {
                CreateNautilusXML(fcsToNautilus.sdgNaut);
            }
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

        public IDataLayer _dal = null;
        private OracleCommand _cmd;

        private string sql;
        private string _errorMsg = "";
        private string _soapActionSample, _cs, _urlApiService, _soapActionResult, LOG, _pfxPath, _hmApiFiles, _xmlTemplateDirection;

        #region Constants

        private string[] classes = new string[]
        { "Attached_Document", "ATTACHED_DOCUMENT", "Report_Notes", "Test_Results", "Test_Result", "Report_Notes", "Lab_Notes", "Report_Date" };

        #endregion
        OracleConnection oraCon = new OracleConnection(ConfigurationManager.ConnectionStrings["connectionString"].ConnectionString);

        #endregion


        #region Api functions


        [HttpPost]
        public string FcsSampleRequest(string barcode)
        {
            try
            {

                InitParam();
                Write("Start FcsSampleRequest");
                openSqlConnection();

                if (oraCon.State != System.Data.ConnectionState.Open) oraCon.Open();

                //building xml with barcode for 1st request
                string xmlStringBarcode = BuildSampleRquest(barcode);

                //adding fcs_msg - removed, stayed in git's 1st version
                //-----------------------------------------------------

                //sending 1st request to HM
                var response = SendRequest2FCS(xmlStringBarcode, _urlApiService, _soapActionSample, _pfxPath);

                //In case of error while sending - return.
                if (response.Equals(Constants.msgList[5])) return Constants.msgList[3];

                //parsing the response to xml
                XDocument xmlDocResponse = XDocument.Parse(response);
                xmlDocResponse.Save(Path.Combine(LOG, "Sample Response " + barcode + ".xml"));

                //parsing the xml to labSampleForm - a HM object
                LabSampleFormResult labSampleForm = ExtractLabSampleForm(xmlDocResponse);
                if (labSampleForm == null)
                {
                    Write($"From FcsSampleRequest: failed");
                    return Constants.msgList[3];
                }

                //labSampleForm contains information about itself
                if (labSampleForm.ReturnCode != "0" && labSampleForm.ReturnCode != "1")
                {
                    Write($"The process was completed unsuccessfully, {labSampleForm.ReturnCodeDesc} )");
                    return labSampleForm.ReturnCodeDesc;
                }

                Write($"Converting Fcs To Nautilus.");

                FcsToNautilus fcsToNautilus = new FcsToNautilus(oraCon, labSampleForm);
                string succsses = fcsToNautilus.Convert();
                //validiating labSampleForm object

                Write($"Converting "+ succsses);


                if (!string.IsNullOrEmpty(succsses))
                    return succsses;

                Write($"Build Xml For Nautilus.");

                //if labSampleForm is valid, parsing it to xml
                DOMDocument nautilusXml = CreateNautilusXML(fcsToNautilus.sdgNaut);

                //The calling method from orderv2 proj gets the xml and proccesses it to a sdg
                if (nautilusXml != null)
                {
                    Write($"The process was completed successfully, xml string for creating sdg was sent to order_v2");
                    return nautilusXml.xml;
                }

                //debug mode - for postman
                //return labSampleForm.ToString();

                Write($"labSampleForm created, but docXml is null");
                return null;

            }
            catch (Exception ex)
            {
                Write($"From FcsSampleRequest: {ex.Message}");
                return Constants.msgList[3];
            }

        }


        [HttpPost]
        public string FcsResultRequest(string coaId)
        {

            try
            {
                bool dbg = true;// (Environment.MachineName == "ONE1PC2643");//true                    
                InitParam();
                Write("Start FcsResultRequest");
                string pdfpath = string.Empty;
                COA_Report coaReport = new COA_Report();


                _dal = new DataLayer();
                _dal.Connect(_cs);

                coaReport = _dal.GetCoaReportById(long.Parse(coaId));

                if (coaReport == null) return Constants.msgList[0];

                if (coaReport.Status != "A")
                { Write(Constants.msgList[1]); return Constants.msgList[1]; }
                if (coaReport.PdfPath == null)
                { Write(Constants.msgList[2]); return Constants.msgList[2]; }
                pdfpath = coaReport.PdfPath;



                if (dbg)
                    pdfpath = "C:\\TEMP\\TEST PDF.pdf";
                else
                {

                }
                //sending 2nd request (result) proccessing response and sending back to HM
                string resultRequest1_response = string.Empty;

                resultRequest1_response = SendRequestWithPDF(pdfpath, _pfxPath);

                //sending request failed
                if (resultRequest1_response.Equals("failed")) return Constants.msgList[3];


                //return "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa debug";

                //3rd request to HM - proccessed response from 2nd request:

                //parsing 2nd response to unique xml format
                var xmlFile = ParseToXML_res_1(coaReport, resultRequest1_response);

                //parsing the xml to string
                string xmlString = xmlFile.OuterXml;

                //sending 3rd request to HM, with proccessed string
                string responseFrom2ndResReq = SendRequest2FCS(xmlString, _urlApiService, _soapActionResult, _pfxPath);

                //loading 3rd response to xml (for easy serching information)

                XmlDocument xmlDoc = new XmlDocument();
                if (!IsXml(responseFrom2ndResReq)) return responseFrom2ndResReq;

                xmlDoc.LoadXml(responseFrom2ndResReq);



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
                        Write($"The process was completed successfully, return code: {returnCodeValue}");
                        return Constants.msgList[4];
                    }
                    //something was wrong
                    else
                    {
                        //find Return_Code_Description and return it
                        XmlNode returnCodeDescNode = xmlDoc.SelectSingleNode("//a:Return_Code_Desc", nsManager);
                        if (returnCodeDescNode != null)
                        {
                            Write($"The process was completed unsuccessfully, return code: {returnCodeValue}");
                            return returnCodeDescNode.InnerText;
                        }
                    }

                }

                Write($"returnCodeDescNode is null");
                return null;
            }
            catch (Exception ex)
            {
                Write($"From FcsResultRequest: {ex.Message}");
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

                bool test = url.ToUpper().Contains("TEST");
                ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
                X509Certificate2Collection collection;
                if (test)
                {
                    Write("test section");
                    collection = new X509Certificate2Collection();
                    collection.Import(pfxPath, "12345678", X509KeyStorageFlags.PersistKeySet);
                }
                else
                {
                    Write("Prod section");
                    string subjectName = "Institute For Food Microbiology";

                    X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                    store.Open(OpenFlags.ReadOnly);
                    X509Certificate2Collection certificates = store.Certificates;
                    X509Certificate2Collection foundCertificates = certificates.Find(X509FindType.FindBySubjectName, subjectName, false);
                    collection = certificates.Find(X509FindType.FindBySubjectName, subjectName, false);

                }


                if (dbg) Write("BEFORE request");

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.ClientCertificates = new X509CertificateCollection();
                request.ClientCertificates.AddRange(collection);

                if (dbg) Write(" SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tl");

                //System.Net.ServicePointManager.SecurityProtocol = (SecurityProtocolType)(0xc0 | 0x300 | 0xc00); 
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                request.Method = "POST";
                request.ContentType = "text/xml;charset=utf-8";
                request.Timeout = 600000;
                request.Headers.Add("SOAPAction", soapAction);


                byte[] xmlBytes = Encoding.UTF8.GetBytes(xmlString);



                using (Stream requestStream = request.GetRequestStream())
                {

                    requestStream.Write(xmlBytes, 0, xmlBytes.Length);

                    if (dbg) Write("BEFORE response");
                    if (dbg) Write("BEFORE response 3333333333");

                    // Basic logging properties

                    if (dbg) LogReuest(request);

                    using (WebResponse response = request.GetResponse())
                    {
                        if (dbg) Write("BEFORE responseStream");

                        using (Stream responseStream = response.GetResponseStream())
                        {
                            if (dbg) Write("BEFORE reader");

                            using (StreamReader reader = new StreamReader(responseStream))
                            {
                                if (dbg) Write("BEFORE ReadToEnd");

                                string responseXml = reader.ReadToEnd();
                                Write(responseXml);
                                return responseXml;
                            }
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                Write($"From SendRequest2FCS: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Write(ex.InnerException.StackTrace);
                    Write(ex.InnerException.Source);
                    Write(ex.InnerException.ToString());
                    Write(ex.InnerException.Message);
                }

                return Constants.msgList[5];
            }
        }



        //sending api request (1st result)
        string SendRequestWithPDF(string coaReportPdfPath, string pfxPath)
        {

            try
            {
                var uploadUrl = _hmApiFiles;
                Write($"Start Upload PDF. \n COA Path is {coaReportPdfPath} \n Url is {uploadUrl}");
                var client = new RestClient(uploadUrl);
                var request = new RestRequest(Method.POST);

                bool test = uploadUrl.ToUpper().Contains("TEST");
                ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
                X509Certificate2Collection collection;
                if (test)
                {
                    Write("test section");
                    collection = new X509Certificate2Collection();
                    collection.Import(pfxPath, "12345678", X509KeyStorageFlags.PersistKeySet);
                }
                else
                {
                    Write("Prod section");
                    string subjectName = "Institute For Food Microbiology";

                    X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                    store.Open(OpenFlags.ReadOnly);
                    X509Certificate2Collection certificates = store.Certificates;
                    X509Certificate2Collection foundCertificates = certificates.Find(X509FindType.FindBySubjectName, subjectName, false);
                    collection = certificates.Find(X509FindType.FindBySubjectName, subjectName, false);


                }

                // Disable SSL certificate validation



                client.ClientCertificates = new X509CertificateCollection();
                client.ClientCertificates.AddRange(collection);
                //  ServicePointManager.SecurityProtocol = (SecurityProtocolType)(0xc0 | 0x300 | 0xc00);
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;


                request.AlwaysMultipartFormData = true;
                request.AddHeader("Content-Type", "multipart/form-data");
                request.AddFile("", coaReportPdfPath);

                var response = client.Execute(request);

                Write($"Response Status code is : {response.StatusCode}");
                Write($"Response Content = {response.Content} \n Ebd Upload PDF.");

                return response.Content;
            }
            catch (Exception ex)
            {
                Write($"From SendRequestWithPDF: {ex.Message}");
                return Constants.msgList[5];
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

                Write(xml);
                return xml;
            }
            catch (Exception e)
            {
                Write($"From BuildInitialXML: {e.Message}");
                return Constants.msgList[6];
            }
        }





        //converting xml to LabSampleFormResult object
        public LabSampleFormResult ExtractLabSampleForm(XDocument xmlDoc)
        {
            try
            {
                XNamespace ns = "http://schemas.datacontract.org/2004/07/FCS_LabsLib";
                XNamespace dataContractNs = "http://schemas.datacontract.org/2004/07/FCS_LabsLib";
                XNamespace xsiNs = "http://www.w3.org/2001/XMLSchema-instance";

                var result = new LabSampleFormResult();

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
                Write($"From ExtractLabSampleForm: {e.Message}");
                return null;
            }


        }

        //creating xml document for sending to order_v2 (xml in sdg format)
        //  private DOMDocument CreateNautilusXML(LabSampleFormResult obj, string barcode)
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


                    }
                }
                //debug mode
                objDom.save(Path.Combine(LOG, "Login " + sdg.Barcode + ".xml"));
                Write($"From Create_XML: xml obj created successfully");
                return objDom;
            }
            catch (Exception ex)
            {
                Write($"From Create_XML: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region result request


        //creating a xml document for sending to fcs
        #region creating xml for result request

        private XmlDocument ParseToXML_res_1(COA_Report coaReport, string resReq)
        {
            try
            {
                var sdg = coaReport.Sdg;
                string fileName = Path.GetFileName(coaReport.PdfPath);
                List<ATTACHED_DOCUMENT> AD = new List<ATTACHED_DOCUMENT>() { new ATTACHED_DOCUMENT(fileName, 1, ".pdf", "?", resReq, coaReport.PdfPath) };

                Attached_Document at = new Attached_Document(AD);
                List<_LabNotes> LN = new List<_LabNotes>()
                {
                    new _LabNotes(sdg.Comments, "C"),
                    new _LabNotes(sdg.U_COA_REMARKS, "A")
                };

                Report_Notes Report_Notes = new Report_Notes(LN);

                List<Test_Result> TR = new List<Test_Result>() { };

                Test_Results tr = new Test_Results(TR);

                Report_Date reportDate = new Report_Date(coaReport.CreatedOn.Value.ToShortDateString());

                string labReportVer = coaReport.Name.Substring(coaReport.Name.IndexOf("(") + 1,
                    coaReport.Name.Length - coaReport.Name.IndexOf(")") - 1);

                Request req = new Request(sdg.U_FOOD_TEMPERATURE, at, 4444, 13, coaReport.Name, int.Parse(labReportVer), false, "?", reportDate, Report_Notes, tr);

                foreach (var sample in sdg.Samples.Where(x => x.Status != "X"))
                {
                    foreach (var aliq in sample.Aliqouts.Where(x => x.Status != "X"))
                    {
                        List<Result> result4Array = new List<Result>();
                        var t = aliq.Tests;

                        //get one correct resultf from results of aliq

                        foreach (var test in aliq.Tests.Where(x => x.STATUS != "X"))
                        {
                            Result result = test.Results.Where(x => x.FormattedResult != null & x.REPORTED == "T").FirstOrDefault();

                            if (result != null)
                            {
                                result4Array.Add(result);
                                break;
                            }

                        }
                    }

                    XmlDocument xdoc = new XmlDocument();
                    xdoc.Load(_xmlTemplateDirection);
                    //xdoc.Save(Path.Combine(LOG, "_xml Template Direction " + coaReport.SdgId.Value + ".xml"));


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
                                case "Report_Notes":
                                    element = GetAllChilds(req.Report_Notes.LabNotes, "LabNotes", element, xdoc);
                                    break;
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

                    string dt = DateTime.Now.ToString("dd-M-yyyy--HH-mm-ss");

                    string XmlFileName = Path.Combine(LOG, "result " + dt + "_" + sdg.ExternalReference + ".xml");

                    xdoc.Save(XmlFileName);

                    Write($"From ParseToXML_res_1: xdoc created successfully");
                    return xdoc;
                }

            }
            catch (Exception e)
            {
                Write($"From ExtractLabSampleForm (result): {e.Message}");
            }

            return null;
        }

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
                Write($"From GetAllChildsReport_Date: {e.Message}");
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
                Write($"From GetAllChilds: {e.Message}");
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
                Write($"From AddXmlNode2: {e.Message}");
                return null;
            }
        }

        protected XmlElement GetAllChildsLabNotes(Test_Result PropParent, string typeNode, XmlElement elementParent, XmlDocument xdoc)
        {
            try
            {
                foreach (var PropChilds in PropParent.Notes.Lab_Notes)
                {
                    XmlElement elementObj = xdoc.CreateElement("fcs1", typeNode, "http://schemas.datacontract.org/2004/07/FCS_LabsLib");

                    foreach (PropertyInfo prop in PropChilds.GetType().GetProperties())
                    {

                        XmlElement element = AddXmlNode2(PropChilds, prop, xdoc);
                        elementObj.AppendChild(element);

                    }
                    elementParent.AppendChild(elementObj);
                }
                return elementParent;
            }
            catch (Exception e)
            {
                Write($"From GetAllChildsLabNotes: {e.Message}");
                return null;
            }
        }

        #endregion


        #endregion

        #region generic functions

        //connecting to microb db
        private void openSqlConnection()
        {
            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            ExeConfigurationFileMap map = new ExeConfigurationFileMap();
            map.ExeConfigFilename = assemblyPath + ".config";
            Configuration cfg = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
        }

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

                Write($"From GetDate: {ex.Message}");
                return null;
            }

            return null;

        }
       


        private void Write(string s)
        {

            try
            {
                string fullPath = Path.Combine(LOG, "Nautilus_WS" + "-" + DateTime.Now.ToString("dd-MM-yyyy") + ".txt");

                using (FileStream stream = new FileStream(fullPath, FileMode.Append, FileAccess.Write))
                {
                    StreamWriter streamWriter = new StreamWriter(stream);
                    streamWriter.WriteLine(DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff"));
                    streamWriter.WriteLine(s);
                    streamWriter.WriteLine();
                    streamWriter.Close();
                }
            }
            catch
            {
            }

        }

        #endregion

        #endregion

        public static bool IsXml(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;

            // Simple regex to check for basic XML structure
            return Regex.IsMatch(input.Trim(), @"^<[^?].*>$", RegexOptions.Singleline);
        }
        private void LogReuest(HttpWebRequest request)
        {

            Write(request.RequestUri.ToString());         // The URI of the request
                                                          //    Write(request.Address             );// The endpoint being accessed
            Write(request.Host);// The host name/authority
            Write(request.ConnectionGroupName);// Name of connection group (useful for logging groups of requests)

            ;// Headers that are useful for logging
             //Write(request.Headers .Coun          );// All headers collection
            Write(request.ContentType);// Content type being sent
                                       // Write(request.ContentLength    );// Size of the request in bytes
            Write(request.UserAgent);// User agent string
            Write(request.Accept);// Accept header value

            // Timing/Performance properties
            Write(request.Timeout.ToString());// Request timeout value
            Write(request.ReadWriteTimeout.ToString());// Read/write timeout value
            Write(request.ContinueTimeout.ToString());// 100-continue timeout value

            // Connection info
            Write(request.ProtocolVersion.ToString());// HTTP protocol version
            Write(request.ServicePoint.ToString());// Details about the connection to the server
            Write(request.Proxy.ToString());
        }
    }
}
