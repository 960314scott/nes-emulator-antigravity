using System;

namespace NesEmulator.Mappers
{
    // Mapper 3 (CNROM)
    // Used in: Solomon's Key, Adventure Island, Arkanoid
    public class Mapper3 : Mapper
    {
        private byte _chrBankSelect = 0;

        public Mapper3(byte prgBanks, byte chrBanks, MirroringMode initialMirroring)
            : base(prgBanks, chrBanks, initialMirroring)
        {
        }

        public override bool CpuMapRead(ushort addr, out uint mappedAddr, ref byte data)
        {
            if (addr >= 0x8000)
            {
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
                _chrBankSelect = (byte)(data & 0x03);
            }
            mappedAddr = 0;
            return false;
        }

        public override bool PpuMapRead(ushort addr, out uint mappedAddr)
        {
            if (addr < 0x2000)
            {
                mappedAddr = (uint)(_chrBankSelect * 0x2000 + addr);
                return true;
            }
            mappedAddr = 0;
            return false;
        }

        public override bool PpuMapWrite(ushort addr, out uint mappedAddr)
        {
            mappedAddr = 0;
            return false;
        }

        public override void Reset()
        {
            _chrBankSelect = 0;
        }
    }
}
