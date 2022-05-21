using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ThaiIDCardReader
{
    public class ReadOptiontions
    {
        public bool PersonalInfo { get; set; } = true;
        public bool Photo { get; set; } = false;
        public bool NHSOInfo { get; set; } = false;
    }
}