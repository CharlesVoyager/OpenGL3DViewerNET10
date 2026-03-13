namespace View3D.Protocol
{
    public enum ErrorCode
    {
        RESIN_TANK_FAIL = 1000,
        Z_AXIS_UP_FAIL = 1001,
        Z_AXIS_DOWN_FAIL = 1002,
        NFC_MODULE_FAIL = 1003,
        ENGINE_BOARD_FAIL = 1004,
        NO_PRINTER_FOUND = 1020,
        NO_SLICER_FOUND = 1021,
        DATA_TRANS_FAIL = 1022,
        LOAD_FILE_FAIL = 1023,
        XYZWARE_RUNTIME_ERR = 1024,
        SAVE_FILE_FAIL = 1025,
        AUTO_REFILL_TIMEOUT = 2000,
        NO_RESIN_TAG = 2001,
        SCANNER_NO_CAL = 2002,
        OUT_OF_RESIN = 2003,
        COVER_OPEN = 2004,
        INVALID_RESIN_TAG = 2005,
        PRINTER_BUSY = 2020,
        RESIN_MISMATCH = 2021
    }
}
