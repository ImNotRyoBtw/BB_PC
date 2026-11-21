using System;
namespace BBPC.Identity
{
    // 6.0.6 AUID/UDID/St/CleanAUID. Delegates keep Windows storage and tests explicit.
    public sealed class RecoveredIdentity
    {
        public const string UdidKey = "UDIDManager_UniqueDeviceIDKey";
        public const string AuidKey = "com.beatrun.UDIDManager.auid.key";
        public const string TransferKey = "com.bbt.UDIDManager.transferID.key";
        public const string PairKey="BBPC.Windows.Identity606.Pair",PendingKey="BBPC.Windows.Identity606.Pending";
        private readonly Func<string, string, string> read;
        private readonly Action<string, string> write;
        private readonly Action flush;
        private string cachedUdid;
        public RecoveredIdentity(Func<string, string, string> read, Action<string, string> write, Action flush)
        {
            this.read=read??throw new ArgumentNullException(nameof(read));this.write=write??throw new ArgumentNullException(nameof(write));this.flush=flush??throw new ArgumentNullException(nameof(flush));
            var record=this.read(PairKey,"")??"";int separator=record.IndexOf(':');int size;
            if(separator>0&&int.TryParse(record.Substring(0,separator),out size)&&size>0&&size<record.Length-separator-1)
            {
                var a=record.Substring(separator+1,size);var u=record.Substring(separator+1+size);
                if(string.IsNullOrEmpty(this.read(AuidKey,""))||string.IsNullOrEmpty(this.read(UdidKey,""))){this.write(AuidKey,a);this.write(UdidKey,u);this.flush();}
            }
        }
        public string Auid => read(AuidKey, "") ?? "";
        public string Udid { get { if (string.IsNullOrEmpty(cachedUdid)) cachedUdid = read(UdidKey, "") ?? ""; return cachedUdid; } }
        public bool HasPair => !string.IsNullOrEmpty(Auid) && !string.IsNullOrEmpty(Udid);
        public bool HasPartialPair=>!HasPair&&(!string.IsNullOrEmpty(Auid)||!string.IsNullOrEmpty(Udid));
        public bool RegistrationPending=>read(PendingKey,"")=="1";
        // GetInstallId 0x2C78B7C creates Guid + '_' + timestamp only when storage is empty.
        // Preserve the existing Windows ID, even its older N format; never rotate an installation to fix HTTP500.
        public static string GetInstallId(Func<string> read,Action<string> save,long now)
        {string existing=read();if(!string.IsNullOrEmpty(existing))return existing;string value=Guid.NewGuid().ToString("D")+"_"+now.ToString(System.Globalization.CultureInfo.InvariantCulture);save(value);return value;}
        public void BeginRegistration(){if(HasPair||HasPartialPair||RegistrationPending)throw new InvalidOperationException("Identity already present or registration unresolved");write(PendingKey,"1");flush();}
        public void AcceptServerPair(string auid, string udid)
        {
            if (string.IsNullOrEmpty(auid) || string.IsNullOrEmpty(udid)) throw new ArgumentException("Incomplete server identity");
            write(PairKey,auid.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+auid+udid);
            write(AuidKey, auid); write(UdidKey, udid);write(PendingKey,null);flush(); cachedUdid = udid;
        }
        public void CleanAuid() { write(AuidKey, null);write(PairKey,null);flush(); }
        // Native delta = server - request-start - signed RTT/2; CurrentServerTimestamp adds it.
        public static long MeasureTimeDelta(long before, long after, long server) => unchecked(server - before - (after - before) / 2);
    }
}
