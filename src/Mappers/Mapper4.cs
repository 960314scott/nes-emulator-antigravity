using System;

namespace NesEmulator.Mappers
{
    // Mapper 4 (MMC3)
    // Used in: Super Mario Bros. 3, Mega Man 3-6, Kirby's Adventure
    public class Mapper4 : Mapper
    {
        private byte _targetRegister = 0;
        private bool _prgBankMode = false;
        private bool _chrInversion = false;

        private readonly int[] _registers = new int[8];
        private readonly uint[] _chrOffsets = new uint[8];
        private readonly uint[] _prgOffsets = new uint[4];

        private bool _irqEnabled = false;
        private byte _irqPeriod = 0;
        private byte _irqCounter = 0;
        private bool _irqReload = false;
        private bool _irqState = false;

        private readonly byte[] _prgRam = new byte[8 * 1024];

        public override bool IrqState { get { return _irqState; } }

        public Mapper4(byte prgBanks, byte chrBanks, MirroringMode initialMirroring)
            : base(prgBanks, chrBanks, initialMirroring)
        {
            _prgOffsets[0] = 0;
            _prgOffsets[1] = 0x2000;
            _prgOffsets[2] = (uint)((prgBanks * 2 - 2) * 0x2000);
            _prgOffsets[3] = (uint)((prgBanks * 2 - 1) * 0x2000);
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
                int bank = (addr - 0x8000) / 0x2000;
                mappedAddr = _prgOffsets[bank] + (uint)(addr & 0x1FFF);
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

            if (addr >= 0x8000 && addr <= 0x9FFF)
            {
                if ((addr & 1) == 0) // 0x8000 Even
                {
                    _targetRegister = (byte)(data & 0x07);
                    _prgBankMode = (data & 0x40) != 0;
                    _chrInversion = (data & 0x80) != 0;
                }
                else // 0x8001 Odd
                {
                    _registers[_targetRegister] = data;
                    UpdateOffsets();
                }
            }
            else if (addr >= 0xA000 && addr <= 0xBFFF)
            {
                if ((addr & 1) == 0) // 0xA000 Even: Mirroring
                {
                    Mirroring = (data & 1) == 0 ? MirroringMode.Vertical : MirroringMode.Horizontal;
                }
            }
            else if (addr >= 0xC000 && addr <= 0xDFFF)
            {
                if ((addr & 1) == 0) // 0xC000 Even: IRQ Latch
                {
                    _irqPeriod = data;
                }
                else // 0xC001 Odd: IRQ Reload
                {
                    _irqReload = true;
                }
            }
            else if (addr >= 0xE000 && addr <= 0xFFFF)
            {
                if ((addr & 1) == 0) // 0xE000 Even: IRQ Disable
                {
                    _irqEnabled = false;
                    _irqState = false;
                }
                else // 0xE001 Odd: IRQ Enable
                {
                    _irqEnabled = true;
                }
            }

            return false;
        }

        private void UpdateOffsets()
        {
            if (!_chrInversion)
            {
                _chrOffsets[0] = (uint)((_registers[0] & ~1) * 0x0400);
                _chrOffsets[1] = (uint)((_registers[0] | 1) * 0x0400);
                _chrOffsets[2] = (uint)((_registers[1] & ~1) * 0x0400);
                _chrOffsets[3] = (uint)((_registers[1] | 1) * 0x0400);
                _chrOffsets[4] = (uint)(_registers[2] * 0x0400);
                _chrOffsets[5] = (uint)(_registers[3] * 0x0400);
                _chrOffsets[6] = (uint)(_registers[4] * 0x0400);
                _chrOffsets[7] = (uint)(_registers[5] * 0x0400);
            }
            else
            {
                _chrOffsets[4] = (uint)((_registers[0] & ~1) * 0x0400);
                _chrOffsets[5] = (uint)((_registers[0] | 1) * 0x0400);
                _chrOffsets[6] = (uint)((_registers[1] & ~1) * 0x0400);
                _chrOffsets[7] = (uint)((_registers[1] | 1) * 0x0400);
                _chrOffsets[0] = (uint)(_registers[2] * 0x0400);
                _chrOffsets[1] = (uint)(_registers[3] * 0x0400);
                _chrOffsets[2] = (uint)(_registers[4] * 0x0400);
                _chrOffsets[3] = (uint)(_registers[5] * 0x0400);
            }

            int numPrg8k = PrgBanks * 2;
            if (!_prgBankMode)
            {
                _prgOffsets[0] = (uint)((_registers[6] & (numPrg8k - 1)) * 0x2000);
                _prgOffsets[1] = (uint)((_registers[7] & (numPrg8k - 1)) * 0x2000);
                _prgOffsets[2] = (uint)((numPrg8k - 2) * 0x2000);
                _prgOffsets[3] = (uint)((numPrg8k - 1) * 0x2000);
            }
            else
            {
                _prgOffsets[0] = (uint)((numPrg8k - 2) * 0x2000);
                _prgOffsets[1] = (uint)((_registers[7] & (numPrg8k - 1)) * 0x2000);
                _prgOffsets[2] = (uint)((_registers[6] & (numPrg8k - 1)) * 0x2000);
                _prgOffsets[3] = (uint)((numPrg8k - 1) * 0x2000);
            }
        }

        public override bool PpuMapRead(ushort addr, out uint mappedAddr)
        {
            if (addr < 0x2000)
            {
                int bank = addr / 0x0400;
                mappedAddr = _chrOffsets[bank] + (uint)(addr & 0x03FF);
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

        public override void Scanline()
        {
            if (_irqCounter == 0 || _irqReload)
            {
                _irqCounter = _irqPeriod;
                _irqReload = false;
            }
            else
            {
                _irqCounter--;
            }

            if (_irqCounter == 0 && _irqEnabled)
            {
                _irqState = true;
            }
        }

        public override void ClearIrq()
        {
            _irqState = false;
        }

        public override void Reset()
        {
            _targetRegister = 0;
            _prgBankMode = false;
            _chrInversion = false;
            _irqEnabled = false;
            _irqPeriod = 0;
            _irqCounter = 0;
            _irqReload = false;
            _irqState = false;
            Array.Clear(_registers, 0, _registers.Length);
            UpdateOffsets();
        }
    }
}
