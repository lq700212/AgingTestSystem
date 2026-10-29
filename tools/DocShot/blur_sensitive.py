#!/usr/bin/env python3
# blur_sensitive.py —— 涉密截图打码（skill"培训文档生成"§三R4 的项目侧沉淀）。
# 设备ID/设备码/激活码/机器码/外网IP/客户真实数据，三份文档一律打码（内部版也不留明文）。
#
# 用法：
#   1) 自动找输入框并打码：python blur_sensitive.py 图.png在里面框序号 [--keep 4]
#      （--auto 模式：按"超过半宽的同一色水平线段"找输入框上下边框，自动定内区）
#   2) 手工坐标：python blur_sensitive.py 图.png x0,y0,x1,y1 [x0,y0,x1,y1 ...] [--keep 4]
# 参数：
#   --block N   马赛克块边长（默认 12）
#   --keep N    保留首尾各 N 段露字（默认 4；0=整区糊——短字段/空框/分字失败时用）
#   --zoom out  同时输出 <stem>-zoom4.png（打码区 4 倍放大，人眼复核用，看完即删）
# 规则（照抄 R4）：
#   - 默认首尾各露 4 段、只糊中间（如设备ID"178B…0F01"）；框体保留（约 2px 边框不覆盖），
#     截图仍是真图，不是 P 图伪造。
#   - 分字：内区逐列统计"非底色像素"，空列切分段，切分点落字间隙中点；
#     段数不足（<2N+1）或分不清字间隙时自动拒绝保留，转 --keep 0 整区糊（不硬露）。
#   - 空区（没有文字笔画）拒绝打码并报错（坐标系写错时第一时间发现，不糊个寂寞）。
#   - 勿对已打码图重跑（马赛克区再跑会二次马赛克，视觉一致但浪费；脚本不做非空检查拦截）。
#   - 打码坐标打印到 stdout（贴进说明.txt 或任务记录，同窗体重拍直接复用）。
#   - PDF 同步：png 打码后三份 PDF 必须重转（PDF 内嵌的是旧图）。
import sys
import os
from collections import Counter

try:
    from PIL import Image
except ImportError:
    print("需要 Pillow：pip install pillow")
    sys.exit(2)


def is_blueish(c):
    r, g, b = c[0], c[1], c[2]
    return b > 130 and b - r > 40 and b - g > 30  # Sunny 输入框聚焦蓝框 / 标题蓝


def find_boxes(img):
    """自动找输入框：水平长蓝线段（>=60% 宽）成对出现即上/下边框；左右边框再从框内找。"""
    w, h = img.size
    px = img.load()
    rows = []
    for y in range(h):
        cnt = sum(1 for x in range(w) if is_blueish(px[x, y]))
        if cnt >= w * 0.6:
            rows.append(y)
    # 长蓝线分组（相邻行合并——边框往往 1~2px）
    lines = []
    for y in rows:
        if lines and y - lines[-1][-1] <= 2:
            lines[-1].append(y)
        else:
            lines.append([y])
    if len(lines) < 2:
        return []
    # 相邻“上边框/下边框”配对：上边框与最近的下一条线组成一个框
    boxes = []
    i = 0
    while i + 1 < len(lines):
        top = lines[i][-1]
        bot = lines[i + 1][0]
        if bot - top > 20:  # 输入框至少 20px 高
            # 左右边框：在 (top+2..bot-2) 中间行上找蓝像素的 x 范围
            mid = (top + bot) // 2
            xs = [x for x in range(w) if is_blueish(px[x, mid])]
            if xs:
                boxes.append((min(xs), top, max(xs), bot))
            i += 2
        else:
            i += 1
    return boxes


def inset(r, n=3):
    """外框缩 n 像素取内区（不覆盖边框线）。"""
    x0, y0, x1, y1 = r
    return (x0 + n, y0 + n, x1 - n, y1 - n)


def segment_columns(img, box):
    """内区逐列统计非底色像素数 → 文字段 [列区间]。底色=内区众数色。"""
    x0, y0, x1, y1 = box
    px = img.load()
    cols = Counter()
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            cols[px[x, y]] += 1
    if not cols:
        return []
    base = cols.most_common(1)[0][0]

    def is_text(c):
        return abs(c[0] - base[0]) + abs(c[1] - base[1]) + abs(c[2] - base[2]) > 90

    counts = []
    for x in range(x0, x1 + 1):
        n = sum(1 for y in range(y0, y1 + 1) if is_text(px[x, y]))
        counts.append(n)
    total = sum(counts)
    if total < 30:  # 内区几乎空白（空输入框打码无意义）
        return None
    segs = []
    start = None
    for i, n in enumerate(counts):
        if n > 0 and start is None:
            start = i
        elif n == 0 and start is not None:
            segs.append((start, i))
            start = None
    if start is not None:
        segs.append((start, len(counts)))
    # 合并被 1~2px 空隙拆碎的段（字内笔画间隙）
    merged = []
    for s in segs:
        if merged and s[0] - merged[-1][1] <= 2:
            merged[-1] = (merged[-1][0], s[1])
        else:
            merged.append(list(s[:2]) if isinstance(s, tuple) else s)
    merged = [tuple(s) if isinstance(s, list) else s for s in merged]
    out = []
    for s in merged:
        s = (s[0] + x0, s[1] + x0)  # 回到全图坐标
        if s[1] - s[0] >= 6:  # 跳过灰尘段
            out.append(s)
    return out


def mosaic(img, box, block):
    """对 box 区域做马赛克（自身像素平均色块化），边缘保留框线。"""
    x0, y0, x1, y1 = box
    if x1 <= x0 or y1 <= y0:
        return
    px = img.load()
    for by in range(y0, y1, block):
        for bx in range(x0, x1, block):
            r = g = b = n = 0
            for y in range(by, min(by + block, y1 + 1)):
                for x in range(bx, min(bx + block, x1 + 1)):
                    c = px[x, y]
                    r += c[0]; g += c[1]; b += c[2]; n += 1
            avg = (r // n, g // n, b // n)
            for y in range(by, min(by + block, y1 + 1)):
                for x in range(bx, min(bx + block, x1 + 1)):
                    px[x, y] = avg


def cover_region(img, box, block):
    """无段坐标时的兜底：整区糊（--keep 0 与分字失败共用）。"""
    mosaic(img, box, block)


def apply(img, outer, keep, block):
    """按框打码：分字成功且段数够 → 露首尾 keep 段、糊中间；否则整区糊。"""
    inner = inset(outer)
    segs = segment_columns(img, inner)
    if segs is None:
        print("  SKIP 空框(无文字) 不打码: %s" % (outer,))
        return False
    need = 2 * keep + 1 if keep > 0 else 1
    if keep > 0 and len(segs) >= need and (segs[-1][1] - segs[0][0]) > 40:
        left = segs[:keep]
        right = segs[-keep:]
        gap0 = left[-1][1]  # 左侧末段结束
        gap1 = right[0][0]  # 右侧首段开始
        if gap1 - gap0 > 20:  # 中间真有可糊的内容
            mosaic(img, (gap0, inner[1], gap1, inner[3]), block)
            return True
    cover_region(img, inner, block)
    return True


def main():
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        sys.exit(2)
    path = args[0]
    rest = args[1:]
    img = Image.open(path).convert("RGB")
    w, h = img.size

    block = 12
    keep = 4
    zoom = False
    auto = False
    coords = []
    i = 0
    while i < len(rest):
        a = rest[i]
        if a == "--block":
            block = int(rest[i + 1]); i += 2
        elif a == "--keep":
            keep = int(rest[i + 1]); i += 2
        elif a in ("--zoom", "--zoom4"):
            zoom = True; i += 1
        elif a == "--auto":
            auto = True; i += 1
        else:
            if "," in a:
                xs = [int(v) for v in a.split(",")]
                if len(xs) != 4:
                    print("坐标格式应为 x0,y0,x1,y1：%s" % a)
                    sys.exit(2)
                coords.append(tuple(xs))
            else:
                try:
                    coords.append(int(a))  # --auto 模式下的框序号
                except ValueError:
                    print("未知参数：%s" % a); sys.exit(2)
            i += 1

    zoomed_boxes = []
    if auto:
        boxes = find_boxes(img)
        if not boxes:
            print("FAIL 未找到蓝色输入框，改手工坐标")
            sys.exit(1)
        if not coords:
            thesel = list(range(0, max(len(boxes) - 1, 1)))  # 缺省打码除最后一个（激活码空框）
        else:
            thesel = [int(c) for c in coords if isinstance(c, int)]
        for idx in thesel:
            if 0 <= idx < len(boxes):
                r = boxes[idx]
                print("框[%d] = %s" % (idx, r))
                if apply(img, r, keep, block):
                    zoomed_boxes.append(r)
    else:
        for r in coords:
            if not isinstance(r, tuple):
                continue
            if r[0] < 0 or r[2] > w or r[3] > h:
                print("FAIL 坐标越界 %s（图 %dx%d）" % (r, w, h))
                sys.exit(1)
            if apply(img, r, keep, block):
                zoomed_boxes.append(r)

    if not zoomed_boxes:
        print("FAIL 没有任何区域被打码（坐标/序号核对后重试）")
        sys.exit(1)
    stem, ext = os.path.splitext(path)
    out = path if stem.endswith("-masked") else stem + "-masked" + ext
    img.save(out)
    print("OK 已输出 %s（尺寸 %dx%d，打码 %d 处，keep=%d）" % (out, w, h, len(zoomed_boxes), keep))
    if zoom:
        z = ",".join("%dx%d" % ((b[2] - b[0]) * 4, (b[3] - b[1]) * 4) for b in zoomed_boxes)
        print("   复核：把 %s 里下方裁 4 倍放大人眼看（块 %d）" % (out, block))
    print("   坐标记录（同窗体重拍直接复用）：%s" % (zoomed_boxes,))


if __name__ == "__main__":
    main()
