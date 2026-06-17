# -*- coding: utf-8 -*-
"""用 Pillow 直接渲染封面 PNG（绕开 cairo 依赖）。
设计来自 cover.svg，此处用 Pillow 矢量原语复刻。"""
import math
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = r'D:\Program Files (x86)\Steam\steamapps\common\The Scroll Of Taiwu'
OUT = os.path.join(ROOT, 'Mod', 'EncyclopediaExporter', 'Cover.png')

W = H = 512
img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
draw = ImageDraw.Draw(img)


def lerp(a, b, t):
    return a + (b - a) * t


def vgrad(w, h, c1, c2, vertical=True):
    """生成垂直/水平线性渐变图。"""
    grad = Image.new('RGB', (w, h))
    px = grad.load()
    for i in range(h if vertical else w):
        t = i / max(1, (h if vertical else w) - 1)
        r = int(lerp(c1[0], c2[0], t))
        g = int(lerp(c1[1], c2[1], t))
        b = int(lerp(c1[2], c2[2], t))
        if vertical:
            for x in range(w):
                px[x, i] = (r, g, b)
        else:
            for y in range(h):
                px[i, y] = (r, g, b)
    return grad


def diag_gradient(w, h, stops):
    """对角线性渐变（stops: [(pos, (r,g,b)), ...]）。"""
    grad = Image.new('RGB', (w, h))
    px = grad.load()
    maxd = w + h
    for y in range(h):
        for x in range(w):
            t = (x + y) / maxd
            # 找插值段
            c = stops[-1][1]
            for i in range(len(stops) - 1):
                p0, c0 = stops[i]
                p1, c1 = stops[i + 1]
                if p0 <= t <= p1:
                    lt = (t - p0) / max(1e-6, p1 - p0)
                    c = (int(lerp(c0[0], c1[0], lt)),
                         int(lerp(c0[1], c1[1], lt)),
                         int(lerp(c0[2], c1[2], lt)))
                    break
            px[x, y] = c
    return grad


# ---- 字体加载 ----
def load_font(size, bold=False):
    candidates = [
        r'C:\Windows\Fonts\simkai.ttf',       # 楷体
        r'C:\Windows\Fonts\STKAITI.TTF',
        r'C:\Windows\Fonts\msyh.ttc',          # 微软雅黑
        r'C:\Windows\Fonts\msyhbd.ttc',
        r'C:\Windows\Fonts\simsun.ttc',        # 宋体
    ]
    if bold:
        candidates = [r'C:\Windows\Fonts\msyhbd.ttc'] + candidates
    for p in candidates:
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, size)
            except Exception:
                pass
    return ImageFont.load_default()


# ============ 1. 背景 ============
bg = vgrad(W, H, (43, 29, 20), (15, 9, 5))
img.paste(bg, (0, 0))
# 背景晕染圆（橘色光晕）
glow = Image.new('RGBA', (W, H), (0, 0, 0, 0))
gd = ImageDraw.Draw(glow)
gd.ellipse([76, 20, 436, 380], fill=(255, 140, 26, 16))
gd.ellipse([136, 80, 376, 320], fill=(255, 179, 71, 12))
glow = glow.filter(ImageFilter.GaussianBlur(40))
img = Image.alpha_composite(img, glow)
draw = ImageDraw.Draw(img)

# ============ 2. 百晓册（古书） ============
# 整体微微倾斜 -6 度，居中偏上
book_layer = Image.new('RGBA', (420, 360), (0, 0, 0, 0))
bd = ImageDraw.Draw(book_layer)
cx, cy = 210, 175  # 书册中心（在 book_layer 坐标系）

# 书脊厚度
bd.polygon([(cx-150, cy+110), (cx+150, cy+110), (cx+140, cy+128), (cx-140, cy+128)],
           fill=(107, 52, 16))

# 左书页（米色渐变）
left_page = diag_gradient(145, 220, [(0, (245, 236, 215)), (1, (232, 217, 184))])
book_layer.paste(left_page, (cx-150, cy-108))
bd.rectangle([cx-150, cy-108, cx-8, cy+110], outline=(196, 166, 115), width=2)

# 右书页
right_page = diag_gradient(145, 220, [(0, (245, 236, 215)), (1, (232, 217, 184))])
book_layer.paste(right_page, (cx+8, cy-108))
bd.rectangle([cx+8, cy-108, cx+150, cy+110], outline=(196, 166, 115), width=2)

# 中缝
bd.rectangle([cx-8, cy-108, cx+8, cy+110], fill=(138, 106, 58))
cover = Image.new('RGBA', (16, 218), (138, 106, 58, 80))
book_layer.alpha_composite(cover, (cx-8, cy-108))

# 书页装饰横线（古书排版感）
line_color = (184, 153, 104, 140)
def hlines(bd, x1, y_start, x2, ys, color):
    for y in ys:
        end_x = x2 if isinstance(x2, int) else x2
        bd.line([(x1, y), (end_x, y)], fill=color, width=1)

hlines(bd, cx-135, cy-108, cx-25, [cy-70, cy-50, cy+20, cy+40, cy+60], line_color)
bd.line([(cx-135, cy-30), (cx-60, cy-32)], fill=line_color, width=1)
bd.line([(cx-135, cy+80), (cx-80, cy+78)], fill=line_color, width=1)
hlines(bd, cx+25, cy-108, cx+135, [cy-74, cy-54, cy+16, cy+36, cy+56], line_color)

# 左书页竖排古风短句（增强古书质感，淡墨色）
font_vert = load_font(15)
vert_text = '知天下事'
vert_color = (120, 95, 60, 200)
for i, ch in enumerate(vert_text):
    bd.text((cx-115, cy-10 + i * 20), ch, font=font_vert, fill=vert_color, anchor='mm')

# 书册封皮（橘色渐变矩形，非线条/文字部分）
orange = diag_gradient(125, 82, [
    (0, (255, 179, 71)), (0.5, (255, 140, 26)), (1, (232, 93, 4))])
book_layer.paste(orange, (cx+20, cy-100))
bd.rectangle([cx+20, cy-100, cx+145, cy-18], outline=(168, 67, 10), width=2)
bd.rectangle([cx+26, cy-94, cx+139, cy-24], outline=(122, 46, 5), width=1)

# "百"字（双色：深褐主色 + 描边效果）
font_bai = load_font(52, bold=True)
# 描边（金黄）
for dx in range(-1, 2):
    for dy in range(-1, 2):
        if dx == 0 and dy == 0:
            continue
        bd.text((cx+82 + dx, cy-70 + dy), '百', font=font_bai,
                fill=(255, 215, 0), anchor='mm')
# 主色（深褐）
bd.text((cx+82, cy-70), '百', font=font_bai, fill=(58, 26, 5), anchor='mm')

# 封皮"百"字下方加"曉冊"二字（繁体，呼应百晓册全称）
font_xiao = load_font(20, bold=True)
for dx in range(-1, 2):
    for dy in range(-1, 2):
        if dx == 0 and dy == 0:
            continue
        bd.text((cx+82 + dx, cy-30 + dy), '曉冊', font=font_xiao,
                fill=(255, 215, 0), anchor='mm')
bd.text((cx+82, cy-30), '曉冊', font=font_xiao, fill=(58, 26, 5), anchor='mm')

# 左书页角落小印（朱红方框 + "册"字）
bd.rectangle([cx-140, cy+88, cx-118, cy+104], outline=(168, 50, 50), width=2)
font_yin = load_font(11)
bd.text((cx-129, cy+96), '册', font=font_yin, fill=(168, 50, 50), anchor='mm')

# 书册投影
shadow = Image.new('RGBA', book_layer.size, (0, 0, 0, 0))
sshadow = book_layer.copy()
# 提取 alpha 做阴影
alpha = book_layer.split()[3]
shadow_fill = Image.new('RGBA', book_layer.size, (0, 0, 0, 110))
shadow_fill.putalpha(alpha.filter(ImageFilter.GaussianBlur(6)))
offset_shadow = Image.new('RGBA', book_layer.size, (0, 0, 0, 0))
offset_shadow.paste(shadow_fill, (0, 6), shadow_fill)

# 旋转并合成到主图
book_rot = book_layer.rotate(-6, resample=Image.BICUBIC, expand=False)
shadow_rot = offset_shadow.rotate(-6, resample=Image.BICUBIC, expand=False)
img.alpha_composite(shadow_rot, (46 + 6, 50))   # 投影稍偏下
img.alpha_composite(book_rot, (46, 50))

# ============ 3. 导出图标（右下角叠层） ============
icon_layer = Image.new('RGBA', (160, 160), (0, 0, 0, 0))
ic = ImageDraw.Draw(icon_layer)
ic_cx, ic_cy = 80, 80
# 圆形底（蓝色渐变）
blue = vgrad(116, 116, (58, 123, 213), (30, 77, 140)).convert('RGBA')
mask = Image.new('L', (116, 116), 0)
ImageDraw.Draw(mask).ellipse([0, 0, 115, 115], fill=255)
# 用 alpha_composite 方式贴圆形渐变
blue_round = Image.new('RGBA', (116, 116), (0, 0, 0, 0))
blue_round.paste(blue, (0, 0))
blue_round.putalpha(mask)
icon_layer.alpha_composite(blue_round, (ic_cx-58, ic_cy-58))
ic.ellipse([ic_cx-58, ic_cy-58, ic_cx+58, ic_cy+58],
           outline=(255, 255, 255), width=3)

# 导出符号：文档 + 向下箭头
doc_x, doc_y = ic_cx, ic_cy - 8
# 文档主体
ic.polygon([(doc_x-20, doc_y-22), (doc_x+8, doc_y-22),
            (doc_x+22, doc_y-8), (doc_x+22, doc_y+18),
            (doc_x-20, doc_y+18)],
           fill=(255, 255, 255), outline=(30, 77, 140), width=2)
# 折角
ic.polygon([(doc_x+8, doc_y-22), (doc_x+8, doc_y-8), (doc_x+22, doc_y-8)],
           fill=(205, 221, 242), outline=(30, 77, 140), width=1)
# 文档内横线
for dy in [-2, 6, 14]:
    x2 = doc_x + 15 if dy < 14 else doc_x + 5
    ic.line([(doc_x-13, doc_y+dy), (x2, doc_y+dy)], fill=(91, 135, 196), width=2)
# 向下箭头（导出动作）
ar_y = doc_y + 30
ic.rectangle([doc_x-4, ar_y-8, doc_x+4, ar_y+2], fill=(255, 255, 255))
ic.polygon([(doc_x-12, ar_y-2), (doc_x, ar_y+12), (doc_x+12, ar_y-2)],
           fill=(255, 255, 255))

# 导出图标投影 + 合成（右下角，部分叠在书册上）
icon_shadow = Image.new('RGBA', icon_layer.size, (0, 0, 0, 0))
icon_alpha = icon_layer.split()[3]
ishadow_fill = Image.new('RGBA', icon_layer.size, (0, 0, 0, 130))
ishadow_fill.putalpha(icon_alpha.filter(ImageFilter.GaussianBlur(4)))
ioffset = Image.new('RGBA', icon_layer.size, (0, 0, 0, 0))
ioffset.paste(ishadow_fill, (0, 4), ishadow_fill)
img.alpha_composite(ioffset, (385-80+4, 380-80+4))
img.alpha_composite(icon_layer, (385-80, 380-80))

# ============ 4. 标题文字 ============
draw = ImageDraw.Draw(img)

# 顶部副标题：太吾繪卷 · 百曉冊（繁体，淡金色小字）
font_sub = load_font(17)
subtitle = '太吾繪卷  ·  百曉冊'
sub_w = draw.textlength(subtitle, font=font_sub)
draw.text(((W - sub_w) / 2, 32), subtitle, font=font_sub, fill=(200, 160, 100))
# 副标题两侧装饰短线
draw.line([(W/2 - sub_w/2 - 50, 42), (W/2 - sub_w/2 - 16, 42)], fill=(140, 100, 60), width=1)
draw.line([(W/2 + sub_w/2 + 16, 42), (W/2 + sub_w/2 + 50, 42)], fill=(140, 100, 60), width=1)

# 底部主标题
font_title = load_font(26, bold=True)
title = '百科导出工具'
# 字间距：逐字绘制
char_spacing = 4
chars = list(title)
char_widths = [draw.textlength(c, font=font_title) for c in chars]
total_w = sum(char_widths) + char_spacing * (len(chars) - 1)
x = (W - total_w) / 2
for ch, cw in zip(chars, char_widths):
    draw.text((x, 458), ch, font=font_title, fill=(255, 217, 160))
    x += cw + char_spacing

# 主标题下装饰线 + 英文
draw.line([(W/2 - 90, 492), (W/2 + 90, 492)], fill=(140, 100, 60), width=1)
font_en = load_font(12)
en_text = 'Encyclopedia  Exporter'
en_w = draw.textlength(en_text, font=font_en)
draw.text(((W - en_w) / 2, 496), en_text, font=font_en, fill=(160, 130, 90))

# 保存（转 RGB，PNG）
final = Image.new('RGB', (W, H), (28, 18, 9))
final.paste(img, (0, 0), img)
final.save(OUT, 'PNG')
print('已生成:', OUT)
print('大小:', os.path.getsize(OUT), 'bytes')
print('尺寸:', final.size)
