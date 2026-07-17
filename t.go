package main

import (
	"fmt"
)

var puzzel = []int{
	1, 1, 0,
	1, 4, 0,
	1, 1, 0,
	2, 2, 0,
}

var puzzle_width = 3

func main() {
	start := 0
	width := 2
	height := 3
	count := 0
	idx := make([]int, width*height)

	for y := range height {
		for x := range width {
			idx[count] = start + (y * puzzle_width) + x
			count += 1
		}
	}

	for _, i := range idx {
		fmt.Print(puzzel[i])
		if i%width == 0 && i != 0 {
			fmt.Println()
		}
	}
	// fmt.Println(idx)
}
