using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

// ============================================================================
// 授权签发工具（【V1.83 新增】只在公司电脑上用，绝不发给客户）。
//
// 【为什么是独立小工具而不是主程序里的按钮】
// 签发要私钥，验签只要公钥。私钥进产品 exe = 把印钞机发给客户，
// 混淆也白搭。所以签发逻辑物理隔离：这个小工具 + license_private.xml
// 只在公司保管，产品端（LicenseManager）只有公钥。
//
// 【编译】（.NET Framework 自带 csc，无需装任何东西）
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe Program.cs
// 说明书见下面 PrintHelp。
// ============================================================================
internal static class KeyGen
{
    private const string PrivFile = "license_private.xml";

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; }
        catch { }
        if (args.Length == 0) { PrintHelp(); return 1; }
        string cmd = args[0].Trim().ToLowerInvariant();
        try
        {
            if (cmd == "genkeys") return GenKeys();
            if (cmd == "issue") return Issue(args);
            if (cmd == "verify") return Verify(args);
            PrintHelp();
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine("失败：" + ex.Message);
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("授权签发工具（公司内部用，私钥不出公司）");
        Console.WriteLine("");
        Console.WriteLine("  KeyGen genkeys");
        Console.WriteLine("    生成 RSA2048 密钥对。私钥存 license_private.xml（已 gitignore，");
        Console.WriteLine("    备份到离线 U 盘）；公钥 XML 打印出来，贴进产品端");
        Console.WriteLine("    LicenseManager.cs 的 PublicKeyXml 常量后重新发版。");
        Console.WriteLine("    注意：换钥=老证全废，只在必要时换。");
        Console.WriteLine("");
        Console.WriteLine("  KeyGen issue --machine <指纹> --expiry <yyyy-MM-dd> --stations <点数>");
        Console.WriteLine("      [--project <项目名>] [--mes] [--rules] [--serial <编号>] [--out <文件>]");
        Console.WriteLine("    签发 License.lic。--project 留空=通用版（不限项目，卖贵点）；");
        Console.WriteLine("    给了项目名=限定版（只能跑这个项目，新开项目即拦）。");
        Console.WriteLine("    例：KeyGen issue --machine a1b2... --project 烧屏测试");
        Console.WriteLine("        --expiry 2027-09-01 --stations 72 --mes --serial LIC2026001");
        Console.WriteLine("");
        Console.WriteLine("  KeyGen verify --in <License.lic>");
        Console.WriteLine("    用私钥反查验一张证能不能过（签发后自检用）。");
        Console.WriteLine("");
        Console.WriteLine("  【中文项目名防乱码】用 cmd 窗口 / .bat 运行，别用 PowerShell 5.1 的 & 调用");
        Console.WriteLine("  （PS5.1 会把中文参数转成乱码，乱码参数会被本工具拒绝；CreateProcessW 路径正常）。");
    }

    // ---- genkeys ----
    private static int GenKeys()
    {
        if (File.Exists(PrivFile))
        {
            Console.Write("license_private.xml 已存在，覆盖会废掉所有已签发的证。覆盖吗？(yes/no)：");
            string ans = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
            if (ans != "yes") { Console.WriteLine("已取消。"); return 1; }
        }
        using (var rsa = new RSACryptoServiceProvider(2048))
        {
            File.WriteAllText(PrivFile, rsa.ToXmlString(true), Encoding.UTF8);
            Console.WriteLine("私钥已存：license_private.xml（备份好，离线保管）");
            Console.WriteLine("");
            Console.WriteLine("公钥（贴进 LicenseManager.cs 的 PublicKeyXml）：");
            Console.WriteLine(rsa.ToXmlString(false));
        }
        return 0;
    }

    // ---- issue ----
    private static string Opt(string[] args, string name)
    {
        for (int i = 1; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        }
        return null;
    }

    private static bool Has(string[] args, string name)
    {
        foreach (string a in args)
        {
            if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static int Issue(string[] args)
    {
        // 【中文参数防乱码】PowerShell 5.1 用 & 调用本工具时会把中文参数按 ANSI 转码，
        // 到达 exe 后变成 U+FFFD 替换符（实测"烧屏测试"变三个 ）。乱码进授权 = 客户
        // 项目名对不上，签完才发现白签。这里检出替换符并硬停，让操作人改用 cmd 窗口
        // 或 Ctrl+C 复制现成命令（CreateProcessW 的 Unicode 路径是好的）。
        foreach (string a in args)
        {
            if (a != null && a.IndexOf('\uFFFD') >= 0)
            {
                Console.WriteLine("失败：参数含乱码字符（U+FFFD），很可能是 PowerShell 5.1 传中文参数导致的。");
                Console.WriteLine("  - 请在 cmd 窗口（chcp 936）里运行本命令；");
                Console.WriteLine("  - 或把命令写进 .bat 文件再双击运行；");
                Console.WriteLine("  - 项目名可先用客户那台机器复制，Ctrl+C 粘贴到命令里。");
                return 1;
            }
        }
        string machine = (Opt(args, "--machine") ?? "").Trim().Replace("-", "").Replace(" ", "").ToLowerInvariant();
        string expiry = (Opt(args, "--expiry") ?? "").Trim();
        string stationsRaw = (Opt(args, "--stations") ?? "").Trim();
        string project = (Opt(args, "--project") ?? "").Trim();
        string serial = (Opt(args, "--serial") ?? "").Trim();
        string outFile = (Opt(args, "--out") ?? "").Trim();
        if (outFile.Length == 0) outFile = "License.lic";

        if (machine.Length != 64 || !IsHex(machine))
        { Console.WriteLine("失败：--machine 必须是 64 位十六进制指纹（客户授权窗里复制/导出）。"); return 1; }
        DateTime exp;
        if (!DateTime.TryParseExact(expiry, "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out exp))
        { Console.WriteLine("失败：--expiry 格式应为 yyyy-MM-dd。"); return 1; }
        int stations;
        if (!int.TryParse(stationsRaw, out stations) || stations <= 0)
        { Console.WriteLine("失败：--stations 应为正整数（如 72）。"); return 1; }
        if (!File.Exists(PrivFile)) { Console.WriteLine("失败：找不到 license_private.xml（先 genkeys，或把备份拷回来）。"); return 1; }

        // 规范串必须与产品端 LicenseInfo.CanonicalString 逐字节一致（v=1 固定）。
        string issued = DateTime.Now.ToString("yyyy-MM-dd");
        string canonical = "v=1&machine=" + machine + "&project=" + project
            + "&expiry=" + expiry + "&maxStations=" + stations
            + "&featuresMes=" + (Has(args, "--mes") ? "1" : "0")
            + "&featuresRules=" + (Has(args, "--rules") ? "1" : "0")
            + "&serial=" + serial + "&issued=" + issued;

        string priv = File.ReadAllText(PrivFile, Encoding.UTF8);
        string sig;
        using (var rsa = new RSACryptoServiceProvider())
        {
            rsa.FromXmlString(priv);
            byte[] s = rsa.SignData(Encoding.UTF8.GetBytes(canonical), "SHA256");
            sig = Convert.ToBase64String(s);
        }

        // 手拼 JSON（字段名与产品端 JObject.FromObject(LicenseInfo) 一致：
        // 小驼峰 featuresMes/featuresRules，其余小写；payload 键序不影响验签，
        // 签名只认 canonical 串）。
        string json = "{\r\n"
            + "  \"payload\": {\r\n"
            + "    \"v\": 1,\r\n"
            + "    \"machine\": \"" + Esc(machine) + "\",\r\n"
            + "    \"project\": \"" + Esc(project) + "\",\r\n"
            + "    \"expiry\": \"" + Esc(expiry) + "\",\r\n"
            + "    \"maxStations\": " + stations + ",\r\n"
            + "    \"featuresMes\": " + (Has(args, "--mes") ? "true" : "false") + ",\r\n"
            + "    \"featuresRules\": " + (Has(args, "--rules") ? "true" : "false") + ",\r\n"
            + "    \"serial\": \"" + Esc(serial) + "\",\r\n"
            + "    \"issued\": \"" + Esc(issued) + "\"\r\n"
            + "  },\r\n"
            + "  \"signature\": \"" + sig + "\"\r\n"
            + "}";
        File.WriteAllText(outFile, json, Encoding.UTF8);
        Console.WriteLine("已签发：" + Path.GetFullPath(outFile));
        Console.WriteLine("项目：" + (project.Length == 0 ? "通用版" : project)
            + "  点数：" + stations + "  到期：" + expiry);
        return 0;
    }

    private static int Verify(string[] args)
    {
        string f = Opt(args, "--in");
        if (string.IsNullOrEmpty(f) || !File.Exists(f)) { Console.WriteLine("失败：--in 文件不存在。"); return 1; }
        // 自检=用公钥验一遍（逻辑与产品端一致，防签发端手拼 JSON 与产品端字段名分叉）。
        string text = File.ReadAllText(f, Encoding.UTF8);
        string machine = Grab(text, "\"machine\"");
        string project = Grab(text, "\"project\"");
        string expiry = Grab(text, "\"expiry\"");
        string stations = Grab(text, "\"maxStations\"");
        string fmes = Grab(text, "\"featuresMes\"");
        string frules = Grab(text, "\"featuresRules\"");
        string serial = Grab(text, "\"serial\"");
        string issued = Grab(text, "\"issued\"");
        string sig = Grab(text, "\"signature\"");
        string canonical = "v=1&machine=" + machine + "&project=" + project
            + "&expiry=" + expiry + "&maxStations=" + stations
            + "&featuresMes=" + (fmes.Contains("true") ? "1" : "0")
            + "&featuresRules=" + (frules.Contains("true") ? "1" : "0")
            + "&serial=" + serial + "&issued=" + issued;
        if (!File.Exists(PrivFile)) { Console.WriteLine("失败：找不到 license_private.xml。"); return 1; }
        string priv = File.ReadAllText(PrivFile, Encoding.UTF8);
        using (var rsa = new RSACryptoServiceProvider())
        {
            rsa.FromXmlString(priv);
            string pub = rsa.ToXmlString(false);
            using (var v = new RSACryptoServiceProvider())
            {
                v.FromXmlString(pub);
                bool ok = v.VerifyData(Encoding.UTF8.GetBytes(canonical), "SHA256",
                    Convert.FromBase64String(sig));
                Console.WriteLine(ok ? "签名有效。" : "签名无效（签发端与产品端规范串分叉了，停发查代码）！");
                return ok ? 0 : 1;
            }
        }
    }

    private static string Grab(string json, string key)
    {
        try
        {
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return "";
            int c = json.IndexOf(':', i);
            if (c < 0) return "";
            int q1 = json.IndexOf('"', c);
            int comma = json.IndexOf(',', c);
            int brace = json.IndexOf('}', c);
            int end = comma > 0 ? comma : brace;
            if (q1 >= 0 && q1 < end)
            {
                int q2 = json.IndexOf('"', q1 + 1);
                return json.Substring(q1 + 1, q2 - q1 - 1);
            }
            return json.Substring(c + 1, end - c - 1).Trim();
        }
        catch { return ""; }
    }

    private static string Esc(string s)
    {
        // 【V1.83.1】控制字符也转义：项目名里若有换行/制表符，手拼 JSON 会断行，
        // 产品端 JObject.Parse 直接失败整张证报废（此前只转了斜杠和引号）。
        return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    }

    private static bool IsHex(string s)
    {
        foreach (char c in s)
        {
            bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!ok) return false;
        }
        return true;
    }
}
