using System;

namespace NesEmulator.Core
{
    public enum NesButton
    {
        A = 0,
        B = 1,
        Select = 2,
        Start = 3,
        Up = 4,
        Down = 5,
        Left = 6,
        Right = 7
    }

    public class Controller
    {
        private byte _buttonStates;
        private byte _shiftRegister;
        private bool _strobe;

        public void SetButton(NesButton button, bool pressed)
        {
            if (pressed)
                _buttonStates |= (byte)(1 << (int)button);
            else
                _buttonStates &= (byte)~(1 << (int)button);
        }

        public void Write(byte data)
        {
            _strobe = (data & 1) == 1;
            if (_strobe)
            {
                _shiftRegister = _buttonStates;
            }
        }

        public byte Read()
        {
            if (_strobe)
            {
                _shiftRegister = _buttonStates;
                return (byte)(_shiftRegister & 1);
            }

            byte val = (byte)(_shiftRegister & 1);
            _shiftRegister >>= 1;
            // High bits return 1 when exhausted
            _shiftRegister |= 0x80;
            return val;
        }

        public void Reset()
        {
            _buttonStates = 0;
            _shiftRegister = 0;
            _strobe = false;
        }
    }
}
