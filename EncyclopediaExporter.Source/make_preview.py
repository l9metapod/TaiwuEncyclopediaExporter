# -*- coding: utf-8 -*-
"""生成 mod 详情图集（创意工坊/论坛展示用）。
风格统一：深褐背景 + 橘金色调，与封面一致。

分辨率：SCALE=2，逻辑画布 512x384，实际输出 1024x768。
所有字号/坐标/间距/圆角/线宽按 SCALE 等比放大。"""
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = r'D:\Program Files (x86)\Steam\steamapps\common\The Scroll Of Taiwu'
OUT_DIR = os.path.join(ROOT, 'Mod', 'EncyclopediaExporter', 'preview')
os.makedirs(OUT_DIR, exist_ok=True)

# 分辨率倍率：1 = 512x384（旧），2 = 1024x768（当前）
SCALE = 2

# 逻辑画布尺寸
LW, LH = 512, 384
# 实际输出尺寸
W, H = LW * SCALE, LH * SCALE


def S(v):
    """按 SCALE 等比缩放（整数/浮点均可）。"""
    return v * SCALE


def load_font(size, bold=False):
    real = int(round(size * SCALE))
    candidates = [
        r'C:\Windows\Fonts\msyhbd.ttc' if bold else r'C:\Windows\Fonts\msyh.ttc',
        r'C:\Windows\Fonts\msyh.ttc',
        r'C:\Windows\Fonts\simhei.ttf',
        r'C:\Windows\Fonts\simsun.ttc',
    ]
    for p in candidates:
        if p and os.path.exists(p):
            try:
                return ImageFont.truetype(p, real)
            except Exception:
                pass
    return ImageFont.load_default()


def lerp(a, b, t):
    return a + (b - a) * t


def vgrad(w, h, c1, c2):
    grad = Image.new('RGB', (w, h))
    px = grad.load()
    for i in range(h):
        t = i / max(1, h - 1)
        r, g, b = int(lerp(c1[0], c2[0], t)), int(lerp(c1[1], c2[1], t)), int(lerp(c1[2], c2[2], t))
        for x in range(w):
            px[x, i] = (r, g, b)
    return grad


def new_canvas():
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    bg = vgrad(W, H, (43, 29, 20), (18, 12, 7))
    img.paste(bg, (0, 0))
    # 橘色光晕
    glow = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse([S(-60), S(-40), S(LW + 60), S(LH)], fill=(255, 140, 26, 10))
    glow = glow.filter(ImageFilter.GaussianBlur(S(30)))
    img = Image.alpha_composite(img, glow)
    return img


def draw_text_centered(draw, y, text, font, fill, spacing=0):
    chars = list(text)
    widths = [draw.textlength(c, font=font) for c in chars]
    total = sum(widths) + S(spacing) * (len(chars) - 1)
    x = (W - total) / 2
    for ch, cw in zip(chars, widths):
        draw.text((x, y), ch, font=font, fill=fill)
        x += cw + S(spacing)


def draw_title_bar(draw, title):
    """顶部标题条：橘色装饰线 + 标题文字"""
    f = load_font(22, bold=True)
    draw_text_centered(draw, S(18), title, f, (255, 217, 160), spacing=3)
    tw = draw.textlength(title, font=f) + S(3) * (len(title) - 1)
    cx = W / 2
    draw.line([cx - tw / 2 - S(30), S(50), cx - tw / 2 - S(10), S(50)], fill=(200, 120, 40), width=max(1, int(S(2))))
    draw.line([cx + tw / 2 + S(10), S(50), cx + tw / 2 + S(30), S(50)], fill=(200, 120, 40), width=max(1, int(S(2))))


def finalize(img, name):
    final = Image.new('RGB', (W, H), (28, 18, 9))
    final.paste(img, (0, 0), img)
    p = os.path.join(OUT_DIR, name)
    final.save(p, 'PNG')
    print('已生成:', p, final.size, os.path.getsize(p), 'bytes')


# ============ 图1: 功能总览 ============
def make_overview():
    img = new_canvas()
    d = ImageDraw.Draw(img)
    draw_title_bar(d, '功 能 总 览')

    f_label = load_font(16, bold=True)
    f_desc = load_font(13)
    f_num = load_font(28, bold=True)

    # 四个特性卡片 (2x2)
    cards = [
        ('188', '页百科文档', '12 章节完整重建'),
        ('102', '张数据表格', '品阶/属性/概率'),
        ('169', '处列表排版', '自动转 Markdown'),
        ('0', '残留冗余标签', '干净可读文本'),
    ]
    card_w, card_h = S(220), S(120)
    gap = S(16)
    start_x = (W - 2 * card_w - gap) / 2
    start_y = S(68)
    for i, (num, label, desc) in enumerate(cards):
        cx = start_x + (i % 2) * (card_w + gap)
        cy = start_y + (i // 2) * (card_h + gap)
        d.rounded_rectangle([cx, cy, cx + card_w, cy + card_h], radius=int(S(8)),
                            fill=(28, 18, 10, 200), outline=(120, 70, 30), width=max(1, int(S(1))))
        d.text((cx + S(20), cy + S(14)), num, font=f_num, fill=(255, 140, 26))
        d.text((cx + S(20), cy + S(56)), label, font=f_label, fill=(255, 217, 160))
        d.text((cx + S(20), cy + S(84)), desc, font=f_desc, fill=(180, 150, 110))

    finalize(img, 'overview.png')


# ============ 图2: 内容样例 ============
def make_sample():
    img = new_canvas()
    d = ImageDraw.Draw(img)
    draw_title_bar(d, '内 容 样 例')

    # 模拟一个 MD 页面的渲染效果（出身特质表格片段）
    win_x, win_y, win_w, win_h = S(36), S(64), S(LW - 72), S(296)
    d.rounded_rectangle([win_x, win_y, win_x + win_w, win_y + win_h], radius=int(S(6)),
                        fill=(245, 238, 225), outline=(180, 150, 100), width=max(1, int(S(2))))

    tx = win_x + S(16)
    ty = win_y + S(14)
    f_h = load_font(15, bold=True)   # 标题
    f_b = load_font(12)              # 正文
    f_cell = load_font(11)           # 表格

    d.text((tx, ty), '出身特质', font=f_h, fill=(60, 35, 15))
    ty += S(26)
    d.text((tx, ty), '创建人物时，可以分配特质点数，为人物选择出身特质。', font=f_b, fill=(80, 55, 30))
    ty += S(20)

    # 列表项
    for item in ['初始共有15点可分配的特质点数', '越高级的特质消耗的点数越多']:
        d.text((tx + S(8), ty), '· ' + item, font=f_b, fill=(80, 55, 30))
        ty += S(18)

    ty += S(6)
    d.text((tx, ty), '出身特质一览', font=load_font(13, bold=True), fill=(60, 35, 15))
    ty += S(20)

    # 简易表格
    headers = ['类别', '出身特质', '效果', '点数']
    rows = [
        ['经历', '锋从磨砺', '膂力+20', '1'],
        ['', '纯凭自然', '体质+20', '1'],
        ['财富', '梦中富贵', '银钱+800', '2'],
        ['技艺', '七窍玲珑', '悟性+20', '2'],
    ]
    col_x = [tx, tx + S(50), tx + S(130), tx + S(250)]
    for j, htext in enumerate(headers):
        d.text((col_x[j], ty), htext, font=f_cell, fill=(60, 35, 15))
    d.line([(tx, ty + S(16)), (tx + S(296), ty + S(16))], fill=(120, 90, 50), width=max(1, int(S(1))))
    ty += S(20)
    for row in rows:
        for j, cell in enumerate(row):
            color = (60, 35, 15) if j != 1 else (140, 100, 30)  # 特质名用橘色
            d.text((col_x[j], ty), cell, font=f_cell, fill=color)
        ty += S(17)
        d.line([(tx, ty), (tx + S(296), ty)], fill=(220, 205, 175), width=max(1, int(S(1))))

    ty += S(8)
    # 链接样例
    link_text = '更多详情见 → [身心强健](战斗/伤害.md)'
    d.text((tx, ty), link_text, font=f_b, fill=(50, 90, 160))

    finalize(img, 'sample.png')


# ============ 图3: 使用步骤 ============
def make_usage():
    img = new_canvas()
    d = ImageDraw.Draw(img)
    draw_title_bar(d, '使 用 步 骤')

    f_step = load_font(15, bold=True)
    f_desc = load_font(12)
    f_arrow = load_font(20, bold=True)

    steps = [
        ('①', '启用 Mod', '游戏内 Mod 管理器一键启用\n自动生成百科文档'),
        ('②', '查看输出', 'Mod\\EncyclopediaExporter\\output\\\n188 个 Markdown 文档'),
        ('③', '交给 AI', '本地 AI 工具打开目录\n或上传给网页 AI'),
    ]
    step_w = S(150)
    gap = S(18)
    start_x = (W - 3 * step_w - 2 * gap) / 2
    sy = S(78)

    for i, (num, title, desc) in enumerate(steps):
        sx = start_x + i * (step_w + gap)
        # 步骤圆圈
        d.ellipse([sx + step_w/2 - S(22), sy, sx + step_w/2 + S(22), sy + S(44)],
                  fill=(255, 140, 26), outline=(255, 217, 160), width=max(1, int(S(2))))
        d.text((sx + step_w/2 - S(10), sy + S(6)), num, font=load_font(22, bold=True), fill=(60, 30, 10))
        # 标题
        tw = d.textlength(title, font=f_step)
        d.text((sx + (step_w - tw)/2, sy + S(54)), title, font=f_step, fill=(255, 217, 160))
        # 描述（多行）
        dy = sy + S(80)
        for line in desc.split('\n'):
            lw = d.textlength(line, font=f_desc)
            d.text((sx + (step_w - lw)/2, dy), line, font=f_desc, fill=(180, 150, 110))
            dy += S(16)
        # 箭头
        if i < 2:
            ax = sx + step_w + gap/2
            d.text((ax - S(6), sy + S(80)), '→', font=f_arrow, fill=(200, 120, 40))

    # 底部提示
    tip = '让 AI 读懂太吾，帮你设计 Build、搭配出生效果、解答疑问'
    f_tip = load_font(13, bold=True)
    tw = d.textlength(tip, font=f_tip)
    d.text(((W - tw)/2, S(340)), tip, font=f_tip, fill=(255, 200, 120))

    finalize(img, 'usage.png')


make_overview()
make_sample()
make_usage()
print('\n全部完成。输出目录:', OUT_DIR, '尺寸:', W, 'x', H)
