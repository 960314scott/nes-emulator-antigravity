using System;
using System.IO;
using NesEmulator.Mappers;

namespace NesEmulator.Core
{
    public class Cartridge
    {
        public byte[] PrgRom { get; private set; }
        public byte[] ChrRom { get; private set; }
        public byte MapperId { get; private set; }
        public byte PrgBanks { get; private set; }
        public byte ChrBanks { get; private set; }
        public bool IsValid { get; private set; }
        public string RomPath { get; private set; }

        public Mapper MapperInstance { get; private set; }

        public MirroringMode Mirroring
        {
            get { return MapperInstance != null ? MapperInstance.Mirroring : MirroringMode.Horizontal; }
        }

        public Cartridge(string filePath)
        {
            RomPath = filePath;
            IsValid = false;

            if (!File.Exists(filePath))
                return;

            byte[] data = File.ReadAllBytes(filePath);
            if (data.Length < 16)
                return;

            // Check iNES Magic: "NES\x1A"
            if (data[0] != 'N' || data[1] != 'E' || data[2] != 'S' || data[3] != 0x1A)
                return;

            PrgBanks = data[4];
            ChrBanks = data[5];

            byte flags6 = data[6];
            byte flags7 = data[7];

            MapperId = (byte)(((flags7 & 0xF0)) | (flags6 >> 4));

            MirroringMode initialMirroring = (flags6 & 0x01) != 0 ? MirroringMode.Vertical : MirroringMode.Horizontal;
            if ((flags6 & 0x08) != 0)
                initialMirroring = MirroringMode.FourScreen;

            int offset = 16;
            // Skip 512 bytes trainer if present
            if ((flags6 & 0x04) != 0)
                offset += 512;

            int prgSize = PrgBanks * 16384;
            PrgRom = new byte[prgSize];
            Array.Copy(data, offset, PrgRom, 0, Math.Min(prgSize, data.Length - offset));
            offset += prgSize;

            int chrSize = ChrBanks == 0 ? 8192 : ChrBanks * 8192;
            ChrRom = new byte[chrSize];
            if (ChrBanks > 0 && offset < data.Length)
            {
                Array.Copy(data, offset, ChrRom, 0, Math.Min(ChrBanks * 8192, data.Length - offset));
            }

            switch (MapperId)
            {
                case 0:
                    MapperInstance = new Mapper0(PrgBanks, ChrBanks, initialMirroring);
                    break;
                case 1:
                    MapperInstance = new Mapper1(PrgBanks, ChrBanks, initialMirroring);
                    break;
                case 2:
                    MapperInstance = new Mapper2(PrgBanks, ChrBanks, initialMirroring);
                    break;
                case 3:
                    MapperInstance = new Mapper3(PrgBanks, ChrBanks, initialMirroring);
                    break;
                case 4:
                    MapperInstance = new Mapper4(PrgBanks, ChrBanks, initialMirroring);
                    break;
                case 7:
                    MapperInstance = new Mapper7(PrgBanks, ChrBanks, initialMirroring);
                    break;
                default:
                    // Fallback to Mapper 0
                    MapperInstance = new Mapper0(PrgBanks, ChrBanks, initialMirroring);
                    break;
            }

            IsValid = true;
        }

        public bool CpuRead(ushort addr, ref byte data)
        {
            uint mappedAddr;
            if (MapperInstance.CpuMapRead(addr, out mappedAddr, ref data))
            {
                if (mappedAddr != 0xFFFFFFFF && mappedAddr < PrgRom.Length)
                {
                    data = PrgRom[mappedAddr];
                }
                return true;
            }
            return false;
        }

        public bool CpuWrite(ushort addr, byte data)
        {
            uint mappedAddr;
            if (MapperInstance.CpuMapWrite(addr, out mappedAddr, data))
            {
                if (mappedAddr < PrgRom.Length)
                {
                    PrgRom[mappedAddr] = data;
                }
                return true;
            }
            return false;
        }

        public bool PpuRead(ushort addr, ref byte data)
        {
            uint mappedAddr;
            if (MapperInstance.PpuMapRead(addr, out mappedAddr))
            {
                if (mappedAddr < ChrRom.Length)
                {
                    data = ChrRom[mappedAddr];
                }
                return true;
            }
            return false;
        }

        public bool PpuWrite(ushort addr, byte data)
        {
            uint mappedAddr;
            if (MapperInstance.PpuMapWrite(addr, out mappedAddr))
            {
                if (mappedAddr < ChrRom.Length)
                {
                    ChrRom[mappedAddr] = data;
                }
                return true;
            }
            return false;
        }

        public void Reset()
        {
            if (MapperInstance != null)
                MapperInstance.Reset();
        }
    }
}
