namespace HealthMinistry.NautObjects
{
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


}

