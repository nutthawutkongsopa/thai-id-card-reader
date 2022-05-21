using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ThaiIDCardReader
{
    public static class CardReaderFactory
    {
        public static IThaiIDCardReder Create()
        {
            return new ThaiIDCardReader.ThaiIDCardReder();
        }
    }
}