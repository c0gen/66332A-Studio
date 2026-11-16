"""Generate the code-defined instrument mark; no image/font dependency."""
from pathlib import Path
import struct
import zlib


def png(size):
    pixels = []
    for y in range(size):
        row = bytearray()
        for x in range(size):
            u, v = (x + 0.5) * 64 / size, (y + 0.5) * 64 / size
            color = (23, 30, 40, 255)
            cx, cy = min(max(u, 10), 54), min(max(v, 10), 54)
            if (u - cx) ** 2 + (v - cy) ** 2 > 100:
                color = (0, 0, 0, 0)
            elif (10 <= u <= 54 and 48 <= v <= 51):
                color = (239, 181, 125, 255)
            elif ((10 <= u <= 23 and 37 <= v <= 41)
                  or (19 <= u <= 23 and 21 <= v <= 41)
                  or (19 <= u <= 45 and 21 <= v <= 25)
                  or (41 <= u <= 45 and 21 <= v <= 41)
                  or (41 <= u <= 54 and 37 <= v <= 41)):
                color = (104, 201, 229, 255)
            row.extend(color)
        pixels.append(b"\0" + row)

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(b"".join(pixels))) + chunk(b"IEND", b""))


if __name__ == "__main__":
    sizes = (16, 32, 48, 64, 128)
    images = [png(size) for size in sizes]
    offset = 6 + 16 * len(sizes)
    directory = bytearray(struct.pack("<HHH", 0, 1, len(sizes)))
    for size, data in zip(sizes, images):
        directory.extend(struct.pack("<BBBBHHII", size, size, 0, 0, 1, 32, len(data), offset))
        offset += len(data)
    target = Path(__file__).resolve().parents[1] / "src/Agilent66332A.Desktop/Assets/Studio.ico"
    target.write_bytes(bytes(directory) + b"".join(images))
