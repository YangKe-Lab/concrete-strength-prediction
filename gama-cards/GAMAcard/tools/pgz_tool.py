#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""pgz_tool.py —— E-GAMA .pgz 卡组文件的离线工具（读参数 / 改数据路径 / 重打包）

.pgz 是**单条 raw-deflate 流**，内容为 UTF-8(BOM) 的 XML：
    <PGW_FILE ver="v05" gver="5.1.1.424"> → <AllCardsOnDeck> → <Card TPN="MyGamaCards.TrainModelCard" ...>
每个端口的当前值存在 <DFTDAT><im> 里（.NET BinaryFormatter 序列化后 base64），
连线关系存在 <DCBSrc><SourceGuid>（= 上游卡的 <CardInsG>）。
⚠️ DFTDAT 只是缓存显示值：端口真正取值可能来自连线的文字白板/滑动条卡，必须同时看 DCBSrc。
字符串值 blob 结构：17 字节头 + 0x06 + objId(4) + 7bit 变长长度 + UTF-8 字节 + 0x0b

用法：
    python pgz_tool.py dump   <file.pgz>                  # 列出卡片、端口值、连线
    python pgz_tool.py paths  <file.pgz>                  # 只列出字符串型端口值 / 文字白板（找数据路径）
    python pgz_tool.py rewrite <in.pgz> <out.pgz> OLD=NEW [OLD=NEW ...]
                                                          # 改写字符串值（含文字白板 _userText）后重新打包
说明：rewrite 只替换值，不改结构；同值多处出现会一起替换（端口 blob + 文字白板显示文本）。
"""
import base64
import re
import struct
import sys
import zlib

HEADER = bytes.fromhex("0001000000ffffffff0100000000000000")


# ---------- 压缩层 ----------
def unload(path: str) -> str:
    """解压 .pgz → XML 文本"""
    return zlib.decompressobj(-15).decompress(open(path, "rb").read()).decode("utf-8-sig")


def pack(xml: str, path: str) -> None:
    """把 XML 文本压回 .pgz（raw deflate，保留 BOM）"""
    raw = b"\xef\xbb\xbf" + xml.encode("utf-8")
    co = zlib.compressobj(9, zlib.DEFLATED, -15)
    open(path, "wb").write(co.compress(raw) + co.flush())


# ---------- 值编解码 ----------
def decode_value(b64: str):
    """解析一个 <im>/<base64Binary> 值；返回 str / int / float / bool / None（复合对象）"""
    if not b64:
        return None
    b = base64.b64decode(b64)
    i = b.find(b"\x06")                       # BinaryObjectString（字符串）
    if i == 17:
        j, ln, shift = i + 5, 0, 0
        while True:
            c = b[j]
            j += 1
            ln |= (c & 0x7F) << shift
            shift += 7
            if not (c & 0x80):
                break
        return b[j:j + ln].decode("utf-8", errors="replace")
    k = b.find(b"m_value")                    # 基元类型
    if k >= 0:
        ptype, rest = b[k + 8], b[k + 9:]
        try:
            if ptype == 8: return struct.unpack("<i", rest[:4])[0]
            if ptype == 6: return struct.unpack("<d", rest[:8])[0]
            if ptype == 9: return struct.unpack("<q", rest[:8])[0]
            if ptype == 11: return struct.unpack("<f", rest[:4])[0]
            if ptype == 1: return rest[0] != 0
        except struct.error:
            pass
    return None


def encode_string(s: str, objid: int = 1) -> str:
    """把字符串编成与 .NET BinaryFormatter 一致的 blob（base64）"""
    raw = s.encode("utf-8")
    ln, varint = len(raw), bytearray()
    while True:
        c = ln & 0x7F
        ln >>= 7
        if ln:
            varint.append(c | 0x80)
        else:
            varint.append(c)
            break
    blob = HEADER + b"\x06" + struct.pack("<i", objid) + bytes(varint) + raw + b"\x0b"
    return base64.b64encode(blob).decode()


# ---------- 结构遍历 ----------
CARD_RE = re.compile(r'<Card CardType="(\w+)"[^>]*TPN="([^"]+)">')
PORT_RE = re.compile(r'(?=<DataCard CardType="DataCard")')


def iter_cards(xml: str):
    """逐张卡 yield (卡片类型, 卡片块, 端口列表)"""
    for m in CARD_RE.finditer(xml):
        seg = xml[m.end():]
        nxt = re.search(r"\r\n    <Card CardType=", seg)
        seg = seg[:nxt.start()] if nxt else seg
        ports = []
        for ch in PORT_RE.split(seg):
            if not ch.startswith("<DataCard"):
                continue
            name = re.search(r"<CardName>([^<]*)</CardName>", ch)
            blob = re.search(r'<DFTDAT len="1">\s*<im null="false">([^<]+)</im>', ch)
            src = re.search(r'<DCBSrc len="1">\s*<SourceGuid>([0-9a-f]+)</SourceGuid>', ch)
            if name:
                ports.append({
                    "name": name.group(1),
                    "value": decode_value(blob.group(1)) if blob else None,
                    "blob": blob.group(1) if blob else None,
                    "src": src.group(1) if src else None,
                })
        yield m.group(2).split(".")[-1], seg, ports


def texts(xml: str):
    """所有文字白板（TextStreamCard）的显示文本"""
    out = []
    for m in re.finditer(r'<Card CardType="Genetic"[^>]*TPN="[^"]*TextStreamCard">', xml):
        blk = xml[m.end(): m.end() + 20000].split("</Card>")[0]
        t = re.search(r"<Key>_userText</Key>\s*<Value>(.*?)</Value>", blk, re.S)
        if t:
            out.append(t.group(1).strip())
    return out


# ---------- 命令 ----------
def cmd_dump(path: str) -> None:
    xml = unload(path)
    by_guid = {}
    for tpn, seg, _ in iter_cards(xml):
        g = re.search(r"<CardInsG>([0-9a-f]+)</CardInsG>", seg)
        if g:
            by_guid[g.group(1)] = tpn
    for tpn, seg, ports in iter_cards(xml):
        vals = []
        for p in ports:
            if p["value"] not in (None, ""):
                vals.append("%s=%r" % (p["name"], p["value"]))
            elif p["src"]:
                vals.append("%s <- %s" % (p["name"], by_guid.get(p["src"], p["src"][:8])))
        if vals:
            print("[%s] %s" % (tpn, " | ".join(vals)))


def cmd_paths(path: str) -> None:
    xml = unload(path)
    for t in texts(xml):
        print("文字白板:", t)
    for _, _, ports in iter_cards(xml):
        for p in ports:
            if isinstance(p["value"], str) and ("\\" in p["value"] or "/" in p["value"]):
                print("端口值:", p["name"], "=", p["value"])


def cmd_rewrite(src: str, dst: str, pairs) -> None:
    xml = unload(src)
    for pair in pairs:
        old, new = pair.split("=", 1)
        n = xml.count(old)
        if n == 0:
            print("!! 未找到:", old, file=sys.stderr)
            continue
        xml = xml.replace(encode_string(old), encode_string(new))          # 端口 blob
        xml = xml.replace("<Value>%s\r\n</Value>" % old,
                          "<Value>%s\r\n</Value>" % new)                   # 文字白板显示文本
        print("替换 %d 处: %s -> %r" % (n, old, new))
    pack(xml, dst)
    print("已写出", dst)


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(1)
    cmd = sys.argv[1]
    if cmd == "dump":
        cmd_dump(sys.argv[2])
    elif cmd == "paths":
        cmd_paths(sys.argv[2])
    elif cmd == "rewrite":
        cmd_rewrite(sys.argv[2], sys.argv[3], sys.argv[4:])
    else:
        print(__doc__)
        sys.exit(1)
