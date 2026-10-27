using System;
using NesEmulator.Mappers;

namespace NesEmulator.Core
{
    public class Ppu
    {
        public const int ScreenWidth = 256;
        public const int ScreenHeight = 240;

        // Raw 32-bit ARGB frame buffer
        public readonly int[] FrameBuffer = new int[ScreenWidth * ScreenHeight];

        // 2KB internal VRAM for Nametables
        private readonly byte[] _vram = new byte[2048];

        // 32-byte Palette RAM
        private readonly byte[] _paletteRam = new byte[32];

        // 256-byte OAM (Sprite RAM)
        public readonly byte[] Oam = new byte[256];

        public Cartridge Cartridge { get; set; }
        public Cpu Cpu { get; set; }

        // Loopy registers
        private ushort _v; // Current VRAM address (15 bits)
        private ushort _t; // Temporary VRAM address (15 bits)
        private byte _x;   // Fine X scroll (3 bits)
        private bool _w;   // Write toggle latch

        // PPU Registers
        private byte _ppuctrl;
        private byte _ppumask;
        private byte _ppustatus;
        private byte _oamaddr;
        private byte _dataBuffer;

        // Timing
        public int Scanline { get; private set; }
        public int Cycle { get; private set; }
        public bool FrameComplete { get; set; }
        public long FrameCount { get; private set; }

        // Control properties
        private bool NmiEnable { get { return (_ppuctrl & 0x80) != 0; } }
        private bool SpriteSize16 { get { return (_ppuctrl & 0x20) != 0; } }
        private ushort BgPatternTable { get { return (ushort)((_ppuctrl & 0x10) != 0 ? 0x1000 : 0x0000); } }
        private ushort SpritePatternTable { get { return (ushort)((_ppuctrl & 0x08) != 0 ? 0x1000 : 0x0000); } }
        private int VramIncrement { get { return (_ppuctrl & 0x04) != 0 ? 32 : 1; } }

        private bool ShowBg { get { return (_ppumask & 0x08) != 0; } }
        private bool ShowSpr { get { return (_ppumask & 0x10) != 0; } }
        private bool ShowBgLeft { get { return (_ppumask & 0x02) != 0; } }
        private bool ShowSprLeft { get { return (_ppumask & 0x04) != 0; } }
        private bool RenderingEnabled { get { return ShowBg || ShowSpr; } }

        // Line pixel buffers for compositing
        private readonly byte[] _bgPixel = new byte[ScreenWidth];
        private readonly byte[] _bgPalette = new byte[ScreenWidth];

        // Standard NES 64-color palette in 0xAARRGGBB format
        public static readonly int[] NesPalette = new int[]
        {
            unchecked((int)0xFF666666), unchecked((int)0xFF002A88), unchecked((int)0xFF1412A7), unchecked((int)0xFF3B00A4),
            unchecked((int)0xFF5C007E), unchecked((int)0xFF6E0040), unchecked((int)0xFF6C0600), unchecked((int)0xFF561D00),
            unchecked((int)0xFF333500), unchecked((int)0xFF0B4800), unchecked((int)0xFF005200), unchecked((int)0xFF004F08),
            unchecked((int)0xFF00404D), unchecked((int)0xFF000000), unchecked((int)0xFF000000), unchecked((int)0xFF000000),

            unchecked((int)0xFFADADAD), unchecked((int)0xFF155FD9), unchecked((int)0xFF4240FF), unchecked((int)0xFF7527FE),
            unchecked((int)0xFFA01ACC), unchecked((int)0xFFB71E7B), unchecked((int)0xFFB53120), unchecked((int)0xFF994E00),
            unchecked((int)0xFF6B6D00), unchecked((int)0xFF388700), unchecked((int)0xFF0C9300), unchecked((int)0xFF008F32),
            unchecked((int)0xFF007C8D), unchecked((int)0xFF000000), unchecked((int)0xFF000000), unchecked((int)0xFF000000),

            unchecked((int)0xFFFFFEFF), unchecked((int)0xFF64B0FF), unchecked((int)0xFF9290FF), unchecked((int)0xFFC676FF),
            unchecked((int)0xFFF36AFF), unchecked((int)0xFFFE6ECC), unchecked((int)0xFFFE8170), unchecked((int)0xFFEA9E22),
            unchecked((int)0xFFBCBE00), unchecked((int)0xFF88D800), unchecked((int)0xFF5CE430), unchecked((int)0xFF45E082),
            unchecked((int)0xFF48CDDE), unchecked((int)0xFF4F4F4F), unchecked((int)0xFF000000), unchecked((int)0xFF000000),

            unchecked((int)0xFFFFFEFF), unchecked((int)0xFFC0DFFF), unchecked((int)0xFFD3D2FF), unchecked((int)0xFFE8C8FF),
            unchecked((int)0xFFFBC2FF), unchecked((int)0xFFFEC4EA), unchecked((int)0xFFFECCC5), unchecked((int)0xFFF7D8A5),
            unchecked((int)0xFFE4E594), unchecked((int)0xFFCFEF96), unchecked((int)0xFFBDF4AB), unchecked((int)0xFFB3F3CC),
            unchecked((int)0xFFB5EBF2), unchecked((int)0xFFB8B8B8), unchecked((int)0xFF000000), unchecked((int)0xFF000000)
        };

        public Ppu()
        {
            Reset();
        }

        public void Reset()
        {
            _v = 0;
            _t = 0;
            _x = 0;
            _w = false;
            _ppuctrl = 0;
            _ppumask = 0;
            _ppustatus = 0;
            _oamaddr = 0;
            _dataBuffer = 0;
            Scanline = 0;
            Cycle = 0;
            FrameCount = 0;
            FrameComplete = false;
            Array.Clear(FrameBuffer, 0, FrameBuffer.Length);
            Array.Clear(_vram, 0, _vram.Length);
            Array.Clear(_paletteRam, 0, _paletteRam.Length);
            Array.Clear(Oam, 0, Oam.Length);
        }

        public byte CpuRead(ushort addr)
        {
            byte res = 0;
            switch (addr & 0x07)
            {
                case 0x02: // PPUSTATUS
                    res = (byte)((_ppustatus & 0xE0) | (_dataBuffer & 0x1F));
                    _ppustatus &= 0x7F; // Clear VBlank flag
                    _w = false;         // Clear write latch
                    break;

                case 0x04: // OAMDATA
                    res = Oam[_oamaddr];
                    break;

                case 0x07: // PPUDATA
                    res = _dataBuffer;
                    _dataBuffer = PpuRead(_v);

                    // Palette reads are unbuffered
                    if (_v >= 0x3F00)
                    {
                        res = _dataBuffer;
                    }

                    _v = (ushort)((_v + VramIncrement) & 0x7FFF);
                    break;
            }
            return res;
        }

        public void CpuWrite(ushort addr, byte val)
        {
            switch (addr & 0x07)
            {
                case 0x00: // PPUCTRL
                    _ppuctrl = val;
                    // Nametable select bits 0-1 into bits 10-11 of t
                    _t = (ushort)((_t & 0x73FF) | ((val & 0x03) << 10));
                    break;

                case 0x01: // PPUMASK
                    _ppumask = val;
                    break;

                case 0x03: // OAMADDR
                    _oamaddr = val;
                    break;

                case 0x04: // OAMDATA
                    Oam[_oamaddr++] = val;
                    break;

                case 0x05: // PPUSCROLL
                    if (!_w)
                    {
                        // 1st write: fine X and coarse X
                        _x = (byte)(val & 0x07);
                        _t = (ushort)((_t & 0x7FE0) | ((val >> 3) & 0x1F));
                        _w = true;
                    }
                    else
                    {
                        // 2nd write: fine Y and coarse Y
                        _t = (ushort)((_t & 0x0C1F) | ((val & 0x07) << 12) | ((val & 0xF8) << 2));
                        _w = false;
                    }
                    break;

                case 0x06: // PPUADDR
                    if (!_w)
                    {
                        // 1st write: High 6 bits of address
                        _t = (ushort)((_t & 0x00FF) | ((val & 0x3F) << 8));
                        _w = true;
                    }
                    else
                    {
                        // 2nd write: Low 8 bits of address
                        _t = (ushort)((_t & 0x7F00) | val);
                        _v = _t;
                        _w = false;
                    }
                    break;

                case 0x07: // PPUDATA
                    PpuWrite(_v, val);
                    _v = (ushort)((_v + VramIncrement) & 0x7FFF);
                    break;
            }
        }

        public byte PpuRead(ushort addr)
        {
            addr &= 0x3FFF;
            byte data = 0;

            if (Cartridge != null && Cartridge.PpuRead(addr, ref data))
            {
                return data;
            }

            if (addr >= 0x2000 && addr <= 0x3EFF)
            {
                ushort mirrored = MirrorAddress(addr);
                return _vram[mirrored];
            }

            if (addr >= 0x3F00 && addr <= 0x3FFF)
            {
                addr &= 0x001F;
                if (addr == 0x10 || addr == 0x14 || addr == 0x18 || addr == 0x1C)
                    addr &= 0x000F;
                return _paletteRam[addr];
            }

            return 0;
        }

        public void PpuWrite(ushort addr, byte data)
        {
            addr &= 0x3FFF;

            if (Cartridge != null && Cartridge.PpuWrite(addr, data))
            {
                return;
            }

            if (addr >= 0x2000 && addr <= 0x3EFF)
            {
                ushort mirrored = MirrorAddress(addr);
                _vram[mirrored] = data;
                return;
            }

            if (addr >= 0x3F00 && addr <= 0x3FFF)
            {
                addr &= 0x001F;
                if (addr == 0x10 || addr == 0x14 || addr == 0x18 || addr == 0x1C)
                    addr &= 0x000F;
                _paletteRam[addr] = data;
            }
        }

        private ushort MirrorAddress(ushort addr)
        {
            MirroringMode mode = Cartridge != null ? Cartridge.Mirroring : MirroringMode.Horizontal;
            addr = (ushort)((addr - 0x2000) & 0x0FFF);
            int nt = addr / 0x0400;
            int offset = addr % 0x0400;

            switch (mode)
            {
                case MirroringMode.Horizontal:
                    // 0 & 1 map to 0; 2 & 3 map to 1
                    return (ushort)(((nt >= 2 ? 1 : 0) * 1024) + offset);

                case MirroringMode.Vertical:
                    // 0 & 2 map to 0; 1 & 3 map to 1
                    return (ushort)(((nt & 1) * 1024) + offset);

                case MirroringMode.SingleScreenLow:
                    return (ushort)offset;

                case MirroringMode.SingleScreenHigh:
                    return (ushort)(1024 + offset);

                case MirroringMode.FourScreen:
                    return (ushort)(addr & 0x07FF);

                default:
                    return (ushort)(offset);
            }
        }

        public void Step()
        {
            Cycle++;

            if (Cycle == 260 && Scanline < 240 && RenderingEnabled)
            {
                if (Cartridge != null && Cartridge.MapperInstance != null)
                {
                    Cartridge.MapperInstance.Scanline();
                }
            }

            if (Cycle >= 341)
            {
                Cycle = 0;
                Scanline++;

                if (Scanline == 241)
                {
                    // VBlank start
                    _ppustatus |= 0x80;
                    if (NmiEnable && Cpu != null)
                    {
                        Cpu.Nmi();
                    }
                    FrameComplete = true;
                }
                else if (Scanline == 261)
                {
                    // Pre-render line
                    _ppustatus &= 0x1F; // Clear VBlank, Sprite0 Hit, Overflow
                }
                else if (Scanline >= 262)
                {
                    Scanline = 0;
                    FrameCount++;
                }

                // Render scanline at end of line 0..239
                if (Scanline >= 0 && Scanline < 240 && Cycle == 0)
                {
                    RenderScanline(Scanline);
                }
            }
        }

        private void RenderScanline(int scanline)
        {
            Array.Clear(_bgPixel, 0, ScreenWidth);
            Array.Clear(_bgPalette, 0, ScreenWidth);

            int backdropColor = NesPalette[PpuRead(0x3F00) & 0x3F];
            int lineOffset = scanline * ScreenWidth;
            for (int i = 0; i < ScreenWidth; i++)
            {
                FrameBuffer[lineOffset + i] = backdropColor;
            }

            if (ShowBg)
            {
                RenderBackgroundLine(scanline);
            }

            if (ShowSpr)
            {
                RenderSpriteLine(scanline);
            }
        }

        private void RenderBackgroundLine(int scanline)
        {
            // Background rendering along current scanline
            int fineY = (scanline + ((_t >> 12) & 0x07)) & 0x07;
            int coarseY = (scanline / 8 + ((_t >> 5) & 0x1F)) % 30;
            int baseNt = (_t >> 10) & 0x03;

            int lineOffset = scanline * ScreenWidth;

            for (int x = 0; x < ScreenWidth; x++)
            {
                if (x < 8 && !ShowBgLeft)
                    continue;

                int scrollX = x + ((_t & 0x1F) * 8) + _x;
                int coarseX = (scrollX / 8) % 32;
                int fineX = scrollX & 0x07;

                int nt = baseNt;
                if ((scrollX / 256) % 2 != 0)
                    nt ^= 1;

                ushort ntAddr = (ushort)(0x2000 + nt * 0x0400 + coarseY * 32 + coarseX);
                byte tileIndex = PpuRead(ntAddr);

                ushort attrAddr = (ushort)(0x23C0 + nt * 0x0400 + (coarseY / 4) * 8 + (coarseX / 4));
                byte attr = PpuRead(attrAddr);
                int paletteIndex = (attr >> (((coarseY % 4) / 2) * 4 + ((coarseX % 4) / 2) * 2)) & 0x03;

                ushort tileAddr = (ushort)(BgPatternTable + tileIndex * 16 + fineY);
                byte lo = PpuRead(tileAddr);
                byte hi = PpuRead((ushort)(tileAddr + 8));

                int bit = 7 - fineX;
                byte pixel = (byte)((((hi >> bit) & 1) << 1) | ((lo >> bit) & 1));

                _bgPixel[x] = pixel;
                _bgPalette[x] = (byte)paletteIndex;

                if (pixel != 0)
                {
                    ushort palAddr = (ushort)(0x3F00 + paletteIndex * 4 + pixel);
                    FrameBuffer[lineOffset + x] = NesPalette[PpuRead(palAddr) & 0x3F];
                }
            }
        }

        private void RenderSpriteLine(int scanline)
        {
            int lineOffset = scanline * ScreenWidth;
            int spriteHeight = SpriteSize16 ? 16 : 8;

            int renderedCount = 0;

            for (int i = 0; i < 64; i++)
            {
                int spriteIdx = i * 4;
                int spriteY = Oam[spriteIdx] + 1;
                int tileIndex = Oam[spriteIdx + 1];
                byte attributes = Oam[spriteIdx + 2];
                int spriteX = Oam[spriteIdx + 3];

                int row = scanline - spriteY;
                if (row < 0 || row >= spriteHeight)
                    continue;

                renderedCount++;
                if (renderedCount > 8)
                {
                    _ppustatus |= 0x20; // Sprite overflow
                }

                bool flipH = (attributes & 0x40) != 0;
                bool flipV = (attributes & 0x80) != 0;
                bool priority = (attributes & 0x20) == 0; // 0 = in front of background
                int palette = (attributes & 0x03) + 4;

                if (flipV)
                    row = spriteHeight - 1 - row;

                ushort tileAddr;
                if (!SpriteSize16)
                {
                    tileAddr = (ushort)(SpritePatternTable + tileIndex * 16 + row);
                }
                else
                {
                    int table = (tileIndex & 1) * 0x1000;
                    int actualTile = tileIndex & 0xFE;
                    if (row >= 8)
                    {
                        actualTile++;
                        row -= 8;
                    }
                    tileAddr = (ushort)(table + actualTile * 16 + row);
                }

                byte lo = PpuRead(tileAddr);
                byte hi = PpuRead((ushort)(tileAddr + 8));

                for (int col = 0; col < 8; col++)
                {
                    int x = spriteX + col;
                    if (x >= ScreenWidth)
                        break;

                    if (x < 8 && !ShowSprLeft)
                        continue;

                    int bit = flipH ? col : (7 - col);
                    byte pixel = (byte)((((hi >> bit) & 1) << 1) | ((lo >> bit) & 1));

                    if (pixel == 0)
                        continue;

                    // Sprite 0 Hit detection
                    if (i == 0 && ShowBg && _bgPixel[x] != 0 && x != 255)
                    {
                        _ppustatus |= 0x40;
                    }

                    if (priority || _bgPixel[x] == 0)
                    {
                        ushort palAddr = (ushort)(0x3F00 + palette * 4 + pixel);
                        FrameBuffer[lineOffset + x] = NesPalette[PpuRead(palAddr) & 0x3F];
                    }
                }
            }
        }
    }
}
