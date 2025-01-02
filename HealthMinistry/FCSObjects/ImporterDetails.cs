using System;

namespace HealthMinistry.FCSObjects
{
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


}

