using System;

namespace NesEmulator.Mappers
{
    // Mapper 2 (UxROM)
    // Used in: Contra, Castlevania, DuckTales, Mega Man
    public class Mapper2 : Mapper
    {
        private byte _prgBankSelect = 0;

        public Mapper2(byte prgBanks, byte chrBanks, MirroringMode initialMirroring)
            : base(prgBanks, chrBanks, initialMirroring)
        {
        }

        public override bool CpuMapRead(ushort addr, out uint mappedAddr, ref byte data)
        {
            if (addr >= 0x8000 && addr <= 0xBFFF)
            {
                mappedAddr = (uint)(_prgBankSelect * 0x4000 + (addr & 0x3FFF));
                return true;
            }
            if (addr >= 0xC000)
            {
                // Fixed to last 16KB bank
                mappedAddr = (uint)((PrgBanks - 1) * 0x4000 + (addr & 0x3FFF));
                return true;
            }

            mappedAddr = 0;
            return false;
        }

        public override bool CpuMapWrite(ushort addr, out uint mappedAddr, byte data)
        {
            if (addr >= 0x8000)
            {
                _prgBankSelect = (byte)(data & 0x0F);
            }
            mappedAddr = 0;
            return false;
        }

        public override bool PpuMapRead(ushort addr, out uint mappedAddr)
        {
            if (addr < 0x2000)
            {
                mappedAddr = addr;
                return true;
            }
            mappedAddr = 0;
            return false;
        }

        public override bool PpuMapWrite(ushort addr, out uint mappedAddr)
        {
            if (addr < 0x2000)
            {
                if (ChrBanks == 0) // CHR RAM
                {
                    mappedAddr = addr;
                    return true;
                }
            }
            mappedAddr = 0;
            return false;
        }

        public override void Reset()
        {
            _prgBankSelect = 0;
        }
    }
}
