using System;

namespace NesEmulator.Core
{
    public class Cpu
    {
        // 6502 Registers
        public byte A;
        public byte X;
        public byte Y;
        public byte SP;
        public ushort PC;
        public byte Status;

        // Flags in Status Register
        public const byte FlagC = 1 << 0; // Carry
        public const byte FlagZ = 1 << 1; // Zero
        public const byte FlagI = 1 << 2; // Interrupt Disable
        public const byte FlagD = 1 << 3; // Decimal Mode (ignored on 2A03)
        public const byte FlagB = 1 << 4; // Break
        public const byte FlagU = 1 << 5; // Unused (always 1)
        public const byte FlagV = 1 << 6; // Overflow
        public const byte FlagN = 1 << 7; // Negative

        public Bus Bus { get; set; }

        public long Cycles { get; set; }
        public int StallCycles { get; set; }

        private bool _nmiPending;
        private bool _irqPending;

        public Cpu(Bus bus)
        {
            Bus = bus;
            Reset();
        }

        public void Reset()
        {
            A = 0;
            X = 0;
            Y = 0;
            SP = 0xFD;
            Status = FlagU | FlagI;

            // Read Reset Vector from 0xFFFC
            if (Bus != null)
            {
                PC = Bus.CpuRead16(0xFFFC);
            }
            else
            {
                PC = 0xC000;
            }

            Cycles = 7;
            StallCycles = 0;
            _nmiPending = false;
            _irqPending = false;
        }

        public void Nmi()
        {
            _nmiPending = true;
        }

        public void Irq()
        {
            if ((Status & FlagI) == 0)
            {
                _irqPending = true;
            }
        }

        private void SetFlag(byte flag, bool val)
        {
            if (val)
                Status |= flag;
            else
                Status &= (byte)~flag;
        }

        private bool GetFlag(byte flag)
        {
            return (Status & flag) != 0;
        }

        private void SetZeroNegative(byte val)
        {
            SetFlag(FlagZ, val == 0);
            SetFlag(FlagN, (val & 0x80) != 0);
        }

        private void Push(byte val)
        {
            Bus.CpuWrite((ushort)(0x0100 + SP), val);
            SP--;
        }

        private byte Pull()
        {
            SP++;
            return Bus.CpuRead((ushort)(0x0100 + SP));
        }

        private void Push16(ushort val)
        {
            Push((byte)(val >> 8));
            Push((byte)(val & 0xFF));
        }

        private ushort Pull16()
        {
            byte lo = Pull();
            byte hi = Pull();
            return (ushort)((hi << 8) | lo);
        }

        public int Step()
        {
            if (StallCycles > 0)
            {
                StallCycles--;
                Cycles++;
                return 1;
            }

            // Check Mapper IRQ
            if (Bus != null && Bus.Cartridge != null && Bus.Cartridge.MapperInstance != null)
            {
                if (Bus.Cartridge.MapperInstance.IrqState)
                {
                    Irq();
                }
            }

            if (_nmiPending)
            {
                _nmiPending = false;
                Push16(PC);
                Push((byte)((Status & ~FlagB) | FlagU));
                SetFlag(FlagI, true);
                PC = Bus.CpuRead16(0xFFFA);
                Cycles += 7;
                return 7;
            }

            if (_irqPending && !GetFlag(FlagI))
            {
                _irqPending = false;
                Push16(PC);
                Push((byte)((Status & ~FlagB) | FlagU));
                SetFlag(FlagI, true);
                PC = Bus.CpuRead16(0xFFFE);
                Cycles += 7;
                return 7;
            }

            byte opcode = Bus.CpuRead(PC++);
            int cycles = ExecuteOpcode(opcode);
            Cycles += cycles;
            return cycles;
        }

        // Addressing helper functions
        private ushort AddrZeroPage()
        {
            return Bus.CpuRead(PC++);
        }

        private ushort AddrZeroPageX()
        {
            return (byte)(Bus.CpuRead(PC++) + X);
        }

        private ushort AddrZeroPageY()
        {
            return (byte)(Bus.CpuRead(PC++) + Y);
        }

        private ushort AddrAbsolute()
        {
            ushort addr = Bus.CpuRead16(PC);
            PC += 2;
            return addr;
        }

        private ushort AddrAbsoluteX(out bool pageCrossed)
        {
            ushort baseAddr = Bus.CpuRead16(PC);
            PC += 2;
            ushort addr = (ushort)(baseAddr + X);
            pageCrossed = ((baseAddr & 0xFF00) != (addr & 0xFF00));
            return addr;
        }

        private ushort AddrAbsoluteY(out bool pageCrossed)
        {
            ushort baseAddr = Bus.CpuRead16(PC);
            PC += 2;
            ushort addr = (ushort)(baseAddr + Y);
            pageCrossed = ((baseAddr & 0xFF00) != (addr & 0xFF00));
            return addr;
        }

        private ushort AddrIndirectX()
        {
            byte b = (byte)(Bus.CpuRead(PC++) + X);
            byte lo = Bus.CpuRead(b);
            byte hi = Bus.CpuRead((byte)(b + 1));
            return (ushort)((hi << 8) | lo);
        }

        private ushort AddrIndirectY(out bool pageCrossed)
        {
            byte b = Bus.CpuRead(PC++);
            byte lo = Bus.CpuRead(b);
            byte hi = Bus.CpuRead((byte)(b + 1));
            ushort baseAddr = (ushort)((hi << 8) | lo);
            ushort addr = (ushort)(baseAddr + Y);
            pageCrossed = ((baseAddr & 0xFF00) != (addr & 0xFF00));
            return addr;
        }

        private int Branch(bool condition)
        {
            sbyte offset = (sbyte)Bus.CpuRead(PC++);
            if (condition)
            {
                ushort newPc = (ushort)(PC + offset);
                int extra = ((PC & 0xFF00) != (newPc & 0xFF00)) ? 2 : 1;
                PC = newPc;
                return 2 + extra;
            }
            return 2;
        }

        private void Adc(byte val)
        {
            int c = GetFlag(FlagC) ? 1 : 0;
            int sum = A + val + c;
            SetFlag(FlagC, sum > 0xFF);
            SetFlag(FlagV, (~(A ^ val) & (A ^ sum) & 0x80) != 0);
            A = (byte)sum;
            SetZeroNegative(A);
        }

        private void Sbc(byte val)
        {
            Adc((byte)~val);
        }

        private void Compare(byte reg, byte val)
        {
            SetFlag(FlagC, reg >= val);
            SetZeroNegative((byte)(reg - val));
        }

        private byte Asl(byte val)
        {
            SetFlag(FlagC, (val & 0x80) != 0);
            val = (byte)(val << 1);
            SetZeroNegative(val);
            return val;
        }

        private byte Lsr(byte val)
        {
            SetFlag(FlagC, (val & 0x01) != 0);
            val = (byte)(val >> 1);
            SetZeroNegative(val);
            return val;
        }

        private byte Rol(byte val)
        {
            int c = GetFlag(FlagC) ? 1 : 0;
            SetFlag(FlagC, (val & 0x80) != 0);
            val = (byte)((val << 1) | c);
            SetZeroNegative(val);
            return val;
        }

        private byte Ror(byte val)
        {
            int c = GetFlag(FlagC) ? 0x80 : 0;
            SetFlag(FlagC, (val & 0x01) != 0);
            val = (byte)((val >> 1) | c);
            SetZeroNegative(val);
            return val;
        }

        private int ExecuteOpcode(byte op)
        {
            bool cross;
            ushort addr;
            byte val;

            switch (op)
            {
                // ADC
                case 0x69: Adc(Bus.CpuRead(PC++)); return 2;
                case 0x65: Adc(Bus.CpuRead(AddrZeroPage())); return 3;
                case 0x75: Adc(Bus.CpuRead(AddrZeroPageX())); return 4;
                case 0x6D: Adc(Bus.CpuRead(AddrAbsolute())); return 4;
                case 0x7D: addr = AddrAbsoluteX(out cross); Adc(Bus.CpuRead(addr)); return cross ? 5 : 4;
                case 0x79: addr = AddrAbsoluteY(out cross); Adc(Bus.CpuRead(addr)); return cross ? 5 : 4;
                case 0x61: Adc(Bus.CpuRead(AddrIndirectX())); return 6;
                case 0x71: addr = AddrIndirectY(out cross); Adc(Bus.CpuRead(addr)); return cross ? 6 : 5;

                // SBC
                case 0xE9: case 0xEB: Sbc(Bus.CpuRead(PC++)); return 2;
                case 0xE5: Sbc(Bus.CpuRead(AddrZeroPage())); return 3;
                case 0xF5: Sbc(Bus.CpuRead(AddrZeroPageX())); return 4;
                case 0xED: Sbc(Bus.CpuRead(AddrAbsolute())); return 4;
                case 0xFD: addr = AddrAbsoluteX(out cross); Sbc(Bus.CpuRead(addr)); return cross ? 5 : 4;
                case 0xF9: addr = AddrAbsoluteY(out cross); Sbc(Bus.CpuRead(addr)); return cross ? 5 : 4;
                case 0xE1: Sbc(Bus.CpuRead(AddrIndirectX())); return 6;
                case 0xF1: addr = AddrIndirectY(out cross); Sbc(Bus.CpuRead(addr)); return cross ? 6 : 5;

                // AND
                case 0x29: A &= Bus.CpuRead(PC++); SetZeroNegative(A); return 2;
                case 0x25: A &= Bus.CpuRead(AddrZeroPage()); SetZeroNegative(A); return 3;
                case 0x35: A &= Bus.CpuRead(AddrZeroPageX()); SetZeroNegative(A); return 4;
                case 0x2D: A &= Bus.CpuRead(AddrAbsolute()); SetZeroNegative(A); return 4;
                case 0x3D: addr = AddrAbsoluteX(out cross); A &= Bus.CpuRead(addr); SetZeroNegative(A); return cross ? 5 : 4;
                case 0x39: addr = AddrAbsoluteY(out cross); A &= Bus.CpuRead(addr); SetZeroNegative(A); return cross ? 5 : 4;
                case 0x21: A &= Bus.CpuRead(AddrIndirectX()); SetZeroNegative(A); return 6;
                case 0x31: addr = AddrIndirectY(out cross); A &= Bus.CpuRead(addr); SetZeroNegative(A); return cross ? 6 : 5;

                // ORA
                case 0x09: A |= Bus.CpuRead(PC++); SetZeroNegative(A); return 2;
                case 0x05: A |= Bus.CpuRead(AddrZeroPage()); SetZeroNegative(A); return 3;
                case 0x15: A |= Bus.CpuRead(AddrZeroPageX()); SetZeroNegative(A); return 4;
                case 0x0D: A |= Bus.CpuRead(AddrAbsolute()); SetZeroNegative(A); return 4;
                case 0x1D: addr = AddrAbsoluteX(out cross); A |= Bus.CpuRead(addr); SetZeroNegative(A); return cross ? 5 : 4;
                case 0x19: addr = AddrAbsoluteY(out cross); A |= Bus.CpuRead(addr); SetZeroNegative(A); return cross ? 5 : 4;
                case 0x01: A |= Bus.CpuRead(AddrIndirectX()); SetZeroNegative(A); return 6;
                case 0x11: addr = AddrIndirectY(out cross); A |= Bus.CpuRead(addr); SetZeroNegative(A); return cross ? 6 : 5;

                // EOR
                case 0x49: A ^= Bus.CpuRead(PC++); SetZeroNegative(A); return 2;
                case 0x45: A ^= Bus.CpuRead(AddrZeroPage()); SetZeroNegative(A); return 3;
                case 0x55: A ^= Bus.CpuRead(AddrZeroPageX()); SetZeroNegative(A); return 4;
                case 0x4D: A ^= Bus.CpuRead(AddrAbsolute()); SetZeroNegative(A); return 4;
                case 0x5D: addr = AddrAbsoluteX(out cross); A ^= Bus.CpuRead(addr); SetZeroNegative(A); return cross ? 5 : 4;
                case 0x59: addr = AddrAbsoluteY(out cross); A ^= Bus.CpuRead(addr); SetZeroNegative(A); return cross ? 5 : 4;
                case 0x41: A ^= Bus.CpuRead(AddrIndirectX()); SetZeroNegative(A); return 6;
                case 0x51: addr = AddrIndirectY(out cross); A ^= Bus.CpuRead(addr); SetZeroNegative(A); return cross ? 6 : 5;

                // LDA
                case 0xA9: A = Bus.CpuRead(PC++); SetZeroNegative(A); return 2;
                case 0xA5: A = Bus.CpuRead(AddrZeroPage()); SetZeroNegative(A); return 3;
                case 0xB5: A = Bus.CpuRead(AddrZeroPageX()); SetZeroNegative(A); return 4;
                case 0xAD: A = Bus.CpuRead(AddrAbsolute()); SetZeroNegative(A); return 4;
                case 0xBD: addr = AddrAbsoluteX(out cross); A = Bus.CpuRead(addr); SetZeroNegative(A); return cross ? 5 : 4;
                case 0xB9: addr = AddrAbsoluteY(out cross); A = Bus.CpuRead(addr); SetZeroNegative(A); return cross ? 5 : 4;
                case 0xA1: A = Bus.CpuRead(AddrIndirectX()); SetZeroNegative(A); return 6;
                case 0xB1: addr = AddrIndirectY(out cross); A = Bus.CpuRead(addr); SetZeroNegative(A); return cross ? 6 : 5;

                // LDX
                case 0xA2: X = Bus.CpuRead(PC++); SetZeroNegative(X); return 2;
                case 0xA6: X = Bus.CpuRead(AddrZeroPage()); SetZeroNegative(X); return 3;
                case 0xB6: X = Bus.CpuRead(AddrZeroPageY()); SetZeroNegative(X); return 4;
                case 0xAE: X = Bus.CpuRead(AddrAbsolute()); SetZeroNegative(X); return 4;
                case 0xBE: addr = AddrAbsoluteY(out cross); X = Bus.CpuRead(addr); SetZeroNegative(X); return cross ? 5 : 4;

                // LDY
                case 0xA0: Y = Bus.CpuRead(PC++); SetZeroNegative(Y); return 2;
                case 0xA4: Y = Bus.CpuRead(AddrZeroPage()); SetZeroNegative(Y); return 3;
                case 0xB4: Y = Bus.CpuRead(AddrZeroPageX()); SetZeroNegative(Y); return 4;
                case 0xAC: Y = Bus.CpuRead(AddrAbsolute()); SetZeroNegative(Y); return 4;
                case 0xBC: addr = AddrAbsoluteX(out cross); Y = Bus.CpuRead(addr); SetZeroNegative(Y); return cross ? 5 : 4;

                // STA
                case 0x85: Bus.CpuWrite(AddrZeroPage(), A); return 3;
                case 0x95: Bus.CpuWrite(AddrZeroPageX(), A); return 4;
                case 0x8D: Bus.CpuWrite(AddrAbsolute(), A); return 4;
                case 0x9D: AddrAbsoluteX(out cross); Bus.CpuWrite((ushort)(Bus.CpuRead16((ushort)(PC - 2)) + X), A); return 5;
                case 0x99: AddrAbsoluteY(out cross); Bus.CpuWrite((ushort)(Bus.CpuRead16((ushort)(PC - 2)) + Y), A); return 5;
                case 0x81: Bus.CpuWrite(AddrIndirectX(), A); return 6;
                case 0x91:
                    byte ptr = Bus.CpuRead((ushort)(PC - 1));
                    addr = (ushort)(((Bus.CpuRead((ushort)((ptr + 1) & 0xFF)) << 8) | Bus.CpuRead(ptr)) + Y);
                    Bus.CpuWrite(addr, A);
                    return 6;

                // STX
                case 0x86: Bus.CpuWrite(AddrZeroPage(), X); return 3;
                case 0x96: Bus.CpuWrite(AddrZeroPageY(), X); return 4;
                case 0x8E: Bus.CpuWrite(AddrAbsolute(), X); return 4;

                // STY
                case 0x84: Bus.CpuWrite(AddrZeroPage(), Y); return 3;
                case 0x94: Bus.CpuWrite(AddrZeroPageX(), Y); return 4;
                case 0x8C: Bus.CpuWrite(AddrAbsolute(), Y); return 4;

                // INC
                case 0xE6: addr = AddrZeroPage(); val = (byte)(Bus.CpuRead(addr) + 1); Bus.CpuWrite(addr, val); SetZeroNegative(val); return 5;
                case 0xF6: addr = AddrZeroPageX(); val = (byte)(Bus.CpuRead(addr) + 1); Bus.CpuWrite(addr, val); SetZeroNegative(val); return 6;
                case 0xEE: addr = AddrAbsolute(); val = (byte)(Bus.CpuRead(addr) + 1); Bus.CpuWrite(addr, val); SetZeroNegative(val); return 6;
                case 0xFE: addr = AddrAbsoluteX(out cross); val = (byte)(Bus.CpuRead(addr) + 1); Bus.CpuWrite(addr, val); SetZeroNegative(val); return 7;

                // DEC
                case 0xC6: addr = AddrZeroPage(); val = (byte)(Bus.CpuRead(addr) - 1); Bus.CpuWrite(addr, val); SetZeroNegative(val); return 5;
                case 0xD6: addr = AddrZeroPageX(); val = (byte)(Bus.CpuRead(addr) - 1); Bus.CpuWrite(addr, val); SetZeroNegative(val); return 6;
                case 0xCE: addr = AddrAbsolute(); val = (byte)(Bus.CpuRead(addr) - 1); Bus.CpuWrite(addr, val); SetZeroNegative(val); return 6;
                case 0xDE: addr = AddrAbsoluteX(out cross); val = (byte)(Bus.CpuRead(addr) - 1); Bus.CpuWrite(addr, val); SetZeroNegative(val); return 7;

                // Register modifies
                case 0xE8: X++; SetZeroNegative(X); return 2; // INX
                case 0xCA: X--; SetZeroNegative(X); return 2; // DEX
                case 0xC8: Y++; SetZeroNegative(Y); return 2; // INY
                case 0x88: Y--; SetZeroNegative(Y); return 2; // DEY

                // Shifts
                case 0x0A: A = Asl(A); return 2;
                case 0x06: addr = AddrZeroPage(); Bus.CpuWrite(addr, Asl(Bus.CpuRead(addr))); return 5;
                case 0x16: addr = AddrZeroPageX(); Bus.CpuWrite(addr, Asl(Bus.CpuRead(addr))); return 6;
                case 0x0E: addr = AddrAbsolute(); Bus.CpuWrite(addr, Asl(Bus.CpuRead(addr))); return 6;
                case 0x1E: addr = AddrAbsoluteX(out cross); Bus.CpuWrite(addr, Asl(Bus.CpuRead(addr))); return 7;

                case 0x4A: A = Lsr(A); return 2;
                case 0x46: addr = AddrZeroPage(); Bus.CpuWrite(addr, Lsr(Bus.CpuRead(addr))); return 5;
                case 0x56: addr = AddrZeroPageX(); Bus.CpuWrite(addr, Lsr(Bus.CpuRead(addr))); return 6;
                case 0x4E: addr = AddrAbsolute(); Bus.CpuWrite(addr, Lsr(Bus.CpuRead(addr))); return 6;
                case 0x5E: addr = AddrAbsoluteX(out cross); Bus.CpuWrite(addr, Lsr(Bus.CpuRead(addr))); return 7;

                case 0x2A: A = Rol(A); return 2;
                case 0x26: addr = AddrZeroPage(); Bus.CpuWrite(addr, Rol(Bus.CpuRead(addr))); return 5;
                case 0x36: addr = AddrZeroPageX(); Bus.CpuWrite(addr, Rol(Bus.CpuRead(addr))); return 6;
                case 0x2E: addr = AddrAbsolute(); Bus.CpuWrite(addr, Rol(Bus.CpuRead(addr))); return 6;
                case 0x3E: addr = AddrAbsoluteX(out cross); Bus.CpuWrite(addr, Rol(Bus.CpuRead(addr))); return 7;

                case 0x6A: A = Ror(A); return 2;
                case 0x66: addr = AddrZeroPage(); Bus.CpuWrite(addr, Ror(Bus.CpuRead(addr))); return 5;
                case 0x76: addr = AddrZeroPageX(); Bus.CpuWrite(addr, Ror(Bus.CpuRead(addr))); return 6;
                case 0x6E: addr = AddrAbsolute(); Bus.CpuWrite(addr, Ror(Bus.CpuRead(addr))); return 6;
                case 0x7E: addr = AddrAbsoluteX(out cross); Bus.CpuWrite(addr, Ror(Bus.CpuRead(addr))); return 7;

                // Branches
                case 0x90: return Branch(!GetFlag(FlagC)); // BCC
                case 0xB0: return Branch(GetFlag(FlagC));  // BCS
                case 0xF0: return Branch(GetFlag(FlagZ));  // BEQ
                case 0xD0: return Branch(!GetFlag(FlagZ)); // BNE
                case 0x30: return Branch(GetFlag(FlagN));  // BMI
                case 0x10: return Branch(!GetFlag(FlagN)); // BPL
                case 0x50: return Branch(!GetFlag(FlagV)); // BVC
                case 0x70: return Branch(GetFlag(FlagV));  // BVS

                // Jumps
                case 0x4C: PC = Bus.CpuRead16(PC); return 3; // JMP Abs
                case 0x6C: // JMP Ind with 6502 page-wrap bug
                    ushort ptrAddr = Bus.CpuRead16(PC);
                    ushort target;
                    if ((ptrAddr & 0x00FF) == 0x00FF)
                        target = (ushort)((Bus.CpuRead((ushort)(ptrAddr & 0xFF00)) << 8) | Bus.CpuRead(ptrAddr));
                    else
                        target = Bus.CpuRead16(ptrAddr);
                    PC = target;
                    return 5;
                case 0x20: // JSR
                    addr = Bus.CpuRead16(PC);
                    Push16((ushort)(PC + 1));
                    PC = addr;
                    return 6;
                case 0x60: PC = (ushort)(Pull16() + 1); return 6; // RTS
                case 0x40: // RTI
                    Status = (byte)((Pull() & ~FlagB) | FlagU);
                    PC = Pull16();
                    return 6;

                // Compares
                case 0xC9: Compare(A, Bus.CpuRead(PC++)); return 2;
                case 0xC5: Compare(A, Bus.CpuRead(AddrZeroPage())); return 3;
                case 0xD5: Compare(A, Bus.CpuRead(AddrZeroPageX())); return 4;
                case 0xCD: Compare(A, Bus.CpuRead(AddrAbsolute())); return 4;
                case 0xDD: addr = AddrAbsoluteX(out cross); Compare(A, Bus.CpuRead(addr)); return cross ? 5 : 4;
                case 0xD9: addr = AddrAbsoluteY(out cross); Compare(A, Bus.CpuRead(addr)); return cross ? 5 : 4;
                case 0xC1: Compare(A, Bus.CpuRead(AddrIndirectX())); return 6;
                case 0xD1: addr = AddrIndirectY(out cross); Compare(A, Bus.CpuRead(addr)); return cross ? 6 : 5;

                case 0xE0: Compare(X, Bus.CpuRead(PC++)); return 2;
                case 0xE4: Compare(X, Bus.CpuRead(AddrZeroPage())); return 3;
                case 0xEC: Compare(X, Bus.CpuRead(AddrAbsolute())); return 4;

                case 0xC0: Compare(Y, Bus.CpuRead(PC++)); return 2;
                case 0xC4: Compare(Y, Bus.CpuRead(AddrZeroPage())); return 3;
                case 0xCC: Compare(Y, Bus.CpuRead(AddrAbsolute())); return 4;

                // BIT
                case 0x24:
                    val = Bus.CpuRead(AddrZeroPage());
                    SetFlag(FlagZ, (A & val) == 0);
                    SetFlag(FlagN, (val & 0x80) != 0);
                    SetFlag(FlagV, (val & 0x40) != 0);
                    return 3;
                case 0x2C:
                    val = Bus.CpuRead(AddrAbsolute());
                    SetFlag(FlagZ, (A & val) == 0);
                    SetFlag(FlagN, (val & 0x80) != 0);
                    SetFlag(FlagV, (val & 0x40) != 0);
                    return 4;

                // Stack operations
                case 0x48: Push(A); return 3; // PHA
                case 0x08: Push((byte)(Status | FlagB | FlagU)); return 3; // PHP
                case 0x68: A = Pull(); SetZeroNegative(A); return 4; // PLA
                case 0x28: Status = (byte)((Pull() & ~FlagB) | FlagU); return 4; // PLP

                // Transfers
                case 0xAA: X = A; SetZeroNegative(X); return 2; // TAX
                case 0x8A: A = X; SetZeroNegative(A); return 2; // TXA
                case 0xA8: Y = A; SetZeroNegative(Y); return 2; // TAY
                case 0x98: A = Y; SetZeroNegative(A); return 2; // TYA
                case 0xBA: X = SP; SetZeroNegative(X); return 2; // TSX
                case 0x9A: SP = X; return 2; // TXS

                // Flags
                case 0x18: SetFlag(FlagC, false); return 2; // CLC
                case 0x38: SetFlag(FlagC, true); return 2;  // SEC
                case 0x58: SetFlag(FlagI, false); return 2; // CLI
                case 0x78: SetFlag(FlagI, true); return 2;  // SEI
                case 0xB8: SetFlag(FlagV, false); return 2; // CLV
                case 0xD8: SetFlag(FlagD, false); return 2; // CLD
                case 0xF8: SetFlag(FlagD, true); return 2;  // SED

                // NOP & BRK
                case 0xEA: return 2; // NOP
                case 0x00: // BRK
                    PC++;
                    Push16(PC);
                    Push((byte)(Status | FlagB | FlagU));
                    SetFlag(FlagI, true);
                    PC = Bus.CpuRead16(0xFFFE);
                    return 7;

                // Unofficial Opcodes commonly used by test ROMs
                case 0xA7: A = X = Bus.CpuRead(AddrZeroPage()); SetZeroNegative(A); return 3; // LAX ZP
                case 0xB7: A = X = Bus.CpuRead(AddrZeroPageY()); SetZeroNegative(A); return 4; // LAX ZP,Y
                case 0xAF: A = X = Bus.CpuRead(AddrAbsolute()); SetZeroNegative(A); return 4;  // LAX Abs
                case 0xBF: addr = AddrAbsoluteY(out cross); A = X = Bus.CpuRead(addr); SetZeroNegative(A); return cross ? 5 : 4; // LAX Abs,Y
                case 0xA3: A = X = Bus.CpuRead(AddrIndirectX()); SetZeroNegative(A); return 6; // LAX Ind,X
                case 0xB3: addr = AddrIndirectY(out cross); A = X = Bus.CpuRead(addr); SetZeroNegative(A); return cross ? 6 : 5; // LAX Ind,Y

                case 0x87: Bus.CpuWrite(AddrZeroPage(), (byte)(A & X)); return 3; // SAX ZP
                case 0x97: Bus.CpuWrite(AddrZeroPageY(), (byte)(A & X)); return 4; // SAX ZP,Y
                case 0x8F: Bus.CpuWrite(AddrAbsolute(), (byte)(A & X)); return 4; // SAX Abs
                case 0x83: Bus.CpuWrite(AddrIndirectX(), (byte)(A & X)); return 6; // SAX Ind,X

                case 0xC7: // DCP ZP
                    addr = AddrZeroPage();
                    val = (byte)(Bus.CpuRead(addr) - 1);
                    Bus.CpuWrite(addr, val);
                    Compare(A, val);
                    return 5;
                case 0xD7: // DCP ZP,X
                    addr = AddrZeroPageX();
                    val = (byte)(Bus.CpuRead(addr) - 1);
                    Bus.CpuWrite(addr, val);
                    Compare(A, val);
                    return 6;
                case 0xCF: // DCP Abs
                    addr = AddrAbsolute();
                    val = (byte)(Bus.CpuRead(addr) - 1);
                    Bus.CpuWrite(addr, val);
                    Compare(A, val);
                    return 6;
                case 0xDF: // DCP Abs,X
                    addr = AddrAbsoluteX(out cross);
                    val = (byte)(Bus.CpuRead(addr) - 1);
                    Bus.CpuWrite(addr, val);
                    Compare(A, val);
                    return 7;
                case 0xDB: // DCP Abs,Y
                    addr = AddrAbsoluteY(out cross);
                    val = (byte)(Bus.CpuRead(addr) - 1);
                    Bus.CpuWrite(addr, val);
                    Compare(A, val);
                    return 7;
                case 0xC3: // DCP Ind,X
                    addr = AddrIndirectX();
                    val = (byte)(Bus.CpuRead(addr) - 1);
                    Bus.CpuWrite(addr, val);
                    Compare(A, val);
                    return 8;
                case 0xD3: // DCP Ind,Y
                    addr = AddrIndirectY(out cross);
                    val = (byte)(Bus.CpuRead(addr) - 1);
                    Bus.CpuWrite(addr, val);
                    Compare(A, val);
                    return 8;

                case 0xE7: // ISC ZP
                    addr = AddrZeroPage();
                    val = (byte)(Bus.CpuRead(addr) + 1);
                    Bus.CpuWrite(addr, val);
                    Sbc(val);
                    return 5;
                case 0xF7: // ISC ZP,X
                    addr = AddrZeroPageX();
                    val = (byte)(Bus.CpuRead(addr) + 1);
                    Bus.CpuWrite(addr, val);
                    Sbc(val);
                    return 6;
                case 0xEF: // ISC Abs
                    addr = AddrAbsolute();
                    val = (byte)(Bus.CpuRead(addr) + 1);
                    Bus.CpuWrite(addr, val);
                    Sbc(val);
                    return 6;
                case 0xFF: // ISC Abs,X
                    addr = AddrAbsoluteX(out cross);
                    val = (byte)(Bus.CpuRead(addr) + 1);
                    Bus.CpuWrite(addr, val);
                    Sbc(val);
                    return 7;
                case 0xFB: // ISC Abs,Y
                    addr = AddrAbsoluteY(out cross);
                    val = (byte)(Bus.CpuRead(addr) + 1);
                    Bus.CpuWrite(addr, val);
                    Sbc(val);
                    return 7;
                case 0xE3: // ISC Ind,X
                    addr = AddrIndirectX();
                    val = (byte)(Bus.CpuRead(addr) + 1);
                    Bus.CpuWrite(addr, val);
                    Sbc(val);
                    return 8;
                case 0xF3: // ISC Ind,Y
                    addr = AddrIndirectY(out cross);
                    val = (byte)(Bus.CpuRead(addr) + 1);
                    Bus.CpuWrite(addr, val);
                    Sbc(val);
                    return 8;

                // NOP (Unofficial variants)
                case 0x1A: case 0x3A: case 0x5A: case 0x7A: case 0xDA: case 0xFA: return 2;
                case 0x80: case 0x82: case 0x89: case 0xC2: case 0xE2: PC++; return 2;
                case 0x04: case 0x44: case 0x64: AddrZeroPage(); return 3;
                case 0x14: case 0x34: case 0x54: case 0x74: case 0xD4: case 0xF4: AddrZeroPageX(); return 4;
                case 0x0C: AddrAbsolute(); return 4;
                case 0x1C: case 0x3C: case 0x5C: case 0x7C: case 0xDC: case 0xFC: AddrAbsoluteX(out cross); return cross ? 5 : 4;

                // SLO: Shift Left then ORA
                case 0x07: addr = AddrZeroPage(); val = Asl(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A |= val; SetZeroNegative(A); return 5;
                case 0x17: addr = AddrZeroPageX(); val = Asl(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A |= val; SetZeroNegative(A); return 6;
                case 0x0F: addr = AddrAbsolute(); val = Asl(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A |= val; SetZeroNegative(A); return 6;
                case 0x1F: addr = AddrAbsoluteX(out cross); val = Asl(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A |= val; SetZeroNegative(A); return 7;
                case 0x1B: addr = AddrAbsoluteY(out cross); val = Asl(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A |= val; SetZeroNegative(A); return 7;
                case 0x03: addr = AddrIndirectX(); val = Asl(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A |= val; SetZeroNegative(A); return 8;
                case 0x13: addr = AddrIndirectY(out cross); val = Asl(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A |= val; SetZeroNegative(A); return 8;

                // RLA: Rotate Left then AND
                case 0x27: addr = AddrZeroPage(); val = Rol(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A &= val; SetZeroNegative(A); return 5;
                case 0x37: addr = AddrZeroPageX(); val = Rol(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A &= val; SetZeroNegative(A); return 6;
                case 0x2F: addr = AddrAbsolute(); val = Rol(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A &= val; SetZeroNegative(A); return 6;
                case 0x3F: addr = AddrAbsoluteX(out cross); val = Rol(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A &= val; SetZeroNegative(A); return 7;
                case 0x3B: addr = AddrAbsoluteY(out cross); val = Rol(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A &= val; SetZeroNegative(A); return 7;
                case 0x23: addr = AddrIndirectX(); val = Rol(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A &= val; SetZeroNegative(A); return 8;
                case 0x33: addr = AddrIndirectY(out cross); val = Rol(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A &= val; SetZeroNegative(A); return 8;

                // SRE: Shift Right then EOR
                case 0x47: addr = AddrZeroPage(); val = Lsr(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A ^= val; SetZeroNegative(A); return 5;
                case 0x57: addr = AddrZeroPageX(); val = Lsr(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A ^= val; SetZeroNegative(A); return 6;
                case 0x4F: addr = AddrAbsolute(); val = Lsr(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A ^= val; SetZeroNegative(A); return 6;
                case 0x5F: addr = AddrAbsoluteX(out cross); val = Lsr(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A ^= val; SetZeroNegative(A); return 7;
                case 0x5B: addr = AddrAbsoluteY(out cross); val = Lsr(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A ^= val; SetZeroNegative(A); return 7;
                case 0x43: addr = AddrIndirectX(); val = Lsr(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A ^= val; SetZeroNegative(A); return 8;
                case 0x53: addr = AddrIndirectY(out cross); val = Lsr(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); A ^= val; SetZeroNegative(A); return 8;

                // RRA: Rotate Right then ADC
                case 0x67: addr = AddrZeroPage(); val = Ror(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); Adc(val); return 5;
                case 0x77: addr = AddrZeroPageX(); val = Ror(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); Adc(val); return 6;
                case 0x6F: addr = AddrAbsolute(); val = Ror(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); Adc(val); return 6;
                case 0x7F: addr = AddrAbsoluteX(out cross); val = Ror(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); Adc(val); return 7;
                case 0x7B: addr = AddrAbsoluteY(out cross); val = Ror(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); Adc(val); return 7;
                case 0x63: addr = AddrIndirectX(); val = Ror(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); Adc(val); return 8;
                case 0x73: addr = AddrIndirectY(out cross); val = Ror(Bus.CpuRead(addr)); Bus.CpuWrite(addr, val); Adc(val); return 8;

                // ANC: AND with Carry = bit 7
                case 0x0B: case 0x2B:
                    A &= Bus.CpuRead(PC++);
                    SetZeroNegative(A);
                    SetFlag(FlagC, GetFlag(FlagN));
                    return 2;

                // ALR / ASR: AND then LSR
                case 0x4B:
                    A &= Bus.CpuRead(PC++);
                    A = Lsr(A);
                    return 2;

                // ARR: AND then ROR with BCD/V quirks
                case 0x6B:
                    A &= Bus.CpuRead(PC++);
                    A = Ror(A);
                    SetFlag(FlagC, (A & 0x40) != 0);
                    SetFlag(FlagV, (((A >> 6) ^ (A >> 5)) & 1) != 0);
                    SetZeroNegative(A);
                    return 2;

                // AXS / SBX: (A & X) - imm -> X
                case 0xCB:
                    byte imm = Bus.CpuRead(PC++);
                    int diff = (A & X) - imm;
                    SetFlag(FlagC, diff >= 0);
                    X = (byte)diff;
                    SetZeroNegative(X);
                    return 2;

                // LAS / LAE: mem & SP -> A, X, SP
                case 0xBB:
                    addr = AddrAbsoluteY(out cross);
                    val = (byte)(Bus.CpuRead(addr) & SP);
                    A = X = SP = val;
                    SetZeroNegative(A);
                    return cross ? 5 : 4;

                default:
                    // Treat unknown opcodes as NOP
                    return 2;
            }
        }
    }
}
