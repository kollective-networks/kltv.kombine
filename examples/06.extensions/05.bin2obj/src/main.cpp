#include <stdint.h>
#include <stdio.h>


extern "C" const unsigned char vararrow_down_svg10913588630147233371[];
extern "C" const uint32_t vararrow_down_svg10913588630147233371_size;

int main(int argc, char** argv) {
	printf("Hello, World!\n");
	printf("Resource size is %u\n", (unsigned)vararrow_down_svg10913588630147233371_size);
	printf("Resource content is:\n%.*s\n", (int)vararrow_down_svg10913588630147233371_size, vararrow_down_svg10913588630147233371);
	return 0;
}