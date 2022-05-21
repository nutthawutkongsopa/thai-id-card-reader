using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ThaiIDCardReader
{
    public class PersonalInfo
    {
        public string CID { get; set; }
        public string TitleTH { get; set; }
        public string FirstNameTH { get; set; }
        public string MiddleNameTH { get; set; }
        public string LastNameTH { get; set; }
        public string TitleEN { get; set; }
        public string FirstNameEN { get; set; }
        public string MiddleNameEN { get; set; }
        public string LastNameEN { get; set; }
        public string BirthDateStr { get; set; }
        public DateTime? BirthDate { get; set; }
        public string Gender { get; set; }
        public string IssueDateStr { get; set; }
        public DateTime? IssueDate { get; set; }
        public string ExpireDateStr { get; set; }
        public DateTime? ExpireDate { get; set; }
        public string Address { get; set; }
        public string HouseNo { get; set; }
        public string VillageNo { get; set; }
        public string Lane { get; set; }
        public string Road { get; set; }
        public string SubDistrict { get; set; }
        public string District { get; set; }
        public string Province { get; set; }
    }
}