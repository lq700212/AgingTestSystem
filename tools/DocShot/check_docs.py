#!/usr/bin/env python3
# check_docs.py —— 文档收尾一键核验（跨平台唯一脚本，Windows 上直接 python 跑）。
# 单脚本原则：只维护这一份（checks: truncated/refs/pdf/html/banned/encoding/scope）。
# 用法：python tools/DocShot/check_docs.py [docs目录] [images目录]（默认 docs / docs/images）
import os
import re
import subprocess
import sys

DOCS = sys.argv[1] if len(sys.argv) > 1 else "docs"
IMGS = sys.argv[2] if len(sys.argv) > 2 else os.path.join(DOCS, "images")

fail = 0


def bad(msg):
    global fail
    fail += 1
    print("FAIL: " + msg)


def ok(msg):
    print("OK: " + msg)


# 读全部 md（UTF-8，空文件/解码失败即红灯）
texts = {}
for m in sorted(os.listdir(DOCS)):
    if not m.endswith(".md"):
        continue
    p = os.path.join(DOCS, m)
    try:
        with open(p, encoding="utf-8") as fh:
            texts[m] = fh.read()
        if not texts[m]:
            bad(m + " 空文件")
    except Exception as e:
        bad(m + " 编码异常: " + str(e))

# 1. 截断标记（分段写作没写完的铁证）
if any("truncated" in t for t in texts.values()):
    bad("截断标记残留")
else:
    ok("无截断标记")

# 2. 图片引用双向检查（引用缺文件 / 文件无人引）
pat = re.compile(r"images/[A-Za-z0-9_.-]+\.(?:png|jpg)")
refs = sorted({m.group(0) for t in texts.values() for m in pat.finditer(t)})
for r in refs:
    if not os.path.isfile(os.path.join(DOCS, r.replace("/", os.sep))):
        bad("引用缺文件: " + r)
files = []
if os.path.isdir(IMGS):
    files = sorted(f for f in os.listdir(IMGS) if f.endswith(".png"))
for f in files:
    if ("images/" + f) not in refs:
        print("WARN: 无人引用 " + f)
ok("图片引用 %d 个，文件 %d 个" % (len(refs), len(files)))

# 2b. PDF 与 md 配对（三份手册必须有同名 pdf 且 >50KB；名单按项目改 trio）
for m in ["车间操作员使用说明.md", "客户技术工艺使用说明.md", "内部培训文档.md"]:
    if m in texts:
        p = os.path.join(DOCS, os.path.splitext(m)[0] + ".pdf")
        if not (os.path.isfile(p) and os.path.getsize(p) > 50 * 1024):
            bad("缺 PDF 或过小: " + p)
ok("PDF 配对检查完")

# 2c. html 中间件残留（转完即删，docs 下不许留）
leftover = [f for f in os.listdir(DOCS)
            if f.endswith(".html") and os.path.isfile(os.path.join(DOCS, f))]
if leftover:
    bad("html 残留未清: " + ",".join(leftover))
else:
    ok("无 html 残留")

# 3. 客户版禁词（按项目改 banned：隐藏账号名、内部工具名、源码关键词）
# dev 用词边界匹配——本站字段 device 含 dev 子串，老写法会误杀字段表（V1.88 实锤）
import re as _re
banned_re = [r"\bdev\b"]
banned_sub = ["发码", "后门"]
for w in banned_re:
    hits = [m for m, t in texts.items()
            if m.startswith("客户") and _re.search(w, t, _re.IGNORECASE)]
    if hits:
        bad("客户版含禁词[%s]" % w)
for w in banned_sub:
    hits = [m for m, t in texts.items() if m.startswith("客户") and w in t]
    if hits:
        bad("客户版含禁词[%s]" % w)
ok("客户版禁词检查完")
ok("编码检查完")

# 4. 改动范围（人眼确认：只有文档＋配图＋harness，无运行时数据/机密/bin 产物）
try:
    out = subprocess.run(["git", "status", "--short"],
                         capture_output=True, text=True, timeout=30)
    print(out.stdout)
except Exception as e:
    bad("git status 失败: " + str(e))

print("ALL PASS" if fail == 0 else "FAILURES=%d" % fail)
sys.exit(1 if fail else 0)
