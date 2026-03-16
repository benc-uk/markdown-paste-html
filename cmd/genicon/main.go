package main

import "os"

func main() {
	const width = 16
	const height = 16

	headerSize := 6
	dirEntrySize := 16
	bmpHeaderSize := 40
	pixelDataSize := width * height * 4
	andMaskSize := height * 4
	imageDataSize := bmpHeaderSize + pixelDataSize + andMaskSize

	total := headerSize + dirEntrySize + imageDataSize
	ico := make([]byte, total)

	ico[2] = 1
	ico[4] = 1

	off := headerSize
	ico[off] = width
	ico[off+1] = height
	le16(ico[off+4:], 1)
	le16(ico[off+6:], 32)
	le32(ico[off+8:], uint32(imageDataSize))
	le32(ico[off+12:], uint32(headerSize+dirEntrySize))

	off = headerSize + dirEntrySize
	le32(ico[off:], 40)
	le32(ico[off+4:], width)
	le32(ico[off+8:], height*2)
	le16(ico[off+12:], 1)
	le16(ico[off+14:], 32)
	le32(ico[off+20:], uint32(pixelDataSize+andMaskSize))

	pOff := headerSize + dirEntrySize + bmpHeaderSize
	blue := [4]byte{0xDE, 0x8C, 0x1E, 0xFF}
	white := [4]byte{0xFF, 0xFF, 0xFF, 0xFF}

	for y := 0; y < height; y++ {
		for x := 0; x < width; x++ {
			p := pOff + (y*width+x)*4
			copy(ico[p:], blue[:])
		}
	}

	mPat := []struct{ x, y int }{
		{3, 3}, {3, 4}, {3, 5}, {3, 6}, {3, 7}, {3, 8}, {3, 9}, {3, 10}, {3, 11}, {3, 12},
		{4, 3}, {4, 4}, {4, 5}, {4, 6}, {4, 7}, {4, 8}, {4, 9}, {4, 10}, {4, 11}, {4, 12},
		{11, 3}, {11, 4}, {11, 5}, {11, 6}, {11, 7}, {11, 8}, {11, 9}, {11, 10}, {11, 11}, {11, 12},
		{12, 3}, {12, 4}, {12, 5}, {12, 6}, {12, 7}, {12, 8}, {12, 9}, {12, 10}, {12, 11}, {12, 12},
		{5, 5}, {5, 6}, {6, 6}, {6, 7}, {7, 7}, {7, 8},
		{10, 5}, {10, 6}, {9, 6}, {9, 7}, {8, 7}, {8, 8},
	}

	for _, pt := range mPat {
		fy := (height - 1) - pt.y
		p := pOff + (fy*width+pt.x)*4
		copy(ico[p:], white[:])
	}

	os.MkdirAll("res", 0755)
	os.WriteFile("res/icon.ico", ico, 0644)
}

func le16(b []byte, v uint16) {
	b[0] = byte(v)
	b[1] = byte(v >> 8)
}

func le32(b []byte, v uint32) {
	b[0] = byte(v)
	b[1] = byte(v >> 8)
	b[2] = byte(v >> 16)
	b[3] = byte(v >> 24)
}
