using System;

namespace NesEmulator.Mappers
{
    // Mapper 7 (AxROM)
    // Used in: Battletoads, Marble Madness, Rare games
    public class Mapper7 : Mapper
    {
        private byte _prgBank = 0;

        public Mapper7(byte prgBanks, byte chrBanks, MirroringMode initialMirroring)
            : base(prgBanks, chrBanks, initialMirroring)
        {
            Mirroring = MirroringMode.SingleScreenLow;
        }

        public override bool CpuMapRead(ushort addr, out uint mappedAddr, ref byte data)
        {
            if (addr >= 0x8000)
            {
                mappedAddr = (uint)(_prgBank * 0x8000 + (addr & 0x7FFF));
                return true;
            }
            mappedAddr = 0;
            return false;
        }

        public override bool CpuMapWrite(ushort addr, out uint mappedAddr, byte data)
        {
            if (addr >= 0x8000)
            {
                _prgBank = (byte)(data & 0x07);
                Mirroring = (data & 0x10) != 0 ? MirroringMode.SingleScreenHigh : MirroringMode.SingleScreenLow;
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
            _prgBank = 0;
            Mirroring = MirroringMode.SingleScreenLow;
        }
    }
}
