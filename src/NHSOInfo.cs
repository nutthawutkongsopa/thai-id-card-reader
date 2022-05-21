using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ThaiIDCardReader
{
    public class NHSOInfo
    {
        public string MainRights { get; set; }
        public string SubRights { get; set; }
        public string MainHospitalName { get; set; }
        public string SubHospitalName { get; set; }
        public int? PaidType { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? ExpireDate { get; set; }
        public DateTime? UpdateDate { get; set; }
        public int? ChangeHospitalAmount { get; set; }
    }
}