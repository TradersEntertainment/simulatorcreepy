#!/usr/bin/env python3
"""Crop a PNG to the first N rows. Pure stdlib — no PIL in this environment."""
import sys, zlib, struct

def chunks(data):
    i = 8
    while i < len(data):
        (ln,) = struct.unpack('>I', data[i:i+4])
        typ = data[i+4:i+8]
        yield typ, data[i+8:i+8+ln]
        i += 8 + ln + 4

def paeth(a, b, c):
    p = a + b - c
    pa, pb, pc = abs(p-a), abs(p-b), abs(p-c)
    return a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)

def crop(src, dst, keep_h):
    data = open(src, 'rb').read()
    idat = bytearray()
    for typ, body in chunks(data):
        if typ == b'IHDR':
            w, h, depth, ctype, comp, filt, inter = struct.unpack('>IIBBBBB', body)
        elif typ == b'IDAT':
            idat += body
    assert depth == 8 and inter == 0, (depth, inter)
    bpp = {0:1, 2:3, 3:1, 4:2, 6:4}[ctype]
    stride = w * bpp
    raw = zlib.decompress(bytes(idat))

    keep_h = min(keep_h, h)
    out = bytearray()
    prev = bytearray(stride)
    pos = 0
    for _ in range(keep_h):
        f = raw[pos]; pos += 1
        line = bytearray(raw[pos:pos+stride]); pos += stride
        if f == 1:
            for x in range(bpp, stride):
                line[x] = (line[x] + line[x-bpp]) & 255
        elif f == 2:
            for x in range(stride):
                line[x] = (line[x] + prev[x]) & 255
        elif f == 3:
            for x in range(stride):
                a = line[x-bpp] if x >= bpp else 0
                line[x] = (line[x] + ((a + prev[x]) >> 1)) & 255
        elif f == 4:
            for x in range(stride):
                a = line[x-bpp] if x >= bpp else 0
                c = prev[x-bpp] if x >= bpp else 0
                line[x] = (line[x] + paeth(a, prev[x], c)) & 255
        out += b'\x00' + line
        prev = line

    def chunk(typ, body):
        return struct.pack('>I', len(body)) + typ + body + \
               struct.pack('>I', zlib.crc32(typ + body) & 0xffffffff)

    png = b'\x89PNG\r\n\x1a\n'
    png += chunk(b'IHDR', struct.pack('>IIBBBBB', w, keep_h, 8, ctype, 0, 0, 0))
    png += chunk(b'IDAT', zlib.compress(bytes(out), 9))
    png += chunk(b'IEND', b'')
    open(dst, 'wb').write(png)
    print(f'{dst}: {w}x{keep_h}')

if __name__ == '__main__':
    crop(sys.argv[1], sys.argv[2], int(sys.argv[3]))
