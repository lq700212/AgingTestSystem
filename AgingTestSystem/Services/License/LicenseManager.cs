using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AgingTestSystem.Services.License
{
    /// <summary>
    /// 授权判定结果（【V1.83 新增】启动闸的唯一输出，主窗体与 Program 只认它）。
    /// </summary>
    public enum LicenseStatus
    {
        /// <summary>持证合法（机器/项目/点数/有效期全过）。</summary>
        Authorized,
        /// <summary>无证试用中（首跑起 30 天内，功能全开，标题栏明示剩余天数）。</summary>
        Trial,
        /// <summary>试用剩 ≤7 天（放行 + 每天弹一次续费提醒，不静默过期）。</summary>
        TrialExpiring,
        /// <summary>持证但已过期 ≤7 天（宽限不断线：放行 + 警告，逼着续费又不误伤生产）。</summary>
        GraceExpired,
        /// <summary>阻断启动（无证超试用 / 机器不对 / 项目不对 / 点数超配 / 证非法）。</summary>
        Blocked,
    }

    /// <summary>
    /// 单次判定的明细（状态 + 给用户看的一句话 + 标题栏后缀 + 剩余天数）。
    /// </summary>
    public class LicenseResult
    {
        public LicenseStatus Status;
        public string Message = "";
        public string TitleSuffix = "";
        public int DaysLeft;
        public LicenseInfo Info;
        /// <summary>功能超范围的软警告（放行但要告诉用户，如开了 MES 但证里没买 MES）。</summary>
        public string FeatureWarning = "";
        /// <summary>是否放行（Authorized/Trial/TrialExpiring/GraceExpired 全放行，只有 Blocked 不放）。</summary>
        public bool Allowed
        {
            get { return Status != LicenseStatus.Blocked; }
        }
    }

    /// <summary>
    /// 软件授权管理器（【V1.83 新增】自研机器码 + RSA2048 离线授权）。
    ///
    /// 【生意怎么防】
    /// 以前拷文件夹到新工控机、改改工艺配置就能跑新项目。现在启动闸拦三道：
    /// ①机器指纹对不上 → 新机直接阻断（拷文件带不走）；
    /// ②限定版授权绑项目名 → 新开项目名即拦截（新项目必须回头联系商务）；
    /// ③工位数超配 → 72 点的证跑不动 80 点的线（按点数卖）。
    /// 到期后 7 天宽限不断线（产线停一分钟都是钱，逼续费但不误伤生产）。
    ///
    /// 【密码学怎么防改】
    /// 签发端（公司，私钥，见 tools/LicenseKeyGen）对载荷规范串签名；
    /// 产品端只认嵌在下面的公钥验签。手改 License.lic 一个字符签名即失效，
    /// 直接判 Blocked。私钥绝不进产品仓库（.gitignore 已拦 license_private.xml）。
    ///
    /// 【试用怎么防删库】
    /// 首跑时间双记：文件（License.trial，DPAPI 保护）+ 注册表
    /// （HKCU\Software\AgingTestSystem\LicenseFirstRun），取最早者——删文件
    /// 还有注册表，删注册表还有文件，两边全删才算重计（工控机现场没人这么干，
    /// 真干了也最多再拿 30 天，认了）。时钟回拨用"上次运行时间"对冲：
    /// 生效 now = max(现在, 上次)，回拨换不来有效期。
    ///
    /// 【测试缝】UtcNow/BaseDir/PublicKeyXml/RegistryPath 四处静态可替换
    ///（MesReporter.Transport 同先例：生产走真实值，用例赋值后 try/finally 复位，
    /// 见 LicenseTests）。RegistryPath 隔离是因为试用双记写在 HKCU，用例必须
    /// 落 GUID 子键、跑完删键，不能污染开发机。
    /// </summary>
    public static class LicenseManager
    {
        /// <summary>产品端验签公钥（签发端私钥的另一半；换钥=老证全废，需同步升级 v）。</summary>
        private const string PublicKeyXml =
            "<RSAKeyValue><Modulus>v7hujt9SZHWJoc9uH7g8hVhyq1zkeitYvmL0GQxWWSAnVftYGST7QndIS/mQ7mNooC75F/pyJs0vVSAVV/QrOcge99gpXD/rD8bBsknKe0VanrWDOARI3iVrlXOH39WnJQ4wfH7605BQ8BIoVMupVFpankDUsu6XGenlKshjShVWgXhHoaC1QQAqxCjRNey07idR4tpZ7oAnjloDpBRS3nogApZq6Qdyx60QRtgb4/NMA03EG0wNSOdCxxY9B/wIX9b4MJ6W0SNV5wKDw5S+sOB1A9j8AXxXdRWP6cCEIvH+DUBhBOpjusVLa4QjjmXh4SaazU7LYl192IVJwvo6fQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        /// <summary>授权文件名（住程序目录，跟机器，gitignore，绝不入库）。</summary>
        public const string LicenseFileName = "License.lic";

        /// <summary>试用记录文件名（同上，DPAPI 保护的首跑/上次时间）。</summary>
        public const string TrialFileName = "License.trial";

        /// <summary>无证试用天数（首跑起 30 天，功能全开，标题栏倒计时）。</summary>
        public const int TrialDays = 30;

        /// <summary>试用/过期宽限提醒线（剩 ≤7 天每天弹一次，不静默过期）。</summary>
        public const int ExpiringWarnDays = 7;

        /// <summary>持证过期宽限天数（过期 7 天内不断线，超 7 天阻断）。</summary>
        public const int ExpireGraceDays = 7;

        // ---- 测试缝（生产不动它，用例才赋值，MesReporter.Transport 同先例） ----
        internal static Func<DateTime> UtcNowFunc;
        internal static string BaseDirOverride;
        internal static string PublicKeyXmlOverride;
        // 注册表隔离缝：试用双记的第二记在 HKCU，用例如不隔离会污染开发机
        //（写进真的 AgingTestSystem 键）。用例给它一个 GUID 子路径，跑完删键，
        // finally 复位 null；生产恒为 null 走默认路径。
        internal static string RegistryPathOverride;

        private static DateTime UtcNow()
        {
            try { return UtcNowFunc != null ? UtcNowFunc() : DateTime.UtcNow; }
            catch { return DateTime.UtcNow; }
        }

        private static string BaseDir()
        {
            try
            {
                if (!string.IsNullOrEmpty(BaseDirOverride)) return BaseDirOverride;
                return AppDomain.CurrentDomain.BaseDirectory;
            }
            catch { return "."; }
        }

        private static string ActivePublicKey()
        {
            return !string.IsNullOrEmpty(PublicKeyXmlOverride) ? PublicKeyXmlOverride : PublicKeyXml;
        }

        /// <summary>
        /// 启动闸唯一入口（Program.Main 在 Application.Run 之前调它）。
        /// 读证 → 验签 → 机器/项目/点数/有效期四道关 → 试用分支。
        /// 永不抛异常：任何内部失败都按 Blocked 带原因返回（启动闸不能自己先炸）。
        /// </summary>
        /// <param name="projectName">当前项目名（ProjectProfile.ActiveProfileName，限定版比对用）</param>
        /// <param name="totalStations">本机工位数（DeviceConfig.TotalBarometers，点数比对用）</param>
        /// <param name="mesInUse">是否启用了 MES（超范围软警告用，不阻断）</param>
        /// <param name="rulesInUse">是否用了高级规则（同上）</param>
        public static LicenseResult EnsureStartupLicense(
            string projectName, int totalStations, bool mesInUse, bool rulesInUse)
        {
            var res = new LicenseResult();
            try
            {
                DateTime now = UtcNow().Date;
                string fp = "";
                string[] compHashes = null;
                try
                {
                    fp = MachineFingerprint.Ungroup(MachineFingerprint.Compute());
                    compHashes = MachineFingerprint.GetComponentHashes();
                }
                catch { fp = ""; }

                // 上次运行时间（回拨对冲 + 试用双记，见 ReadRunStamps/UpdateRunStamps）。
                DateTime lastRun;
                DateTime firstRun;
                ReadRunStamps(out firstRun, out lastRun);
                DateTime effective = now > lastRun ? now : lastRun;   // 回拨换不来时间
                try { UpdateRunStamps(firstRun, now); }
                catch { /* 记戳失败不拦启动 */ }

                LicenseInfo info;
                string sig;
                if (TryLoadLicense(out info, out sig))
                {
                    return CheckLicensed(info, sig, projectName, totalStations,
                        mesInUse, rulesInUse, effective, fp, compHashes);
                }

                // 无证 → 试用分支（首跑起 30 天）。
                if (firstRun == DateTime.MinValue)
                {
                    firstRun = now;
                    try { UpdateRunStamps(firstRun, now); }
                    catch { }
                }
                int used = (int)(effective - firstRun.Date).TotalDays;
                int left = TrialDays - used;
                if (left < 0) left = 0;
                if (used < TrialDays)
                {
                    res.Info = null;
                    res.DaysLeft = left;
                    if (left <= ExpiringWarnDays)
                    {
                        res.Status = LicenseStatus.TrialExpiring;
                        res.Message = string.Format(
                            "试用版剩余 {0} 天，到期后将无法启动。请联系商务获取正式授权（机器码见【关于→软件授权】）。", left);
                        res.TitleSuffix = "[试用版剩余" + left + "天]";
                    }
                    else
                    {
                        res.Status = LicenseStatus.Trial;
                        res.Message = string.Format("试用版（剩余 {0} 天），功能全开。", left);
                        res.TitleSuffix = "[试用版剩余" + left + "天]";
                    }
                    return res;
                }
                res.Status = LicenseStatus.Blocked;
                res.Message = "试用期（30 天）已到，且未找到有效授权文件（License.lic）。\n\n" +
                    "请在【关于→软件授权】里导出机器码发给商务，导入签发的 License.lic 后再启动。";
                res.TitleSuffix = "[试用到期]";
                return res;
            }
            catch (Exception ex)
            {
                res.Status = LicenseStatus.Blocked;
                res.Message = "授权检查异常（已安全阻断启动）：" + ex.Message;
                res.TitleSuffix = "[授权异常]";
                return res;
            }
        }

        // ================= 持证四道关 =================

        private static LicenseResult CheckLicensed(LicenseInfo info, string sig,
            string projectName, int totalStations, bool mesInUse, bool rulesInUse,
            DateTime effective, string fp, string[] compHashes)
        {
            var res = new LicenseResult { Info = info };

            // 第 0 关：签名（手改一个字符都过不去；公钥不对=签发端换钥了，直接拦）。
            if (info == null || info.v != 1 || !VerifySignature(info, sig))
            {
                res.Status = LicenseStatus.Blocked;
                res.Message = "授权文件签名无效（文件被改动或不是本软件签发的证）。\n\n请重新导入商务签发的 License.lic。";
                res.TitleSuffix = "[授权无效]";
                return res;
            }

            // 第 1 关：机器（总指纹命中直接过；差一块硬件警告过；差两块以上=换整机，拦）。
            if (!CheckMachine(info, fp, compHashes, res)) return res;   // 失败时 res 已填好

            // 第 2 关：项目（空=通用版跳过；非空必须与当前项目一致，新开项目名即拦）。
            string need = (info.project ?? "").Trim();
            string cur = (projectName ?? "").Trim();
            if (need.Length > 0 && !string.Equals(need, cur, StringComparison.OrdinalIgnoreCase))
            {
                res.Status = LicenseStatus.Blocked;
                res.Message = string.Format("本授权限定项目【{0}】，当前项目是【{1}】，不允许启动。\n\n" +
                    "新项目请联系商务签发新授权（通用版不限项目）。", need, cur);
                res.TitleSuffix = "[项目未授权]";
                return res;
            }

            // 第 3 关：点数（证里最大工位数 < 本机配置即拦，防 36 点的证跑 72 点的线）。
            int needStations = totalStations > 0 ? totalStations : 0;
            if (info.maxStations <= 0)
            {
                // 【大扫荡】点数字段缺失/非法≠超配：单独报非法，不误导用户去"升级点数"。
                res.Status = LicenseStatus.Blocked;
                res.Message = "授权文件损坏（最大工数字段缺失或非法），请重新导入商务签发的 License.lic。";
                res.TitleSuffix = "[授权无效]";
                return res;
            }
            if (needStations > info.maxStations)
            {
                res.Status = LicenseStatus.Blocked;
                res.Message = string.Format("本授权最大工位数 {0}，本机配置 {1}，超配不允许启动。\n\n请联系商务升级点数。",
                    info.maxStations, needStations);
                res.TitleSuffix = "[点数超配]";
                return res;
            }

            // 第 4 关：有效期（含当天；过期 7 天内宽限放行 + 警告，超 7 天阻断）。
            DateTime exp = info.ExpiryDate();
            if (exp == DateTime.MinValue)
            {
                res.Status = LicenseStatus.Blocked;
                res.Message = "授权文件到期日非法，请重新导入商务签发的 License.lic。";
                res.TitleSuffix = "[授权无效]";
                return res;
            }
            int over = (int)(effective - exp).TotalDays;   // ≤0=有效期内
            if (over > ExpireGraceDays)
            {
                res.Status = LicenseStatus.Blocked;
                // 【大扫荡】显示实际超期天数（以前传常量 7，超期 30 天也显示"超宽限 7 天"）。
                res.Message = string.Format("授权已于 {0:yyyy-MM-dd} 到期（已超期 {1} 天，宽限 {2} 天），不允许启动。\n\n请联系商务续费。",
                    exp, over, ExpireGraceDays);
                res.TitleSuffix = "[授权到期]";
                return res;
            }

            // 功能超范围：只警告不阻断（现场不断线，续费慢慢谈）。
            var warns = new List<string>();
            if (mesInUse && !info.featuresMes)
                warns.Add("MES 上报已开但本授权未含 MES 功能");
            if (rulesInUse && !info.featuresRules)
                warns.Add("高级规则已用但本授权未含规则功能");
            if (warns.Count > 0)
                res.FeatureWarning = string.Join("；", warns.ToArray()) + "（本次放行，请联系商务补购，超范围使用不在维保内）。";

            if (over > 0)
            {
                res.Status = LicenseStatus.GraceExpired;
                int left = ExpireGraceDays - over;
                res.DaysLeft = left;
                res.Message = string.Format("授权已于 {0:yyyy-MM-dd} 到期，宽限剩余 {1} 天（宽限内可正常生产）。请尽快联系商务续费。",
                    exp, left);
                res.TitleSuffix = "[授权宽限剩余" + left + "天]";
                return res;
            }
            int days = (int)(exp - effective).TotalDays;
            res.Status = LicenseStatus.Authorized;
            res.DaysLeft = days;
            string proj = string.IsNullOrEmpty(need) ? "通用版" : need;
            res.Message = string.Format("已授权（{0}，{1} 点，至 {2:yyyy-MM-dd}）。", proj, info.maxStations, exp);
            res.TitleSuffix = string.Format("[已授权至{0:yyyy-MM-dd}]", exp);
            return res;
        }

        /// <summary>
        /// 机器关（总指纹命中→过；否则数分量命中：≥3=换一块硬件警告过；
        /// ≤2=换整机/虚拟机克隆，拦。分量缺失（UNKNOWN）不计命中也不计脱靶——
        /// 精简系统读不出三样时，总指纹是主要依据，分量只做加分项）。
        ///
        /// 【V1.83.1 两处收紧】
        /// 1) 证里 machine 为空直接拦（fail-closed：签发端 KeyGen 强制 64 位指纹，
        ///    空指纹的证只有手造一种来源，不能按"没绑机器"放行）。
        /// 2) 分量段不进签名是刻意为之（换一块硬件后总指纹变了签名必须照样有效，
        ///    分量只能做"是不是同一台机"的辅助判断）。如实说明威胁模型：分量段防的是
        ///    "拷文件夹改配置就跑"的顺手复用；真下决心读源码伪造分量的人，
        ///    同等 effort 也能直接 patch 掉 exe 里的检查（无混淆的 .NET 都一样），
        ///    所以这里不堆复杂度，修前注释"篡改分量占不了便宜"的说法不准确，特此纠正。
        /// </summary>
        private static bool CheckMachine(LicenseInfo info, string fp,
            string[] compHashes, LicenseResult res)
        {
            string want = MachineFingerprint.Ungroup(info.machine);
            if (string.IsNullOrEmpty(want))
            {
                res.Status = LicenseStatus.Blocked;
                res.Message = "授权文件未绑定机器（machine 为空），拒绝启动。\n\n请联系商务重新签发 License.lic。";
                res.TitleSuffix = "[授权无效]";
                return false;
            }
            if (!string.IsNullOrEmpty(want) && !string.IsNullOrEmpty(fp)
                && string.Equals(want, fp, StringComparison.Ordinal))
            {
                return true;
            }
            // 总指纹没对上：试分量（导入时把本机分量一起写进文件扩展段，
            // 见 SaveLicenseFile；老证没有分量段 → 按换机处理，不猜）。
            List<string> wantComps = LoadComponentHashes();
            if (wantComps != null && compHashes != null
                && wantComps.Count == compHashes.Length
                && wantComps.Count == MachineFingerprint.ComponentNames.Length)
            {
                int hit = 0, judged = 0;
                for (int i = 0; i < wantComps.Count; i++)
                {
                    string no = MachineFingerprint.HashOf(MachineFingerprint.ComponentNames[i] + "=UNKNOWN");
                    string w = MachineFingerprint.Ungroup(wantComps[i]);
                    string c = MachineFingerprint.Ungroup(compHashes[i]);
                    if (string.Equals(w, MachineFingerprint.Ungroup(no), StringComparison.Ordinal))
                        continue;   // 签发端该分量就没读出来，不算题
                    judged++;
                    if (!string.IsNullOrEmpty(w) && string.Equals(w, c, StringComparison.Ordinal)) hit++;
                }
                if (judged > 0 && hit >= 2 && hit >= judged - 1)
                {
                    // 只换了一块硬件：放行 + 明示（换两块以上？分量全变=整机换了，拦）。
                    res.FeatureWarning = AppendWarn(res.FeatureWarning,
                        "检测到硬件变更（一块），本次放行；再换硬件需重新签发授权");
                    return true;
                }
            }
            res.Status = LicenseStatus.Blocked;
            res.Message = "授权绑定的机器与本机不符（授权是一机一证，拷到新工控机不能直接用）。\n\n" +
                "请在本机【关于→软件授权】里导出机器码，发给商务签发新授权。";
            res.TitleSuffix = "[机器未授权]";
            return false;
        }

        private static string AppendWarn(string old, string add)
        {
            if (string.IsNullOrEmpty(old)) return add + "。";
            return old + "；" + add + "。";
        }

        // ================= 文件与签验 =================

        /// <summary>读授权文件（不存在/损坏返回 false，调用方走试用分支；绝不抛）。</summary>
        public static bool TryLoadLicense(out LicenseInfo info, out string signatureB64)
        {
            info = null;
            signatureB64 = null;
            try
            {
                string path = Path.Combine(BaseDir(), LicenseFileName);
                if (!File.Exists(path)) return false;
                string text = AtomicFile.SafeReadAllText(path, Encoding.UTF8);
                return LicenseInfo.TryParseFile(text, out info, out signatureB64);
            }
            catch { return false; }
        }

        /// <summary>
        /// RSA 验签（SHA256 + PKCS#1 v1.5；.NET Framework 4.x 用 RSACryptoServiceProvider
        /// 的 VerifyData 重载，兼容最老编译器）。
        /// </summary>
        public static bool VerifySignature(LicenseInfo info, string signatureB64)
        {
            if (info == null || string.IsNullOrWhiteSpace(signatureB64)) return false;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(info.CanonicalString());
                byte[] sig = Convert.FromBase64String(signatureB64.Trim());
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(ActivePublicKey());
                    return rsa.VerifyData(data, "SHA256", sig);
                }
            }
            catch { return false; }
        }

        /// <summary>
        /// 签发端用的签名函数（【仅签发工具调】产品端不用它，但验签逻辑与它严格互逆，
        /// 共用 CanonicalString；私钥只存在公司电脑的 license_private.xml 里）。
        /// </summary>
        public static string SignForIssuer(LicenseInfo info, string privateKeyXml)
        {
            if (info == null || string.IsNullOrEmpty(privateKeyXml)) return null;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(info.CanonicalString());
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(privateKeyXml);
                    byte[] sig = rsa.SignData(data, "SHA256");
                    return Convert.ToBase64String(sig);
                }
            }
            catch { return null; }
        }

        /// <summary>授权文件写盘（导入/签发后调它；写失败返回 false，调用方弹框）。
        /// 无 components 时按"当前机器分量"补（本机导入本机证）；有则原样保留
        ///（KeyGen 签发的证没有分量段，导入时由 LicenseForm 从原文件读出来透传，
        /// 保证拷到别台机分量对不上、漂移放过不了——见 CheckMachine）。</summary>
        public static bool SaveLicenseFile(LicenseInfo info, string signatureB64)
        {
            return SaveLicenseFile(info, signatureB64, null);
        }

        /// <summary>带分量段的写盘（components 传 null=补当前机器分量）。</summary>
        public static bool SaveLicenseFile(LicenseInfo info, string signatureB64, List<string> components)
        {
            try
            {
                if (info == null || string.IsNullOrWhiteSpace(signatureB64)) return false;
                var root = new JObject();
                root["payload"] = JObject.FromObject(info);
                root["signature"] = signatureB64.Trim();
                // 分量指纹扩展段（漂移容忍用，不进签名——换一块硬件后总指纹变了，
                // 签名照样有效，分量只做"是不是同一台机"的辅助判断；威胁模型见
                // CheckMachine 注释：防顺手拷机，不防读源码伪造（后者与 patch exe 同级）。
                try
                {
                    if (components == null || components.Count == 0)
                    {
                        var auto = new List<string>();
                        foreach (string h in MachineFingerprint.GetComponentHashes()) auto.Add(h);
                        components = auto;
                    }
                    var comps = new JArray();
                    foreach (string h in components)
                    {
                        if (!string.IsNullOrEmpty(h)) comps.Add(h);
                    }
                    if (comps.Count > 0) root["components"] = comps;
                }
                catch { }
                // 【大扫荡】原子写。
                AtomicFile.WriteAllText(Path.Combine(BaseDir(), LicenseFileName),
                    root.ToString(Formatting.Indented), Encoding.UTF8);
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// 读文件里的分量段（TryLoadLicense 只读 payload+signature；
        /// 机器漂移比对要分量段，走这里。无分量段返回空表=老证，按换机处理）。
        /// </summary>
        public static List<string> LoadComponentHashes()
        {
            var list = new List<string>();
            try
            {
                string path = Path.Combine(BaseDir(), LicenseFileName);
                if (!File.Exists(path)) return list;
                var root = JObject.Parse(AtomicFile.SafeReadAllText(path, Encoding.UTF8));
                var arr = root["components"] as JArray;
                if (arr == null) return list;
                foreach (var t in arr) list.Add((string)t ?? "");
            }
            catch { }
            return list;
        }

        // ================= 试用双记（文件 + 注册表，取最早首跑） =================

        private const string RegPathDefault = @"Software\AgingTestSystem";
        private const string RegFirst = "LicenseFirstRun";
        private const string RegLast = "LicenseLastRun";

        private static string RegPathActive()
        {
            try
            {
                if (!string.IsNullOrEmpty(RegistryPathOverride)) return RegistryPathOverride;
            }
            catch { }
            return RegPathDefault;
        }

        private static void ReadRunStamps(out DateTime firstRun, out DateTime lastRun)
        {
            firstRun = DateTime.MinValue;
            lastRun = DateTime.MinValue;
            try
            {
                // 文件（DPAPI：删了重建=重计，所以才有注册表第二记）。
                string path = Path.Combine(BaseDir(), TrialFileName);
                if (File.Exists(path))
                {
                    string raw = AtomicFile.SafeReadAllText(path, Encoding.UTF8).Trim();
                    // DPAPI 密文优先解；明文兜底（UpdateRunStamps 在 DPAPI 失败时写过明文，
                    // 读必须认这两种，否则"写了读不回"试用首跑日期会丢）。见 UpdateRunStamps 注释。
                    string plain = raw.StartsWith(MesCrypto.Prefix, StringComparison.Ordinal)
                        ? MesCrypto.Unprotect(raw)
                        : raw;
                    if (!string.IsNullOrEmpty(plain))
                    {
                        // 文件首格用 "NONE" 表示"首跑未登记"（UpdateRunStamps 在 firstRun=MinValue
                        // 时不写垃圾日期 0001-01-01，见该方法的防空转注释）。
                        string[] parts = plain.Split('|');
                        DateTime f, l;
                        if (parts.Length >= 1 && parts[0].Trim().Length > 0
                            && parts[0].Trim() != "NONE"
                            && DateTime.TryParse(parts[0], out f) && f.Date != DateTime.MinValue)
                            firstRun = f.Date;
                        if (parts.Length >= 2 && DateTime.TryParse(parts[1], out l) && l.Date != DateTime.MinValue)
                            lastRun = l.Date;
                    }
                }
            }
            catch { }
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RegPathActive(), false))
                {
                    if (k != null)
                    {
                        DateTime f, l;
                        if (DateTime.TryParse((k.GetValue(RegFirst) ?? "").ToString(), out f)
                            && f.Date != DateTime.MinValue)
                        {
                            if (firstRun == DateTime.MinValue || f.Date < firstRun) firstRun = f.Date;
                        }
                        if (DateTime.TryParse((k.GetValue(RegLast) ?? "").ToString(), out l)
                            && l.Date != DateTime.MinValue)
                        {
                            if (l.Date > lastRun) lastRun = l.Date;
                        }
                    }
                }
            }
            catch { }
        }

        private static void UpdateRunStamps(DateTime firstRun, DateTime today)
        {
            DateTime last = today.Date;
            try
            {
                // 【防 0001-01-01 垃圾入库】firstRun=MinValue 时（持证首跑/注册表被删但
                // EnsureStartupLicense 顶部统一调了一次），文件首格写 NONE 不写假日期，
                // 注册表 RegFirst 也跳过——这样 ReadRunStamps 不会取到假的 0001-01-01
                // 把它当"最早首跑"（修前用例"试用31天不拦"实锤：RegFirst 被顶成 0001-01-01，
                // used 恒 0，试用永不结束）。
                string firstField = (firstRun != DateTime.MinValue)
                    ? firstRun.Date.ToString("yyyy-MM-dd") : "NONE";
                string plain = firstField + "|" + last.ToString("yyyy-MM-dd");
                string enc = MesCrypto.Protect(plain);
                if (enc == null) enc = plain;   // DPAPI 失败（如测试隔离环境）就明文记，
                // 试用防线降级但不断启动闸（真现场 Windows DPAPI 一定可用）。
                // 【大扫荡】原子写（与配方/快照同病）。
                AtomicFile.WriteAllText(Path.Combine(BaseDir(), TrialFileName), enc, Encoding.UTF8);
            }
            catch { }
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RegPathActive()))
                {
                    if (k != null)
                    {
                        if (firstRun != DateTime.MinValue)   // 未登记首跑不写假日期
                        {
                            try
                            {
                                object old = k.GetValue(RegFirst);
                                DateTime of;
                                if (old == null || !DateTime.TryParse(old.ToString(), out of) || firstRun.Date < of.Date)
                                    k.SetValue(RegFirst, firstRun.Date.ToString("yyyy-MM-dd"));
                            }
                            catch { try { k.SetValue(RegFirst, firstRun.Date.ToString("yyyy-MM-dd")); } catch { } }
                        }
                        try
                        {
                            object ol = k.GetValue(RegLast);
                            DateTime ll;
                            if (ol == null || !DateTime.TryParse(ol.ToString(), out ll) || last > ll.Date)
                                k.SetValue(RegLast, last.ToString("yyyy-MM-dd"));
                        }
                        catch { try { k.SetValue(RegLast, last.ToString("yyyy-MM-dd")); } catch { } }
                    }
                }
            }
            catch { }
        }
    }
}