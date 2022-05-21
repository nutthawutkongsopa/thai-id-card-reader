using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PCSC;
using PCSC.Monitoring;

namespace ThaiIDCardReader
{
    public interface IThaiIDCardReder : IDisposable
    {
        event DeviceChangeEvent DeviceStatusChanged;
        event DeviceMonitorExceptionEvent DeviceMonitorException;
        event StatusChangeEvent CardStatusChanged;
        event CardInsertedEvent CardInserted;
        event CardRemovedEvent CardRemoved;
        event MonitorExceptionEvent CardMonitorException;
        string[] GetReaders();
        /// <summary>
        /// Auto start monitoring card and card reader on device change, And auto re-initial monitoring if them are gone or not working.
        /// </summary>
        void AutoMonitor();
        void BeginMonitorDeviceChange();
        void BeginMonitorCardChange(string deviceName);
        ICardReader ConnectReader(string readerName, SCardShareMode mode, SCardProtocol preferredProtocol);
        ICardReader ConnectReader(string readerName);
        PersonalData GetData(ICardReader cardReader, ReadOptiontions options = null);
        PersonalData GetData(string readerName, ReadOptiontions options = null);
    }
}