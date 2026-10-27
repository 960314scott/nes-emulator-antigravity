using System;

namespace NesEmulator.Mappers
{
    // Mapper 0 (NROM)
    // Used in: Super Mario Bros., Pac-Man, Donkey Kong, AccuracyCoin
    public class Mapper0 : Mapper
    {
        public Mapper0(byte prgBanks, byte chrBanks, MirroringMode initialMirroring)
            : base(prgBanks, chrBanks, initialMirroring)
        {
        }

        public override bool CpuMapRead(ushort addr, out uint mappedAddr, ref byte data)
        {
            if (addr >= 0x8000)
            {
                // If 16KB PRG (1 bank), mirror 0x8000-0xBFFF into 0xC000-0xFFFF
                mappedAddr = (uint)(addr & (PrgBanks > 1 ? 0x7FFF : 0x3FFF));
                return true;
            }
            mappedAddr = 0;
            return false;
        }

        public override bool CpuMapWrite(ushort addr, out uint mappedAddr, byte data)
        {
            if (addr >= 0x8000)
            {
                mappedAddr = (uint)(addr & (PrgBanks > 1 ? 0x7FFF : 0x3FFF));
                return false; // NROM PRG is ROM, cannot write
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
    }
}
