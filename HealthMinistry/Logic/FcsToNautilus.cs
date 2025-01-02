﻿using HealthMinistry.Controllers;
using System;
using System.Collections.Generic;
using System.Configuration;
using Oracle.DataAccess.Client;

using System.Linq;
using System.Web;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using HealthMinistry.NautObjects;
using HealthMinistry.FCSObjects;
using DAL;
using System.Xml.Linq;

namespace HealthMinistry.Logic
{
    public class FcsToNautilus
    {

        private OracleCommand _cmd;
        private OracleConnection _oraCon;
        private LabSampleFormResult _labSampleFcs;
        public SdgNaut sdgNaut { get; private set; }
        private string _errorMsg;



        public FcsToNautilus(LabSampleFormResult labSampleForm)
        {

            _oraCon = new OracleConnection();
            OpenConnection();
            _labSampleFcs = labSampleForm;

        }
        public FcsToNautilus(OracleConnection oraCon, LabSampleFormResult labSampleForm)
        {
            _oraCon = oraCon;
            _labSampleFcs = labSampleForm;

        }

        public string Convert()
        {
            //   if (requestedTestsList == null) { }
            if (_labSampleFcs.requestedTestsList.Count == 0)
            {
                _errorMsg = Constants._ValidationMsgs[3];
                return _errorMsg;
            }
            int numSmp = _labSampleFcs.requestedTestsList.Max(x => x.NumOfSamples);

            sdgNaut = new SdgNaut(numSmp);
            sdgNaut.Barcode = _labSampleFcs.Barcode;

            foreach (var sample in sdgNaut.Samples)
            {

                sample.description = _labSampleFcs.ProductNameHeb;
                sample.DelFileNum = _labSampleFcs.DelFileNum;
                sample.ContainerNum = _labSampleFcs.ContainerNum;
                sample.dateProduction = _labSampleFcs.ManufactureDate.ToString();
                sample.batchNum = _labSampleFcs.BatchNum;
                sample.SamplingDate = _labSampleFcs.SamplingDate.HasValue ? _labSampleFcs.SamplingDate.Value.ToString("dd/MM/yyyy") : "";
                sample.SamplingTime = _labSampleFcs.SamplingTime;

            }
            //Find The Client in Nautilus.
            sdgNaut.Client = GetClient();
            if (sdgNaut.Client == null)
            {
                _errorMsg = Constants._ValidationMsgs[0];
                return _errorMsg;
            }

            //Find Tests in Nautilus.

            string resAliq = BuildAliquots();

            if (!string.IsNullOrEmpty(resAliq))
            {
                _errorMsg = Constants._ValidationMsgs[1];
                return _errorMsg;
            }







            return "";//Success

        }



        private ClientObj GetClient()
        {

            OpenConnection();
            //getClient
            string labName = ConfigurationManager.AppSettings["LAB_NAME"];
            string sql = string.Format("SELECT C.NAME AS U_SDG_CLIENT,AD.PHONE AS U_PHONE,AD.EMAIL AS U_EMAIL,AD.ADDRESS_LINE_2 AS U_ADDRESS,AD.ADDRESS_LINE_1 AS U_CONTECT_NAME,AD.ADDRESS_LINE_4 AS U_CONTACT_PHONE " +
                  " fROM lims_sys.ADDRESS AD INNER JOIN lims_sys.CLIENT C ON AD.ADDRESS_ITEM_ID = C.CLIENT_ID INNER JOIN lims_sys.CLIENT_user Cu ON  Cu.CLIENT_ID = c.CLIENT_ID " +
                  " WHERE CU.U_Bn_Number = '{0}' AND ADDRESS_TYPE = '{1}'"
                  , _labSampleFcs.ImporterDetails.CompanyId, labName);
            _cmd = new OracleCommand(sql, _oraCon);
            OracleDataReader reader3 = _cmd.ExecuteReader();
            ClientObj newClient;
            if (!reader3.HasRows)
            {
                return null;
            }
            else
            {
                newClient = new ClientObj(
                         reader3["U_SDG_CLIENT"].ToString(),
                         reader3["U_PHONE"].ToString(),
                       reader3["U_EMAIL"].ToString(), // _labSampleFcs.ImporterDetails.Email,יכול להגיע גם מהממשק, אליאס לא רוצה
                         reader3["U_ADDRESS"].ToString(),
                         reader3["U_CONTECT_NAME"].ToString(),
                      reader3["U_CONTACT_PHONE"].ToString() //  $" {_labSampleFcs.ImporterDetails.Phone1};{_labSampleFcs.ImporterDetails.Phone2}",יכול להגיע גם מהממשק, אליאס לא רוצה
                     );

            }
            return newClient; ;
        }



        private string BuildAliquots()
        {


            if (_labSampleFcs.requestedTestsList != null && _labSampleFcs.requestedTestsList.Count > 0)
            {
                foreach (var item in _labSampleFcs.requestedTestsList)
                {
                    string sql = string.Format("   SELECT u_lab_ttex, W.NAME ALIQWFNAME, TTEX.DESCRIPTION,TTEX.NAME TTEXNAME  FROM  U_TEST_TEMPLATE_EX_USER TTEXU     INNER JOIN  U_TEST_TEMPLATE_EX TTEX ON TTEX.U_TEST_TEMPLATE_EX_ID = TTEXU.U_TEST_TEMPLATE_EX_ID    INNER JOIN  WORKFLOW W ON TTEXU.U_ALIQ_WORKFLOW = W.WORKFLOW_ID   INNER JOIN  U_Fcs_Test_User FT ON TTEXU.U_TEST_TEMPLATE_EX_ID = Ft.U_Lab_Ttex    where Ft.U_Lab_Ttex IS NOT NULL AND  U_Test_Description = q'[{0}]'  and U_Test_Group_Code = {1}", item.TestDescription, item.TestTypeCode);

                    _cmd = new OracleCommand(sql, _oraCon);
                    OracleDataReader reader5 = _cmd.ExecuteReader();

                    if (!reader5.HasRows)
                    {
                        _errorMsg = Constants._ValidationMsgs[1];
                        return _errorMsg;
                    }
                    else
                    {
                        if (reader5.Read())
                        {

                            var Workflow_name = reader5["ALIQWFNAME"].ToString();
                            var desc = reader5["DESCRIPTION"].ToString();
                            var ttex = reader5["TTEXNAME"].ToString();

                            var alq = new AliquotObj(Workflow_name, desc, ttex, item.Barcode);
                            for (int i = 0; i < item.NumOfSamples; i++)
                            {

                                //Add to Sample
                                sdgNaut.Samples[i].AddAliquot(alq);
                                sdgNaut.Samples[i].Barcode = item.Barcode;

                            }



                        }
                    }


                }
                BuildLastAliquot();
            }


            return "";
        }

        private void BuildLastAliquot()
        {
            string ttexid = ConfigurationManager.AppSettings["ExtraTest"];
            string sql = "      SELECT w.name wn_name,T.name ttex_name FROM U_Test_Template_Ex_User tt " +
                " INNER JOIN U_Test_Template_Ex t on T.U_Test_Template_Ex_Id = Tt.U_Test_Template_Ex_Id " +
                " INNER JOIN WORKFLOW W ON W.Workflow_Id = tt.U_Aliq_Workflow " +
" WHERE tt.U_TEST_TEMPLATE_EX_ID =" + ttexid;

            _cmd = new OracleCommand(sql, _oraCon);
            OracleDataReader reader5 = _cmd.ExecuteReader();

            if (!reader5.HasRows)
            {

            }
            else
            {
                if (reader5.Read())
                {

                    var Workflow_name = reader5["wn_name"].ToString();
                    var desc = "Extra";
                    var ttex = reader5["ttex_name"].ToString();

                    var alq = new AliquotObj(Workflow_name, desc, ttex, "");
                    sdgNaut.Samples[sdgNaut.Samples.Count - 1].AddAliquot(alq);




                }
            }


        }


        private void OpenConnection()
        {
            if (_oraCon.State != System.Data.ConnectionState.Open)
            {
                _oraCon = new OracleConnection(ConfigurationManager.ConnectionStrings["connectionString"].ConnectionString);

                _oraCon.Open();
            }
        }

    }
}