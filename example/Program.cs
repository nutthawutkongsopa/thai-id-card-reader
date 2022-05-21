using System;
using ThaiIDCardReader;
using PCSC;

namespace ThaiIDCardReader.Example
{
    class Program
    {
        static void Main(string[] args)
        {
            using var reader = CardReaderFactory.Create();
            reader.AutoMonitor(); //If don't call this function, You can using function BeginMonitorDeviceChange for monitor card reader device change and BeginMonitorCardChange for monitor card change.

            var readerNames = reader.GetReaders(); //Get connected readers.

            reader.DeviceStatusChanged += (sender, args) =>
            {
                Console.WriteLine($"Attached Readers: \n\t {string.Join("\n", args.AttachedReaders)}");
            };
            reader.CardInserted += (sender, args) =>
            {
                if (args.State == SCRState.Present)
                {
                    var data = reader.GetData(args.ReaderName); //Read data from ID Card
                }
            };
            Console.ReadKey();
        }
    }
}
