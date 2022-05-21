using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ThaiIDCardReader
{
    public class PersonalData
    {
        public PersonalInfo PersonalInfo { get; set; }
        public byte[] Photo { get; set; }
        public NHSOInfo NHSOInfo { get; set; }
    }
}