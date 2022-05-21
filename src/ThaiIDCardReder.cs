using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PCSC.Monitoring;
using PCSC;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using PCSC.Exceptions;

namespace ThaiIDCardReader
{
    internal class ThaiIDCardReder : IThaiIDCardReder, IDisposable
    {
        //private Dictionary<string, ISCardMonitor> cardMonitors = new Dictionary<string, ISCardMonitor>();
        private ISCardContext context;
        private IDeviceMonitor deviceMonitor;
        ISCardMonitor cardMonitor;
        public event DeviceChangeEvent DeviceStatusChanged;
        public event DeviceMonitorExceptionEvent DeviceMonitorException;
        public event StatusChangeEvent CardStatusChanged;
        public event CardInsertedEvent CardInserted;
        public event CardRemovedEvent CardRemoved;
        public event MonitorExceptionEvent CardMonitorException;
        private bool autoMonitor = false;

        public ThaiIDCardReder()
        {

        }

        public string[] GetReaders()
        {
            InitContext();
            return this.context.GetReaders();
        }

        public void AutoMonitor()
        {
            this.autoMonitor = true;
            BeginMonitorDeviceChange();
        }

        public void BeginMonitorDeviceChange()
        {
            this.InitDeviceMonitor();
        }

        private void InitContext()
        {
            if (context != null)
            {
                context.Dispose();
            }

            var contextFactory = ContextFactory.Instance;
            this.context = contextFactory.Establish(SCardScope.System);
        }

        private void InitDeviceMonitor()
        {
            if (this.deviceMonitor != null)
            {
                this.deviceMonitor.Dispose();
            }
            var deviceMonitorFactory = DeviceMonitorFactory.Instance;
            this.deviceMonitor = deviceMonitorFactory.Create(SCardScope.System);
            this.deviceMonitor.StatusChanged += OnDeviceMonitorStatusChange;
            this.deviceMonitor.MonitorException += OnDeviceMonitorException;
            this.deviceMonitor.Start();
            if (autoMonitor)
            {
                InitialCardMonitors();
            }
        }

        private void OnDeviceMonitorException(object sender, DeviceMonitorExceptionEventArgs args)
        {
            if (this.autoMonitor)
            {
                this.InitDeviceMonitor();
            }
            this.DeviceMonitorException?.Invoke(this, args);
        }

        private void OnDeviceMonitorStatusChange(object sender, DeviceChangeEventArgs e)
        {
            if (autoMonitor)
            {
                InitialCardMonitors();
            }
            this.DeviceStatusChanged?.Invoke(this, e);
        }

        public void BeginMonitorCardChange(string deviceName)
        {
            if (this.cardMonitor == null)
            {
                var monitorFactory = MonitorFactory.Instance;
                this.cardMonitor = monitorFactory.Create(SCardScope.System);
            }
            this.cardMonitor.StatusChanged += this.OnCardMonitorStatusChanged;
            this.cardMonitor.CardInserted += this.OnCardInserted;
            this.cardMonitor.CardRemoved += this.OnCardRemoved;
            this.cardMonitor.MonitorException += this.OnCardMonitorException;
            this.cardMonitor.Start(deviceName);
        }

        private void OnCardMonitorException(object sender, PCSCException exception)
        {
            if (autoMonitor)
            {
                this.InitialCardMonitors();
            }
            else
            {
                this.CardMonitorException?.Invoke(sender, exception);
            }
        }

        private void OnCardRemoved(object sender, CardStatusEventArgs e)
        {
            this.CardRemoved?.Invoke(this, e);
        }

        private void OnCardInserted(object sender, CardStatusEventArgs e)
        {
            this.CardInserted?.Invoke(this, e);
        }

        private void InitialCardMonitors()
        {
            if (this.cardMonitor != null)
            {
                this.cardMonitor.Dispose();
            }
            var monitorFactory = MonitorFactory.Instance;
            this.cardMonitor = monitorFactory.Create(SCardScope.System);
            var newDevices = this.GetReaders();
            foreach (var item in newDevices)
            {
                this.BeginMonitorCardChange(item);
            }
        }

        private void OnCardMonitorStatusChanged(object sender, StatusChangeEventArgs e)
        {
            this.CardStatusChanged?.Invoke(this, e);
        }

        public ICardReader ConnectReader(string readerName, SCardShareMode mode, SCardProtocol preferredProtocol)
        {
            InitContext();
            return this.context.ConnectReader(readerName, SCardShareMode.Shared, SCardProtocol.Any);
        }

        public ICardReader ConnectReader(string readerName)
        {
            return this.ConnectReader(readerName, SCardShareMode.Shared, SCardProtocol.Any);
        }

        private byte[] SendCommand(ICardReader reader, byte[] command)
        {
            var attr = reader.GetStatus().GetAtr();
            var requestCommand = attr[0] == 0x3B && attr[1] == 0x67
                        ? new byte[] { 0x00, 0xc0, 0x00, 0x01, command[command.Length - 1] }
                        : new byte[] { 0x00, 0xc0, 0x00, 0x00, command[command.Length - 1] };

            var receiveBuffer = new Byte[258];
            reader.Transmit(command, receiveBuffer);
            var length = reader.Transmit(requestCommand, receiveBuffer);
            var resultBuffer = new byte[length];
            for (int i = 0; i < length; i++)
            {
                resultBuffer[i] = receiveBuffer[i];
            }
            return resultBuffer;
        }

        private byte[] GetPhoto(ICardReader cardReader)
        {
            cardReader.Transmit(SmartCardCommand.SelectThaiIDCard, new byte[258]);
            var commands = SmartCardCommand.Photo;
            using (var s = new MemoryStream())
            {
                var resultBuffer = new List<byte>();
                for (int i = 0; i < commands.Length; i++)
                {
                    var result = SendCommand(cardReader, commands[i]);
                    result = result.Take(result.Length - 2).ToArray();
                    s.Write(result, 0, result.Length);
                }
                s.Seek(0, SeekOrigin.Begin);
                return s.ToArray();
            }
        }

        private string GetString(ICardReader cardReader, byte[] command)
        {
            var bytes = SendCommand(cardReader, command).Where(t => t != 0 && t != 144).ToArray();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var utf8Bytes = Encoding.Convert(Encoding.GetEncoding("TIS-620"), Encoding.UTF8, bytes);
            var result = Encoding.UTF8.GetString(utf8Bytes).TrimEnd();
            result = Regex.Unescape(result);
            return result;
        }

        public PersonalInfo GetPersonalInfo(ICardReader reader)
        {
            reader.Transmit(SmartCardCommand.SelectThaiIDCard, new byte[258]);
            var cid = GetString(reader, SmartCardCommand.CID);
            var fullNameTH = GetString(reader, SmartCardCommand.FullNameTH);
            var fullNameEN = GetString(reader, SmartCardCommand.FullNameEN);
            var birthday = GetString(reader, SmartCardCommand.BirthDay).PadRight(8, ' ');
            var genderCode = GetString(reader, SmartCardCommand.Gender);
            var issuer = GetString(reader, SmartCardCommand.Issuer);
            var issueDateStr = GetString(reader, SmartCardCommand.IssueDate);
            var expireDateStr = GetString(reader, SmartCardCommand.ExpireDate);
            var address = GetString(reader, SmartCardCommand.Address);

            var titleTH = fullNameTH.Split('#')[0];
            var firstNameTH = fullNameTH.Split('#')[1];
            var middleNameTH = fullNameTH.Split('#')[2];
            var lastNameTH = fullNameTH.Split('#')[3];

            var titleEN = fullNameEN.Split('#')[0];
            var firstNameEN = fullNameEN.Split('#')[1];
            var middleNameEN = fullNameEN.Split('#')[2];
            var lastNameEN = fullNameEN.Split('#')[3];

            int? birthdayYear = int.TryParse(birthday.Substring(0, 4).Trim(), out int obYear) ? obYear - 543 : (int?)null;
            int? birthdayMonth = int.TryParse(birthday.Substring(4, 2).Trim(), out int obMonth) ? obMonth : (int?)null;
            int? birthdayDay = int.TryParse(birthday.Substring(6, 2).Trim(), out int obDay) ? obDay : (int?)null;

            DateTime? birthDate = null;
            if (birthdayYear.HasValue)
                birthDate = DateTime.TryParse($"{birthdayYear.Value}-{(birthdayMonth ?? 1)}-{(birthdayDay ?? 1)}", out DateTime bDate) ? bDate : (DateTime?)null;

            var gender = genderCode == "1" ? "M" : genderCode == "2" ? "F" : null;

            int? issueDateYear = int.TryParse(issueDateStr.Substring(0, 4).Trim(), out int isYear) ? isYear - 543 : (int?)null;
            int? issueDateMonth = int.TryParse(issueDateStr.Substring(4, 2).Trim(), out int isMonth) ? isMonth : (int?)null;
            int? issueDateDay = int.TryParse(issueDateStr.Substring(6, 2).Trim(), out int isDay) ? isDay : (int?)null;

            DateTime? issueDate = issueDateYear.HasValue && DateTime.TryParse($"{issueDateYear.Value}-{(issueDateMonth ?? 1)}-{(issueDateDay ?? 1)}", out DateTime isDate) ? isDate : (DateTime?)null;

            int? expDateYear = int.TryParse(expireDateStr.Substring(0, 4).Trim(), out int expYear) ? expYear - 543 : (int?)null;
            int? expDateMonth = int.TryParse(expireDateStr.Substring(4, 2).Trim(), out int expMonth) ? expMonth : (int?)null;
            int? expDateDay = int.TryParse(expireDateStr.Substring(6, 2).Trim(), out int expDay) ? expDay : (int?)null;

            DateTime? expireDate = expDateYear.HasValue && DateTime.TryParse($"{expDateYear.Value}-{(expDateMonth ?? 1)}-{(expDateDay ?? 1)}", out DateTime expDate) ? expDate : (DateTime?)null;

            var addressText = Regex.Replace(address, "#+", " ");

            var addressData = address.Split('#');

            var houseNo = addressData[0].Trim();
            var villageNo = addressData[1].Trim();
            var lane = addressData[2].Trim();
            var road = addressData[3].Trim();
            var subDistrict = Regex.Replace(addressData[5].Trim(), "^(ตำบล|แขวง)", "");
            var district = Regex.Replace(addressData[6].Trim(), "^(อำเภอ|เขต)", "");
            var province = Regex.Replace(addressData[7].Trim(), "^(จังหวัด)", "");

            return new PersonalInfo()
            {
                CID = cid,
                Address = addressText,
                BirthDate = birthDate,
                BirthDateStr = birthday,
                District = district,
                ExpireDate = expireDate,
                ExpireDateStr = expireDateStr,
                FirstNameEN = firstNameEN,
                FirstNameTH = firstNameTH,
                Gender = gender,
                HouseNo = houseNo,
                IssueDate = issueDate,
                IssueDateStr = issueDateStr,
                Lane = lane,
                LastNameEN = lastNameEN,
                LastNameTH = lastNameTH,
                MiddleNameEN = middleNameEN,
                MiddleNameTH = middleNameTH,
                Province = province,
                Road = road,
                SubDistrict = subDistrict,
                TitleEN = titleEN,
                TitleTH = titleTH,
                VillageNo = villageNo
            };
        }

        private NHSOInfo GetNHSOInfo(ICardReader cardReader)
        {
            cardReader.Transmit(SmartCardCommand.SelectNHSO, new byte[258]);
            var mainRights = GetString(cardReader, SmartCardCommand.MainRights);
            var subRights = GetString(cardReader, SmartCardCommand.SubRights);
            var mainHospitalName = GetString(cardReader, SmartCardCommand.MainHospitalName);
            var subHospitalName = GetString(cardReader, SmartCardCommand.SubHospitalName);
            var paidTypeStr = GetString(cardReader, SmartCardCommand.PaidType);
            var issueDateStr = GetString(cardReader, SmartCardCommand.NHSO_IssueDate);
            var expDateStr = GetString(cardReader, SmartCardCommand.NHSO_ExpDate);
            var updateDateStr = GetString(cardReader, SmartCardCommand.NHSO_UpdateDate);
            var changeHospitalAmountStr = GetString(cardReader, SmartCardCommand.ChangeHospitalAmount);

            DateTime? issueDate = null;
            if (!string.IsNullOrWhiteSpace(issueDateStr))
            {
                int? issueDateYear = int.TryParse(issueDateStr.Substring(0, 4).Trim(), out int isYear) ? isYear - 543 : (int?)null;
                int? issueDateMonth = int.TryParse(issueDateStr.Substring(4, 2).Trim(), out int isMonth) ? isMonth : (int?)null;
                int? issueDateDay = int.TryParse(issueDateStr.Substring(6, 2).Trim(), out int isDay) ? isDay : (int?)null;
                if (issueDateYear.HasValue)
                    issueDate = DateTime.TryParse($"{issueDateYear.Value}-{(issueDateMonth ?? 1)}-{(issueDateDay ?? 1)}", out DateTime iDate) ? iDate : (DateTime?)null;

            }

            DateTime? expDate = null;
            if (!string.IsNullOrWhiteSpace(expDateStr))
            {
                int? expDateYear = int.TryParse(expDateStr.Substring(0, 4).Trim(), out int exYear) ? exYear - 543 : (int?)null;
                int? expDateMonth = int.TryParse(expDateStr.Substring(4, 2).Trim(), out int exMonth) ? exMonth : (int?)null;
                int? expDateDay = int.TryParse(expDateStr.Substring(6, 2).Trim(), out int exDay) ? exDay : (int?)null;
                if (expDateYear.HasValue)
                    expDate = DateTime.TryParse($"{expDateYear.Value}-{(expDateMonth ?? 1)}-{(expDateDay ?? 1)}", out DateTime eDate) ? eDate : (DateTime?)null;

            }

            DateTime? updateDate = null;
            if (!string.IsNullOrWhiteSpace(updateDateStr))
            {
                int? updateDateYear = int.TryParse(updateDateStr.Substring(0, 4).Trim(), out int upYear) ? upYear - 543 : (int?)null;
                int? updateDateMonth = int.TryParse(updateDateStr.Substring(4, 2).Trim(), out int upMonth) ? upMonth : (int?)null;
                int? updateDateDay = int.TryParse(updateDateStr.Substring(6, 2).Trim(), out int upDay) ? upDay : (int?)null;
                if (updateDateYear.HasValue)
                    updateDate = DateTime.TryParse($"{updateDateYear.Value}-{(updateDateMonth ?? 1)}-{(updateDateDay ?? 1)}", out DateTime uDate) ? uDate : (DateTime?)null;

            }

            int? changHosAmount = int.TryParse(changeHospitalAmountStr, out int amt) ? amt : (int?)null;
            int? paidType = int.TryParse(paidTypeStr, out int pt) ? pt : (int?)null;

            var result = new NHSOInfo()
            {
                ChangeHospitalAmount = changHosAmount,
                ExpireDate = expDate,
                IssueDate = issueDate,
                MainRights = mainRights,
                MainHospitalName = mainHospitalName,
                PaidType = paidType,
                SubHospitalName = subHospitalName,
                SubRights = subRights,
                UpdateDate = updateDate
            };

            return result;
        }

        public PersonalData GetData(ICardReader cardReader, ReadOptiontions options = null)
        {
            options = options ?? new ReadOptiontions();
            var result = new PersonalData();
            if (options.PersonalInfo)
            {
                result.PersonalInfo = GetPersonalInfo(cardReader);
            }
            if (options.Photo)
            {
                result.Photo = GetPhoto(cardReader);
            }
            if (options.NHSOInfo)
            {
                result.NHSOInfo = GetNHSOInfo(cardReader);
            }
            return result;
        }

        public PersonalData GetData(string readerName, ReadOptiontions options = null)
        {
            using (var reader = this.ConnectReader(readerName))
            {
                return this.GetData(reader, options);
            }
        }

        private void KillAll()
        {
            try
            {
                this.context?.Cancel();
                this.context?.Dispose();
            }
            catch (System.Exception)
            {
            }

            try
            {
                if (this.cardMonitor != null)
                {
                    this.cardMonitor.Dispose();
                }
            }
            catch (System.Exception)
            {


            }

            try
            {
                this.deviceMonitor.Cancel();
                this.deviceMonitor.Dispose();
            }
            catch (System.Exception)
            {
            }
        }

        public void Dispose()
        {
            this.KillAll();
        }
    }

    static class SmartCardCommand
    {
        public static readonly byte[] SelectThaiIDCard = new byte[] { 0x00, 0xA4, 0x04, 0x00, 0x08, 0xA0, 0x00, 0x00, 0x00, 0x54, 0x48, 0x00, 0x01 };
        public static readonly byte[] CID = new byte[] { 0x80, 0xb0, 0x00, 0x04, 0x02, 0x00, 0x0d };
        public static readonly byte[] FullNameTH = new byte[] { 0x80, 0xb0, 0x00, 0x11, 0x02, 0x00, 0x64 };
        public static readonly byte[] FullNameEN = new byte[] { 0x80, 0xb0, 0x00, 0x75, 0x02, 0x00, 0x64 };
        public static readonly byte[] BirthDay = new byte[] { 0x80, 0xb0, 0x00, 0xD9, 0x02, 0x00, 0x08 };
        public static readonly byte[] Gender = new byte[] { 0x80, 0xb0, 0x00, 0xE1, 0x02, 0x00, 0x01 };
        public static readonly byte[] Issuer = new byte[] { 0x80, 0xb0, 0x00, 0xF6, 0x02, 0x00, 0x64 };
        public static readonly byte[] IssueDate = new byte[] { 0x80, 0xb0, 0x01, 0x67, 0x02, 0x00, 0x08 };
        public static readonly byte[] ExpireDate = new byte[] { 0x80, 0xb0, 0x01, 0x6F, 0x02, 0x00, 0x08 };
        public static readonly byte[] Address = new byte[] { 0x80, 0xb0, 0x15, 0x79, 0x02, 0x00, 0x64 };
        public static readonly byte[][] Photo = new byte[][]{
            new byte[]{ 0x80, 0xB0, 0x01, 0x7B, 0x02, 0x00, 0xFF },
            new byte[]{ 0x80, 0xB0, 0x02, 0x7A, 0x02, 0x00, 0xFF },
            new byte[]{ 0x80, 0xB0, 0x03, 0x79, 0x02, 0x00, 0xFF },
            new byte[]{ 0x80, 0xB0, 0x04, 0x78, 0x02, 0x00, 0xFF },
            new byte[]{ 0x80, 0xB0, 0x05, 0x77, 0x02, 0x00, 0xFF },
            new byte[]{ 0x80, 0xB0, 0x06, 0x76, 0x02, 0x00, 0xFF },
            new byte[]{ 0x80, 0xB0, 0x07, 0x75, 0x02, 0x00, 0xFF },
            new byte[]{ 0x80, 0xB0, 0x08, 0x74, 0x02, 0x00, 0xFF },
            new byte[]{ 0x80, 0xB0, 0x09, 0x73, 0x02, 0x00, 0xFF },
            new byte[]{ 0x80, 0xB0, 0x0A, 0x72, 0x02, 0x00, 0xFF },
            new byte[]{ 0x80, 0xB0, 0x0B, 0x71, 0x02, 0x00, 0xFF },
            new byte[]{ 0x80, 0xB0, 0x0C, 0x70, 0x02, 0x00, 0xFF },
            new byte[]{ 0x80, 0xB0, 0x0D, 0x6F, 0x02, 0x00, 0xFF },
            new byte[]{ 0x80, 0xB0, 0x0E, 0x6E, 0x02, 0x00, 0xFF },
            new byte[]{ 0x80, 0xB0, 0x0F, 0x6D, 0x02, 0x00, 0xFF },
            new byte[]{ 0x80, 0xB0, 0x10, 0x6C, 0x02, 0x00, 0xFF },
            new byte[]{ 0x80, 0xB0, 0x11, 0x6B, 0x02, 0x00, 0xFF },
            new byte[]{ 0x80, 0xB0, 0x12, 0x6A, 0x02, 0x00, 0xFF },
            new byte[]{ 0x80, 0xB0, 0x13, 0x69, 0x02, 0x00, 0xFF },
            new byte[]{ 0x80, 0xB0, 0x14, 0x68, 0x02, 0x00, 0xFF },
        };

        public static readonly byte[] SelectNHSO = new byte[] { 0x00, 0xa4, 0x04, 0x00, 0x08, 0xa0, 0x00, 0x00, 0x00, 0x54, 0x48, 0x00, 0x83 };
        public static readonly byte[] MainRights = new byte[] { 0x80, 0xb0, 0x00, 0x04, 0x02, 0x00, 0x3c };
        public static readonly byte[] SubRights = new byte[] { 0x80, 0xb0, 0x00, 0x40, 0x02, 0x00, 0x64 };
        public static readonly byte[] MainHospitalName = new byte[] { 0x80, 0xb0, 0x00, 0xa4, 0x02, 0x00, 0x50 };
        public static readonly byte[] SubHospitalName = new byte[] { 0x80, 0xb0, 0x00, 0xf4, 0x02, 0x00, 0x50 };
        public static readonly byte[] PaidType = new byte[] { 0x80, 0xb0, 0x01, 0x44, 0x02, 0x00, 0x01 };
        public static readonly byte[] NHSO_IssueDate = new byte[] { 0x80, 0xb0, 0x01, 0x45, 0x02, 0x00, 0x08 };
        public static readonly byte[] NHSO_ExpDate = new byte[] { 0x80, 0xb0, 0x01, 0x4d, 0x02, 0x00, 0x08 };
        public static readonly byte[] NHSO_UpdateDate = new byte[] { 0x80, 0xb0, 0x01, 0x55, 0x02, 0x00, 0x08 };
        public static readonly byte[] ChangeHospitalAmount = new byte[] { 0x80, 0xb0, 0x01, 0x5d, 0x02, 0x00, 0x01 };

    }
}