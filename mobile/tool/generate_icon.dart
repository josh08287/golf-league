import 'dart:io';
import 'dart:typed_data';

// Generates a 1024x1024 PNG of the golf flag icon matching golf-flag.svg
// Uses only dart:io and manual PNG encoding (no external deps).
void main() {
  const size = 1024;
  final pixels = Uint32List(size * size); // ARGB

  void fillPixel(int x, int y, int r, int g, int b, [int a = 255]) {
    if (x < 0 || x >= size || y < 0 || y >= size) return;
    pixels[y * size + x] = (a << 24) | (r << 16) | (g << 8) | b;
  }

  void fillRect(int x1, int y1, int x2, int y2, int r, int g, int b) {
    for (var y = y1; y <= y2; y++) {
      for (var x = x1; x <= x2; x++) {
        fillPixel(x, y, r, g, b);
      }
    }
  }

  void fillEllipse(
      int cx, int cy, int rx, int ry, int r, int g, int b) {
    for (var y = cy - ry; y <= cy + ry; y++) {
      for (var x = cx - rx; x <= cx + rx; x++) {
        final dx = (x - cx) / rx;
        final dy = (y - cy) / ry;
        if (dx * dx + dy * dy <= 1.0) {
          fillPixel(x, y, r, g, b);
        }
      }
    }
  }

  void fillTriangle(int x1, int y1, int x2, int y2, int x3, int y3,
      int r, int g, int b) {
    final minX = [x1, x2, x3].reduce((a, b) => a < b ? a : b);
    final maxX = [x1, x2, x3].reduce((a, b) => a > b ? a : b);
    final minY = [y1, y2, y3].reduce((a, b) => a < b ? a : b);
    final maxY = [y1, y2, y3].reduce((a, b) => a > b ? a : b);

    double sign(int px, int py, int ax, int ay, int bx, int by) =>
        ((px - bx) * (ay - by) - (ax - bx) * (py - by)).toDouble();

    for (var y = minY; y <= maxY; y++) {
      for (var x = minX; x <= maxX; x++) {
        final d1 = sign(x, y, x1, y1, x2, y2);
        final d2 = sign(x, y, x2, y2, x3, y3);
        final d3 = sign(x, y, x3, y3, x1, y1);
        final hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        final hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        if (!(hasNeg && hasPos)) {
          fillPixel(x, y, r, g, b);
        }
      }
    }
  }

  // White background
  pixels.fillRange(0, pixels.length, 0xFFFFFFFF);

  // Scale factors from 100x100 SVG viewBox to 1024x1024
  const s = 1024 / 100;

  // Pole: rect x=45 y=15 w=4 h=70, color #8B4513 (saddlebrown)
  fillRect((45 * s).round(), (15 * s).round(), (49 * s).round(),
      (85 * s).round(), 0x8B, 0x45, 0x13);

  // Flag: path M49 15 L85 28 L49 41 Z, color #228B22 (forestgreen)
  fillTriangle((49 * s).round(), (15 * s).round(), (85 * s).round(),
      (28 * s).round(), (49 * s).round(), (41 * s).round(), 0x22, 0x8B, 0x22);

  // Ground: ellipse cx=50 cy=88 rx=25 ry=8, color #32CD32 (limegreen)
  fillEllipse((50 * s).round(), (88 * s).round(), (25 * s).round(),
      (8 * s).round(), 0x32, 0xCD, 0x32);

  // Encode as PNG
  final png = encodePng(size, size, pixels);
  final outFile = File('assets/icons/golf-flag.png');
  outFile.writeAsBytesSync(png);
  print('Written: ${outFile.path} (${png.length} bytes)');
}

Uint8List encodePng(int width, int height, Uint32List argb) {
  // Convert ARGB to RGBA bytes
  final rgba = Uint8List(width * height * 4);
  for (var i = 0; i < argb.length; i++) {
    final p = argb[i];
    rgba[i * 4 + 0] = (p >> 16) & 0xFF; // R
    rgba[i * 4 + 1] = (p >> 8) & 0xFF;  // G
    rgba[i * 4 + 2] = p & 0xFF;          // B
    rgba[i * 4 + 3] = (p >> 24) & 0xFF; // A
  }

  final out = BytesBuilder();

  void writeU32(int v) {
    out.addByte((v >> 24) & 0xFF);
    out.addByte((v >> 16) & 0xFF);
    out.addByte((v >> 8) & 0xFF);
    out.addByte(v & 0xFF);
  }

  void writeChunk(String type, Uint8List data) {
    writeU32(data.length);
    final typeBytes = type.codeUnits;
    out.add(typeBytes);
    out.add(data);
    final crcData = Uint8List(4 + data.length);
    crcData.setRange(0, 4, typeBytes);
    crcData.setRange(4, 4 + data.length, data);
    writeU32(crc32(crcData));
  }

  // PNG signature
  out.add([137, 80, 78, 71, 13, 10, 26, 10]);

  // IHDR
  final ihdr = BytesBuilder();
  void w32(BytesBuilder b, int v) {
    b.addByte((v >> 24) & 0xFF);
    b.addByte((v >> 16) & 0xFF);
    b.addByte((v >> 8) & 0xFF);
    b.addByte(v & 0xFF);
  }
  w32(ihdr, width);
  w32(ihdr, height);
  ihdr.addByte(8); // bit depth
  ihdr.addByte(6); // color type RGBA
  ihdr.addByte(0); // compression
  ihdr.addByte(0); // filter
  ihdr.addByte(0); // interlace
  writeChunk('IHDR', ihdr.toBytes());

  // IDAT: filter + compress each row
  final raw = BytesBuilder();
  for (var y = 0; y < height; y++) {
    raw.addByte(0); // None filter
    raw.add(rgba.sublist(y * width * 4, (y + 1) * width * 4));
  }
  final compressed = zlib.encode(raw.toBytes());
  writeChunk('IDAT', Uint8List.fromList(compressed));

  // IEND
  writeChunk('IEND', Uint8List(0));

  return out.toBytes();
}

int crc32(Uint8List data) {
  var crc = 0xFFFFFFFF;
  for (final b in data) {
    crc ^= b;
    for (var i = 0; i < 8; i++) {
      crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
    }
  }
  return crc ^ 0xFFFFFFFF;
}

final zlib = _Zlib();

class _Zlib {
  List<int> encode(List<int> data) {
    // Use dart:io ZLibEncoder
    return ZLibEncoder().convert(data);
  }
}
