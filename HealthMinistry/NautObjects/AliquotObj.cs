namespace HealthMinistry.NautObjects
{
    public class AliquotObj
    {
        public string Workflow_name { get; set; }
        public string DESCRIPTION { get; set; }

        public string U_TEST_TEMPLATE_EXTENDED { get; set; }
        public string External_ref { get; set; }

        public AliquotObj(string workflow_name, string desc, string ttex, string external_ref)
        {
            Workflow_name = workflow_name;
            DESCRIPTION = desc;
            U_TEST_TEMPLATE_EXTENDED = ttex;
            External_ref = external_ref;
        }

    }


}

