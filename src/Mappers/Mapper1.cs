using System;

namespace NesEmulator.Mappers
{
    // Mapper 1 (MMC1)
    // Used in: The Legend of Zelda, Metroid, Mega Man 2
    public class Mapper1 : Mapper
    {
        private byte _shiftRegister = 0x10;
        private byte _control = 0x0C;

        private byte _chrBank0 = 0;
        private byte _chrBank1 = 0;
        private byte _prgBank = 0;

        private readonly byte[] _prgRam = new byte[32 * 1024]; // 32KB PRG RAM support

        public Mapper1(byte prgBanks, byte chrBanks, MirroringMode initialMirroring)
            : base(prgBanks, chrBanks, initialMirroring)
        {
        }

        public override bool CpuMapRead(ushort addr, out uint mappedAddr, ref byte data)
        {
            if (addr >= 0x6000 && addr <= 0x7FFF)
            {
                data = _prgRam[addr - 0x6000];
                mappedAddr = 0xFFFFFFFF;
                return true;
            }

            if (addr >= 0x8000)
            {
                int prgMode = (_control >> 2) & 3;
                if (prgMode == 0 || prgMode == 1)
                {
                    // 32KB mode
                    mappedAddr = (uint)((_prgBank & ~1) * 0x4000 + (addr & 0x7FFF));
                }
                else if (prgMode == 2)
                {
                    // Fix first bank at 0x8000, switch 0xC000
                    if (addr < 0xC000)
                        mappedAddr = (uint)(addr & 0x3FFF);
                    else
                        mappedAddr = (uint)(_prgBank * 0x4000 + (addr & 0x3FFF));
                }
                else // prgMode == 3
                {
                    // Switch 0x8000, fix last bank at 0xC000
                    if (addr < 0xC000)
                        mappedAddr = (uint)(_prgBank * 0x4000 + (addr & 0x3FFF));
                    else
                        mappedAddr = (uint)((PrgBanks - 1) * 0x4000 + (addr & 0x3FFF));
                }
                return true;
            }

            mappedAddr = 0;
            return false;
        }

        public override bool CpuMapWrite(ushort addr, out uint mappedAddr, byte data)
        {
            mappedAddr = 0;
            if (addr >= 0x6000 && addr <= 0x7FFF)
            {
                _prgRam[addr - 0x6000] = data;
                return false;
            }

            if (addr >= 0x8000)
            {
                if ((data & 0x80) != 0)
                {
                    _shiftRegister = 0x10;
                    _control |= 0x0C;
                    UpdateMirroring();
                }
                else
                {
                    bool full = (_shiftRegister & 1) != 0;
                    _shiftRegister = (byte)((_shiftRegister >> 1) | ((data & 1) << 4));

                    if (full)
                    {
                        byte regData = _shiftRegister;
                        _shiftRegister = 0x10;

                        if (addr < 0xA000) // 0x8000 - 0x9FFF: Control
                        {
                            _control = (byte)(regData & 0x1F);
                            UpdateMirroring();
                        }
                        else if (addr < 0xC000) // 0xA000 - 0xBFFF: CHR Bank 0
                        {
                            _chrBank0 = (byte)(regData & 0x1F);
                        }
                        else if (addr < 0xE000) // 0xC000 - 0xDFFF: CHR Bank 1
                        {
                            _chrBank1 = (byte)(regData & 0x1F);
                        }
                        else // 0xE000 - 0xFFFF: PRG Bank
                        {
                            _prgBank = (byte)(regData & 0x0F);
                        }
                    }
                }
            }

            return false;
        }

        private void UpdateMirroring()
        {
            switch (_control & 3)
            {
                case 0: Mirroring = MirroringMode.SingleScreenLow; break;
                case 1: Mirroring = MirroringMode.SingleScreenHigh; break;
                case 2: Mirroring = MirroringMode.Vertical; break;
                case 3: Mirroring = MirroringMode.Horizontal; break;
            }
        }

        public override bool PpuMapRead(ushort addr, out uint mappedAddr)
        {
            if (addr < 0x2000)
            {
                if (ChrBanks == 0)
                {
                    mappedAddr = addr;
                    return true;
                }

                if ((_control & 0x10) != 0) // 4KB CHR mode
                {
                    if (addr < 0x1000)
                        mappedAddr = (uint)(_chrBank0 * 0x1000 + (addr & 0x0FFF));
                    else
                        mappedAddr = (uint)(_chrBank1 * 0x1000 + (addr & 0x0FFF));
                }
                else // 8KB CHR mode
                {
                    mappedAddr = (uint)((_chrBank0 & ~1) * 0x1000 + (addr & 0x1FFF));
                }
                return true;
            }

            mappedAddr = 0;
            return false;
        }

        public override bool PpuMapWrite(ushort addr, out uint mappedAddr)
        {
            if (addr < 0x2000 && ChrBanks == 0)
            {
                mappedAddr = addr;
                return true;
            }
            mappedAddr = 0;
            return false;
        }

        public override void Reset()
        {
            _shiftRegister = 0x10;
            _control = 0x0C;
            _chrBank0 = 0;
            _chrBank1 = 0;
            _prgBank = 0;
            UpdateMirroring();
        }
    }
}
