#!/usr/bin/env python3
# md_to_pdf.py —— markdown（含本地图片）转 PDF，全链路单脚本。
# 链条：pip install markdown → md 转样式 HTML → 系统 Edge 无头打印 PDF。
# 不用 pandoc / weasyprint（Windows 装机成本高）/ Word COM（不可靠）。
# 用法：python tools/DocShot/md_to_pdf.py [docs目录] [输出目录] [md文件...] [--keep-html]
#   缺省转三份手册；输出与 md 同名 .pdf；.html 只是中间件，
#   预览核对完即删（成功后脚本自删，--keep-html 调试保留）。
import os
import re
import subprocess
import sys
import urllib.parse

try:
    import markdown
except ImportError:
    print("FAIL: 缺 markdown 库，先跑 pip install markdown")
    sys.exit(2)

DOCS = sys.argv[1] if len(sys.argv) > 1 else "docs"
OUT = sys.argv[2] if len(sys.argv) > 2 else DOCS
NAMES = [a for a in sys.argv[3:] if not a.startswith("--")] or [
    "车间操作员使用说明.md", "客户技术工艺使用说明.md", "内部培训文档.md"]
KEEP_HTML = "--keep-html" in sys.argv

EDGES = [
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
]

CSS = """
body{font-family:"Microsoft YaHei","PingFang SC","SimSun",sans-serif;
  line-height:1.7;color:#222;max-width:1000px;margin:0 auto;padding:24px;}
h1{border-bottom:2px solid #2b7cd3;padding-bottom:8px;}
h2{border-bottom:1px solid #ccc;padding-bottom:6px;margin-top:1.8em;}
table{border-collapse:collapse;width:100%;margin:12px 0;}
th,td{border:1px solid #999;padding:6px 10px;text-align:left;}
th{background:#e8f1fc;}
img{max-width:100%;height:auto;border:1px solid #ddd;margin:8px 0;}
code{background:#f4f4f4;padding:1px 5px;border-radius:3px;}
pre{background:#f4f4f4;padding:12px;overflow-x:auto;}
pre code{background:none;padding:0;}
blockquote{border-left:4px solid #2b7cd3;margin:12px 0;padding:6px 12px;
  background:#f6f9fe;color:#444;}
*{-webkit-print-color-adjust:exact;print-color-adjust:exact;}
@page{size:A4;margin:15mm;}
"""


def find_edge():
    for e in EDGES:
        if os.path.isfile(e):
            return e
    return None


def md_to_html(md_path):
    with open(md_path, encoding="utf-8") as fh:
        text = fh.read()
    base = os.path.dirname(os.path.abspath(md_path))

    def fix_img(m):
        alt, src = m.group(1), m.group(2)
        if re.match(r"(?i)^(https?:|data:|file:)", src):
            return m.group(0)
        full = os.path.abspath(os.path.join(base, src.replace("/", os.sep)))
        url = "file:///" + urllib.parse.quote(full.replace(os.sep, "/"))
        return "![%s](%s)" % (alt, url)

    text = re.sub(r"!\[([^\]]*)\]\(([^)]+)\)", fix_img, text)
    body = markdown.markdown(text, extensions=["tables", "fenced_code", "toc"])
    m = re.search(r"^#\s+(.+)$", text, re.M)
    title = m.group(1).strip() if m else os.path.basename(md_path)
    return ("<!DOCTYPE html><html><head><meta charset=\"utf-8\">"
            "<title>%s</title><style>%s</style></head>"
            "<body>%s</body></html>") % (title, CSS, body)


def main():
    edge = find_edge()
    if not edge:
        print("FAIL: 没找到 Edge/Chrome")
        return 2
    os.makedirs(OUT, exist_ok=True)
    fail = 0
    for name in NAMES:
        src = os.path.join(DOCS, name)
        if not os.path.isfile(src):
            print("FAIL: 缺文件 " + src)
            fail += 1
            continue
        stem = os.path.splitext(name)[0]
        html_p = os.path.join(OUT, stem + ".html")
        pdf_p = os.path.join(OUT, stem + ".pdf")
        with open(html_p, "w", encoding="utf-8") as fh:
            fh.write(md_to_html(src))
        # Edge 的 stderr 含非 GBK 字节：按字节收再 replace 解码，否则中文机直接炸线程
        r = subprocess.run(
            [edge, "--headless", "--disable-gpu", "--no-pdf-header-footer",
             "--print-to-pdf=" + os.path.abspath(pdf_p),
             "file:///" + urllib.parse.quote(os.path.abspath(html_p).replace(os.sep, "/"))],
            capture_output=True, timeout=120)
        err = r.stderr.decode("utf-8", errors="replace")[-300:]
        if os.path.isfile(pdf_p) and os.path.getsize(pdf_p) > 50 * 1024:
            print("OK: %s (%.1f KB)" % (pdf_p, os.path.getsize(pdf_p) / 1024))
        else:
            print("FAIL: %s 生成失败 %s" % (pdf_p, err))
            fail += 1
    if fail == 0 and not KEEP_HTML:
        for name in NAMES:
            h = os.path.join(OUT, os.path.splitext(name)[0] + ".html")
            try:
                if os.path.isfile(h):
                    os.remove(h)
            except Exception as e:
                print("WARN: html 清理失败 %s %s" % (h, e))
        print("html 中间件已清理（--keep-html 可保留）")
    print("ALL PASS" if fail == 0 else "FAILURES=%d" % fail)
    return 1 if fail else 0


if __name__ == "__main__":
    sys.exit(main())
