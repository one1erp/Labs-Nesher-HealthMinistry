using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace TestHealthMinistry_FCS_only_
{
    internal class Program
    {

        static bool OpenFile;
        static string logPath;
        static void Main(string[] args)
        {


            string _urlApiService = ConfigurationManager.AppSettings["API_HM_INTERFACE_ADDRESS"];
            string _soapActionSample = ConfigurationManager.AppSettings["SOAP_ACTION_SAMPLE"];
            string lab_code = ConfigurationManager.AppSettings["LAB_CODE"];
            string _pfxPath = ConfigurationManager.AppSettings["PFX_DIRECTION_PATH"];

            string Barcode = ConfigurationManager.AppSettings["Barcode"];


            Console.WriteLine("Enter Barcode");
            var input = Console.ReadLine();
            if (!string.IsNullOrEmpty(input))
            {
                Barcode = input;

            }
            Console.WriteLine($"Barcode is {Barcode}", Barcode);
            OpenFile = ConfigurationManager.AppSettings["OpenFile"] == "T";

            logPath = ConfigurationManager.AppSettings["LogDirectory"];// Environment.CurrentDirectory;
            Console.WriteLine(logPath);
            var xmlStringBarcode = BuildInitialXML(Barcode, lab_code);
            var sampleRequest_result = SendRequestWithXML(xmlStringBarcode, _urlApiService, _soapActionSample, _pfxPath);
            //return sampleRequest_result;

            Console.WriteLine("Do you want to open Log?");
            input = Console.ReadLine();
            if (!string.IsNullOrEmpty(input) && input.ToUpper().StartsWith("Y"))
            {
                string a = Path.Combine(logPath, DateTime.Now.ToString("dd-MM-yyyy") + ".txt");
                Console.WriteLine(a);
                Process.Start(a);

            }


        }
        static void MainSampleRequest(string[] args)
        {


            string _urlApiService = ConfigurationManager.AppSettings["API_HM_INTERFACE_ADDRESS"];
            string _soapActionSample = ConfigurationManager.AppSettings["SOAP_ACTION_SAMPLE"];
            string lab_code = ConfigurationManager.AppSettings["LAB_CODE"];
            string _pfxPath = ConfigurationManager.AppSettings["PFX_DIRECTION_PATH"];

            string Barcode = ConfigurationManager.AppSettings["Barcode"];


            Console.WriteLine("Enter Barcode");
            var input = Console.ReadLine();
            if (!string.IsNullOrEmpty(input))
            {
                Barcode = input;

            }
            Console.WriteLine($"Barcode is {Barcode}", Barcode);
            OpenFile = ConfigurationManager.AppSettings["OpenFile"] == "T";

            logPath = ConfigurationManager.AppSettings["LogDirectory"];// Environment.CurrentDirectory;
            Console.WriteLine(logPath);
            var xmlStringBarcode = BuildInitialXML(Barcode, lab_code);
            var sampleRequest_result = SendRequestWithXML(xmlStringBarcode, _urlApiService, _soapActionSample, _pfxPath);
            //return sampleRequest_result;

            Console.WriteLine("Do you want to open Log?");
            input = Console.ReadLine();
            if (!string.IsNullOrEmpty(input) && input.ToUpper().StartsWith("Y"))
            {
                string a = Path.Combine(logPath, DateTime.Now.ToString("dd-MM-yyyy") + ".txt");
                Console.WriteLine(a);
                Process.Start(a);

            }


        }

        #region SampleRequest


        static string BuildInitialXML(string barcode_number, string lab_code)
        {
            try
            {

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

                return xml;
            }
            catch (Exception e)
            {
                Write(e.Message);
                return "נכשלה יצירת XML ראשוני לשליחה";
            }
        }
        static string SendRequestWithXML(string xmlString, string url, string soapAction, string pfxPath)
        {
            try
            {

                bool test = url.ToUpper().Contains("TEST");
                Write(url);
                Write("test Environment = " + test);



                //soapAction = "http://www.moh.gov.co.il/FoodApp/FCS_Labs/IFCS_Labs/Lab_Results_Form";
                // Disable SSL certificate validation

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

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.ClientCertificates = new X509CertificateCollection();
                request.ClientCertificates.AddRange(collection);

                if (true)
                    System.Net.ServicePointManager.SecurityProtocol = (SecurityProtocolType)(0xc0 | 0x300 | 0xc00); //SecurityProtocolType.Tls;//| SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12
                else
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                //    System.Net.ServicePointManager.SecurityProtocol = (SecurityProtocolType)(0xc0 | 0x300 | 0xc00); //SecurityProtocolType.Tls;//| SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12
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
                            Write("Response");
                            Write(responseXml);
                            Write("---------------------------");
                            XDocument xmlDoc = XDocument.Parse(responseXml);
                            string fileName = "response_" + DateTime.Now + ".xml";
                            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                            {
                                fileName = fileName.Replace(c, '_');
                            }

                            fileName = Path.Combine(logPath, fileName);
                            Console.WriteLine(fileName);
                            xmlDoc.Save(fileName);
                            if (OpenFile)
                            {
                                Process.Start(fileName);

                            }
                            return responseXml;
                        }
                    }
                }

            }
            catch (WebException ex)
            {
                Write("An error occurred: " + ex.Message);
                Write("An error occurred: " + ex.Response.ToString());
                return "false";
            }
        }

        #endregion


        static void Write(string s)
        {
            try
            {
                Console.WriteLine(s);
                var fullPath = Path.Combine(logPath, DateTime.Now.ToString("dd-MM-yyyy") + ".txt");

                using (FileStream file = new FileStream(fullPath, FileMode.Append, FileAccess.Write))
                {
                    var streamWriter = new StreamWriter(file);
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
    }
}