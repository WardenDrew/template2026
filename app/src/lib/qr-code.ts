export type QrErrorCorrectionLevel = "low" | "medium" | "quartile" | "high";

export type QrCodeOptions = {
  border?: number;
  errorCorrectionLevel?: QrErrorCorrectionLevel;
  maxVersion?: number;
  minVersion?: number;
};

export type QrCode = {
  border: number;
  errorCorrectionLevel: QrErrorCorrectionLevel;
  modules: boolean[][];
  path: string;
  payload: string;
  size: number;
  version: number;
  viewBox: string;
};

const ECC_CODEWORDS_PER_BLOCK: Record<
  QrErrorCorrectionLevel,
  readonly number[]
> = {
  high: [
    -1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28,
    26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30,
    30, 30, 30
  ],
  low: [
    -1, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30,
    28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30,
    30, 30, 30
  ],
  medium: [
    -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26,
    26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
    28, 28, 28
  ],
  quartile: [
    -1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28,
    26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30,
    30, 30, 30
  ]
};

const FORMAT_BITS: Record<QrErrorCorrectionLevel, number> = {
  high: 2,
  low: 1,
  medium: 0,
  quartile: 3
};

const NUM_ERROR_CORRECTION_BLOCKS: Record<
  QrErrorCorrectionLevel,
  readonly number[]
> = {
  high: [
    -1, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25,
    25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77,
    81
  ],
  low: [
    -1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 4, 4, 4, 6, 6, 6, 6, 7, 8, 8, 9, 9, 10,
    12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25
  ],
  medium: [
    -1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17,
    17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49
  ],
  quartile: [
    -1, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8, 8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23,
    23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68
  ]
};

class BitBuffer {
  private readonly bits: number[] = [];

  get length() {
    return this.bits.length;
  }

  append(value: number, length: number) {
    if (!Number.isInteger(value) || value < 0 || value >= 2 ** length) {
      throw new Error("QR value is out of range.");
    }

    for (let i = length - 1; i >= 0; i -= 1) {
      this.bits.push((value >>> i) & 1);
    }
  }

  toBytes() {
    const result: number[] = [];

    for (let i = 0; i < this.bits.length; i += 8) {
      let value = 0;

      for (let bit = 0; bit < 8; bit += 1) {
        value = (value << 1) | (this.bits[i + bit] ?? 0);
      }

      result.push(value);
    }

    return result;
  }
}

class QrMatrixBuilder {
  private readonly alignmentPatternPositions: readonly number[];
  private readonly errorCorrectionLevel: QrErrorCorrectionLevel;
  private readonly isFunction: boolean[][];
  private readonly modules: boolean[][];
  private readonly size: number;
  private readonly version: number;

  constructor(version: number, errorCorrectionLevel: QrErrorCorrectionLevel) {
    this.errorCorrectionLevel = errorCorrectionLevel;
    this.version = version;
    this.size = getQrSize(version);
    this.alignmentPatternPositions = getAlignmentPatternPositions(version);
    this.modules = createGrid(this.size, false);
    this.isFunction = createGrid(this.size, false);
    this.drawFunctionPatterns();
  }

  get matrix() {
    return this.modules.map(row => [...row]);
  }

  applyMask(mask: number) {
    for (let y = 0; y < this.size; y += 1) {
      for (let x = 0; x < this.size; x += 1) {
        if (!this.isFunction[y]?.[x] && getMaskBit(mask, x, y)) {
          this.modules[y]![x] = !this.modules[y]![x];
        }
      }
    }
  }

  drawCodewords(codewords: readonly number[]) {
    let bitIndex = 0;

    for (let right = this.size - 1; right >= 1; right -= 2) {
      if (right === 6) {
        right -= 1;
      }

      for (let vertical = 0; vertical < this.size; vertical += 1) {
        const y = (right + 1) & 2 ? vertical : this.size - 1 - vertical;

        for (let offset = 0; offset < 2; offset += 1) {
          const x = right - offset;

          if (!this.isFunction[y]?.[x]) {
            const codeword = codewords[bitIndex >>> 3] ?? 0;
            this.modules[y]![x] = ((codeword >>> (7 - (bitIndex & 7))) & 1) > 0;
            bitIndex += 1;
          }
        }
      }
    }
  }

  drawFormatBits(mask: number) {
    const data = (FORMAT_BITS[this.errorCorrectionLevel] << 3) | mask;
    let remainder = data;

    for (let i = 0; i < 10; i += 1) {
      remainder = (remainder << 1) ^ ((remainder >>> 9) * 0x537);
    }

    const bits = ((data << 10) | remainder) ^ 0x5412;

    for (let i = 0; i <= 5; i += 1) {
      this.setFunctionModule(8, i, getBit(bits, i));
    }

    this.setFunctionModule(8, 7, getBit(bits, 6));
    this.setFunctionModule(8, 8, getBit(bits, 7));
    this.setFunctionModule(7, 8, getBit(bits, 8));

    for (let i = 9; i < 15; i += 1) {
      this.setFunctionModule(14 - i, 8, getBit(bits, i));
    }

    for (let i = 0; i < 8; i += 1) {
      this.setFunctionModule(this.size - 1 - i, 8, getBit(bits, i));
    }

    for (let i = 8; i < 15; i += 1) {
      this.setFunctionModule(8, this.size - 15 + i, getBit(bits, i));
    }

    this.setFunctionModule(8, this.size - 8, true);
  }

  getPenaltyScore() {
    let result = 0;

    for (let y = 0; y < this.size; y += 1) {
      result += getRunPenalty(this.modules[y]!);
    }

    for (let x = 0; x < this.size; x += 1) {
      result += getRunPenalty(this.modules.map(row => row[x]!));
    }

    for (let y = 0; y < this.size - 1; y += 1) {
      for (let x = 0; x < this.size - 1; x += 1) {
        const color = this.modules[y]![x];

        if (
          color === this.modules[y]![x + 1] &&
          color === this.modules[y + 1]![x] &&
          color === this.modules[y + 1]![x + 1]
        ) {
          result += 3;
        }
      }
    }

    result += this.getFinderLikePenalty();

    const darkModules = this.modules.flat().filter(Boolean).length;
    const totalModules = this.size * this.size;
    const balancePenalty =
      Math.ceil(Math.abs(darkModules * 20 - totalModules * 10) / totalModules) -
      1;

    return result + balancePenalty * 10;
  }

  private drawAlignmentPattern(centerX: number, centerY: number) {
    for (let y = -2; y <= 2; y += 1) {
      for (let x = -2; x <= 2; x += 1) {
        const distance = Math.max(Math.abs(x), Math.abs(y));
        this.setFunctionModule(centerX + x, centerY + y, distance !== 1);
      }
    }
  }

  private drawFinderPattern(centerX: number, centerY: number) {
    for (let y = -4; y <= 4; y += 1) {
      for (let x = -4; x <= 4; x += 1) {
        const moduleX = centerX + x;
        const moduleY = centerY + y;

        if (
          moduleX < 0 ||
          moduleX >= this.size ||
          moduleY < 0 ||
          moduleY >= this.size
        ) {
          continue;
        }

        const distance = Math.max(Math.abs(x), Math.abs(y));
        this.setFunctionModule(
          moduleX,
          moduleY,
          distance !== 2 && distance !== 4
        );
      }
    }
  }

  private drawFunctionPatterns() {
    this.drawFinderPattern(3, 3);
    this.drawFinderPattern(this.size - 4, 3);
    this.drawFinderPattern(3, this.size - 4);

    for (const y of this.alignmentPatternPositions) {
      for (const x of this.alignmentPatternPositions) {
        if (
          (x === 6 && y === 6) ||
          (x === 6 && y === this.size - 7) ||
          (x === this.size - 7 && y === 6)
        ) {
          continue;
        }

        this.drawAlignmentPattern(x, y);
      }
    }

    for (let i = 0; i < this.size; i += 1) {
      if (!this.isFunction[6]![i]) {
        this.setFunctionModule(i, 6, i % 2 === 0);
      }

      if (!this.isFunction[i]![6]) {
        this.setFunctionModule(6, i, i % 2 === 0);
      }
    }

    this.drawFormatBits(0);

    if (this.version >= 7) {
      this.drawVersionBits();
    }
  }

  private drawVersionBits() {
    let remainder = this.version;

    for (let i = 0; i < 12; i += 1) {
      remainder = (remainder << 1) ^ ((remainder >>> 11) * 0x1f25);
    }

    const bits = (this.version << 12) | remainder;

    for (let i = 0; i < 18; i += 1) {
      const bit = getBit(bits, i);
      const a = this.size - 11 + (i % 3);
      const b = Math.floor(i / 3);

      this.setFunctionModule(a, b, bit);
      this.setFunctionModule(b, a, bit);
    }
  }

  private getFinderLikePenalty() {
    const pattern = [true, false, true, true, true, false, true];
    let result = 0;

    for (let y = 0; y < this.size; y += 1) {
      for (let x = 0; x <= this.size - 7; x += 1) {
        if (matchesPattern(this.modules[y]!.slice(x, x + 7), pattern)) {
          const before = this.modules[y]!.slice(Math.max(0, x - 4), x);
          const after = this.modules[y]!.slice(
            x + 7,
            Math.min(this.size, x + 11)
          );

          if (isLightRun(before) || isLightRun(after)) {
            result += 40;
          }
        }
      }
    }

    for (let x = 0; x < this.size; x += 1) {
      const column = this.modules.map(row => row[x]!);

      for (let y = 0; y <= this.size - 7; y += 1) {
        if (matchesPattern(column.slice(y, y + 7), pattern)) {
          const before = column.slice(Math.max(0, y - 4), y);
          const after = column.slice(y + 7, Math.min(this.size, y + 11));

          if (isLightRun(before) || isLightRun(after)) {
            result += 40;
          }
        }
      }
    }

    return result;
  }

  private setFunctionModule(x: number, y: number, isDark: boolean) {
    this.modules[y]![x] = isDark;
    this.isFunction[y]![x] = true;
  }
}

export function createQrCode(
  payload: string,
  options: QrCodeOptions = {}
): QrCode {
  const border = options.border ?? 4;
  const errorCorrectionLevel = options.errorCorrectionLevel ?? "medium";
  const minVersion = options.minVersion ?? 1;
  const maxVersion = options.maxVersion ?? 40;

  validateVersionRange(minVersion, maxVersion);

  const version = chooseVersion(
    payload,
    errorCorrectionLevel,
    minVersion,
    maxVersion
  );
  const dataCodewords = createDataCodewords(
    payload,
    version,
    errorCorrectionLevel
  );
  const codewords = addErrorCorrectionAndInterleave(
    dataCodewords,
    version,
    errorCorrectionLevel
  );
  let bestMatrix: boolean[][] | null = null;
  let bestPenalty = Number.POSITIVE_INFINITY;

  for (let mask = 0; mask < 8; mask += 1) {
    const qr = new QrMatrixBuilder(version, errorCorrectionLevel);
    qr.drawCodewords(codewords);
    qr.applyMask(mask);
    qr.drawFormatBits(mask);

    const penalty = qr.getPenaltyScore();

    if (penalty < bestPenalty) {
      bestPenalty = penalty;
      bestMatrix = qr.matrix;
    }
  }

  if (!bestMatrix) {
    throw new Error("QR generation failed.");
  }

  return {
    border,
    errorCorrectionLevel,
    modules: bestMatrix,
    path: renderQrCodePath(bestMatrix, border),
    payload,
    size: bestMatrix.length,
    version,
    viewBox: createQrCodeViewBox(bestMatrix, border)
  };
}

export function createQrCodeMatrix(
  payload: string,
  options: QrCodeOptions = {}
) {
  return createQrCode(payload, options).modules;
}

export function createQrCodeViewBox(modules: readonly boolean[][], border = 4) {
  const size = modules.length + border * 2;
  return `0 0 ${size} ${size}`;
}

export function renderQrCodePath(modules: readonly boolean[][], border = 4) {
  return modules
    .flatMap((row, y) =>
      row
        .map((isDark, x) =>
          isDark ? `M${x + border},${y + border}h1v1h-1z` : ""
        )
        .filter(Boolean)
    )
    .join("");
}

export function renderQrCodeSvg(
  qrCode: QrCode,
  options: {
    background?: string;
    foreground?: string;
    title?: string;
  } = {}
) {
  const background = options.background ?? "#fff";
  const foreground = options.foreground ?? "#111827";
  const title = options.title
    ? `<title>${escapeXml(options.title)}</title>`
    : "";

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${qrCode.viewBox}" shape-rendering="crispEdges">${title}<rect width="100%" height="100%" fill="${escapeXml(background)}"/><path d="${qrCode.path}" fill="${escapeXml(foreground)}"/></svg>`;
}

function addErrorCorrectionAndInterleave(
  data: readonly number[],
  version: number,
  errorCorrectionLevel: QrErrorCorrectionLevel
) {
  const rawCodewords = getNumRawDataModules(version) >>> 3;
  const blockEccLength =
    ECC_CODEWORDS_PER_BLOCK[errorCorrectionLevel][version]!;
  const numBlocks = NUM_ERROR_CORRECTION_BLOCKS[errorCorrectionLevel][version]!;
  const divisor = createReedSolomonDivisor(blockEccLength);
  const numShortBlocks = numBlocks - (rawCodewords % numBlocks);
  const shortBlockLength = Math.floor(rawCodewords / numBlocks);
  const blocks: number[][] = [];
  let dataOffset = 0;

  for (let i = 0; i < numBlocks; i += 1) {
    const dataLength =
      shortBlockLength - blockEccLength + (i < numShortBlocks ? 0 : 1);
    const blockData = data.slice(dataOffset, dataOffset + dataLength);
    dataOffset += dataLength;

    const errorCorrection = createReedSolomonRemainder(blockData, divisor);

    if (i < numShortBlocks) {
      blockData.push(0);
    }

    blocks.push([...blockData, ...errorCorrection]);
  }

  const result: number[] = [];

  for (let i = 0; i < blocks[0]!.length; i += 1) {
    for (let j = 0; j < blocks.length; j += 1) {
      if (i !== shortBlockLength - blockEccLength || j >= numShortBlocks) {
        result.push(blocks[j]![i]!);
      }
    }
  }

  return result;
}

function chooseVersion(
  payload: string,
  errorCorrectionLevel: QrErrorCorrectionLevel,
  minVersion: number,
  maxVersion: number
) {
  for (let version = minVersion; version <= maxVersion; version += 1) {
    try {
      createDataCodewords(payload, version, errorCorrectionLevel);
      return version;
    } catch {
      continue;
    }
  }

  throw new Error("QR payload is too long.");
}

function createDataCodewords(
  payload: string,
  version: number,
  errorCorrectionLevel: QrErrorCorrectionLevel
) {
  const bytes = [...new TextEncoder().encode(payload)];
  const characterCountBits = getByteModeCharacterCountBits(version);
  const dataCodewords = getNumDataCodewords(version, errorCorrectionLevel);
  const buffer = new BitBuffer();
  const capacityBits = dataCodewords * 8;

  if (bytes.length >= 2 ** characterCountBits) {
    throw new Error("QR payload is too long.");
  }

  buffer.append(0x4, 4);
  buffer.append(bytes.length, characterCountBits);

  for (const byte of bytes) {
    buffer.append(byte, 8);
  }

  if (buffer.length > capacityBits) {
    throw new Error("QR payload is too long.");
  }

  buffer.append(0, Math.min(4, capacityBits - buffer.length));

  while (buffer.length % 8 !== 0) {
    buffer.append(0, 1);
  }

  const result = buffer.toBytes();

  for (let pad = 0xec; result.length < dataCodewords; pad ^= 0xfd) {
    result.push(pad);
  }

  return result;
}

function createGrid(size: number, value: boolean) {
  return Array.from({ length: size }, () =>
    Array.from({ length: size }, () => value)
  );
}

function createReedSolomonDivisor(degree: number) {
  const result = Array.from({ length: degree }, () => 0);
  result[degree - 1] = 1;
  let root = 1;

  for (let i = 0; i < degree; i += 1) {
    for (let j = 0; j < degree; j += 1) {
      result[j] = finiteFieldMultiply(result[j]!, root);

      if (j + 1 < degree) {
        result[j] = result[j]! ^ result[j + 1]!;
      }
    }

    root = finiteFieldMultiply(root, 0x02);
  }

  return result;
}

function createReedSolomonRemainder(
  data: readonly number[],
  divisor: readonly number[]
) {
  const result = Array.from({ length: divisor.length }, () => 0);

  for (const byte of data) {
    const factor = byte ^ result.shift()!;
    result.push(0);

    for (let i = 0; i < result.length; i += 1) {
      result[i] = result[i]! ^ finiteFieldMultiply(divisor[i]!, factor);
    }
  }

  return result;
}

function escapeXml(value: string) {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll('"', "&quot;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;");
}

function finiteFieldMultiply(left: number, right: number) {
  let result = 0;

  for (let i = 7; i >= 0; i -= 1) {
    result = (result << 1) ^ ((result >>> 7) * 0x11d);
    result ^= ((right >>> i) & 1) * left;
  }

  return result & 0xff;
}

function getAlignmentPatternPositions(version: number) {
  if (version === 1) {
    return [];
  }

  const size = getQrSize(version);
  const numPositions = Math.floor(version / 7) + 2;
  const step =
    version === 32
      ? 26
      : Math.ceil((version * 4 + 4) / (numPositions * 2 - 2)) * 2;
  const result = [6];

  for (
    let position = size - 7;
    result.length < numPositions;
    position -= step
  ) {
    result.splice(1, 0, position);
  }

  return result;
}

function getBit(value: number, index: number) {
  return ((value >>> index) & 1) > 0;
}

function getByteModeCharacterCountBits(version: number) {
  return version <= 9 ? 8 : 16;
}

function getMaskBit(mask: number, x: number, y: number) {
  switch (mask) {
    case 0:
      return (x + y) % 2 === 0;
    case 1:
      return y % 2 === 0;
    case 2:
      return x % 3 === 0;
    case 3:
      return (x + y) % 3 === 0;
    case 4:
      return (Math.floor(y / 2) + Math.floor(x / 3)) % 2 === 0;
    case 5:
      return ((x * y) % 2) + ((x * y) % 3) === 0;
    case 6:
      return (((x * y) % 2) + ((x * y) % 3)) % 2 === 0;
    case 7:
      return (((x + y) % 2) + ((x * y) % 3)) % 2 === 0;
    default:
      throw new Error("QR mask is invalid.");
  }
}

function getNumDataCodewords(
  version: number,
  errorCorrectionLevel: QrErrorCorrectionLevel
) {
  return (
    (getNumRawDataModules(version) >>> 3) -
    ECC_CODEWORDS_PER_BLOCK[errorCorrectionLevel][version]! *
      NUM_ERROR_CORRECTION_BLOCKS[errorCorrectionLevel][version]!
  );
}

function getNumRawDataModules(version: number) {
  let result = (16 * version + 128) * version + 64;

  if (version >= 2) {
    const numAlign = Math.floor(version / 7) + 2;
    result -= (25 * numAlign - 10) * numAlign - 55;

    if (version >= 7) {
      result -= 36;
    }
  }

  return result;
}

function getQrSize(version: number) {
  return version * 4 + 17;
}

function getRunPenalty(row: readonly boolean[]) {
  let result = 0;
  let runColor = row[0]!;
  let runLength = 1;

  for (let i = 1; i < row.length; i += 1) {
    if (row[i] === runColor) {
      runLength += 1;
      continue;
    }

    if (runLength >= 5) {
      result += runLength - 2;
    }

    runColor = row[i]!;
    runLength = 1;
  }

  if (runLength >= 5) {
    result += runLength - 2;
  }

  return result;
}

function isLightRun(modules: readonly boolean[]) {
  return modules.length >= 4 && modules.every(module => !module);
}

function matchesPattern(
  modules: readonly boolean[],
  pattern: readonly boolean[]
) {
  return modules.every((module, index) => module === pattern[index]);
}

function validateVersionRange(minVersion: number, maxVersion: number) {
  if (
    !Number.isInteger(minVersion) ||
    !Number.isInteger(maxVersion) ||
    minVersion < 1 ||
    maxVersion > 40 ||
    minVersion > maxVersion
  ) {
    throw new Error("QR version range must be between 1 and 40.");
  }
}
