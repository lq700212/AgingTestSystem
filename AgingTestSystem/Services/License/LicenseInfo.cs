using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AgingTestSystem.Services.License
{
    /// <summary>
    /// 授权载荷（【V1.83 新增】软件授权：一机一证，离线 RSA 签名）。
    ///
    /// 【为什么是这个字段集】
    /// 客户的打击场景是"拷文件夹到新工控机、改改工艺配置就跑新项目"，
    /// 所以授权必须同时绑住两样东西：
    /// - machine：机器指纹（见 <see cref="MachineFingerprint"/>），拷到新机即失效；
    /// - project：项目名（空=通用版，不限项目；非空=限定版，只能跑这一个项目，
    ///   新开项目名即拦截，逼着新项目回头联系商务）。
    /// 其它字段都是生意：
    /// - expiry：到期日（yyyy-MM-dd，按年收费的抓手；过去后 7 天宽限不断线）；
    /// - maxStations：最大工位数（72/36/16 按档卖，超配即拦）；
    /// - features：功能开关（MES/高级规则，超范围只警告不阻断，见 LicenseManager）；
    /// - serial/issued：证书编号与签发日期（客服对账用，验签不依赖它）。
    ///
    /// 【防篡改原理】签发端用私钥对 <see cref="CanonicalString"/> 签名，
    /// 产品端用嵌在代码里的公钥验签（见 <see cref="LicenseManager"/>）。
    /// 手改文件任何一个字符，签名即对不上，直接判非法——所以 JSON 解析用
    /// JObject 宽容读（缺字段给缺省），但签名校验是严格的（少一个键也算改过）。
    /// </summary>
    public class LicenseInfo
    {
        /// <summary>载荷版本号（签发工具与产品端对不上即拒收，防旧证新用）。</summary>
        public int v = 1;

        /// <summary>绑定的机器指纹（MachineFingerprint.Compute 的 64 位 hex，见该类）。</summary>
        public string machine = "";

        /// <summary>绑定的项目名（空=通用版，跑哪个项目都行；非空=只能跑这个项目）。</summary>
        public string project = "";

        /// <summary>到期日（含当天有效），格式 yyyy-MM-dd，如 2027-09-01。</summary>
        public string expiry = "";

        /// <summary>最大工位数（如 72；DeviceConfig.TotalBarometers 超过它即阻断启动）。</summary>
        public int maxStations = 72;

        /// <summary>是否含 MES 上报功能（超范围只警告，见 LicenseManager，现场不断线）。</summary>
        public bool featuresMes;

        /// <summary>是否含高级规则功能（自定义报警/完成表达式/跳过抽真空；超范围只警告）。</summary>
        public bool featuresRules;

        /// <summary>证书编号（签发端自增/日期编号，客服对账用，不参与验签逻辑）。</summary>
        public string serial = "";

        /// <summary>签发日期（yyyy-MM-dd，客服对账用，不参与验签逻辑）。</summary>
        public string issued = "";

        /// <summary>
        /// 签名用的规范串（【唯一】签发端与产品端必须逐字节一致）。
        ///
        /// 【为什么不用 JSON 序列化结果直接签名】
        /// 不同 JSON 库/版本的键序、空格、转义写法可能不同，"同义不同字"的串
        /// 会导致签名对不上。用这里手拼的固定顺序 + 小写键名 + 原值直拼，
        /// 两边按同一份代码生成（签发工具拷了这份文件的逻辑，见 tools/LicenseKeyGen），
        /// 逐字节一致才算合法。新增字段必须同时改两边并升级 v，否则按篡改拒收。
        /// </summary>
        public string CanonicalString()
        {
            var sb = new StringBuilder(256);
            sb.Append("v=").Append(v).Append('&');
            sb.Append("machine=").Append((machine ?? "").Trim()).Append('&');
            sb.Append("project=").Append((project ?? "").Trim()).Append('&');
            sb.Append("expiry=").Append((expiry ?? "").Trim()).Append('&');
            sb.Append("maxStations=").Append(maxStations).Append('&');
            sb.Append("featuresMes=").Append(featuresMes ? "1" : "0").Append('&');
            sb.Append("featuresRules=").Append(featuresRules ? "1" : "0").Append('&');
            sb.Append("serial=").Append((serial ?? "").Trim()).Append('&');
            sb.Append("issued=").Append((issued ?? "").Trim());
            return sb.ToString();
        }

        /// <summary>
        /// 从 License.lic 文件内容解析（payload + signature 双段）。
        /// 文件格式：{"payload":{...},"signature":"Base64"}。
        /// 解析失败返回 null（调用方按"无有效授权"走试用/阻断分支，绝不抛异常——
        /// 授权坏了不能拖垮启动，最多是进试用或弹框）。
        /// </summary>
        public static bool TryParseFile(string fileText, out LicenseInfo info, out string signatureB64)
        {
            info = null;
            signatureB64 = null;
            try
            {
                if (string.IsNullOrWhiteSpace(fileText)) return false;
                var root = JObject.Parse(fileText);
                var payload = root["payload"] as JObject;
                var sig = (string)root["signature"];
                if (payload == null || string.IsNullOrWhiteSpace(sig)) return false;
                info = new LicenseInfo
                {
                    v = (int?)payload["v"] ?? 1,
                    machine = (string)payload["machine"] ?? "",
                    project = (string)payload["project"] ?? "",
                    expiry = (string)payload["expiry"] ?? "",
                    maxStations = (int?)payload["maxStations"] ?? 0,
                    featuresMes = (bool?)payload["featuresMes"] ?? false,
                    featuresRules = (bool?)payload["featuresRules"] ?? false,
                    serial = (string)payload["serial"] ?? "",
                    issued = (string)payload["issued"] ?? "",
                };
                signatureB64 = sig.Trim();
                return true;
            }
            catch
            {
                info = null;
                signatureB64 = null;
                return false;
            }
        }

        /// <summary>到期日解析（yyyy-MM-dd；解析失败返回 DateTime.MinValue，调用方按"非法证"处理）。</summary>
        public DateTime ExpiryDate()
        {
            try
            {
                DateTime d;
                if (DateTime.TryParseExact((expiry ?? "").Trim(), "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out d))
                {
                    return d.Date;
                }
            }
            catch { }
            return DateTime.MinValue;
        }
    }
}
