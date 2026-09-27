        CLS
        LD VD, 0
        LD VE, 0

        LD V0, 0xFF
        LD V1, 0x01
        ADD V0, V1
        SE V0, 0x00
        JP fail
        SE VF, 0x01
        JP fail
        ADD VE, 1

        LD V0, 0x10
        LD V1, 0x20
        ADD V0, V1
        SE V0, 0x30
        JP fail
        SE VF, 0x00
        JP fail
        ADD VE, 1

        LD V0, 0x10
        LD V1, 0x20
        SUB V0, V1
        SE V0, 0xF0
        JP fail
        SE VF, 0x00
        JP fail
        ADD VE, 1

        LD V0, 0x20
        LD V1, 0x10
        SUB V0, V1
        SE V0, 0x10
        JP fail
        SE VF, 0x01
        JP fail
        ADD VE, 1

        LD V0, 0x10
        LD V1, 0x30
        SUBN V0, V1
        SE V0, 0x20
        JP fail
        SE VF, 0x01
        JP fail
        ADD VE, 1

        LD V0, 0x05
        SHR V0, V0
        SE V0, 0x02
        JP fail
        SE VF, 0x01
        JP fail
        ADD VE, 1

        LD V0, 0x81
        SHL V0, V0
        SE V0, 0x02
        JP fail
        SE VF, 0x01
        JP fail
        ADD VE, 1

        LD V0, 0xF0
        LD V1, 0x3C
        OR V0, V1
        SE V0, 0xFC
        JP fail
        ADD VE, 1

        LD V0, 0xF0
        AND V0, V1
        SE V0, 0x30
        JP fail
        ADD VE, 1

        LD V0, 0xF0
        XOR V0, V1
        SE V0, 0xCC
        JP fail
        ADD VE, 1

        LD VF, 0
        LD V0, 0xFF
        ADD V0, 2
        SE V0, 0x01
        JP fail
        SE VF, 0x00
        JP fail
        ADD VE, 1

        LD V0, 234
        LD I, scratch
        LD B, V0
        LD I, scratch
        LD V2, [I]
        SE V0, 2
        JP fail
        SE V1, 3
        JP fail
        SE V2, 4
        JP fail
        ADD VE, 1

        LD V0, 0x11
        LD V1, 0x22
        LD V2, 0x33
        LD I, scratch
        LD [I], V2
        LD V0, 0
        LD V1, 0
        LD V2, 0
        LD I, scratch
        LD V2, [I]
        SE V0, 0x11
        JP fail
        SE V1, 0x22
        JP fail
        SE V2, 0x33
        JP fail
        ADD VE, 1

        LD V5, 0
        CALL setter
        SE V5, 0x42
        JP fail
        ADD VE, 1

        LD V0, 0x10
        LD I, scratch - 0x10
        ADD I, V0
        LD [I], V0
        LD I, scratch
        LD V0, [I]
        SE V0, 0x10
        JP fail
        ADD VE, 1

        LD V0, 4
        JP V0, jumpBase
jumpBase:
        JP fail
        JP fail
        ADD VE, 1

        LD V0, 7
        LD V1, 7
        SNE V0, V1
        JP skipTaken
        JP fail
skipTaken:
        ADD VE, 1

show:
        LD I, scratch
        LD B, VE
        LD I, scratch
        LD V2, [I]
        LD V3, 0
        LD V4, 0
        LD F, V1
        DRW V3, V4, 5
        LD V3, 5
        LD F, V2
        DRW V3, V4, 5
halt:
        JP halt

fail:
        LD VD, 1
        JP show

setter:
        LD V5, 0x42
        RET

scratch:
        DB 0, 0, 0, 0
