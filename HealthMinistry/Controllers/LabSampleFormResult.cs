using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HealthMinistry.Controllers
{

        public class LabSampleFormResult
        {

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
            public ImporterDetails ImporterDetails { get; set; }
            public string ImporterStore { get; set; }
            public string InspectorTitle { get; set; }
            public string IsOpenPackaging { get; set; }
            public string IsOriginalPacking { get; set; }
            public string IsVet { get; set; }
            public DateTime? ManufactureDate { get; set; }
            public string NumOfSamples { get; set; }
            public string NumOfSamplesVet { get; set; }
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

            public override string ToString()
            {
                return String.Format("Barcode: {0}\n" +
                           "ContainerNum: {1}\n" +
                           "CountryName: {2}\n" +
                           "DelFileNum: {3}\n" +
                           "DeliveryToLab: {4}\n" +
                           "AmilDetails: {5}\n" +
                           "AttachedDocument: {6}\n" +
                           "BatchNum: {7}\n" +
                           "ExpiryDate: {8}\n" +
                           "FormType: {9}\n" +
                           "ImporterDetails: {10}\n" +
                           "ImporterStore: {11}\n" +
                           "InspectorTitle: {12}\n" +
                           "IsOpenPackaging: {13}\n" +
                           "IsOriginalPacking: {14}\n" +
                           "IsVet: {15}\n" +
                           "ManufactureDate: {16}\n" +
                           "NumOfSamples: {17}\n" +
                           "NumOfSamplesVet: {18}\n" +
                           "Organization: {19}\n" +
                           "PackingType: {20}\n" +
                           "PayerID: {21}\n" +
                           "ProducerCountry: {22}\n" +
                           "ProducerName: {23}\n" +
                           "ProductBrandName: {24}\n" +
                           "ProductGroupCode: {25}\n" +
                           "ProductGroupDescription: {26}\n" +
                           "ProductOwner: {27}\n" +
                           "ProductNameEng: {28}\n" +
                           "ProductNameHeb: {29}\n" +
                           "PropertyPlus: {30}\n" +
                           "Remark: {31}\n" +
                           "RequestedTests: {32}\n" +
                           "ReturnCode: {33}\n" +
                           "ReturnCodeDesc: {34}\n" +
                           "SampleFormNum: {35}\n" +
                           "SamplingDate: {36}\n" +
                           "SamplingInspector: {37}\n" +
                           "SamplingPlace: {38}\n" +
                           "SamplingReason: {39}\n" +
                           "SamplingTemp: {40}\n" +
                           "SamplingTime: {41}\n" +
                           "TestDescription: {42}\n" +
                           "TestSubCode: {43}\n" +
                           "EmailTo: {44}",
                           Barcode, ContainerNum, CountryName, DelFileNum, DeliveryToLab, AmilDetails, AttachedDocument,
                           BatchNum, ExpiryDate, FormType, ImporterDetails, ImporterStore, InspectorTitle,
                           IsOpenPackaging, IsOriginalPacking, IsVet, ManufactureDate, NumOfSamples, NumOfSamplesVet,
                           Organization, PackingType, PayerID, ProducerCountry, ProducerName, ProductBrandName,
                           ProductGroupCode, ProductGroupDescription, ProductOwner, ProductNameEng, ProductNameHeb,
                           PropertyPlus, Remark, RequestedTests, ReturnCode, ReturnCodeDesc, SampleFormNum,
                           SamplingDate, SamplingInspector, SamplingPlace, SamplingReason, SamplingTemp, SamplingTime,
                           TestDescription, TestSubCode, EmailTo);

            }
        }

        public class AmilDetails
        {
            public string Address { get; set; }
            public string City { get; set; }
            public string CompanyID { get; set; }
            public string CompanyName { get; set; }
            public string Email { get; set; }
            public string Fax { get; set; }
            public string Phone1 { get; set; }
            public string Phone2 { get; set; }
            public string ZIPCode { get; set; }

            public override string ToString()
            {
                return String.Format("\nAddress: {0}\n" +
                          "City: {1}\n" +
                          "CompanyID: {2}\n" +
                          "CompanyName: {3}\n" +
                          "Email: {4}\n" +
                          "Fax: {5}\n" +
                          "Phone1: {6}\n" +
                          "Phone2: {7}\n" +
                          "ZIPCode: {8}",
                          Address ?? "N/A", City ?? "N/A", CompanyID ?? "N/A", CompanyName ?? "N/A",
                          Email ?? "N/A", Fax ?? "N/A", Phone1 ?? "N/A", Phone2 ?? "N/A", ZIPCode ?? "N/A");

            }
        }


        public class ImporterDetails
        {
            public string Address { get; set; }
            public string City { get; set; }
            public string CompanyId { get; set; }
            public string CompanyName { get; set; }
            public string Email { get; set; }
            public string Fax { get; set; }
            public string Phone1 { get; set; }
            public string Phone2 { get; set; }
            public string ZIPCode { get; set; }

            public override string ToString()
            {
                return String.Format("\nAddress: {0}\n" +
                          "City: {1}\n" +
                          "CompanyId: {2}\n" +
                          "CompanyName: {3}\n" +
                          "Email: {4}\n" +
                          "Fax: {5}\n" +
                          "Phone1: {6}\n" +
                          "Phone2: {7}\n" +
                          "ZIPCode: {8}",
                          Address ?? "N/A", City ?? "N/A", CompanyId ?? "N/A", CompanyName ?? "N/A",
                          Email ?? "N/A", Fax ?? "N/A", Phone1 ?? "N/A", Phone2 ?? "N/A", ZIPCode ?? "N/A");

            }
        }

        public class ClientObj
        {
            public string U_SDG_CLIENT { get; set; }
            public string U_PHONE { get; set; }
            public string U_EMAIL { get; set; }
            public string U_ADDRESS { get; set; }
            public string U_CONTECT_NAME { get; set; }
            public string U_CONTACT_PHONE { get; set; }    

            public ClientObj(string p1, string p2, string p3, string p4, string p5, string p6)
            {
                this.U_SDG_CLIENT = p1;
                this.U_PHONE = p2;
                this.U_EMAIL = p3;
                this.U_ADDRESS = p4;
                this.U_CONTECT_NAME = p5;
                this.U_CONTACT_PHONE = p6;
            }
        
        }

        public class AliquotObj
        {
            public string aliquotWorkf { get; set; }
            public string DESCRIPTION { get; set; }
            public string U_TEST_TEMPLATE_EXTENDED { get; set; }


            public AliquotObj(string p1, string p2, string p3)
            {
                this.aliquotWorkf = p1;
                this.DESCRIPTION = p2;
                this.U_TEST_TEMPLATE_EXTENDED = p3;
            }
       
        }


    }

