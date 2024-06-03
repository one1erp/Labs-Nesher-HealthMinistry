using DAL;
using MSXML;
using Oracle.DataAccess.Client;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Web.Mvc;
using System.Xml;
using System.Xml.Linq;



namespace HealthMinistry.Controllers
{
    public class HomeController : Controller
    {

        public ActionResult Index()
        {
            return View();
        }


        #region variables

        public IDataLayer _dal = null;
        List<AliquotObj> _aliquotList = new List<AliquotObj>();
        private OracleCommand cmd;
        private ClientObj myClient;
        private string sql;
        private string _errorMsg = "";
        string _urlApiService = ConfigurationManager.AppSettings["API_HM_INTERFACE_ADDRESS"];
        string _soapActionSample = ConfigurationManager.AppSettings["SOAP_ACTION_SAMPLE"];
        string _soapActionResult = ConfigurationManager.AppSettings["SOAP_ACTION_RESULT"];



        string _pfxPath = ConfigurationManager.AppSettings["PFX_DIRECTION_PATH"];
        string _hmApiFiles = ConfigurationManager.AppSettings["API_HM_INTERFACE_ADDRESS_FILES"];
        string _xmlTemplateDirection = ConfigurationManager.AppSettings["xmlTemplateDirection"];
        string _cs = ConfigurationManager.ConnectionStrings["connectionStringEF"].ConnectionString;

        protected string[] classes = new string[] { "Attached_Document", "ATTACHED_DOCUMENT", "Report_Notes", "Test_Results", "Test_Result", "Report_Notes", "Lab_Notes", "Report_Date" };

        private readonly List<string> msgList = new List<string>()
                {
                    "לא נמצאה תעודה עם המזהה שנשלח",
                    "התעודה לא מאושרת,לא ניתן לשלוח למשרד הבריאות",
                    "לתעודה של דרישה זו pdf לא נמצא מסמך ",
                    "שגיאה בשליחת הבקשה למשרד הבריאות",
                    "התהליך עבר בהצלחה",
                    "failed",
                    "נכשלה יצירת XML ראשוני לשליחה"
                };
        OracleConnection oraCon;
        private static string path;
        private static string flag;
        private static bool loggingEnable = true;
        private static bool firstLine = true;
        private static string logPath;

        #endregion


        #region base functions


        [HttpPost]
        public string FcsSampleRequest(string barcode)
        {
            try
            {
                oraCon = new OracleConnection(System.Configuration.ConfigurationManager.ConnectionStrings["connectionString"].ConnectionString);
                Write("Start FcsSampleRequest");

                openSqlConnection();

                if (oraCon.State != System.Data.ConnectionState.Open) oraCon.Open();

                //building xml with barcode for 1st request
                string xmlStringBarcode = BuildInitialXML(barcode);

                //adding fcs_msg - removed, exists in git's 1st version
                //-----------------------------------------------------

                //sending 1st request to HM
                var sampleRequest_result = SendRequestWithXML(xmlStringBarcode, _urlApiService, _soapActionSample, _pfxPath);

                //In case of error while sending - return.
                if (sampleRequest_result.Equals(msgList[5])) return msgList[3];

                //parsing the response to xml
                XDocument xmlDoc = XDocument.Parse(sampleRequest_result);

                //parsing the xml to labSampleForm - a HM object
                LabSampleFormResult labSampleForm = ExtractLabSampleForm(xmlDoc);
                if (labSampleForm == null)
                {
                    Write($"From FcsSampleRequest: failed");
                    oraCon.Close();
                    return msgList[3];
                }

                //labSampleForm contains information about itself
                if (labSampleForm.ReturnCode != "0" && labSampleForm.ReturnCode != "1")
                {
                    Write($"The process was completed unsuccessfully, {labSampleForm.ReturnCodeDesc}");
                    oraCon.Close();
                    return labSampleForm.ReturnCodeDesc;
                }

                //validiating labSampleForm object
                var isValid = Validation(labSampleForm);

                if (!isValid)
                    return _errorMsg;

                //if labSampleForm is valid, parsing it to xml
                var docXml = Create_XML(labSampleForm, barcode);

                //The calling method from orderv2 proj gets the xml and proccesses it to a sdg
                if (docXml != null)
                {
                    Write($"The process was completed successfully, xml string for creating sdg was sent to order_v2");
                    oraCon.Close();
                    return docXml.xml;
                }

                //debug mode - for postman
                //return labSampleForm.ToString();

                Write($"labSampleForm created, but docXml is null");
                oraCon.Close();
                return null;

            }
            catch (Exception ex)
            {
                Write($"From FcsSampleRequest: {ex.Message}");
                oraCon.Close();
                return msgList[3] + $" From FcsSampleRequest: {ex.Message}";
            }

        }



        [HttpPost]
        public string FcsResultRequest(string coaId)
        {

            try
            {
                Write("Start FcsResultRequest");
                oraCon = new OracleConnection(System.Configuration.ConfigurationManager.ConnectionStrings["connectionString"].ConnectionString);

                openSqlConnection();

                if (oraCon.State != System.Data.ConnectionState.Open) oraCon.Open();

                _dal = new DataLayer();
                _dal.Connect(_cs);

                COA_Report coaReport = _dal.GetCoaReportById(long.Parse(coaId));

                if (coaReport == null) return msgList[0];

                if (coaReport.Status != "A") return msgList[1];

                if (coaReport.PdfPath == null) return msgList[2];

                //sending 2nd request (result) proccessing response and sending back to HM
                var resultRequest1_response = SendRequestWithPDF(coaReport.PdfPath);

                //sending request failed
                if (resultRequest1_response.Equals("failed")) return msgList[3];

                //3rd request to HM - proccessed response from 2nd request:

                //parsing 2nd response to unique xml format
                var xmlFile = ParseToXML_res_1(coaReport, resultRequest1_response);

                //parsing the xml to string
                string xmlString = xmlFile.OuterXml;

                //sending 3rd request to HM, with proccessed string
                string responseFrom2ndResReq = SendRequestWithXML(xmlString, _urlApiService, _soapActionResult, _pfxPath);

                //loading 3rd response to xml (for easy serching information)
                XmlDocument xmlDoc = new XmlDocument();
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
                        return msgList[4];
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


        #region other functions

        #region sample request

        //building initial xml(for sending to HM interface)
        private string BuildInitialXML(string barcode_number)
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
                      <fcs1:Barcode>{0}</fcs1:Barcode>
                      <fcs1:Lab_Code>{1}</fcs1:Lab_Code>
                   </fcs:Request>
                </fcs:Lab_Sample_Form>
             </soapenv:Body>
          </soapenv:Envelope>", barcode, lab_code);

                return xml;
            }
            catch (Exception e)
            {
                Write($"From BuildInitialXML: {e.Message}");
                return msgList[6];
            }
        }

        //validation tests 
        private bool Validation(LabSampleFormResult obj)
        {
            try
            {
                var _errorMsgs = new List<string>()
            {
                "לקוח לא קיים, אנא פנה למנהל מערכת",
                "לא נמצאה בדיקה, אנא פנה למנהל מערכת",
                "מזהה בדיקה לא קיים, אנא פנה למנהל מערכת"
            };

                if (oraCon.State != System.Data.ConnectionState.Open)
                {
                    oraCon.Open();
                }


                //getClient
                string labName = ConfigurationManager.AppSettings["LAB_NAME"];
                sql = string.Format("SELECT C.NAME AS U_SDG_CLIENT,AD.PHONE AS U_PHONE,AD.EMAIL AS U_EMAIL,AD.ADDRESS_LINE_2 AS U_ADDRESS,AD.ADDRESS_LINE_1 AS U_CONTECT_NAME,AD.ADDRESS_LINE_4 AS U_CONTACT_PHONE FROM lims_sys.ADDRESS AD INNER JOIN lims_sys.CLIENT C ON AD.ADDRESS_ITEM_ID = C.CLIENT_ID WHERE C.CLIENT_CODE = '{0}' AND ADDRESS_TYPE = '{1}'", obj.PayerID, labName);
                cmd = new OracleCommand(sql, oraCon);
                OracleDataReader reader3 = cmd.ExecuteReader();

                if (!reader3.HasRows)
                {
                    _errorMsg = _errorMsgs[0];
                    return false;
                }
                else
                {
                    ClientObj newClient = new ClientObj(
                              reader3["U_SDG_CLIENT"].ToString(),
                              reader3["U_PHONE"].ToString(),
                              reader3["U_EMAIL"].ToString(),
                              reader3["U_ADDRESS"].ToString(),
                              reader3["U_CONTECT_NAME"].ToString(),
                              reader3["U_CONTACT_PHONE"].ToString()
                          );
                    myClient = newClient;
                }

                //getTest
                List<string> listLabTtex = new List<string>();

                sql = string.Format("select FTU.u_lab_ttex FROM lims_sys.U_FCS_TEST FT INNER JOIN lims_sys.U_FCS_TEST_USER FTU ON FTU.U_FCS_TEST_ID=FT.U_FCS_TEST_ID WHERE FT.NAME = '{0}'", obj.TestSubCode);
                cmd = new OracleCommand(sql, oraCon);
                OracleDataReader reader = cmd.ExecuteReader();

                if (!reader.HasRows)
                {
                    _errorMsg = _errorMsgs[1];
                    return false;
                }
                else
                {
                    string[] arrLabTtex;

                    while (reader.Read())
                    {
                        string labTtex = reader["u_lab_ttex"].ToString();
                        if (labTtex != null && labTtex != "")
                        {

                            if (labTtex.Contains(","))
                            {
                                arrLabTtex = labTtex.Split(',');
                                foreach (string item in arrLabTtex)
                                {
                                    listLabTtex.Add(item);
                                }
                            }
                            else
                            {
                                listLabTtex.Add(labTtex);
                            }
                        }

                        else
                        {
                            _errorMsg = _errorMsgs[2];
                            return false;
                        }
                    }

                }


                sql = string.Format("SELECT W.NAME ALIQWFNAME,TTEX.DESCRIPTION,TTEX.NAME TTEXNAME " +
               "FROM lims_sys.U_TEST_TEMPLATE_EX_USER TTEXU " +
               "INNER JOIN lims_sys.U_TEST_TEMPLATE_EX TTEX ON TTEX.U_TEST_TEMPLATE_EX_ID= TTEXU.U_TEST_TEMPLATE_EX_ID " +
               "INNER JOIN lims_sys.WORKFLOW  W ON TTEXU.U_ALIQ_WORKFLOW=W.WORKFLOW_ID " +
               "where TTEXU.U_TEST_TEMPLATE_EX_ID in ("
           + string.Join(",", listLabTtex)
           + ")");

                cmd = new OracleCommand(sql, oraCon);
                OracleDataReader reader5 = cmd.ExecuteReader();

                if (!reader5.HasRows)
                {
                    _errorMsg = _errorMsgs[2];
                    return false;
                }
                else
                {
                    while (reader5.Read())
                    {

                        var a = reader5["ALIQWFNAME"].ToString();
                        var b = reader5["DESCRIPTION"].ToString();
                        var c = reader5["TTEXNAME"].ToString();


                        AliquotObj newAliquot = new AliquotObj(a, b, c);

                        _aliquotList.Add(newAliquot);
                    }
                }

                return true;

            }
            catch (Exception ex)
            {

                Write($"Validation failed: {ex.Message}");
                return false;
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

                var numOfSamplesElement = xmlDoc.Descendants(dataContractNs + "Num_Of_Samples").FirstOrDefault();
                if (numOfSamplesElement != null)
                    result.NumOfSamples = numOfSamplesElement.Value;

                var numOfSamplesVetElement = xmlDoc.Descendants(dataContractNs + "Num_Of_Samples_Vet").FirstOrDefault();
                if (numOfSamplesVetElement != null)
                    result.NumOfSamplesVet = numOfSamplesVetElement.Value;

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

                var requestedTestsElement = xmlDoc.Descendants(dataContractNs + "Requested_Tests").FirstOrDefault();
                if (requestedTestsElement != null)
                    result.RequestedTests = requestedTestsElement.Value;

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
        private DOMDocument Create_XML(LabSampleFormResult obj, string barcode)
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

                var description = objDom.createElement("DESCRIPTION");
                objSdg.appendChild(description);
                description.text = ConfigurationManager.AppSettings["SDG_DESC"];

                var fcsMessageId = objDom.createElement("U_FCS_MSG_ID");
                objSdg.appendChild(fcsMessageId);
                fcsMessageId.text = barcode;

                var sampledBy = objDom.createElement("U_SAMPLED_BY");
                objSdg.appendChild(sampledBy);
                sampledBy.text = ConfigurationManager.AppSettings["SAMPLED_BY"];

                var sdgExternalReference = objDom.createElement("EXTERNAL_REFERENCE");
                objSdg.appendChild(sdgExternalReference);
                sdgExternalReference.text = barcode;

                var U_SDG_CLIENT = objDom.createElement("U_SDG_CLIENT");
                objSdg.appendChild(U_SDG_CLIENT);
                U_SDG_CLIENT.text = myClient.U_SDG_CLIENT;

                var U_PHONE = objDom.createElement("U_PHONE");
                objSdg.appendChild(U_PHONE);
                U_PHONE.text = myClient.U_PHONE;

                var U_ADDRESS = objDom.createElement("U_ADDRESS");
                objSdg.appendChild(U_ADDRESS);
                U_ADDRESS.text = myClient.U_ADDRESS;

                var U_EMAIL = objDom.createElement("U_EMAIL");
                objSdg.appendChild(U_EMAIL);
                U_EMAIL.text = myClient.U_EMAIL;

                var U_CONTECT_NAME = objDom.createElement("U_CONTECT_NAME");
                objSdg.appendChild(U_CONTECT_NAME);
                U_CONTECT_NAME.text = myClient.U_CONTECT_NAME;

                var U_CONTACT_PHONE = objDom.createElement("U_CONTACT_PHONE");
                objSdg.appendChild(U_CONTACT_PHONE);
                U_CONTACT_PHONE.text = myClient.U_CONTACT_PHONE;

                // Create samples
                var objSample = objDom.createElement("SAMPLE");
                objSdg.appendChild(objSample);

                objCreateByWorkflowElem = objDom.createElement("create-by-workflow");
                objSample.appendChild(objCreateByWorkflowElem);

                objWorkflowName = objDom.createElement("workflow-name");
                objCreateByWorkflowElem.appendChild(objWorkflowName);
                objWorkflowName.text = ConfigurationManager.AppSettings["SAMPLE_WF"];

                description = objDom.createElement("DESCRIPTION");
                objSample.appendChild(description);
                description.text = obj.ProductBrandName;

                var product = objDom.createElement("PRODUCT_ID");
                objSample.appendChild(product);
                product.text = ConfigurationManager.AppSettings["PROD_ID"];

                var dateProduction = objDom.createElement("U_DATE_PRODUCTION");
                objSample.appendChild(dateProduction);
                dateProduction.text = obj.ManufactureDate.ToString();

                var containerNumber = objDom.createElement("U_CONTAINER_NUMBER");
                objSample.appendChild(containerNumber);
                containerNumber.text = obj.ContainerNum;

                var batchNum = objDom.createElement("U_BATCH");
                objSample.appendChild(batchNum);
                batchNum.text = obj.BatchNum;

                var samplingTime = objDom.createElement("U_TXT_SAMPLING_TIME");
                objSample.appendChild(samplingTime);
                samplingTime.text = obj.SamplingTime;

                var delFileNum = objDom.createElement("Del_File_Num");
                objSample.appendChild(delFileNum);
                delFileNum.text = obj.DelFileNum;

                var smplExternalReference = objDom.createElement("EXTERNAL_REFERENCE");
                objSample.appendChild(smplExternalReference);
                smplExternalReference.text = barcode;


                foreach (AliquotObj aliq in _aliquotList)
                {
                    var objAliquot = objDom.createElement("ALIQUOT");
                    objSample.appendChild(objAliquot);

                    objCreateByWorkflowElem = objDom.createElement("create-by-workflow");
                    objAliquot.appendChild(objCreateByWorkflowElem);

                    objWorkflowName = objDom.createElement("workflow-name");
                    objCreateByWorkflowElem.appendChild(objWorkflowName);
                    objWorkflowName.text = aliq.aliquotWorkf;

                    var aliquotWorkf = objDom.createElement("aliquotWorkf");
                    objAliquot.appendChild(aliquotWorkf);
                    aliquotWorkf.text = aliq.aliquotWorkf;

                    var DESCRIPTION = objDom.createElement("DESCRIPTION");
                    objAliquot.appendChild(DESCRIPTION);
                    DESCRIPTION.text = aliq.DESCRIPTION;

                    var U_TEST_TEMPLATE_EXTENDED = objDom.createElement("U_TEST_TEMPLATE_EXTENDED");
                    objAliquot.appendChild(U_TEST_TEMPLATE_EXTENDED);
                    U_TEST_TEMPLATE_EXTENDED.text = aliq.U_TEST_TEMPLATE_EXTENDED;

                }
                //debug mode
                //objDom.save(@"C:\Users\avigaile\Desktop\new 7.xml");
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

                    string XmlFileName = _xmlTemplateDirection + dt + "_" + sdg.U_FCS_MSG + ".xml";

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

        #region api requests

        //sending api request (sample, 2nd result)
        public string SendRequestWithXML(string xmlString, string url, string soapAction, string pfxPath)
        {
            try
            {

                ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
                X509Certificate2Collection collection;
                collection = new X509Certificate2Collection();
                try
                {
                    //password is not correct - real
                    collection.Import(pfxPath, "12345678", X509KeyStorageFlags.PersistKeySet);

                }
                catch (Exception ex)
                {
                    Write($"After collection.import: {ex.Message}");
                    return msgList[5];
                }

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.ClientCertificates = new X509CertificateCollection();
                request.ClientCertificates.AddRange(collection);
                System.Net.ServicePointManager.SecurityProtocol = (SecurityProtocolType)(0xc0 | 0x300 | 0xc00); //SecurityProtocolType.Tls;//| SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12
                request.Method = "POST";
                request.ContentType = "text/xml;charset=utf-8";
                request.Timeout = 600000;
                request.Headers.Add("SOAPAction", soapAction);


                byte[] xmlBytes = Encoding.UTF8.GetBytes(xmlString);


                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(xmlBytes, 0, xmlBytes.Length);
                }

                using (WebResponse response = request.GetResponse())
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        using (StreamReader reader = new StreamReader(responseStream))
                        {
                            string responseXml = reader.ReadToEnd();
                            return responseXml;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Write($"From SendRequestWithXML: {ex.Message}");
                return msgList[5];
            }
        }

        //sending api request (1st result)
        public string SendRequestWithPDF(string coaReportPdfPath)
        {

            try
            {
                var client = new RestClient(_hmApiFiles);
                var request = new RestRequest(Method.POST);

                // Disable SSL certificate validation
                ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
                X509Certificate2Collection collection = new X509Certificate2Collection();
                collection.Import(_pfxPath, "12345678", X509KeyStorageFlags.PersistKeySet);


                client.ClientCertificates = new X509CertificateCollection();
                client.ClientCertificates.AddRange(collection);
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)(0xc0 | 0x300 | 0xc00);

                request.AlwaysMultipartFormData = true;
                request.AddHeader("Content-Type", "multipart/form-data");
                request.AddFile("", coaReportPdfPath);

                var response = client.Execute(request);
                return response.Content;
            }
            catch (Exception ex)
            {
                Write($"From SendRequestWithPDF: {ex.Message}");
                return msgList[5];
            }
        }
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
            catch (Exception ex) {

                Write($"From GetDate: {ex.Message}");
                return null;
            }

            return null;

        }


        private static void Write(string strLog)
        {

            path = ConfigurationManager.AppSettings["LogPath"];
            flag = ConfigurationManager.AppSettings["LogFlag"];

            if (flag == null || flag != "T")
            {
                loggingEnable = false;
                return;
            }

            if (string.IsNullOrEmpty(path)) path = "C:\\temp\\";
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            logPath = $"{path}HM.txt";

            using (StreamWriter sw = System.IO.File.AppendText(logPath))
            {
                if (firstLine) { sw.WriteLine($"\n{DateTime.Now}"); firstLine = false; }
                sw.WriteLine(strLog);
            }
        }

   

        #endregion


        #endregion
    }
}
