
# ThaiIDCardReader

A library for reading Thai ID card data.

## Package
https://www.nuget.org/packages/ThaiIDCardReader


## Examples

```csharp
using System;
using ThaiIDCardReader;
using PCSC;
using System.Text.Json;

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
                    var data = reader.GetData(args.ReaderName, new ReadOptiontions() { NHSOInfo = true }); //Read data from ID Card
                    Console.WriteLine("Card Inserted");
                    var json = JsonSerializer.Serialize(data, new JsonSerializerOptions()
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    });
                    Console.WriteLine(json);
                }
            };
            Console.ReadKey();
        }
    }
}
```

